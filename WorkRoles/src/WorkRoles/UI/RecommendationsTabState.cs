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

        private int orderStamp = -1;
        private float orderWidth = -1f;
        private List<int> order;
        private Dictionary<int, RoleView> orderById;
        private List<Role> orderRoles;
        private readonly List<Rect> orderLayout = new List<Rect>();

        private int tipsStamp = -1;

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
        private readonly List<RecTuningSection> tuningSections =
            new List<RecTuningSection>();

        // Cache contract — Owner: Recommendations tab. Key: (UiVersion.Current,
        // RoleStore identity, available width, language generation via explicit
        // invalidation). Value: immutable-by-publication list of the resolved
        // recommendation-order roles with measured header chip widths, plus
        // the measured help paragraph. Dependencies: the resolved
        // recommendation order (EnsureOrder must run first), role labels and
        // colors, chip label widths, width, language. Refresh: immediate on
        // key change. Equality: matching key preserves list identity.
        // Teardown: Reset/InvalidateLanguageCaches clears the list and help.
        private int panelsStamp = -1;
        private RoleStore panelsStore;
        private float panelsWidth = -1f;
        private readonly List<RecRolePanel> panels = new List<RecRolePanel>();

        // Cache contract — Owner: Recommendations tab. Key: (UiVersion.Current,
        // RoleStore identity, expanded role id, available width, language
        // generation via explicit invalidation). Value: one immutable
        // RecRoleDetailSnapshot (single slot: the accordion expands one panel).
        // Dependencies: the role's category/time/championPenalty/skill lists,
        // holder scales and training paths (embedded scale editor snapshot,
        // holder summary, path views), skill/enum labels, language, font,
        // width. Refresh: immediate on key change. Equality: matching key
        // preserves snapshot identity. Teardown: Reset/InvalidateLanguageCaches
        // releases the snapshot.
        private int detailStamp = -1;
        private RoleStore detailStore;
        private int detailRoleId = -1;
        private float detailWidth = -1f;
        private RecRoleDetailSnapshot detail;

        internal int OrderStamp => orderStamp;
        internal IReadOnlyList<int> Order => order;
        internal IReadOnlyDictionary<int, RoleView> OrderById => orderById;
        internal IReadOnlyList<Role> OrderRoles => orderRoles;
        internal IReadOnlyList<Rect> OrderLayout => orderLayout;
        internal Rect OrderAddRect { get; private set; }
        internal float OrderLayoutHeight { get; private set; }

        internal StructuredTip RecommendationOrderTip { get; private set; }
        internal StructuredTip TrainingTip { get; private set; }
        internal string RecommendationOrderHelp { get; private set; }
        internal float RecommendationOrderHelpHeight { get; private set; }
        internal IReadOnlyList<RecTuningSection> TuningSections => tuningSections;
        internal string TuningReset { get; private set; }
        internal string GlobalHelp { get; private set; }
        internal float GlobalHelpHeight { get; private set; }
        internal IReadOnlyList<RecRolePanel> Panels => panels;
        internal string PanelsHelp { get; private set; }
        internal float PanelsHelpHeight { get; private set; }

        internal void Reset()
        {
            InvalidateLanguageCaches();
        }

        internal void InvalidateLanguageCaches()
        {
            orderStamp = -1;
            order = null;
            orderById = null;
            orderRoles = null;
            orderLayout.Clear();

            tipsStamp = -1;
            RecommendationOrderTip = null;
            TrainingTip = null;

            helpWidth = -1f;
            RecommendationOrderHelp = null;
            RecommendationOrderHelpHeight = 0f;

            tuningStore = null;
            tuningRevision = -1;
            tuningWidth = -1f;
            tuningSections.Clear();
            TuningReset = null;
            GlobalHelp = null;
            GlobalHelpHeight = 0f;

            panelsStamp = -1;
            panelsStore = null;
            panelsWidth = -1f;
            panels.Clear();
            PanelsHelp = null;
            PanelsHelpHeight = 0f;

            detailStamp = -1;
            detailStore = null;
            detailRoleId = -1;
            detailWidth = -1f;
            detail = null;
        }

        internal void EnsureOrder(RoleStore store, float width)
        {
            if (orderStamp == UiVersion.Current && orderWidth == width) return;
            orderStamp = UiVersion.Current;
            orderWidth = width;

            List<RoleView> views = RecsAdapter.RoleViewsOf(store.roles);
            order = OrderTemplate.ResolveTemplate(store.recommendationOrder, views);
            orderById = views.ToDictionary(role => role.Id);
            orderRoles = order.Select(store.RoleById).Where(role => role != null).ToList();
            orderLayout.Clear();
            OrderLayoutHeight = LayoutOrderChips(
                width, orderRoles, orderLayout, out Rect addRect);
            OrderAddRect = addRect;
        }

        internal int OrderIndexOf(int roleId)
        {
            if (order == null) return -1;
            for (int i = 0; i < order.Count; i++)
                if (order[i] == roleId) return i;
            return -1;
        }

        internal void EnsureTuning(RoleStore store, float width)
        {
            if (ReferenceEquals(tuningStore, store)
                && tuningRevision == store.RecommendationTuningRevision
                && tuningWidth == width)
                return;
            tuningStore = store;
            tuningRevision = store.RecommendationTuningRevision;
            tuningWidth = width;
            tuningSections.Clear();
            TuningReset = "WR_RecTuneReset".Translate();

            RecommendationsTuningOptions options = store.recommendationTuning
                ?? RecommendationsTuningOptions.Default;
            const float rowGap = 6f;
            const float descriptionWidthReserve = 116f;
            string cellHint = "WR_RecTuneCellHint".Translate();
            string sectionKey = null;
            string sectionLabel = null;
            string sectionIntro = null;
            float sectionIntroHeight = 0f;
            List<RecTuningItem> items = null;
            RecTuningTable table = null;
            RecTuningTableGroup tableGroup = RecTuningTableGroup.None;
            float tableCellsX = 0f;
            float tableCellsY = 0f;
            float y = 0f;
            void CloseSection()
            {
                if (items != null)
                    tuningSections.Add(new RecTuningSection(
                        sectionKey, sectionLabel, sectionIntro,
                        sectionIntroHeight, items, y - rowGap));
            }
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                GlobalHelp = "WR_RecGlobalPanelHelp".Translate();
                GlobalHelpHeight = Text.CalcHeight(GlobalHelp, width);
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
                        table = null;
                        tableGroup = RecTuningTableGroup.None;
                        string label = descriptor.LabelKey.Translate();
                        string description = descriptor.DescriptionKey.Translate();
                        float descriptionHeight = Text.CalcHeight(
                            description,
                            System.Math.Max(80f, width - descriptionWidthReserve));
                        float rowHeight = System.Math.Max(
                            44f, 21f + descriptionHeight);
                        List<string> enumOptions = null;
                        List<Color> enumColors = null;
                        if (descriptor.ValueKind
                            == RecommendationTuningValueKind.SignalBucket)
                        {
                            enumOptions = new List<string>();
                            enumColors = new List<Color>();
                            for (int bucket = descriptor.MinimumValue;
                                 bucket <= descriptor.MaximumValue; bucket++)
                            {
                                enumOptions.Add(SignalLetter((SignalBucket)bucket));
                                enumColors.Add(Signals.SkillSignalPresentation
                                    .VerdictColor((SignalBucket)bucket));
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
                            enumColors)));
                        y += rowHeight + rowGap;
                        continue;
                    }

                    if (group != tableGroup)
                    {
                        tableGroup = group;
                        (string labelKey, string descKey) = GroupKeys(group);
                        // Cells right-align like the single-row controls; the
                        // caption wraps in the space left of them.
                        int columns = GroupColumns(group);
                        float cellsWidth = columns * RecTuningTable.CellW
                            + (columns - 1) * RecTuningTable.CellGap;
                        float textWidth = width - cellsWidth - 12f;
                        string description = descKey.Translate();
                        float descriptionHeight = Text.CalcHeight(
                            description, textWidth);
                        float cellsHeight = RecTuningTable.HeaderH
                            + RecTuningTable.CellH + 2f + RecTuningTable.HintH;
                        tableCellsX = width - cellsWidth;
                        tableCellsY = y;
                        table = new RecTuningTable(
                            labelKey.Translate(),
                            description,
                            cellHint,
                            new Rect(0f, y, textWidth, 21f),
                            new Rect(0f, y + 21f, textWidth, descriptionHeight),
                            new Rect(0f, y + RecTuningTable.HeaderH
                                + RecTuningTable.CellH + 2f,
                                width, RecTuningTable.HintH));
                        items.Add(new RecTuningItem(table));
                        y += System.Math.Max(
                            21f + descriptionHeight, cellsHeight) + rowGap;
                    }
                    int column = table.CellCount;
                    float cellX = tableCellsX + column
                        * (RecTuningTable.CellW + RecTuningTable.CellGap);
                    (string header, Color headerColor, string headerTipKey) =
                        HeaderFor(group, column);
                    table.AddCell(new RecTuningTableCell(
                        descriptor, value, valueLabel,
                        header, headerColor, headerTipKey,
                        new Rect(cellX, tableCellsY,
                            RecTuningTable.CellW, RecTuningTable.HeaderH),
                        new Rect(cellX, tableCellsY + RecTuningTable.HeaderH,
                            RecTuningTable.CellW, RecTuningTable.CellH)));
                }
                CloseSection();
            }
            finally
            {
                Text.Font = previousFont;
            }
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
                default:
                    return ("WR_RecTuneHunterTiersRow",
                        "WR_RecTuneHunterTiersRowDesc");
            }
        }

        private static int GroupColumns(RecTuningTableGroup group) =>
            group == RecTuningTableGroup.MinimumPick
            || group == RecTuningTableGroup.HunterTiers ? 4 : 6;

        /// Signal columns carry the skill-tooltip verdict colors and a
        /// keyed tooltip (rendered through WrTip); pick and tier columns are
        /// plain ordinals. The multiplier row's Awful column carries the
        /// admits-Awful caveat in its tooltip.
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
            if (tipsStamp == UiVersion.Current) return;
            tipsStamp = UiVersion.Current;

            var recommendation = new TipModel { Title = "WR_RecOrderHeader".Translate() };
            recommendation.AddSection().Text("WR_OptRecOrderTipWhat".Translate());
            recommendation.AddSection()
                .Action("WR_ActDrag".Translate(), "WR_ActRecDrag".Translate())
                .Action("WR_ActX".Translate(), "WR_ActRecX".Translate());
            recommendation.AddSection().Text("WR_OptRecOrderTipAuto".Translate(), dim: true);
            RecommendationOrderTip =
                new StructuredTip("options:recommendation-order", recommendation);

            var training = new TipModel { Title = "WR_TrainingSection".Translate() };
            training.AddSection().Text("WR_TrainingTipWhat".Translate());
            training.AddSection().Text("WR_TrainingHelp".Translate());
            training.AddSection()
                .Text("WR_TrainingTipBands".Translate(), dim: true)
                .Text("WR_TrainingTipOrder".Translate(), dim: true);
            TrainingTip = new StructuredTip("options:training", training);

        }

        internal void EnsurePanels(RoleStore store, float width)
        {
            if (panelsStamp == UiVersion.Current
                && ReferenceEquals(panelsStore, store)
                && panelsWidth == width) return;
            panelsStamp = UiVersion.Current;
            panelsStore = store;
            panelsWidth = width;
            panels.Clear();
            Text.Font = GameFont.Small;
            PanelsHelp = "WR_RecRolePanelHelp".Translate();
            PanelsHelpHeight = Text.CalcHeight(PanelsHelp, width);
            if (order == null) return;
            for (int i = 0; i < order.Count; i++)
            {
                Role role = store.RoleById(order[i]);
                if (role == null) continue;
                panels.Add(new RecRolePanel(role,
                    RoleChipUI.WidthFor(role, showRemove: false)));
            }
        }

        /// Null when the role is gone: the view must collapse the accordion
        /// before layout.
        internal RecRoleDetailSnapshot EnsureDetail(
            RoleStore store, int roleId, float width)
        {
            if (detailStamp == UiVersion.Current
                && ReferenceEquals(detailStore, store)
                && detailRoleId == roleId && detailWidth == width)
                return detail;
            detailStamp = UiVersion.Current;
            detailStore = store;
            detailRoleId = roleId;
            detailWidth = width;
            detail = null;

            Role role = store.RoleById(roleId);
            if (role == null) return null;

            // Skill chips flow inside the right half column.
            Text.Font = GameFont.Small;
            float halfWidth = (width - 12f) / 2f;
            var requiredChips = new List<RecSkillChip>();
            var optionalChips = new List<RecSkillChip>();
            var present = new HashSet<string>();
            float requiredHeight = LayoutSkillChips(
                role.requiredSkills, halfWidth, requiredChips, present);
            float optionalHeight = LayoutSkillChips(
                role.optionalSkills, halfWidth, optionalChips, present);

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
                "WR_RequiredSkillsLabel".Translate().ToString(),
                requiredChips, requiredHeight,
                "WR_OptionalSkillsLabel".Translate().ToString(),
                optionalChips, optionalHeight,
                present,
                "WR_AddSkill".Translate().ToString(),
                "WR_RoleColonyMinLabel".Translate().ToString(),
                role.colonyMin,
                role.colonyMin.ToString(CultureInfo.InvariantCulture),
                "WR_RoleCoverageLabel".Translate().ToString(),
                role.coverage,
                role.coverage.ToString(CultureInfo.InvariantCulture),
                showTraining,
                paths);
            return detail;
        }

        /// One 26px row per skill; the Add button shares the top row, so an
        /// empty table still spans one row. The view lays the three columns
        /// (caption, skill + remove, Add button) arithmetically.
        internal const float SkillRowPitch = 26f;

        private static float LayoutSkillChips(List<string> skillDefNames,
            float width, List<RecSkillChip> chips, HashSet<string> present)
        {
            for (int i = 0; i < skillDefNames.Count; i++)
            {
                string defName = skillDefNames[i];
                present.Add(defName);
                chips.Add(new RecSkillChip(defName, SkillLabel(defName), default));
            }
            return Mathf.Max(1, chips.Count) * SkillRowPitch;
        }

        private static string SkillLabel(string defName)
        {
            SkillDef skill = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            return skill == null ? defName
                : (skill.skillLabel ?? skill.label ?? skill.defName).CapitalizeFirst();
        }

        /// The role's own path; an unstored (implicit) path synthesizes as the
        /// owner alone on the full skill axis.
        private static RecPathView BuildPathView(RoleStore store, Role owner)
        {
            var roleIds = new List<int>();
            var mins = new List<int>();
            var maxes = new List<int>();
            var roles = new List<Role>();
            for (int i = 0; i < owner.trainingRoleIds.Count; i++)
            {
                Role role = store.RoleById(owner.trainingRoleIds[i]);
                if (role == null) continue;
                roleIds.Add(owner.trainingRoleIds[i]);
                mins.Add(owner.trainingMins[i]);
                maxes.Add(owner.trainingMaxes[i]);
                roles.Add(role);
            }
            if (roleIds.Count == 0)
            {
                roleIds.Add(owner.id);
                mins.Add(0);
                maxes.Add(SkillProgressionMath.MaxLevel);
                roles.Add(owner);
            }

            List<int> rows = SkillProgressionMath.PackRows(
                mins.Select((min, i) => (min, maxes[i])).ToList());
            int rowCount = rows.Count == 0 ? 1 : rows.Max() + 1;
            int displayRows = rowCount + 1;
            return new RecPathView(
                owner.id,
                roleIds,
                mins,
                maxes,
                rows,
                roles,
                displayRows);
        }

        private static float LayoutOrderChips(float width, IReadOnlyList<Role> roles,
            List<Rect> result, out Rect addRect)
        {
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

            for (int i = 0; i < roles.Count; i++)
                result.Add(Place(RoleChipUI.WidthFor(roles[i], showRemove: true)));
            // Add Role pins to the panel's bottom-right corner, on a fresh row
            // when the last chip row has no room for it.
            Text.Font = GameFont.Small;
            float addWidth = WrText.FitWidth("WR_AddRole".Translate()) + 16f;
            if (x + addWidth > width && x > 0f)
                y += RoleChipUI.Height + FlowGap;
            addRect = new Rect(width - addWidth, y, addWidth, RoleChipUI.Height);
            return y + RoleChipUI.Height;
        }
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

        private readonly List<RecTuningTableCell> cells =
            new List<RecTuningTableCell>();

        internal RecTuningTable(string label, string description, string hint,
            Rect labelRect, Rect descriptionRect, Rect hintRect)
        {
            Label = label;
            Description = description;
            Hint = hint;
            LabelRect = labelRect;
            DescriptionRect = descriptionRect;
            HintRect = hintRect;
        }

        internal string Label { get; }
        internal string Description { get; }
        internal string Hint { get; }
        internal Rect LabelRect { get; }
        internal Rect DescriptionRect { get; }
        internal Rect HintRect { get; }
        internal int CellCount => cells.Count;
        internal RecTuningTableCell CellAt(int index) => cells[index];

        internal void AddCell(RecTuningTableCell cell) => cells.Add(cell);
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
        internal RecRolePanel(Role role, float chipWidth)
        {
            Role = role;
            ChipWidth = chipWidth;
        }

        internal Role Role { get; }
        internal float ChipWidth { get; }
    }

    internal readonly struct RecSkillChip
    {
        internal RecSkillChip(string defName, string label, Rect rect)
        {
            DefName = defName;
            Label = label;
            Rect = rect;
        }

        internal string DefName { get; }
        internal string Label { get; }
        internal Rect Rect { get; }
    }

    internal sealed class RecRoleDetailSnapshot
    {
        private readonly List<RecSkillChip> requiredChips;
        private readonly List<RecSkillChip> optionalChips;
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
            string requiredCaption, List<RecSkillChip> requiredChips,
            float requiredHeight,
            string optionalCaption, List<RecSkillChip> optionalChips,
            float optionalHeight,
            HashSet<string> presentSkills, string addSkillLabel,
            string colonyMinCaption, int colonyMin, string colonyMinLabel,
            string coverageCaption, int coverage, string coverageLabel,
            bool showTrainingSection, List<RecPathView> paths)
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
            RequiredCaption = requiredCaption;
            this.requiredChips = requiredChips;
            RequiredHeight = requiredHeight;
            OptionalCaption = optionalCaption;
            this.optionalChips = optionalChips;
            OptionalHeight = optionalHeight;
            this.presentSkills = presentSkills;
            AddSkillLabel = addSkillLabel;
            ColonyMinCaption = colonyMinCaption;
            ColonyMin = colonyMin;
            ColonyMinLabel = colonyMinLabel;
            CoverageCaption = coverageCaption;
            Coverage = coverage;
            CoverageLabel = coverageLabel;
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
        internal string RequiredCaption { get; }
        internal int RequiredChipCount => requiredChips.Count;
        internal RecSkillChip RequiredChipAt(int index) => requiredChips[index];
        internal float RequiredHeight { get; }
        internal string OptionalCaption { get; }
        internal int OptionalChipCount => optionalChips.Count;
        internal RecSkillChip OptionalChipAt(int index) => optionalChips[index];
        internal float OptionalHeight { get; }
        internal bool HasSkill(string defName) => presentSkills.Contains(defName);
        internal string AddSkillLabel { get; }
        internal string ColonyMinCaption { get; }
        internal int ColonyMin { get; }
        internal string ColonyMinLabel { get; }
        internal string CoverageCaption { get; }
        internal int Coverage { get; }
        internal string CoverageLabel { get; }
        internal bool ShowTrainingSection { get; }
        internal int PathCount => paths.Count;
        internal RecPathView PathAt(int index) => paths[index];
    }

    /// One role-owned training path projection; PathId is the owner role id.
    internal sealed class RecPathView
    {
        internal RecPathView(int pathId, IReadOnlyList<int> roleIds,
            IReadOnlyList<int> mins, IReadOnlyList<int> maxes, IReadOnlyList<int> rows,
            IReadOnlyList<Role> roles, int displayRows)
        {
            PathId = pathId;
            RoleIds = roleIds;
            Mins = mins;
            Maxes = maxes;
            Rows = rows;
            Roles = roles;
            DisplayRows = displayRows;
        }

        internal int PathId { get; }
        internal IReadOnlyList<int> RoleIds { get; }
        internal IReadOnlyList<int> Mins { get; }
        internal IReadOnlyList<int> Maxes { get; }
        internal IReadOnlyList<int> Rows { get; }
        internal IReadOnlyList<Role> Roles { get; }
        internal int DisplayRows { get; }

        internal int IndexOfRole(int roleId)
        {
            for (int i = 0; i < RoleIds.Count; i++)
                if (RoleIds[i] == roleId) return i;
            return -1;
        }
    }
}
