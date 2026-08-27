using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    internal static class RoleIconStyle
    {
        internal const float FrameSize = 30f;
        internal const float IconSize = 24f;
        internal const float IconInset = 3f;
        internal const float TitleGap = 6f;

        internal static readonly Color FrameColor =
            new Color(160f / 255f, 160f / 255f, 160f / 255f);
        internal static readonly Color IconTint =
            new Color(226f / 255f, 224f / 255f, 218f / 255f);
        internal static readonly Color PlaceholderTint =
            new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    internal sealed class RoleIconChoiceSnapshot
    {
        internal RoleIconChoiceSnapshot(string path, Texture2D texture)
        {
            Path = path;
            Texture = texture;
        }

        internal string Path { get; }
        internal Texture2D Texture { get; }
        internal bool Unassigned => Path.Length == 0;

        internal bool ContentEquals(RoleIconChoiceSnapshot other) =>
            other != null
            && string.Equals(Path, other.Path, StringComparison.Ordinal)
            && ReferenceEquals(Texture, other.Texture);
    }

    internal sealed class RoleIconCatalogSnapshot
    {
        internal static readonly RoleIconCatalogSnapshot Empty =
            new RoleIconCatalogSnapshot(Array.Empty<RoleIconChoiceSnapshot>());

        private readonly RoleIconChoiceSnapshot[] choices;

        internal RoleIconCatalogSnapshot(RoleIconChoiceSnapshot[] choices)
        {
            this.choices = choices;
        }

        internal int Count => choices.Length;
        internal RoleIconChoiceSnapshot At(int index) => choices[index];

        internal bool ContentEquals(RoleIconCatalogSnapshot other)
        {
            if (other == null || choices.Length != other.choices.Length)
                return false;
            for (int i = 0; i < choices.Length; i++)
                if (!choices[i].ContentEquals(other.choices[i])) return false;
            return true;
        }
    }

    internal static class RoleIconCatalog
    {
        private static readonly string[] IconPaths =
        {
            "WorkRoles/RoleIcons/art",
            "WorkRoles/RoleIcons/atom",
            "WorkRoles/RoleIcons/award-badge",
            "WorkRoles/RoleIcons/axe",
            "WorkRoles/RoleIcons/baby",
            "WorkRoles/RoleIcons/balance-scale",
            "WorkRoles/RoleIcons/basic",
            "WorkRoles/RoleIcons/basics",
            "WorkRoles/RoleIcons/bean",
            "WorkRoles/RoleIcons/bedrest",
            "WorkRoles/RoleIcons/brain",
            "WorkRoles/RoleIcons/brewer",
            "WorkRoles/RoleIcons/butcher",
            "WorkRoles/RoleIcons/check-badge",
            "WorkRoles/RoleIcons/childcare",
            "WorkRoles/RoleIcons/circled-x",
            "WorkRoles/RoleIcons/classical-building",
            "WorkRoles/RoleIcons/cleaner",
            "WorkRoles/RoleIcons/cleaning",
            "WorkRoles/RoleIcons/construction",
            "WorkRoles/RoleIcons/cooking",
            "WorkRoles/RoleIcons/core",
            "WorkRoles/RoleIcons/cpu",
            "WorkRoles/RoleIcons/crafting",
            "WorkRoles/RoleIcons/crosshair",
            "WorkRoles/RoleIcons/darkstudy",
            "WorkRoles/RoleIcons/dashed-circle-dot",
            "WorkRoles/RoleIcons/dice",
            "WorkRoles/RoleIcons/dna",
            "WorkRoles/RoleIcons/doctor",
            "WorkRoles/RoleIcons/drafting-compass",
            "WorkRoles/RoleIcons/drugmaker",
            "WorkRoles/RoleIcons/factory",
            "WorkRoles/RoleIcons/fire-extinguisher",
            "WorkRoles/RoleIcons/firefighter",
            "WorkRoles/RoleIcons/fishing",
            "WorkRoles/RoleIcons/fishing-rod",
            "WorkRoles/RoleIcons/footprints",
            "WorkRoles/RoleIcons/fuel-pump",
            "WorkRoles/RoleIcons/growing",
            "WorkRoles/RoleIcons/grunt",
            "WorkRoles/RoleIcons/handling",
            "WorkRoles/RoleIcons/handshake",
            "WorkRoles/RoleIcons/handyman",
            "WorkRoles/RoleIcons/hard-drive-download",
            "WorkRoles/RoleIcons/hard-drive-upload",
            "WorkRoles/RoleIcons/hat-and-glasses",
            "WorkRoles/RoleIcons/hauling",
            "WorkRoles/RoleIcons/hunting",
            "WorkRoles/RoleIcons/jailor",
            "WorkRoles/RoleIcons/jointmaker",
            "WorkRoles/RoleIcons/lasso",
            "WorkRoles/RoleIcons/locked-user",
            "WorkRoles/RoleIcons/medic",
            "WorkRoles/RoleIcons/megaphone",
            "WorkRoles/RoleIcons/mining",
            "WorkRoles/RoleIcons/mountain",
            "WorkRoles/RoleIcons/nurse",
            "WorkRoles/RoleIcons/open-hand",
            "WorkRoles/RoleIcons/orbit",
            "WorkRoles/RoleIcons/paintbrush",
            "WorkRoles/RoleIcons/painter",
            "WorkRoles/RoleIcons/patient",
            "WorkRoles/RoleIcons/plantcut",
            "WorkRoles/RoleIcons/pyrophobe",
            "WorkRoles/RoleIcons/radar",
            "WorkRoles/RoleIcons/rescuer",
            "WorkRoles/RoleIcons/research",
            "WorkRoles/RoleIcons/researcher",
            "WorkRoles/RoleIcons/ringing-bell",
            "WorkRoles/RoleIcons/road-barrier",
            "WorkRoles/RoleIcons/robot-head",
            "WorkRoles/RoleIcons/rocket",
            "WorkRoles/RoleIcons/round-mirror",
            "WorkRoles/RoleIcons/running-sack",
            "WorkRoles/RoleIcons/satellite-dish",
            "WorkRoles/RoleIcons/scissors",
            "WorkRoles/RoleIcons/shirt",
            "WorkRoles/RoleIcons/shopping-basket",
            "WorkRoles/RoleIcons/shovel",
            "WorkRoles/RoleIcons/siren",
            "WorkRoles/RoleIcons/smelter",
            "WorkRoles/RoleIcons/smithing",
            "WorkRoles/RoleIcons/soup-bowl",
            "WorkRoles/RoleIcons/sowing-seeds",
            "WorkRoles/RoleIcons/sprout",
            "WorkRoles/RoleIcons/steak",
            "WorkRoles/RoleIcons/syringe",
            "WorkRoles/RoleIcons/tailoring",
            "WorkRoles/RoleIcons/target",
            "WorkRoles/RoleIcons/terminal",
            "WorkRoles/RoleIcons/text-scroll",
            "WorkRoles/RoleIcons/trash-can",
            "WorkRoles/RoleIcons/user-search",
            "WorkRoles/RoleIcons/user-x",
            "WorkRoles/RoleIcons/utensils",
            "WorkRoles/RoleIcons/warden",
            "WorkRoles/RoleIcons/wheat",
            "WorkRoles/RoleIcons/wrench",
        };

        // Owner: process play-data session. Key: definition revision. Value:
        // one immutable-by-publication, alphabetically ordered icon-choice
        // snapshot, including the unassigned placeholder. Dependencies: the
        // fixed packaged RoleIcons paths, their resolved Texture2D assets, the
        // vanilla placeholder texture, and DefinitionReloadCoordinator.Revision.
        // Refresh: eagerly at startup/window PreOpen and definition-regeneration
        // lifecycle boundaries, never from OnGUI. Equality: equal rebuilt
        // contents retain snapshot identity.
        // Teardown: ReleaseForTeardown drops all externally owned references.
        private static RoleIconCatalogSnapshot snapshot =
            RoleIconCatalogSnapshot.Empty;
        private static int snapshotDefinitionRevision = int.MinValue;

        internal static RoleIconCatalogSnapshot Snapshot => snapshot;

        internal static void WarmDefinitions()
        {
            int revision = DefinitionReloadCoordinator.Revision;
            if (snapshotDefinitionRevision == revision
                && !ReferenceEquals(snapshot, RoleIconCatalogSnapshot.Empty))
                return;

            var choices = new List<RoleIconChoiceSnapshot>(IconPaths.Length + 1)
            {
                new RoleIconChoiceSnapshot("", WorkRolesTex.RoleIconPlaceholder)
            };
            for (int i = 0; i < IconPaths.Length; i++)
            {
                string path = IconPaths[i];
                Texture2D? texture = ContentFinder<Texture2D>.Get(
                    path, reportFailure: false);
                if (texture != null)
                    choices.Add(new RoleIconChoiceSnapshot(path, texture));
            }

            var rebuilt = new RoleIconCatalogSnapshot(choices.ToArray());
            if (ReferenceEquals(snapshot, RoleIconCatalogSnapshot.Empty)
                || !snapshot.ContentEquals(rebuilt))
                snapshot = rebuilt;
            snapshotDefinitionRevision = revision;
        }

        internal static string EffectivePath(Role role)
        {
            if (role.iconPath != null) return role.iconPath;
            if (role.templateDefName.NullOrEmpty()) return "";
            return DefDatabase<RoleDef>.GetNamedSilentFail(
                role.templateDefName)?.iconPath ?? "";
        }

        internal static Texture2D? ResolveForRefresh(string path) =>
            path.Length == 0 ? null
                : ContentFinder<Texture2D>.Get(path, reportFailure: false);

        internal static void ReleaseForTeardown()
        {
            snapshot = RoleIconCatalogSnapshot.Empty;
            snapshotDefinitionRevision = int.MinValue;
        }
    }
}
