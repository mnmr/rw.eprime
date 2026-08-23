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
            string automationHeader,
            string autoOptimizeLabel,
            string displayHeader,
            string skillCaptionsLabel,
            string colonistVerdictsLabel,
            string paletteVerdictsLabel,
            string recommendationVerdictsLabel,
            StructuredTip numericTip,
            StructuredTip rangeTip,
            StructuredTip autoOptimizeTip,
            bool numeric,
            bool vanillaRange,
            bool autoOptimize,
            bool skillCaptions,
            bool colonistVerdicts,
            bool paletteVerdicts,
            bool recommendationVerdicts)
        {
            CompatibilityHeader = compatibilityHeader;
            NumericLabel = numericLabel;
            RangeLabel = rangeLabel;
            AutomationHeader = automationHeader;
            AutoOptimizeLabel = autoOptimizeLabel;
            DisplayHeader = displayHeader;
            SkillCaptionsLabel = skillCaptionsLabel;
            ColonistVerdictsLabel = colonistVerdictsLabel;
            PaletteVerdictsLabel = paletteVerdictsLabel;
            RecommendationVerdictsLabel = recommendationVerdictsLabel;
            NumericTip = numericTip;
            RangeTip = rangeTip;
            AutoOptimizeTip = autoOptimizeTip;
            Numeric = numeric;
            VanillaRange = vanillaRange;
            AutoOptimize = autoOptimize;
            SkillCaptions = skillCaptions;
            ColonistVerdicts = colonistVerdicts;
            PaletteVerdicts = paletteVerdicts;
            RecommendationVerdicts = recommendationVerdicts;
        }

        internal string CompatibilityHeader { get; }
        internal string NumericLabel { get; }
        internal string RangeLabel { get; }
        internal string AutomationHeader { get; }
        internal string AutoOptimizeLabel { get; }
        internal string DisplayHeader { get; }
        internal string SkillCaptionsLabel { get; }
        internal string ColonistVerdictsLabel { get; }
        internal string PaletteVerdictsLabel { get; }
        internal string RecommendationVerdictsLabel { get; }
        internal StructuredTip NumericTip { get; }
        internal StructuredTip RangeTip { get; }
        internal StructuredTip AutoOptimizeTip { get; }
        internal bool Numeric { get; }
        internal bool VanillaRange { get; }
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
            && NumericTip.ContentEquals(other.NumericTip)
            && RangeTip.ContentEquals(other.RangeTip)
            && AutoOptimizeTip.ContentEquals(other.AutoOptimizeTip)
            && Numeric == other.Numeric
            && VanillaRange == other.VanillaRange
            && AutoOptimize == other.AutoOptimize
            && SkillCaptions == other.SkillCaptions
            && ColonistVerdicts == other.ColonistVerdicts
            && PaletteVerdicts == other.PaletteVerdicts
            && RecommendationVerdicts == other.RecommendationVerdicts;
    }

    /// Owns the Options tab's detached open-window projection.
    internal sealed class OptionsTabState
    {
        // Owner: Options tab window instance.
        // Key: RoleStore identity, WorkRolesSettings identity,
        // LanguageChangeCoordinator.Revision, and the exact seven boolean
        // values displayed by the tab.
        // Value: immutable OptionsRenderSnapshot containing translated chrome,
        // structured tips, and detached checkbox values.
        // Dependencies: language, manual-priority mode, reported priority range,
        // the auto-optimize schedule toggle, and the four client-local display
        // preferences.
        // Refresh: immediate when an exact key input changes, including paused
        // synced execution and local preference edits.
        // Equality: exact equal rebuilt contents preserve snapshot identity.
        // Teardown: Reset releases the snapshot and owner references;
        // InvalidateLanguageCaches forces a language comparison without dropping
        // an equal published snapshot.
        private RoleStore? owner;
        private WorkRolesSettings? settingsOwner;
        private int languageRevision = -1;
        private bool builtNumeric;
        private bool builtVanillaRange;
        private bool builtAutoOptimize;
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
            bool numeric = Current.Game?.playSettings?.useWorkPriorities ?? false;
            bool vanillaRange = store?.reportVanillaPriorities ?? true;
            bool autoOptimize = store?.autoOptimize ?? false;
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
                && builtNumeric == numeric
                && builtVanillaRange == vanillaRange
                && builtAutoOptimize == autoOptimize
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
            var rebuilt = new OptionsRenderSnapshot(
                "WR_CompatSection".Translate(),
                numericLabel,
                rangeLabel,
                "WR_AutomationSection".Translate(),
                autoOptimizeLabel,
                "WR_DisplaySection".Translate(),
                "WR_OptSkillCaptions".Translate(),
                "WR_OptVerdictsColonists".Translate(),
                "WR_OptVerdictsPalette".Translate(),
                "WR_OptVerdictsRecommendations".Translate(),
                new StructuredTip("options:numeric", numericModel),
                new StructuredTip("options:vanilla-range", rangeModel),
                new StructuredTip("options:auto-optimize", autoOptimizeModel),
                numeric,
                vanillaRange,
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
            builtNumeric = numeric;
            builtVanillaRange = vanillaRange;
            builtAutoOptimize = autoOptimize;
            builtSkillCaptions = skillCaptions;
            builtColonistVerdicts = colonistVerdicts;
            builtPaletteVerdicts = paletteVerdicts;
            builtRecommendationVerdicts = recommendationVerdicts;
            return snapshot;
        }
    }
}
