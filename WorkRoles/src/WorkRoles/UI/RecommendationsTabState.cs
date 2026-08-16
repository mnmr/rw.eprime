using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.UI
{
    /// Owns the Recommendations tab's open-window projections: the global
    /// recommendation order and tuning-parameter caches plus the per-role
    /// panel list and the single expanded panel's editor snapshot. The view
    /// consumes snapshots and retains only immediate-mode interaction state.
    internal sealed class RecommendationsTabState
    {
        internal const float FlowGap = 8f;

        // Cache contract — Owner: Recommendations tab. Key: RoleStore identity,
        // UiVersion.Current, available width, and language generation through
        // explicit invalidation. Value: one immutable recommendation-order
        // snapshot containing detached chip data, layout, catalog lookup, and
        // add-menu projections. Dependencies: role catalog/order, labels,
        // colors, role rules, hunting identity, width, font, and language.
        // Refresh: immediate on a key change. Equality: key hits preserve
        // snapshot identity. Teardown: Reset/InvalidateLanguageCaches releases it.
        private RoleStore orderStore;
        private int orderUiVersion = -1;
        private int orderGeneration;
        private float orderWidth = -1f;
        private RecOrderSnapshot orderSnapshot;

        // Owner: Recommendations window. Key: language revision. Value: two
        // immutable StructuredTip models. Dependencies: translated tip text.
        // Refresh: lazy on the next read after language changes. Equality:
        // equal rebuilt contents preserve each tip identity. Teardown: Reset
        // releases both references.
        private int tipsLanguageRevision = -1;

        private float helpWidth = -1f;

        // Cache contract — Owner: Recommendations tab. Key: RoleStore identity,
        // RecommendationTuningRevision, available width, and language-cache
        // generation (explicit invalidation). Value: immutable-by-publication
        // sections of translated rows, formatted values, and measured geometry.
        // Dependencies: tuning descriptors, normalized values, language, font,
        // width. Refresh: immediate on a key change. Equality: key hits preserve
        // section/row identity. Teardown: Reset/InvalidateLanguageCaches clears it.
        private RoleStore tuningStore;
        private int tuningRevision = -1;
        private float tuningWidth = -1f;
        private RecTuningSnapshot tuningSnapshot;

        // Cache contract — Owner: Recommendations tab. Key: published order
        // snapshot identity and available width. Value: one immutable panel
        // snapshot with detached chip render data and measured help geometry.
        // Dependencies: resolved order chip projections, width, font, and
        // language (the order snapshot is replaced on language invalidation).
        // Refresh: immediate on a key change. Equality: matching key preserves
        // snapshot identity. Teardown: Reset/InvalidateLanguageCaches releases it.
        private RecOrderSnapshot panelsOrder;
        private float panelsWidth = -1f;
        private RecRolePanelsSnapshot panelsSnapshot;

        // Cache contract — Owner: Recommendations tab. Key: (UiVersion.Current,
        // RoleStore identity, expanded role id, available width, live Small
        // line height, language generation via explicit invalidation). Value:
        // one immutable
        // RecRoleDetailSnapshot (single slot: the accordion expands one panel).
        // Dependencies: the role's category/time/championPenalty, explicit
        // required-skill gates, job-derived Used/Trained facts, holder scales
        // and training paths, detached role-chip and add-menu projections,
        // skill/enum labels, language, GameFont.Small widths/line height, and
        // available width. Refresh:
        // immediate on key change. DefinitionReloadCoordinator.Revision is part
        // of the key because skill labels resolve SkillDefs. Equality: matching
        // key preserves snapshot identity. Teardown: Reset/language invalidation
        // releases it.
        private int detailStamp = -1;
        private int detailDefinitionRevision = -1;
        private RoleStore detailStore;
        private int detailRoleId = -1;
        private float detailWidth = -1f;
        private float detailSmallLineHeight = -1f;
        private RecRoleDetailSnapshot detail;

        internal int OrderStamp => orderGeneration;
        internal RecOrderSnapshot Order => orderSnapshot;

        internal StructuredTip RecommendationOrderTip { get; private set; }
        internal StructuredTip TrainingTip { get; private set; }
        internal string RecommendationOrderHelp { get; private set; }
        internal float RecommendationOrderHelpHeight { get; private set; }
        internal RecTuningSnapshot Tuning => tuningSnapshot;
        internal RecRolePanelsSnapshot Panels => panelsSnapshot;

        internal void Reset()
        {
            InvalidateLanguageCaches();
            RecommendationOrderTip = null;
            TrainingTip = null;
        }

        internal void InvalidateLanguageCaches()
        {
            orderStore = null;
            orderUiVersion = -1;
            orderWidth = -1f;
            orderSnapshot = null;

            tipsLanguageRevision = -1;

            helpWidth = -1f;
            RecommendationOrderHelp = null;
            RecommendationOrderHelpHeight = 0f;

            tuningStore = null;
            tuningRevision = -1;
            tuningWidth = -1f;
            tuningSnapshot = null;

            panelsOrder = null;
            panelsWidth = -1f;
            panelsSnapshot = null;

            detailStamp = -1;
            detailDefinitionRevision = -1;
            detailStore = null;
            detailRoleId = -1;
            detailWidth = -1f;
            detailSmallLineHeight = -1f;
            detail = null;
        }

        internal void EnsureOrder(RoleStore store, float width)
        {
            if (ReferenceEquals(orderStore, store)
                && orderUiVersion == UiVersion.Current
                && orderWidth == width) return;
            List<RoleView> views = RecsAdapter.RoleViewsOf(store.roles);
            List<int> resolved = OrderTemplate.ResolveTemplate(
                store.recommendationOrder, views);
            var viewsById = views.ToDictionary(role => role.Id);
            var chipsById = new Dictionary<int, RoleChipRenderData>(
                store.roles.Count);
            for (int i = 0; i < store.roles.Count; i++)
            {
                Role role = store.roles[i];
                chipsById[role.id] = RoleChipRenderData.From(role);
            }

            List<RecOrderChip> chips = LayoutOrderChips(width, resolved,
                chipsById, viewsById, out Rect addRect, out float layoutHeight,
                out string addLabel);
            string headerLabel = "WR_RecOrderHeader".Translate();
            var addOptions = new List<RecRoleMenuOption>();
            List<int> candidateIds = OrderTemplate.AddCandidates(views, resolved);
            for (int i = 0; i < candidateIds.Count; i++)
            {
                if (!chipsById.TryGetValue(candidateIds[i], out var chip))
                    continue;
                addOptions.Add(new RecRoleMenuOption(
                    chip.RoleId, chip.Label, null));
            }
            addOptions.Sort(RecRoleMenuOption.CompareByLabel);

            var rebuilt = new RecOrderSnapshot(
                chips, chipsById, addOptions, addRect, layoutHeight, addLabel,
                headerLabel);
            if (!ReferenceEquals(orderStore, store)
                || orderSnapshot == null
                || !orderSnapshot.ContentEquals(rebuilt))
            {
                orderSnapshot = rebuilt;
                unchecked { orderGeneration++; }
            }
            orderStore = store;
            orderUiVersion = UiVersion.Current;
            orderWidth = width;
        }

        internal void EnsureTuning(RoleStore store, float width)
        {
            if (ReferenceEquals(tuningStore, store)
                && tuningRevision == store.RecommendationTuningRevision
                && tuningWidth == width)
                return;
            var rebuiltSections = new List<RecTuningSection>();
            string tuningReset = "WR_RecTuneReset".Translate();
            string headerLabel = "WR_RecGlobalPanel".Translate();
            string globalHelp = null;
            float globalHelpHeight = 0f;

            RecommendationsTuningOptions options = store.recommendationTuning
                ?? RecommendationsTuningOptions.Default;
            const float rowGap = 6f;
            // Controls occupy 108px at the right edge; captions keep 20px clear.
            const float descriptionWidthReserve = 128f;
            string cellHint = "WR_RecTuneCellHint".Translate();
            string sectionKey = null;
            string sectionLabel = null;
            string sectionIntro = null;
            float sectionIntroHeight = 0f;
            List<RecTuningItem> items = null;
            RecTuningTableBuilder table = null;
            RecTuningTableGroup tableGroup = RecTuningTableGroup.None;
            float tableCellsX = 0f;
            float tableCellsY = 0f;
            float tableCellW = RecTuningTable.CellW;
            float y = 0f;
            void CloseTable()
            {
                if (table != null)
                {
                    items.Add(new RecTuningItem(table.Publish()));
                    table = null;
                }
                tableGroup = RecTuningTableGroup.None;
            }
            void CloseSection()
            {
                CloseTable();
                if (items != null)
                    rebuiltSections.Add(new RecTuningSection(
                        sectionKey, sectionLabel, sectionIntro,
                        sectionIntroHeight, items, y - rowGap));
            }
            GameFont previousFont = Text.Font;
            try
            {
                // The right-aligned cell hint under a table block can be wider
                // than the cells; captions must clear both (drawn Tiny).
                Text.Font = GameFont.Tiny;
                float cellHintWidth = WrText.FitWidth(cellHint);
                Text.Font = GameFont.Small;
                globalHelp = "WR_RecGlobalPanelHelp".Translate();
                globalHelpHeight = Text.CalcHeight(globalHelp, width);
                foreach (RecommendationTuningDescriptor descriptor in
                         RecommendationsTuningOptions.Descriptors)
                {
                    if (descriptor.Hidden) continue;
                    if (descriptor.SectionLabelKey != sectionKey)
                    {
                        CloseSection();
                        sectionKey = descriptor.SectionLabelKey;
                        sectionLabel = sectionKey.Translate();
                        sectionIntro = (sectionKey + "Intro").Translate();
                        // The intro shares its top row with the Reset button
                        // inside the expanded area: narrower than the rows.
                        sectionIntroHeight = Text.CalcHeight(
                            sectionIntro, width - 66f);
                        items = new List<RecTuningItem>();
                        table = null;
                        tableGroup = RecTuningTableGroup.None;
                        y = 0f;
                    }
                    int value = options.Get(descriptor.Option);
                    string valueLabel = FormatTuningValue(
                        descriptor.ValueKind, value);
                    RecTuningTableGroup group = GroupOf(descriptor.Option);
                    if (group == RecTuningTableGroup.None)
                    {
                        CloseTable();
                        string label = descriptor.LabelKey.Translate();
                        string description = descriptor.DescriptionKey.Translate();
                        float descriptionHeight = Text.CalcHeight(
                            description,
                            System.Math.Max(80f, width - descriptionWidthReserve));
                        float rowHeight = System.Math.Max(
                            44f, 21f + descriptionHeight);
                        List<string> enumOptions = null;
                        List<Color> enumColors = null;
                        List<string> enumTipKeys = null;
                        if (descriptor.ValueKind
                            == RecommendationTuningValueKind.SignalBucket)
                        {
                            enumOptions = new List<string>();
                            enumColors = new List<Color>();
                            enumTipKeys = new List<string>();
                            for (int bucket = descriptor.MinimumValue;
                                 bucket <= descriptor.MaximumValue; bucket++)
                            {
                                enumOptions.Add(SignalLetter((SignalBucket)bucket));
                                enumColors.Add(Signals.SkillSignalPresentation
                                    .VerdictColor((SignalBucket)bucket));
                                enumTipKeys.Add(VerdictKey((SignalBucket)bucket));
                            }
                        }
                        items.Add(new RecTuningItem(new RecommendationTuningRow(
                            descriptor,
                            value,
                            sectionLabel: null,
                            label,
                            description,
                            valueLabel,
                            sectionRect: default,
                            new Rect(0f, y, width, rowHeight),
                            "WR_Tune_" + descriptor.StableKey,
                            enumOptions,
                            enumColors,
                            enumTipKeys)));
                        y += rowHeight + rowGap;
                        continue;
                    }

                    if (group != tableGroup)
                    {
                        CloseTable();
                        tableGroup = group;
                        (string labelKey, string descKey) = GroupKeys(group);
                        // Cells right-align like the single-row controls; the
                        // caption wraps in the space left of them.
                        int columns = GroupColumns(group);
                        // Every block spans at least the four-column min-pick
                        // footprint; fewer columns (category/time) divide the
                        // same block into wider word-header cells, keeping the
                        // grids aligned.
                        int footprintColumns = System.Math.Max(columns, 4);
                        float cellsWidth = footprintColumns * RecTuningTable.CellW
                            + (footprintColumns - 1) * RecTuningTable.CellGap;
                        tableCellW = (cellsWidth
                            - (columns - 1) * RecTuningTable.CellGap) / columns;
                        float textWidth = width
                            - System.Math.Max(cellsWidth, cellHintWidth) - 20f;
                        string description = descKey.Translate();
                        float descriptionHeight = Text.CalcHeight(
                            description, textWidth);
                        float cellsHeight = RecTuningTable.HeaderH
                            + RecTuningTable.CellH + 2f + RecTuningTable.HintH;
                        tableCellsX = width - cellsWidth;
                        tableCellsY = y;
                        table = new RecTuningTableBuilder(
                            labelKey.Translate(),
                            description,
                            cellHint,
                            new Rect(0f, y, textWidth, 21f),
                            new Rect(0f, y + 21f, textWidth, descriptionHeight),
                            new Rect(0f, y + RecTuningTable.HeaderH
                                + RecTuningTable.CellH + 2f,
                                width, RecTuningTable.HintH));
                        y += System.Math.Max(
                            21f + descriptionHeight, cellsHeight) + rowGap;
                    }
                    int column = table.CellCount;
                    float cellX = tableCellsX + column
                        * (tableCellW + RecTuningTable.CellGap);
                    (string header, Color headerColor, string headerTipKey) =
                        HeaderFor(group, column);
                    table.AddCell(new RecTuningTableCell(
                        descriptor, value, valueLabel,
                        header, headerColor, headerTipKey,
                        new Rect(cellX, tableCellsY,
                            tableCellW, RecTuningTable.HeaderH),
                        new Rect(cellX, tableCellsY + RecTuningTable.HeaderH,
                            tableCellW, RecTuningTable.CellH)));
                }
                CloseSection();
            }
            finally
            {
                Text.Font = previousFont;
            }
            tuningSnapshot = new RecTuningSnapshot(rebuiltSections,
                tuningReset, headerLabel, globalHelp, globalHelpHeight);
            tuningStore = store;
            tuningRevision = store.RecommendationTuningRevision;
            tuningWidth = width;
        }

        private sealed class RecTuningTableBuilder
        {
            private readonly string label;
            private readonly string description;
            private readonly string hint;
            private readonly Rect labelRect;
            private readonly Rect descriptionRect;
            private readonly Rect hintRect;
            private readonly List<RecTuningTableCell> cells =
                new List<RecTuningTableCell>();

            internal RecTuningTableBuilder(string label, string description,
                string hint, Rect labelRect, Rect descriptionRect,
                Rect hintRect)
            {
                this.label = label;
                this.description = description;
                this.hint = hint;
                this.labelRect = labelRect;
                this.descriptionRect = descriptionRect;
                this.hintRect = hintRect;
            }

            internal int CellCount => cells.Count;
            internal void AddCell(RecTuningTableCell cell) => cells.Add(cell);

            internal RecTuningTable Publish() => new RecTuningTable(
                label, description, hint, labelRect, descriptionRect,
                hintRect, cells);
        }

        /// Consecutive descriptor runs rendered as one table row of compact
        /// value cells instead of individual captioned rows.
        private enum RecTuningTableGroup
        {
            None,
            ChampionMultiplier,
            ChampionTieBreak,
            OrderingPoints,
            MinimumPick,
            CategoryPoints,
            TimePoints,
            HunterTiers,
        }

        private static RecTuningTableGroup GroupOf(
            RecommendationTuningOption option)
        {
            switch (option)
            {
                case RecommendationTuningOption.ChampionAwfulMultiplierQuarterUnits:
                case RecommendationTuningOption.ChampionPoorMultiplierQuarterUnits:
                case RecommendationTuningOption.ChampionNeutralMultiplierQuarterUnits:
                case RecommendationTuningOption.ChampionStrongMultiplierQuarterUnits:
                case RecommendationTuningOption.ChampionGreatMultiplierQuarterUnits:
                case RecommendationTuningOption.ChampionExceptionalMultiplierQuarterUnits:
                    return RecTuningTableGroup.ChampionMultiplier;
                case RecommendationTuningOption.ChampionAwfulTieBreakPoints:
                case RecommendationTuningOption.ChampionPoorTieBreakPoints:
                case RecommendationTuningOption.ChampionNeutralTieBreakPoints:
                case RecommendationTuningOption.ChampionStrongTieBreakPoints:
                case RecommendationTuningOption.ChampionGreatTieBreakPoints:
                case RecommendationTuningOption.ChampionExceptionalTieBreakPoints:
                    return RecTuningTableGroup.ChampionTieBreak;
                case RecommendationTuningOption.OrderingAwfulSignalPoints:
                case RecommendationTuningOption.OrderingPoorSignalPoints:
                case RecommendationTuningOption.OrderingNeutralSignalPoints:
                case RecommendationTuningOption.OrderingStrongSignalPoints:
                case RecommendationTuningOption.OrderingGreatSignalPoints:
                case RecommendationTuningOption.OrderingExceptionalSignalPoints:
                    return RecTuningTableGroup.OrderingPoints;
                case RecommendationTuningOption.FirstMinimumPickBonus:
                case RecommendationTuningOption.SecondMinimumPickBonus:
                case RecommendationTuningOption.ThirdMinimumPickBonus:
                case RecommendationTuningOption.LaterMinimumPickBonus:
                    return RecTuningTableGroup.MinimumPick;
                case RecommendationTuningOption.OrderingImportantCategoryPoints:
                case RecommendationTuningOption.OrderingOptionalCategoryPoints:
                    return RecTuningTableGroup.CategoryPoints;
                case RecommendationTuningOption.OrderingPartTimePoints:
                case RecommendationTuningOption.OrderingOpportunisticPoints:
                    return RecTuningTableGroup.TimePoints;
                case RecommendationTuningOption.HunterFirstTierMaximum:
                case RecommendationTuningOption.HunterSecondTierMaximum:
                case RecommendationTuningOption.HunterThirdTierMaximum:
                case RecommendationTuningOption.HunterFourthTierMaximum:
                    return RecTuningTableGroup.HunterTiers;
                default:
                    return RecTuningTableGroup.None;
            }
        }

        private static (string labelKey, string descKey) GroupKeys(
            RecTuningTableGroup group)
        {
            switch (group)
            {
                case RecTuningTableGroup.ChampionMultiplier:
                    return ("WR_RecTuneChampionMultiplierRow",
                        "WR_RecTuneChampionMultiplierRowDesc");
                case RecTuningTableGroup.ChampionTieBreak:
                    return ("WR_RecTuneChampionTieBreakRow",
                        "WR_RecTuneChampionTieBreakRowDesc");
                case RecTuningTableGroup.OrderingPoints:
                    return ("WR_RecTuneOrderingPointsRow",
                        "WR_RecTuneOrderingPointsRowDesc");
                case RecTuningTableGroup.MinimumPick:
                    return ("WR_RecTuneMinimumPickRow",
                        "WR_RecTuneMinimumPickRowDesc");
                case RecTuningTableGroup.CategoryPoints:
                    return ("WR_RecTuneCategoryPointsRow",
                        "WR_RecTuneCategoryPointsRowDesc");
                case RecTuningTableGroup.TimePoints:
                    return ("WR_RecTuneTimePointsRow",
                        "WR_RecTuneTimePointsRowDesc");
                default:
                    return ("WR_RecTuneHunterTiersRow",
                        "WR_RecTuneHunterTiersRowDesc");
            }
        }

        private static int GroupColumns(RecTuningTableGroup group) =>
            group == RecTuningTableGroup.CategoryPoints || group == RecTuningTableGroup.TimePoints ? 2
            : group == RecTuningTableGroup.MinimumPick
            || group == RecTuningTableGroup.HunterTiers ? 4 : 6;

        /// Signal columns carry the skill-tooltip verdict colors and a
        /// keyed tooltip (rendered through WrTip); pick and tier columns are
        /// plain ordinals, category and time columns their full editor labels.
        /// The multiplier row's Awful column carries the admits-Awful caveat
        /// in its tooltip.
        private static (string text, Color color, string tipKey) HeaderFor(
            RecTuningTableGroup group, int column)
        {
            switch (group)
            {
                case RecTuningTableGroup.ChampionMultiplier:
                case RecTuningTableGroup.ChampionTieBreak:
                case RecTuningTableGroup.OrderingPoints:
                    var bucket = (SignalBucket)column;
                    string tipKey = group == RecTuningTableGroup.ChampionMultiplier
                        && bucket == SignalBucket.Awful
                        ? "WR_RecTuneMultiplierAwfulTip"
                        : VerdictKey(bucket);
                    return (SignalLetter(bucket),
                        Signals.SkillSignalPresentation.VerdictColor(bucket),
                        tipKey);
                case RecTuningTableGroup.MinimumPick:
                    return (column == 3 ? "4+" : (column + 1).ToString(),
                        WrStyle.CaptionText, null);
                case RecTuningTableGroup.CategoryPoints:
                    return ((column == 0 ? "WR_RoleCategoryImportant" : "WR_RoleCategoryOptional").Translate(), WrStyle.CaptionText, null);
                case RecTuningTableGroup.TimePoints:
                    return ((column == 0 ? "WR_RoleTimePartTime" : "WR_RoleTimeOpportunistic").Translate(), WrStyle.CaptionText, null);
                default:
                    return ((column + 1).ToString(), WrStyle.CaptionText, null);
            }
        }

        private static string SignalLetter(SignalBucket bucket)
        {
            switch (bucket)
            {
                case SignalBucket.Awful: return "WR_SignalLetterAwful".Translate();
                case SignalBucket.Poor: return "WR_SignalLetterPoor".Translate();
                case SignalBucket.Neutral: return "WR_SignalLetterNeutral".Translate();
                case SignalBucket.Strong: return "WR_SignalLetterStrong".Translate();
                case SignalBucket.Great: return "WR_SignalLetterGreat".Translate();
                default: return "WR_SignalLetterExceptional".Translate();
            }
        }

        private static string VerdictKey(SignalBucket bucket)
        {
            switch (bucket)
            {
                case SignalBucket.Awful: return "WR_VerdictAwful";
                case SignalBucket.Poor: return "WR_VerdictPoor";
                case SignalBucket.Neutral: return "WR_VerdictNeutral";
                case SignalBucket.Strong: return "WR_VerdictStrong";
                case SignalBucket.Great: return "WR_VerdictGreat";
                default: return "WR_VerdictExceptional";
            }
        }

        private static string FormatTuningValue(
            RecommendationTuningValueKind kind,
            int value)
        {
            if (kind == RecommendationTuningValueKind.QuarterMultiplier)
                return (value * 0.25).ToString(
                    "0.##", CultureInfo.InvariantCulture) + "×";
            if (kind == RecommendationTuningValueKind.SignalBucket)
            {
                switch ((SignalBucket)value)
                {
                    case SignalBucket.Awful: return "WR_VerdictAwful".Translate();
                    case SignalBucket.Poor: return "WR_VerdictPoor".Translate();
                    case SignalBucket.Neutral: return "WR_VerdictNeutral".Translate();
                    case SignalBucket.Strong: return "WR_VerdictStrong".Translate();
                    case SignalBucket.Great: return "WR_VerdictGreat".Translate();
                    default: return "WR_VerdictExceptional".Translate();
                }
            }
            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal void EnsureHelpLayout(float width)
        {
            if (helpWidth == width) return;
            helpWidth = width;
            Text.Font = GameFont.Small;
            RecommendationOrderHelp = "WR_RecOrderHelp".Translate();
            RecommendationOrderHelpHeight = Text.CalcHeight(
                RecommendationOrderHelp, width);
        }

        internal void EnsureTips()
        {
            int languageRevision = LanguageChangeCoordinator.Revision;
            if (tipsLanguageRevision == languageRevision) return;
            tipsLanguageRevision = languageRevision;

            var recommendation = new TipModel { Title = "WR_RecOrderHeader".Translate() };
            recommendation.AddSection().Text("WR_OptRecOrderTipWhat".Translate());
            recommendation.AddSection()
                .Action("WR_ActDrag".Translate(), "WR_ActRecDrag".Translate())
                .Action("WR_ActX".Translate(), "WR_ActRecX".Translate());
            recommendation.AddSection().Text("WR_OptRecOrderTipAuto".Translate(), dim: true);
            var rebuiltRecommendation =
                new StructuredTip("options:recommendation-order", recommendation);
            if (RecommendationOrderTip == null
                || !RecommendationOrderTip.ContentEquals(rebuiltRecommendation))
                RecommendationOrderTip = rebuiltRecommendation;

            var training = new TipModel { Title = "WR_TrainingSection".Translate() };
            training.AddSection().Text("WR_TrainingTipWhat".Translate());
            training.AddSection().Text("WR_TrainingHelp".Translate());
            training.AddSection()
                .Text("WR_TrainingTipBands".Translate(), dim: true)
                .Text("WR_TrainingTipOrder".Translate(), dim: true);
            var rebuiltTraining = new StructuredTip("options:training", training);
            if (TrainingTip == null || !TrainingTip.ContentEquals(rebuiltTraining))
                TrainingTip = rebuiltTraining;

        }

        internal void EnsurePanels(float width)
        {
            if (ReferenceEquals(panelsOrder, orderSnapshot)
                && panelsWidth == width) return;
            var rebuilt = new List<RecRolePanel>();
            string headerLabel = "WR_RecRolePanel".Translate();
            string help;
            float helpHeight;
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                help = "WR_RecRolePanelHelp".Translate();
                helpHeight = Text.CalcHeight(help, width);
                if (orderSnapshot != null)
                    for (int i = 0; i < orderSnapshot.Count; i++)
                    {
                        RoleChipRenderData chip = orderSnapshot.ChipAt(i).Chip;
                        rebuilt.Add(new RecRolePanel(chip,
                            RoleChipUI.WidthFor(chip, showRemove: false)));
                    }
            }
            finally
            {
                Text.Font = previousFont;
            }
            panelsSnapshot = new RecRolePanelsSnapshot(
                rebuilt, headerLabel, help, helpHeight);
            panelsOrder = orderSnapshot;
            panelsWidth = width;
        }

        /// Null when the role is gone: the view must collapse the accordion
        /// before layout.
        internal RecRoleDetailSnapshot EnsureDetail(
            RoleStore store, int roleId, float width)
        {
            float smallLineHeight = Text.LineHeightOf(GameFont.Small);
            if (detailStamp == UiVersion.Current
                && detailDefinitionRevision == DefinitionReloadCoordinator.Revision
                && ReferenceEquals(detailStore, store)
                && detailRoleId == roleId && detailWidth == width
                && detailSmallLineHeight == smallLineHeight)
                return detail;
            detailStamp = UiVersion.Current;
            detailDefinitionRevision = DefinitionReloadCoordinator.Revision;
            detailStore = store;
            detailRoleId = roleId;
            detailWidth = width;
            detailSmallLineHeight = smallLineHeight;
            detail = null;

            Role role = store.RoleById(roleId);
            if (role == null) return null;

            // Skill facts render as compact comma-separated rows in the right
            // half column. Every inline segment keeps its own premeasured hit
            // region so effect and gate details remain individually hoverable.
            Text.Font = GameFont.Small;
            float halfWidth = Mathf.Max(0f, (width - 12f) / 2f);
            string workTypesCaption = "WR_WorkTypesLabel".Translate().ToString();
            string usedCaption = "WR_UsedSkillsLabel".Translate().ToString();
            string trainedCaption = "WR_TrainedSkillsLabel".Translate().ToString();
            string gatedCaption = "WR_GatedSkillsLabel".Translate().ToString();
            string requiredCaption = "WR_RequiredSkillsLabel".Translate().ToString();
            string addSkillLabel = "WR_AddSkill".Translate().ToString();
            float desiredAddSkillWidth = WrText.FitWidth(addSkillLabel) + 16f;
            float desiredCaptionWidth = Mathf.Max(72f,
                Mathf.Max(WrText.FitWidth(workTypesCaption),
                    Mathf.Max(WrText.FitWidth(usedCaption),
                        Mathf.Max(WrText.FitWidth(trainedCaption),
                            Mathf.Max(WrText.FitWidth(gatedCaption),
                                WrText.FitWidth(requiredCaption)))))) + 8f;
            float derivedRowHeight = Mathf.Ceil(smallLineHeight);
            float requiredRowHeight = Mathf.Max(24f, derivedRowHeight);

            const float minimumSkillWidth = 32f;
            const float requiredLabelGap = 4f;
            const float removeWidth = 24f;
            const float addGap = 8f;
            float requiredMinimum = minimumSkillWidth
                + requiredLabelGap + removeWidth;
            bool hasRequiredSkills = role.requiredSkills.Count > 0;
            float minimumAddSkillWidth = Mathf.Min(
                desiredAddSkillWidth, 40f);
            float reservedRightWidth = minimumAddSkillWidth
                + (hasRequiredSkills ? addGap + requiredMinimum : 0f);
            float captionWidth = Mathf.Min(desiredCaptionWidth,
                Mathf.Max(0f, halfWidth - reservedRightWidth));
            float skillWidth = Mathf.Max(0f, halfWidth - captionWidth);
            float addFloor = Mathf.Min(minimumAddSkillWidth,
                Mathf.Max(0f, skillWidth
                    - (hasRequiredSkills ? addGap : 0f)));
            float fullAddCapacity = Mathf.Max(0f, skillWidth
                - (hasRequiredSkills ? requiredMinimum + addGap : 0f));
            float addSkillWidth = Mathf.Min(desiredAddSkillWidth,
                Mathf.Max(addFloor, fullAddCapacity));
            if (addSkillWidth < desiredAddSkillWidth)
            {
                float addTextWidth = Mathf.Max(0f, addSkillWidth - 16f);
                addSkillLabel = addTextWidth > 0f
                    ? addSkillLabel.Truncate(addTextWidth)
                    : string.Empty;
            }
            float requiredAddX = halfWidth - addSkillWidth;
            float actualAddGap = hasRequiredSkills && addSkillWidth > 0f
                ? Mathf.Min(addGap,
                    Mathf.Max(0f, requiredAddX - captionWidth))
                : 0f;
            float requiredRemoveRight = requiredAddX - actualAddGap;
            float requiredRemoveWidth = Mathf.Min(removeWidth,
                Mathf.Max(0f, requiredRemoveRight - captionWidth));
            float requiredRemoveX = requiredRemoveRight - requiredRemoveWidth;
            float actualLabelGap = requiredRemoveWidth > 0f
                ? Mathf.Min(requiredLabelGap,
                    Mathf.Max(0f, requiredRemoveX - captionWidth))
                : 0f;
            float requiredSkillWidth = Mathf.Max(0f,
                requiredRemoveX - actualLabelGap - captionWidth);
            var workTypeChips = new List<RecSkillChip>();
            var usedChips = new List<RecSkillChip>();
            var trainedChips = new List<RecSkillChip>();
            var gatedChips = new List<RecSkillChip>();
            var requiredChips = new List<RecSkillChip>();
            var present = new HashSet<string>();
            RoleWorkSpec workSpec = RoleWorkSpecs.For(role);
            // The role's work-type capabilities, in capability order; each
            // chip carries the shared work-type tooltip.
            foreach (RoleWorkCapabilitySpec capability in workSpec.Capabilities)
                workTypeChips.Add(new RecSkillChip(
                    capability.WorkTypeDefName,
                    WorkTypeLabel(capability.WorkTypeDefName),
                    default,
                    JobSkillProfiles.WorkTypeStructuredTip(
                        capability.WorkTypeDefName)));
            foreach (RoleSkillFact skill in workSpec.Skills)
            {
                string label = SkillLabel(skill.SkillDefName);
                if (skill.UsedGivers > 0)
                    usedChips.Add(new RecSkillChip(skill.SkillDefName, label,
                        default, EffectTip(roleId, skill, label)));
                if (skill.TrainedGivers > 0)
                    trainedChips.Add(new RecSkillChip(
                        skill.SkillDefName, label, default));
                if (skill.GatedContents > 0)
                    gatedChips.Add(new RecSkillChip(skill.SkillDefName, label,
                        default, GateTip(roleId, workSpec, skill.SkillDefName, label)));
            }
            LayoutInlineSkillChips(workTypeChips, skillWidth, derivedRowHeight);
            LayoutInlineSkillChips(usedChips, skillWidth, derivedRowHeight);
            LayoutInlineSkillChips(trainedChips, skillWidth, derivedRowHeight);
            LayoutInlineSkillChips(gatedChips, skillWidth, derivedRowHeight);
            float requiredHeight = LayoutRequiredSkillChips(
                role.requiredSkills, requiredRowHeight, requiredChips, present);

            // Unskilled roles have no skill progression to train: their panel
            // drops the training path section entirely. Every skilled role
            // owns a path with at least itself (synthesized when unstored).
            bool showTraining = !RecsAdapter.IsUnskilledRole(role);
            var paths = new List<RecPathView>();
            if (showTraining)
                paths.Add(BuildPathView(store, role));

            detail = new RecRoleDetailSnapshot(
                roleId,
                "WR_RecClassificationSection".Translate().ToString(),
                "WR_RecSkillsSection".Translate().ToString(),
                "WR_RecScalingSection".Translate().ToString(),
                "WR_RoleCategoryLabel".Translate().ToString(),
                (int)role.category,
                new[]
                {
                    "WR_RoleCategoryOptional".Translate().ToString(),
                    "WR_RoleCategoryNormal".Translate().ToString(),
                    "WR_RoleCategoryImportant".Translate().ToString(),
                },
                "WR_RoleTimeLabel".Translate().ToString(),
                (int)role.time,
                new[]
                {
                    "WR_RoleTimePartTime".Translate().ToString(),
                    "WR_RoleTimeFullTime".Translate().ToString(),
                    "WR_RoleTimeOpportunistic".Translate().ToString(),
                },
                "WR_ChampionPenalty".Translate().ToString(),
                role.championPenalty,
                captionWidth, derivedRowHeight, requiredRowHeight,
                requiredSkillWidth, requiredRemoveX, requiredRemoveWidth,
                requiredAddX,
                workTypesCaption,
                workTypeChips, derivedRowHeight,
                usedCaption,
                usedChips, derivedRowHeight,
                trainedCaption,
                trainedChips, derivedRowHeight,
                gatedCaption,
                gatedChips, derivedRowHeight,
                requiredCaption,
                requiredChips, requiredHeight,
                present,
                addSkillLabel, addSkillWidth,
                "WR_RoleColonyMinLabel".Translate().ToString(),
                role.colonyMin,
                role.colonyMin.ToString(CultureInfo.InvariantCulture),
                "WR_RoleCoverageLabel".Translate().ToString(),
                role.coverage,
                role.coverage.ToString(CultureInfo.InvariantCulture),
                "WR_TrainingSection".Translate().ToString(),
                showTraining,
                paths);
            return detail;
        }

        /// One derived row, with each comma-separated skill carrying a cached
        /// local draw/hit rectangle. Widths and strings are built only behind
        /// the detail snapshot's explicit revision/width invalidation gate.
        private static void LayoutInlineSkillChips(
            List<RecSkillChip> chips, float width, float rowHeight)
        {
            float x = 0f;
            for (int i = 0; i < chips.Count; i++)
            {
                RecSkillChip chip = chips[i];
                string label = i + 1 < chips.Count
                    ? chip.Label + ", " : chip.Label;
                float remaining = Mathf.Max(0f, width - x);
                float fullWidth = WrText.FitWidth(label);
                float fittedWidth = Mathf.Min(remaining, fullWidth);
                if (fullWidth > remaining)
                    label = remaining > 0f
                        ? label.Truncate(remaining) : string.Empty;
                chips[i] = new RecSkillChip(
                    chip.DefName, label,
                    new Rect(x, 0f, fittedWidth, rowHeight), chip.Tip);
                x += fittedWidth;
            }
        }

        /// Required skills remain editable one per row. The row is never
        /// shorter than either the Small font's live line box or its 24px
        /// controls, so glyphs and buttons cannot clip.
        private static float LayoutRequiredSkillChips(
            IReadOnlyList<string> skillDefNames, float rowHeight,
            List<RecSkillChip> chips, HashSet<string> present)
        {
            for (int i = 0; i < skillDefNames.Count; i++)
            {
                string defName = skillDefNames[i];
                present?.Add(defName);
                chips.Add(new RecSkillChip(defName, SkillLabel(defName), default));
            }
            return Mathf.Max(1, chips.Count) * rowHeight;
        }

        private static string SkillLabel(string defName)
        {
            SkillDef skill = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            return skill == null ? defName
                : (skill.skillLabel ?? skill.label ?? skill.defName).CapitalizeFirst();
        }

        private static string WorkTypeLabel(string defName)
        {
            WorkTypeDef workType =
                DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            return workType == null ? defName
                : (workType.gerundLabel ?? workType.labelShort ?? workType.defName)
                    .CapitalizeFirst();
        }

        /// Effect kinds as localized phrases; null when nothing is known so
        /// no unsupported effect claim is invented for modded work.
        private static StructuredTip EffectTip(
            int roleId, RoleSkillFact skill, string label)
        {
            if (skill.Effects == RoleWorkEffect.Unspecified) return null;
            var model = new TipModel { Title = label };
            var section = model.AddSection();
            if ((skill.Effects & RoleWorkEffect.Speed) != 0)
                section.Text("WR_EffectSpeed".Translate().ToString());
            if ((skill.Effects & RoleWorkEffect.Quality) != 0)
                section.Text("WR_EffectQuality".Translate().ToString());
            if ((skill.Effects & RoleWorkEffect.Yield) != 0)
                section.Text("WR_EffectYield".Translate().ToString());
            if ((skill.Effects & RoleWorkEffect.Success) != 0)
                section.Text("WR_EffectSuccess".Translate().ToString());
            return new StructuredTip(
                $"rec-skill-effects:{roleId}:{skill.SkillDefName}", model);
        }

        /// The exact content minimums behind a gate-bearing skill, capped so
        /// content-heavy roles stay readable.
        private static StructuredTip GateTip(
            int roleId, RoleWorkSpec spec, string skillDefName, string label)
        {
            const int MaxLines = 12;
            var model = new TipModel { Title = label };
            var section = model.AddSection();
            int lines = 0;
            int more = 0;
            foreach (RoleWorkCapabilitySpec capability in spec.Capabilities)
                foreach (RoleWorkGiverSpec giver in capability.Givers)
                    foreach (RoleWorkContentSpec content in giver.Contents)
                        foreach (RoleContentGate gate in content.Gates)
                        {
                            if (gate.SkillDefName != skillDefName
                                || content.DefName == null)
                                continue;
                            if (lines >= MaxLines)
                            {
                                more++;
                                continue;
                            }
                            section.Fact(ContentLabel(content),
                                "WR_TipLevelN".Translate(
                                    gate.MinimumLevel).ToString());
                            lines++;
                        }
            if (more > 0)
                section.Text(
                    "WR_RecMoreContentGates".Translate(more).ToString(),
                    dim: true);
            if (lines == 0 && more == 0) return null;
            return new StructuredTip(
                $"rec-skill-gates:{roleId}:{skillDefName}", model);
        }

        private static string ContentLabel(RoleWorkContentSpec content)
        {
            switch (content.Kind)
            {
                case RoleWorkContentKind.Recipe:
                    RecipeDef recipe =
                        DefDatabase<RecipeDef>.GetNamedSilentFail(content.DefName);
                    if (recipe != null) return recipe.LabelCap.ToString();
                    break;
                default:
                    ThingDef thing =
                        DefDatabase<ThingDef>.GetNamedSilentFail(content.DefName);
                    if (thing != null) return thing.LabelCap.ToString();
                    TerrainDef terrain =
                        DefDatabase<TerrainDef>.GetNamedSilentFail(content.DefName);
                    if (terrain != null) return terrain.LabelCap.ToString();
                    break;
            }
            return content.DefName;
        }

        /// The role's own path; an unstored (implicit) path synthesizes as the
        /// owner alone on the full skill axis.
        private static RecPathView BuildPathView(RoleStore store, Role owner)
        {
            // A candidate contributes when it trains the owner's primary or
            // gate-bearing skills, or trains its own primary skill that the
            // owner's work carries; an empty contribution is identified
            // before adding.
            RoleWorkSpec ownerSpec = RoleWorkSpecs.For(owner);
            var neededSkills = new List<string>();
            var ownerSkills = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (RoleSkillFact fact in ownerSpec.Skills)
            {
                if (fact.Primary || fact.GatedContents > 0)
                    neededSkills.Add(fact.SkillDefName);
                if (fact.UsedGivers > 0 || fact.TrainedGivers > 0)
                    ownerSkills.Add(fact.SkillDefName);
            }
            string noContributionTip = "WR_NoContributionRoleTip".Translate();

            var roleIds = new List<int>();
            var mins = new List<int>();
            var maxes = new List<int>();
            var chips = new List<RoleChipRenderData>();
            var tips = new List<StructuredTip>();
            for (int i = 0; i < owner.trainingRoleIds.Count; i++)
            {
                Role role = store.RoleById(owner.trainingRoleIds[i]);
                if (role == null) continue;
                roleIds.Add(owner.trainingRoleIds[i]);
                mins.Add(owner.trainingMins[i]);
                maxes.Add(owner.trainingMaxes[i]);
                chips.Add(RoleChipRenderData.From(role));
                tips.Add(BandChipTip(owner, role, neededSkills, ownerSkills,
                    noContributionTip));
            }
            if (roleIds.Count == 0)
            {
                roleIds.Add(owner.id);
                mins.Add(0);
                maxes.Add(SkillProgressionMath.MaxLevel);
                chips.Add(RoleChipRenderData.From(owner));
                tips.Add(BandChipTip(owner, owner, neededSkills, ownerSkills,
                    noContributionTip));
            }

            List<int> rows = SkillProgressionMath.PackRows(
                mins.Select((min, i) => (min, maxes[i])).ToList());
            int rowCount = rows.Count == 0 ? 1 : rows.Max() + 1;
            int displayRows = rowCount + 1;
            var presentRoleIds = new HashSet<int>(roleIds);
            var addOptions = new List<RecRoleMenuOption>();
            for (int i = 0; i < store.roles.Count; i++)
            {
                Role role = store.roles[i];
                if (!IsNormalTrainingRole(role)
                    || presentRoleIds.Contains(role.id)) continue;
                List<string> contribution = ContributionTo(
                    neededSkills, ownerSkills, role);
                bool contributes = contribution.Count > 0;
                string label = contributes ? role.label
                    : role.label.Colorize(new Color(0.62f, 0.62f, 0.62f));
                string tip = contributes
                    ? "WR_ContributesTip".Translate(
                        contribution.Select(SkillLabel).ToCommaList()).ToString()
                    : noContributionTip;
                addOptions.Add(new RecRoleMenuOption(
                    role.id, label, tip,
                    contributes ? 0 : 1, role.label));
            }
            addOptions.Sort(RecRoleMenuOption.CompareByLabel);
            return new RecPathView(
                owner.id,
                roleIds,
                mins,
                maxes,
                rows,
                chips,
                addOptions,
                displayRows,
                tips);
        }

        /// One placed band chip's tip: what the entry trains toward the
        /// owner (the owner's own chip carries just the drag hint), with the
        /// band drag hint folded into the same region.
        private static StructuredTip BandChipTip(
            Role owner,
            Role entry,
            IReadOnlyList<string> neededSkills,
            HashSet<string> ownerSkills,
            string noContributionTip)
        {
            var model = new TipModel { Title = entry.label };
            if (entry.id != owner.id)
            {
                List<string> contribution = ContributionTo(
                    neededSkills, ownerSkills, entry);
                model.AddSection().Text(contribution.Count > 0
                    ? "WR_ContributesTip".Translate(
                        contribution.Select(SkillLabel).ToCommaList()).ToString()
                    : noContributionTip);
            }
            model.AddSection().Text(
                "WR_BandChipTip".Translate().ToString(), dim: true);
            return new StructuredTip(
                $"rec-path-entry:{owner.id}:{entry.id}", model);
        }

        private static List<string> ContributionTo(
            IReadOnlyList<string> neededSkills,
            HashSet<string> ownerSkills,
            Role candidate)
        {
            var contribution = new List<string>();
            RoleWorkSpec spec = RoleWorkSpecs.For(candidate);
            foreach (RoleSkillFact fact in spec.Skills)
                if (fact.Participates && fact.TrainedGivers > 0
                    && (neededSkills.Contains(fact.SkillDefName)
                        || fact.Primary
                        && ownerSkills.Contains(fact.SkillDefName)))
                    contribution.Add(fact.SkillDefName);
            return contribution;
        }

        private static bool IsNormalTrainingRole(Role role) =>
            !role.blocker && !role.HasRules;

        private static List<RecOrderChip> LayoutOrderChips(float width,
            IReadOnlyList<int> roleIds,
            IReadOnlyDictionary<int, RoleChipRenderData> chipsById,
            IReadOnlyDictionary<int, RoleView> viewsById,
            out Rect addRect, out float height, out string addLabel)
        {
            var result = new List<RecOrderChip>(roleIds.Count);
            float x = 0f;
            float y = 0f;
            Rect Place(float itemWidth)
            {
                if (x + itemWidth > width && x > 0f)
                {
                    x = 0f;
                    y += RoleChipUI.Height + FlowGap;
                }
                var rect = new Rect(x, y, itemWidth, RoleChipUI.Height);
                x += itemWidth + FlowGap;
                return rect;
            }

            for (int i = 0; i < roleIds.Count; i++)
            {
                int roleId = roleIds[i];
                if (!chipsById.TryGetValue(roleId, out var chip)) continue;
                viewsById.TryGetValue(roleId, out RoleView roleView);
                result.Add(new RecOrderChip(chip,
                    Place(RoleChipUI.WidthFor(chip, showRemove: true)),
                    roleView?.Hunting == true));
            }
            // Add Role pins to the panel's bottom-right corner, on a fresh row
            // when the last chip row has no room for it.
            Text.Font = GameFont.Small;
            addLabel = "WR_AddRole".Translate();
            float addWidth = WrText.FitWidth(addLabel) + 16f;
            if (x + addWidth > width && x > 0f)
                y += RoleChipUI.Height + FlowGap;
            addRect = new Rect(width - addWidth, y, addWidth, RoleChipUI.Height);
            height = y + RoleChipUI.Height;
            return result;
        }
    }

    internal readonly struct RecRoleMenuOption
    {
        internal RecRoleMenuOption(int roleId, string label, string tooltip,
            int sortTier = 0, string sortLabel = null)
        {
            RoleId = roleId;
            Label = label;
            Tooltip = tooltip;
            SortTier = sortTier;
            SortLabel = sortLabel ?? label;
        }

        internal int RoleId { get; }
        internal string Label { get; }
        internal string Tooltip { get; }
        private int SortTier { get; }
        private string SortLabel { get; }

        internal bool ContentEquals(RecRoleMenuOption other) =>
            RoleId == other.RoleId
            && string.Equals(Label, other.Label,
                System.StringComparison.Ordinal)
            && string.Equals(Tooltip, other.Tooltip,
                System.StringComparison.Ordinal)
            && SortTier == other.SortTier
            && string.Equals(SortLabel, other.SortLabel,
                System.StringComparison.Ordinal);

        internal static int CompareByLabel(
            RecRoleMenuOption left, RecRoleMenuOption right)
        {
            int tier = left.SortTier.CompareTo(right.SortTier);
            return tier != 0 ? tier
                : System.StringComparer.OrdinalIgnoreCase.Compare(
                    left.SortLabel, right.SortLabel);
        }
    }

    internal readonly struct RecOrderChip
    {
        internal RecOrderChip(RoleChipRenderData chip, Rect rect, bool locked)
        {
            Chip = chip;
            Rect = rect;
            Locked = locked;
        }

        internal RoleChipRenderData Chip { get; }
        internal Rect Rect { get; }
        internal bool Locked { get; }

        internal bool ContentEquals(RecOrderChip other) =>
            Chip.ContentEquals(other.Chip)
            && Rect.x == other.Rect.x
            && Rect.y == other.Rect.y
            && Rect.width == other.Rect.width
            && Rect.height == other.Rect.height
            && Locked == other.Locked;
    }

    internal sealed class RecOrderSnapshot
    {
        private static readonly System.Func<RecOrderChip, Rect> ChipRect =
            chip => chip.Rect;
        private readonly List<RecOrderChip> chips;
        private readonly Dictionary<int, RoleChipRenderData> catalogChips;
        private readonly List<RecRoleMenuOption> addOptions;

        internal RecOrderSnapshot(List<RecOrderChip> chips,
            Dictionary<int, RoleChipRenderData> catalogChips,
            List<RecRoleMenuOption> addOptions, Rect addRect,
            float layoutHeight, string addLabel, string headerLabel)
        {
            this.chips = chips;
            this.catalogChips = catalogChips;
            this.addOptions = addOptions;
            AddRect = addRect;
            LayoutHeight = layoutHeight;
            AddLabel = addLabel;
            HeaderLabel = headerLabel;
        }

        internal int Count => chips.Count;
        internal RecOrderChip ChipAt(int index) => chips[index];
        internal Rect AddRect { get; }
        internal float LayoutHeight { get; }
        internal string AddLabel { get; }
        internal string HeaderLabel { get; }
        internal int AddOptionCount => addOptions.Count;
        internal RecRoleMenuOption AddOptionAt(int index) => addOptions[index];

        internal bool ContainsRole(int roleId) => IndexOfRole(roleId) >= 0;

        internal int IndexOfRole(int roleId)
        {
            for (int i = 0; i < chips.Count; i++)
                if (chips[i].Chip.RoleId == roleId) return i;
            return -1;
        }

        internal int ChipInsertIndex(Vector2 point) =>
            RoleDrag.ChipInsertIndex(point, chips, ChipRect);

        internal List<int> CopyRoleIds()
        {
            var result = new List<int>(chips.Count);
            for (int i = 0; i < chips.Count; i++)
                result.Add(chips[i].Chip.RoleId);
            return result;
        }

        internal bool TryGetCatalogChip(int roleId,
            out RoleChipRenderData chip) =>
            catalogChips.TryGetValue(roleId, out chip);

        internal bool ContentEquals(RecOrderSnapshot other)
        {
            if (other == null || chips.Count != other.chips.Count
                || catalogChips.Count != other.catalogChips.Count
                || addOptions.Count != other.addOptions.Count
                || AddRect.x != other.AddRect.x
                || AddRect.y != other.AddRect.y
                || AddRect.width != other.AddRect.width
                || AddRect.height != other.AddRect.height
                || LayoutHeight != other.LayoutHeight
                || !string.Equals(AddLabel, other.AddLabel,
                    System.StringComparison.Ordinal)
                || !string.Equals(HeaderLabel, other.HeaderLabel,
                    System.StringComparison.Ordinal))
                return false;
            for (int i = 0; i < chips.Count; i++)
                if (!chips[i].ContentEquals(other.chips[i]))
                    return false;
            for (int i = 0; i < addOptions.Count; i++)
                if (!addOptions[i].ContentEquals(other.addOptions[i]))
                    return false;
            foreach (KeyValuePair<int, RoleChipRenderData> pair in catalogChips)
                if (!other.catalogChips.TryGetValue(pair.Key, out var otherChip)
                    || !pair.Value.ContentEquals(otherChip))
                    return false;
            return true;
        }
    }

    internal sealed class RecTuningSnapshot
    {
        private readonly List<RecTuningSection> sections;

        internal RecTuningSnapshot(List<RecTuningSection> sections,
            string resetLabel, string headerLabel, string globalHelp,
            float globalHelpHeight)
        {
            this.sections = sections;
            ResetLabel = resetLabel;
            HeaderLabel = headerLabel;
            GlobalHelp = globalHelp;
            GlobalHelpHeight = globalHelpHeight;
        }

        internal int Count => sections.Count;
        internal RecTuningSection SectionAt(int index) => sections[index];
        internal string ResetLabel { get; }
        internal string HeaderLabel { get; }
        internal string GlobalHelp { get; }
        internal float GlobalHelpHeight { get; }
    }

    internal sealed class RecRolePanelsSnapshot
    {
        private readonly List<RecRolePanel> panels;

        internal RecRolePanelsSnapshot(List<RecRolePanel> panels,
            string headerLabel, string help, float helpHeight)
        {
            this.panels = panels;
            HeaderLabel = headerLabel;
            Help = help;
            HelpHeight = helpHeight;
        }

        internal int Count => panels.Count;
        internal RecRolePanel PanelAt(int index) => panels[index];
        internal string HeaderLabel { get; }
        internal string Help { get; }
        internal float HelpHeight { get; }
    }

    internal sealed class RecTuningSection
    {
        private readonly List<RecTuningItem> items;

        internal RecTuningSection(string key, string label, string intro,
            float introHeight, List<RecTuningItem> items, float height)
        {
            Key = key;
            Label = label;
            Intro = intro;
            IntroHeight = introHeight;
            this.items = items;
            Height = height;
        }

        /// The section's language key: doubles as the stable synced-command
        /// identifier for ResetRecommendationTuningSection.
        internal string Key { get; }
        internal string Label { get; }
        internal string Intro { get; }
        internal float IntroHeight { get; }
        internal int Count => items.Count;
        internal RecTuningItem ItemAt(int index) => items[index];
        internal float Height { get; }
    }

    /// One vertical slot in a tuning section: a captioned single-value row or
    /// a compact multi-column value table (exactly one is set).
    internal readonly struct RecTuningItem
    {
        internal RecTuningItem(RecommendationTuningRow row)
        {
            Row = row;
            Table = null;
        }

        internal RecTuningItem(RecTuningTable table)
        {
            Row = null;
            Table = table;
        }

        internal RecommendationTuningRow Row { get; }
        internal RecTuningTable Table { get; }
    }

    internal sealed class RecTuningTable
    {
        internal const float CellW = 46f;
        internal const float CellGap = 4f;
        internal const float HeaderH = 16f;
        internal const float CellH = 26f;
        internal const float HintH = 14f;

        private readonly List<RecTuningTableCell> cells;

        internal RecTuningTable(string label, string description, string hint,
            Rect labelRect, Rect descriptionRect, Rect hintRect,
            List<RecTuningTableCell> cells)
        {
            Label = label;
            Description = description;
            Hint = hint;
            LabelRect = labelRect;
            DescriptionRect = descriptionRect;
            HintRect = hintRect;
            this.cells = cells;
        }

        internal string Label { get; }
        internal string Description { get; }
        internal string Hint { get; }
        internal Rect LabelRect { get; }
        internal Rect DescriptionRect { get; }
        internal Rect HintRect { get; }
        internal int CellCount => cells.Count;
        internal RecTuningTableCell CellAt(int index) => cells[index];
    }

    internal readonly struct RecTuningTableCell
    {
        internal RecTuningTableCell(RecommendationTuningDescriptor descriptor,
            int value, string valueLabel, string header, Color headerColor,
            string headerTip, Rect headerRect, Rect cellRect)
        {
            Descriptor = descriptor;
            Value = value;
            ValueLabel = valueLabel;
            Header = header;
            HeaderColor = headerColor;
            HeaderTip = headerTip;
            HeaderRect = headerRect;
            CellRect = cellRect;
        }

        internal RecommendationTuningDescriptor Descriptor { get; }
        internal int Value { get; }
        internal string ValueLabel { get; }
        internal string Header { get; }
        internal Color HeaderColor { get; }
        internal string HeaderTip { get; }
        internal Rect HeaderRect { get; }
        internal Rect CellRect { get; }
    }

    internal readonly struct RecRolePanel
    {
        internal RecRolePanel(RoleChipRenderData chip, float chipWidth)
        {
            Chip = chip;
            ChipWidth = chipWidth;
        }

        internal RoleChipRenderData Chip { get; }
        internal float ChipWidth { get; }
    }

    internal readonly struct RecSkillChip
    {
        internal RecSkillChip(string defName, string label, Rect rect,
            StructuredTip tip = null)
        {
            DefName = defName;
            Label = label;
            Rect = rect;
            Tip = tip;
        }

        /// Effect kinds or gated-content lines; null renders no tip region.
        internal StructuredTip Tip { get; }

        internal string DefName { get; }
        internal string Label { get; }
        internal Rect Rect { get; }
    }

    internal sealed class RecRoleDetailSnapshot
    {
        private readonly List<RecSkillChip> workTypeChips;
        private readonly List<RecSkillChip> usedChips;
        private readonly List<RecSkillChip> trainedChips;
        private readonly List<RecSkillChip> gatedChips;
        private readonly List<RecSkillChip> requiredChips;
        private readonly HashSet<string> presentSkills;
        private readonly List<RecPathView> paths;

        internal RecRoleDetailSnapshot(int roleId,
            string classificationHeader, string skillsHeader,
            string scalingHeader,
            string categoryCaption, int categoryValue,
            IReadOnlyList<string> categoryOptions,
            string timeCaption, int timeValue,
            IReadOnlyList<string> timeOptions,
            string championLabel, bool championPenalty,
            float skillCaptionWidth, float derivedSkillRowHeight,
            float requiredSkillRowHeight, float requiredSkillWidth,
            float requiredRemoveX, float requiredRemoveWidth,
            float requiredAddX,
            string workTypesCaption, List<RecSkillChip> workTypeChips,
            float workTypesHeight,
            string usedCaption, List<RecSkillChip> usedChips,
            float usedHeight,
            string trainedCaption, List<RecSkillChip> trainedChips,
            float trainedHeight,
            string gatedCaption, List<RecSkillChip> gatedChips,
            float gatedHeight,
            string requiredCaption, List<RecSkillChip> requiredChips,
            float requiredHeight,
            HashSet<string> presentSkills, string addSkillLabel,
            float addSkillWidth,
            string colonyMinCaption, int colonyMin, string colonyMinLabel,
            string coverageCaption, int coverage, string coverageLabel,
            string trainingHeader, bool showTrainingSection,
            List<RecPathView> paths)
        {
            RoleId = roleId;
            ClassificationHeader = classificationHeader;
            SkillsHeader = skillsHeader;
            ScalingHeader = scalingHeader;
            CategoryCaption = categoryCaption;
            CategoryValue = categoryValue;
            CategoryOptions = categoryOptions;
            TimeCaption = timeCaption;
            TimeValue = timeValue;
            TimeOptions = timeOptions;
            ChampionLabel = championLabel;
            ChampionPenalty = championPenalty;
            SkillCaptionWidth = skillCaptionWidth;
            DerivedSkillRowHeight = derivedSkillRowHeight;
            RequiredSkillRowHeight = requiredSkillRowHeight;
            RequiredSkillWidth = requiredSkillWidth;
            RequiredRemoveX = requiredRemoveX;
            RequiredRemoveWidth = requiredRemoveWidth;
            RequiredAddX = requiredAddX;
            WorkTypesCaption = workTypesCaption;
            this.workTypeChips = workTypeChips;
            WorkTypesHeight = workTypesHeight;
            UsedCaption = usedCaption;
            this.usedChips = usedChips;
            UsedHeight = usedHeight;
            TrainedCaption = trainedCaption;
            this.trainedChips = trainedChips;
            TrainedHeight = trainedHeight;
            GatedCaption = gatedCaption;
            this.gatedChips = gatedChips;
            GatedHeight = gatedHeight;
            RequiredCaption = requiredCaption;
            this.requiredChips = requiredChips;
            RequiredHeight = requiredHeight;
            this.presentSkills = presentSkills;
            AddSkillLabel = addSkillLabel;
            AddSkillWidth = addSkillWidth;
            ColonyMinCaption = colonyMinCaption;
            ColonyMin = colonyMin;
            ColonyMinLabel = colonyMinLabel;
            CoverageCaption = coverageCaption;
            Coverage = coverage;
            CoverageLabel = coverageLabel;
            TrainingHeader = trainingHeader;
            ShowTrainingSection = showTrainingSection;
            this.paths = paths;
        }

        internal int RoleId { get; }
        internal string ClassificationHeader { get; }
        internal string SkillsHeader { get; }
        internal string ScalingHeader { get; }
        internal string CategoryCaption { get; }
        internal int CategoryValue { get; }
        /// Segment labels in picker-position order (0 = leftmost).
        internal IReadOnlyList<string> CategoryOptions { get; }
        internal string TimeCaption { get; }
        internal int TimeValue { get; }
        internal IReadOnlyList<string> TimeOptions { get; }
        internal string ChampionLabel { get; }
        internal bool ChampionPenalty { get; }
        internal float SkillCaptionWidth { get; }
        internal float DerivedSkillRowHeight { get; }
        internal float RequiredSkillRowHeight { get; }
        internal float RequiredSkillWidth { get; }
        internal float RequiredRemoveX { get; }
        internal float RequiredRemoveWidth { get; }
        internal float RequiredAddX { get; }
        internal string WorkTypesCaption { get; }
        internal int WorkTypeChipCount => workTypeChips.Count;
        internal RecSkillChip WorkTypeChipAt(int index) => workTypeChips[index];
        internal float WorkTypesHeight { get; }
        internal string UsedCaption { get; }
        internal int UsedChipCount => usedChips.Count;
        internal RecSkillChip UsedChipAt(int index) => usedChips[index];
        internal float UsedHeight { get; }
        internal string TrainedCaption { get; }
        internal int TrainedChipCount => trainedChips.Count;
        internal RecSkillChip TrainedChipAt(int index) => trainedChips[index];
        internal float TrainedHeight { get; }
        internal string GatedCaption { get; }
        internal int GatedChipCount => gatedChips.Count;
        internal RecSkillChip GatedChipAt(int index) => gatedChips[index];
        internal float GatedHeight { get; }
        internal string RequiredCaption { get; }
        internal int RequiredChipCount => requiredChips.Count;
        internal RecSkillChip RequiredChipAt(int index) => requiredChips[index];
        internal float RequiredHeight { get; }
        internal bool HasSkill(string defName) => presentSkills.Contains(defName);
        internal string AddSkillLabel { get; }
        internal float AddSkillWidth { get; }
        internal string ColonyMinCaption { get; }
        internal int ColonyMin { get; }
        internal string ColonyMinLabel { get; }
        internal string CoverageCaption { get; }
        internal int Coverage { get; }
        internal string CoverageLabel { get; }
        internal string TrainingHeader { get; }
        internal bool ShowTrainingSection { get; }
        internal int PathCount => paths.Count;
        internal RecPathView PathAt(int index) => paths[index];
    }

    /// One role-owned training path projection; PathId is the owner role id.
    internal sealed class RecPathView
    {
        private readonly List<int> roleIds;
        private readonly List<int> mins;
        private readonly List<int> maxes;
        private readonly List<int> rows;
        private readonly List<RoleChipRenderData> chips;
        private readonly List<RecRoleMenuOption> addOptions;

        internal RecPathView(int pathId, List<int> roleIds,
            List<int> mins, List<int> maxes, List<int> rows,
            List<RoleChipRenderData> chips,
            List<RecRoleMenuOption> addOptions, int displayRows,
            List<StructuredTip> tips = null)
        {
            PathId = pathId;
            this.roleIds = roleIds;
            this.mins = mins;
            this.maxes = maxes;
            this.rows = rows;
            this.chips = chips;
            this.addOptions = addOptions;
            this.tips = tips;
            DisplayRows = displayRows;
        }

        private readonly List<StructuredTip> tips;

        /// Per-entry band tip: the entry's contribution to the owner plus the
        /// drag hint; null when the snapshot carried no tips.
        internal StructuredTip TipAt(int index) =>
            tips != null && index < tips.Count ? tips[index] : null;

        internal int PathId { get; }
        internal int Count => roleIds.Count;
        internal int DisplayRows { get; }
        internal int RoleIdAt(int index) => roleIds[index];
        internal int MinAt(int index) => mins[index];
        internal int MaxAt(int index) => maxes[index];
        internal int RowAt(int index) => rows[index];
        internal RoleChipRenderData ChipAt(int index) => chips[index];
        internal int AddOptionCount => addOptions.Count;
        internal RecRoleMenuOption AddOptionAt(int index) => addOptions[index];

        internal bool ContainsRole(int roleId) => IndexOfRole(roleId) >= 0;

        internal List<int> CopyRoleIds() => new List<int>(roleIds);
        internal List<int> CopyMins() => new List<int>(mins);
        internal List<int> CopyMaxes() => new List<int>(maxes);

        internal int IndexOfRole(int roleId)
        {
            for (int i = 0; i < roleIds.Count; i++)
                if (roleIds[i] == roleId) return i;
            return -1;
        }
    }
}
