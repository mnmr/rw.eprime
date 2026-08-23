using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorkRoles.UI
{
    internal readonly struct RoleIconPresentation
    {
        internal RoleIconPresentation(string effectivePath, Texture2D? texture)
        {
            EffectivePath = effectivePath;
            Texture = texture;
        }

        internal string EffectivePath { get; }
        internal Texture2D? Texture { get; }
        internal bool Assigned => EffectivePath.Length > 0 && Texture != null;

        internal bool ContentEquals(RoleIconPresentation other) =>
            string.Equals(EffectivePath, other.EffectivePath,
                StringComparison.Ordinal)
            && ReferenceEquals(Texture, other.Texture);
    }

    internal sealed class RoleIconPresentationSnapshot
    {
        internal static readonly RoleIconPresentationSnapshot Empty =
            new RoleIconPresentationSnapshot(
                new Dictionary<int, RoleIconPresentation>());

        private readonly Dictionary<int, RoleIconPresentation> roles;

        internal RoleIconPresentationSnapshot(
            Dictionary<int, RoleIconPresentation> roles)
        {
            this.roles = roles;
        }

        internal RoleIconPresentation For(int roleId) =>
            roles.TryGetValue(roleId, out RoleIconPresentation presentation)
                ? presentation : new RoleIconPresentation("", null);

        internal bool ContentEquals(RoleIconPresentationSnapshot other)
        {
            if (other == null || roles.Count != other.roles.Count) return false;
            foreach (KeyValuePair<int, RoleIconPresentation> pair in roles)
                if (!other.roles.TryGetValue(pair.Key,
                        out RoleIconPresentation candidate)
                    || !pair.Value.ContentEquals(candidate)) return false;
            return true;
        }
    }

    internal static class RoleIconPresentationCatalog
    {
        // Owner: open WorkRoles window. Key: RoleStore identity, UiVersion, and
        // DefinitionReloadCoordinator.Revision. Value: one immutable-by-
        // publication lookup of effective persisted/template icon paths and
        // resolved externally owned textures for every role. Dependencies: the
        // role catalog, role/template icon paths, RoleDef icon paths, and loaded
        // texture content. Refresh: eagerly from PreOpen/WindowUpdate, outside
        // OnGUI, after a key change. Equality: equal contents retain snapshot
        // identity; owner changes always publish a new snapshot. Teardown:
        // ReleaseForTeardown drops the store and all external asset references.
        private static RoleIconPresentationSnapshot snapshot =
            RoleIconPresentationSnapshot.Empty;
        private static RoleStore? owner;
        private static int uiVersion = int.MinValue;
        private static int definitionRevision = int.MinValue;
        private static int publishedRevision;

        internal static int Revision => publishedRevision;

        internal static RoleIconPresentation For(int roleId) =>
            snapshot.For(roleId);

        internal static void Refresh(RoleStore? store)
        {
            int nextUiVersion = UiVersion.Current;
            int nextDefinitionRevision = DefinitionReloadCoordinator.Revision;
            if (ReferenceEquals(owner, store)
                && uiVersion == nextUiVersion
                && definitionRevision == nextDefinitionRevision)
                return;

            RoleIconPresentationSnapshot rebuilt;
            if (store == null)
                rebuilt = RoleIconPresentationSnapshot.Empty;
            else
            {
                var roles = new Dictionary<int, RoleIconPresentation>(
                    store.roles.Count);
                for (int i = 0; i < store.roles.Count; i++)
                {
                    Role role = store.roles[i];
                    string path = RoleIconCatalog.EffectivePath(role);
                    roles[role.id] = new RoleIconPresentation(path,
                        RoleIconCatalog.ResolveForRefresh(path));
                }
                rebuilt = new RoleIconPresentationSnapshot(roles);
            }

            bool ownerChanged = !ReferenceEquals(owner, store);
            if (ownerChanged || !snapshot.ContentEquals(rebuilt))
            {
                snapshot = rebuilt;
                unchecked { publishedRevision++; }
            }
            owner = store;
            uiVersion = nextUiVersion;
            definitionRevision = nextDefinitionRevision;
        }

        internal static void ReleaseForTeardown()
        {
            bool changed = owner != null
                || !ReferenceEquals(snapshot, RoleIconPresentationSnapshot.Empty);
            snapshot = RoleIconPresentationSnapshot.Empty;
            owner = null;
            uiVersion = int.MinValue;
            definitionRevision = int.MinValue;
            if (changed)
                unchecked { publishedRevision++; }
        }
    }
}
