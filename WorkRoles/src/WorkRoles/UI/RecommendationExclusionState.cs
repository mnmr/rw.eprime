using System.Collections.Generic;
using RimShared.UiLib;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    internal sealed partial class RecommendationsTabState
    {
        // Owner: Recommendations window. Key: store, catalog/presentation/rules/capability/
        // recommendation revisions, definitions, UI metrics, width and language.
        // Value: immutable-by-ownership excluded chips, add choices, translated
        // labels and measured geometry. Dependencies: role identity/order,
        // exclusion flags, chip presentation, language/font and width.
        // Refresh: immediate behind those gates; no game polling. Equality:
        // equal chip/menu/layout contents retain snapshot identity. Teardown:
        // ResetExclusions on window release and language invalidation.
        private RoleStore? exclusionsStore;
        private int exclusionsRevision = -1, exclusionsMetrics = -1, exclusionsDefinitions = -1;
        private float exclusionsWidth = -1f;
        internal RecOrderSnapshot? Exclusions { get; private set; }
        internal string? ExclusionsHelp { get; private set; }
        internal float ExclusionsHelpHeight { get; private set; }
        internal float ExclusionsHeaderHeight { get; private set; }

        private void ResetExclusions()
        {
            exclusionsStore = null;
            exclusionsRevision = exclusionsMetrics = exclusionsDefinitions = -1;
            exclusionsWidth = -1f;
            Exclusions = null;
            ExclusionsHelp = null;
            ExclusionsHelpHeight = ExclusionsHeaderHeight = 0f;
        }

        internal void EnsureExclusions(RoleStore store, float width)
        {
            int revision = store.Revisions.For(ModelDomain.Catalog
                | ModelDomain.RolePresentation | ModelDomain.RoleRules | ModelDomain.RoleCapability
                | ModelDomain.RoleRecommendation);
            if (ReferenceEquals(exclusionsStore, store)
                && exclusionsRevision == revision && exclusionsWidth == width
                && exclusionsMetrics == UiRevision.Current
                && exclusionsDefinitions == DefinitionReloadCoordinator.Revision) return;
            var chipsById = new Dictionary<int, RoleChipRenderData>(store.roles.Count);
            var excluded = new List<int>();
            var choices = new List<RecRoleMenuOption>();
            for (int i = 0; i < store.roles.Count; i++)
            {
                Role role = store.roles[i];
                RoleChipRenderData chip = RoleChipRenderData.From(role);
                chipsById.Add(role.id, chip);
                if (role.excludedFromRecommendations) excluded.Add(role.id);
                else choices.Add(new RecRoleMenuOption(role.id, chip.Label, null));
            }
            choices.Sort(RecRoleMenuOption.CompareByLabel);
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                ExclusionsHeaderHeight = Mathf.Ceil(Text.LineHeight);
                ExclusionsHelp = "WR_RecExcludedHelp".Translate();
                ExclusionsHelpHeight = Text.CalcHeight(ExclusionsHelp, width);
                List<RecOrderChip> chips = LayoutOrderChips(width, excluded,
                    chipsById, out Rect addRect, out float height, out string addLabel);
                var rebuilt = new RecOrderSnapshot(chips, chipsById, choices,
                    addRect, height, addLabel, "WR_RecExcludedHeader".Translate());
                if (!ReferenceEquals(exclusionsStore, store)
                    || Exclusions == null || !Exclusions.ContentEquals(rebuilt)) Exclusions = rebuilt;
            }
            finally { Text.Font = previousFont; }
            exclusionsStore = store;
            exclusionsRevision = revision;
            exclusionsMetrics = UiRevision.Current;
            exclusionsDefinitions = DefinitionReloadCoordinator.Revision;
            exclusionsWidth = width;
        }
    }
}
