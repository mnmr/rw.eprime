using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Immutable-by-publication Options tab chrome and checkbox values.
    internal sealed class OptionsRenderSnapshot
    {
        internal OptionsRenderSnapshot(
            string compatibilityHeader,
            string numericLabel,
            string rangeLabel,
            string emergencyRuleLabel,
            string automationHeader,
            string autoOptimizeLabel,
            string displayHeader,
            string skillCaptionsLabel,
            string colonistVerdictsLabel,
            string paletteVerdictsLabel,
            string recommendationVerdictsLabel,
            string paletteGroupingLabel,
            string[] paletteGroupingOptions,
            int paletteGroupingIndex,
            float paletteGroupingControlWidth,
            StructuredTip numericTip,
            StructuredTip rangeTip,
            StructuredTip emergencyRuleTip,
            StructuredTip autoOptimizeTip,
            bool numeric,
            bool vanillaRange,
            bool vanillaEmergencyRule,
            bool autoOptimize,
            bool skillCaptions,
            bool colonistVerdicts,
            bool paletteVerdicts,
            bool recommendationVerdicts)
        {
            CompatibilityHeader = compatibilityHeader;
            NumericLabel = numericLabel;
            RangeLabel = rangeLabel;
            EmergencyRuleLabel = emergencyRuleLabel;
            AutomationHeader = automationHeader;
            AutoOptimizeLabel = autoOptimizeLabel;
            DisplayHeader = displayHeader;
            SkillCaptionsLabel = skillCaptionsLabel;
            ColonistVerdictsLabel = colonistVerdictsLabel;
            PaletteVerdictsLabel = paletteVerdictsLabel;
            RecommendationVerdictsLabel = recommendationVerdictsLabel;
            PaletteGroupingLabel = paletteGroupingLabel;
            PaletteGroupingOptions = paletteGroupingOptions;
            PaletteGroupingIndex = paletteGroupingIndex;
            PaletteGroupingControlWidth = paletteGroupingControlWidth;
            NumericTip = numericTip;
            RangeTip = rangeTip;
            EmergencyRuleTip = emergencyRuleTip;
            AutoOptimizeTip = autoOptimizeTip;
            Numeric = numeric;
            VanillaRange = vanillaRange;
            VanillaEmergencyRule = vanillaEmergencyRule;
            AutoOptimize = autoOptimize;
            SkillCaptions = skillCaptions;
            ColonistVerdicts = colonistVerdicts;
            PaletteVerdicts = paletteVerdicts;
            RecommendationVerdicts = recommendationVerdicts;
        }

        internal string CompatibilityHeader { get; }
        internal string NumericLabel { get; }
        internal string RangeLabel { get; }
        internal string EmergencyRuleLabel { get; }
        internal string AutomationHeader { get; }
        internal string AutoOptimizeLabel { get; }
        internal string DisplayHeader { get; }
        internal string SkillCaptionsLabel { get; }
        internal string ColonistVerdictsLabel { get; }
        internal string PaletteVerdictsLabel { get; }
        internal string RecommendationVerdictsLabel { get; }
        internal string PaletteGroupingLabel { get; }
        /// Segment labels in PaletteMode ordinal order (Skills, Groups);
        /// producer-owned array, never mutated after publication.
        internal string[] PaletteGroupingOptions { get; }
        internal int PaletteGroupingIndex { get; }
        internal float PaletteGroupingControlWidth { get; }
        internal StructuredTip NumericTip { get; }
        internal StructuredTip RangeTip { get; }
        internal StructuredTip EmergencyRuleTip { get; }
        internal StructuredTip AutoOptimizeTip { get; }
        internal bool Numeric { get; }
        internal bool VanillaRange { get; }
        internal bool VanillaEmergencyRule { get; }
        internal bool AutoOptimize { get; }
        internal bool SkillCaptions { get; }
        internal bool ColonistVerdicts { get; }
        internal bool PaletteVerdicts { get; }
        internal bool RecommendationVerdicts { get; }

        internal bool ContentEquals(OptionsRenderSnapshot other) =>
            other != null
            && string.Equals(CompatibilityHeader, other.CompatibilityHeader,
                System.StringComparison.Ordinal)
            && string.Equals(NumericLabel, other.NumericLabel,
                System.StringComparison.Ordinal)
            && string.Equals(RangeLabel, other.RangeLabel,
                System.StringComparison.Ordinal)
            && string.Equals(EmergencyRuleLabel, other.EmergencyRuleLabel,
                System.StringComparison.Ordinal)
            && string.Equals(AutomationHeader, other.AutomationHeader,
                System.StringComparison.Ordinal)
            && string.Equals(AutoOptimizeLabel, other.AutoOptimizeLabel,
                System.StringComparison.Ordinal)
            && string.Equals(DisplayHeader, other.DisplayHeader,
                System.StringComparison.Ordinal)
            && string.Equals(SkillCaptionsLabel, other.SkillCaptionsLabel,
                System.StringComparison.Ordinal)
            && string.Equals(ColonistVerdictsLabel, other.ColonistVerdictsLabel,
                System.StringComparison.Ordinal)
            && string.Equals(PaletteVerdictsLabel, other.PaletteVerdictsLabel,
                System.StringComparison.Ordinal)
            && string.Equals(RecommendationVerdictsLabel,
                other.RecommendationVerdictsLabel,
                System.StringComparison.Ordinal)
            && string.Equals(PaletteGroupingLabel, other.PaletteGroupingLabel,
                System.StringComparison.Ordinal)
            && SameOptions(PaletteGroupingOptions,
                other.PaletteGroupingOptions)
            && PaletteGroupingIndex == other.PaletteGroupingIndex
            && PaletteGroupingControlWidth
                == other.PaletteGroupingControlWidth
            && NumericTip.ContentEquals(other.NumericTip)
            && RangeTip.ContentEquals(other.RangeTip)
            && EmergencyRuleTip.ContentEquals(other.EmergencyRuleTip)
            && AutoOptimizeTip.ContentEquals(other.AutoOptimizeTip)
            && Numeric == other.Numeric
            && VanillaRange == other.VanillaRange
            && VanillaEmergencyRule == other.VanillaEmergencyRule
            && AutoOptimize == other.AutoOptimize
            && SkillCaptions == other.SkillCaptions
            && ColonistVerdicts == other.ColonistVerdicts
            && PaletteVerdicts == other.PaletteVerdicts
            && RecommendationVerdicts == other.RecommendationVerdicts;

        private static bool SameOptions(string[] left, string[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (!string.Equals(left[i], right[i],
                        System.StringComparison.Ordinal))
                    return false;
            return true;
        }
    }

    /// Owns the Options tab's detached open-window projection.
    internal sealed class OptionsTabState
    {
        // Owner: Options tab window instance.
        // Key: RoleStore identity, WorkRolesSettings identity,
        // LanguageChangeCoordinator.Revision, UiVersion.Current (the palette
        // grouping control width is a text measurement), the palette grouping
        // value, and the exact eight boolean values displayed by the tab.
        // Value: immutable OptionsRenderSnapshot containing translated chrome,
        // structured tips, measured control geometry, and detached values.
        // Dependencies: language, UI metrics, manual-priority mode, reported
        // priority range, the emergency rule, the auto-optimize schedule toggle, the palette
        // grouping preference, and the four client-local display preferences.
        // Refresh: immediate when an exact key input changes, including paused
        // synced execution and local preference edits.
        // Equality: exact equal rebuilt contents preserve snapshot identity.
        // Teardown: Reset releases the snapshot and owner references;
        // InvalidateLanguageCaches forces a language comparison without dropping
        // an equal published snapshot.
        private RoleStore? owner;
        private WorkRolesSettings? settingsOwner;
        private int languageRevision = -1;
        private int uiRevision = -1;
        private bool builtNumeric;
        private bool builtVanillaRange;
        private bool builtEmergencyRule;
        private bool builtAutoOptimize;
        private int builtPaletteGrouping = -1;
        private bool builtSkillCaptions;
        private bool builtColonistVerdicts;
        private bool builtPaletteVerdicts;
        private bool builtRecommendationVerdicts;
        private OptionsRenderSnapshot? snapshot;

        internal void Reset()
        {
            owner = null;
            settingsOwner = null;
            languageRevision = -1;
            uiRevision = -1;
            builtPaletteGrouping = -1;
            snapshot = null;
        }

        internal void InvalidateLanguageCaches()
        {
            languageRevision = -1;
        }

        internal OptionsRenderSnapshot Snapshot(RoleStore? store)
        {
            WorkRolesSettings? settings = WorkRolesMod.Settings;
            int language = LanguageChangeCoordinator.Revision;
            int ui = UiVersion.Current;
            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            bool vanillaRange = store?.reportVanillaPriorities ?? true;
            bool emergencyRule = store?.vanillaEmergencyRule ?? false;
            bool autoOptimize = store?.autoOptimize ?? false;
            PaletteMode paletteGrouping =
                settings?.paletteMode == PaletteMode.Groups
                    ? PaletteMode.Groups : PaletteMode.Skills;
            bool skillCaptions = settings?.colonistSkillCaptions ?? true;
            bool colonistVerdicts = settings?.verdictsOnColonistChips ?? true;
            bool paletteVerdicts = settings?.verdictsInPalette ?? true;
            bool recommendationVerdicts =
                settings?.verdictsOnRecommendationChips ?? true;
            bool ownerChanged = !ReferenceEquals(owner, store)
                || !ReferenceEquals(settingsOwner, settings);
            if (snapshot != null
                && !ownerChanged
                && languageRevision == language
                && uiRevision == ui
                && builtNumeric == numeric
                && builtVanillaRange == vanillaRange
                && builtEmergencyRule == emergencyRule
                && builtAutoOptimize == autoOptimize
                && builtPaletteGrouping == (int)paletteGrouping
                && builtSkillCaptions == skillCaptions
                && builtColonistVerdicts == colonistVerdicts
                && builtPaletteVerdicts == paletteVerdicts
                && builtRecommendationVerdicts == recommendationVerdicts)
                return snapshot;

            string numericLabel = "WR_OptNumeric".Translate();
            string rangeLabel = "WR_OptVanillaRange".Translate();
            var numericModel = new TipModel { Title = numericLabel };
            numericModel.AddSection().Text("WR_OptNumericTipWhat".Translate());
            numericModel.AddSection()
                .Fact("WR_TipOff".Translate(), "WR_OptNumericTipOff".Translate())
                .Fact("WR_TipOn".Translate(), "WR_OptNumericTipOn".Translate());
            numericModel.AddSection().Text(
                "WR_OptNumericTipWhy".Translate(), dim: true);
            var rangeModel = new TipModel { Title = rangeLabel };
            rangeModel.AddSection().Text("WR_OptVanillaRangeTipWhat".Translate());
            rangeModel.AddSection()
                .Fact("WR_TipOff".Translate(),
                    "WR_OptVanillaRangeTipOff".Translate())
                .Fact("WR_TipOn".Translate(),
                    "WR_OptVanillaRangeTipOn".Translate());
            string emergencyRuleLabel = "WR_OptEmergencyRule".Translate();
            var emergencyRuleModel = new TipModel { Title = emergencyRuleLabel };
            emergencyRuleModel.AddSection().Text(
                "WR_OptEmergencyRuleTipWhat".Translate());
            emergencyRuleModel.AddSection()
                .Fact("WR_TipOff".Translate(),
                    "WR_OptEmergencyRuleTipOff".Translate())
                .Fact("WR_TipOn".Translate(),
                    "WR_OptEmergencyRuleTipOn".Translate());
            emergencyRuleModel.AddSection().Text(
                "WR_OptEmergencyRuleTipWhy".Translate(), dim: true);
            string autoOptimizeLabel = "WR_OptAutoOptimize".Translate();
            var autoOptimizeModel = new TipModel { Title = autoOptimizeLabel };
            autoOptimizeModel.AddSection().Text(
                "WR_OptAutoOptimizeTipWhat".Translate());
            autoOptimizeModel.AddSection()
                .Fact("WR_TipOff".Translate(),
                    "WR_OptAutoOptimizeTipOff".Translate())
                .Fact("WR_TipOn".Translate(),
                    "WR_OptAutoOptimizeTipOn".Translate());
            autoOptimizeModel.AddSection().Text(
                "WR_OptAutoOptimizeTipWhy".Translate(), dim: true);
            // Segment labels in PaletteMode ordinal order; the control width
            // fits the widest translated label per segment (measured behind
            // this snapshot's language/UI-revision gate).
            var paletteGroupingOptions = new[]
            {
                "WR_PaletteBySkills".Translate().ToString(),
                "WR_PaletteByGroups".Translate().ToString(),
            };
            float paletteControlWidth;
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                float widestSegment = 0f;
                foreach (string option in paletteGroupingOptions)
                {
                    float width = WrText.FitWidth(option);
                    if (width > widestSegment) widestSegment = width;
                }
                paletteControlWidth = Mathf.Max(140f,
                    (widestSegment + 24f) * paletteGroupingOptions.Length + 5f);
            }
            finally
            {
                Text.Font = previousFont;
            }
            var rebuilt = new OptionsRenderSnapshot(
                "WR_CompatSection".Translate(),
                numericLabel,
                rangeLabel,
                emergencyRuleLabel,
                "WR_AutomationSection".Translate(),
                autoOptimizeLabel,
                "WR_DisplaySection".Translate(),
                "WR_OptSkillCaptions".Translate(),
                "WR_OptVerdictsColonists".Translate(),
                "WR_OptVerdictsPalette".Translate(),
                "WR_OptVerdictsRecommendations".Translate(),
                "WR_OptPaletteGrouping".Translate(),
                paletteGroupingOptions,
                paletteGrouping == PaletteMode.Groups ? 1 : 0,
                paletteControlWidth,
                new StructuredTip("options:numeric", numericModel),
                new StructuredTip("options:vanilla-range", rangeModel),
                new StructuredTip("options:emergency-rule", emergencyRuleModel),
                new StructuredTip("options:auto-optimize", autoOptimizeModel),
                numeric,
                vanillaRange,
                emergencyRule,
                autoOptimize,
                skillCaptions,
                colonistVerdicts,
                paletteVerdicts,
                recommendationVerdicts);
            if (ownerChanged || snapshot == null
                || !snapshot.ContentEquals(rebuilt))
                snapshot = rebuilt;
            owner = store;
            settingsOwner = settings;
            languageRevision = language;
            uiRevision = ui;
            builtNumeric = numeric;
            builtVanillaRange = vanillaRange;
            builtEmergencyRule = emergencyRule;
            builtAutoOptimize = autoOptimize;
            builtPaletteGrouping = (int)paletteGrouping;
            builtSkillCaptions = skillCaptions;
            builtColonistVerdicts = colonistVerdicts;
            builtPaletteVerdicts = paletteVerdicts;
            builtRecommendationVerdicts = recommendationVerdicts;
            return snapshot;
        }
    }
}
