using System.Collections.Generic;
using HarmonyLib;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs
{
    public class QualityJobsMod : Mod
    {
        // Initialized by the Mod constructor before any game code can run.
        // RimWorld constructs Mod subclasses during the earliest loading phase,
        // so Settings and Instance are always non-null by the time patches or
        // game components execute.
        public static QualityJobsMod Instance = null!;
        public static QualityJobsSettings Settings = null!;

        public QualityJobsMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<QualityJobsSettings>();
            new Harmony("EPrime.QualityJobs").PatchAll();
        }

        public override string SettingsCategory() => "EPrime's Quality Jobs";

        // Header panel height constant (mirroring Dialog_ReadoutConfig idiom).
        private const float PanelH = 56f;

        // Gap below the header panel: matches Window.StandardMargin (18f) so the
        // top gap between the panel bottom and the first MiniHeader equals the
        // 18f side margins that Window already bakes into inRect before our
        // DoSettingsWindowContents is called.
        private const float PanelGap = Window.StandardMargin; // 18f

        // Row height constants (matching Listing_Standard / Dialog_ConstructionPlanConfig).
        // CheckboxH = Text.LineHeight at GameFont.Small = 22f.
        // SliderH   = Listing_Standard.SliderLabeled GetRect height = 30f.
        // RowGap    = Listing_Standard.verticalSpacing = 2f.
        // ColGap    = horizontal gap between the two columns.
        private const float CheckboxH = 22f;
        private const float SliderH   = 30f;
        private const float RowGap    =  2f;
        private const float ColGap    = 24f;

        // EprStyle color values replicated from EPrimeReadouts\src\EPrimeReadouts\UI\EprStyle.cs
        // (lines 29-31). Do not reference the other mod; values copied verbatim.
        private static readonly Color PanelBackground = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        private static readonly Color PanelOutline    = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color HeaderText      = new Color(0.85f, 0.85f, 0.85f);

        public override void DoSettingsWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
                DrawSettingsWindowContents(inRect);
        }

        private void DrawSettingsWindowContents(Rect inRect)
        {
            SettingsLabels.Ensure();

            // A Game and its components exist before FinalizeInit publishes the
            // per-save settings snapshot. Treat the store as an editable source
            // only while playing and after that snapshot is actually ready.
            QualityJobsStore? loadedStore =
                Current.ProgramState == ProgramState.Playing
                    ? QualityJobsStore.Active
                    : null;
            StoreSettingsSnapshot? activeSettings = null;
            QualityJobsStore? activeStore = loadedStore != null
                && loadedStore.TryGetSettingsPresentation(out activeSettings)
                    ? loadedStore
                    : null;

            // ── Header panel ─────────────────────────────────────────────────────────
            // Full-width, PanelH tall, drawn at top of inRect (y=0).
            // inRect is already ContractedBy(Window.Margin=18f) and AtZero'd by
            // Window.InnerWindowOnGUI, so top margin equals side margins at 18f.
            var panelRect = new Rect(inRect.x, inRect.y, inRect.width, PanelH);
            Widgets.DrawBoxSolidWithOutline(panelRect, PanelBackground, PanelOutline);

            // Mod icon — 40x40 at 8px left padding, vertically centred.
            var iconRect = new Rect(panelRect.x + 8f, panelRect.y + 8f, 40f, 40f);
            GUI.DrawTexture(iconRect, QualityJobsTex.ModIcon);

            // Fix 2: Title in GameFont.Medium (was Small), MiddleLeft, header-text color.
            GameFont prevFont   = Text.Font;
            TextAnchor prevAnch = Text.Anchor;
            Color prevColor     = GUI.color;
            Text.Font   = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color   = HeaderText;
            Widgets.Label(
                new Rect(iconRect.xMax + 8f, panelRect.y,
                    panelRect.width - iconRect.xMax - 8f - 158f, PanelH),
                SettingsLabels.Title!);
            GUI.color   = prevColor;
            Text.Anchor = prevAnch;
            Text.Font   = prevFont;

            // Right-side button: Disable / Enable (only when a game is loaded).
            // Multi-pass safety: Widgets.ButtonText returns true only on the
            // MouseUp event pass; dialog open is therefore per-click, not per-frame.
            if (Current.ProgramState == ProgramState.Playing)
            {
                float btnY    = panelRect.y + (PanelH - 28f) / 2f;
                var   btnRect = new Rect(panelRect.xMax - 158f, btnY, 150f, 28f);
                if (loadedStore != null)
                {
                    // Disable button: opens confirmation dialog.
                    if (Widgets.ButtonText(btnRect, SettingsLabels.DisableButton!))
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            SettingsLabels.DisableWarning!,
                            Commands.Disable));
                    }
                    WrTips.Key("QJ_SettingsEnabledNote").Region(btnRect);
                }
                else
                {
                    // Enable button: seeds store from current defaults.
                    if (Widgets.ButtonText(btnRect, SettingsLabels.EnableButton!))
                    {
                        Commands.RequestEnable();
                    }
                    WrTips.Key("QJ_SettingsEnabledNote").Region(btnRect);
                }
            }

            // ── Body (below panel + gap) ──────────────────────────────────────────
            // I4: constant labels are cached by language; interpolated labels by value.
            float bodyX = inRect.x;
            float bodyW = inRect.width;
            float y     = inRect.y + PanelH + PanelGap;

            // Dual-pattern: when a game is loaded and the store is active, the
            // grid reads from/writes to the store via synced commands. Otherwise
            // reads/writes global Settings directly (new-save seeds).
            // ── Two-column defaults grid ──────────────────────────────────────────
            // Left column: bill defaults.  Right column: construction defaults.
            // Column widths: (bodyW - ColGap) / 2 — split at midpoint with 24px gap.
            //
            // Row sequence follows the canonical option order shared with the
            // bill dialog and the construction fold-out (both columns consume
            // identical heights on every row):
            //   Row 0  MiniHeader                              30f
            //   Row 1  Manage bills / Manage construction      CheckboxH + RowGap
            //   Row 2  Require inspired                        CheckboxH + RowGap
            //   Row 3  Require specialist (blank without       CheckboxH + RowGap
            //            Ideology; identical height)
            //   Row 4  Auto-adjust finisher skill              CheckboxH + RowGap
            //   Row 5  Finisher skill sliders                  SliderH + RowGap
            //   Row 6  Target quality pickers (both columns)   CheckboxH + RowGap
            //   Row 7  Stock cap / blank                       SliderH + RowGap
            //   Row 8  "0 = unlimited" hint / blank            CheckboxH (last row)
            float colW   = (bodyW - ColGap) / 2f;
            float leftX  = bodyX;
            float rightX = bodyX + colW + ColGap;

            // Row 0: MiniHeaders.
            QjUi.MiniHeader(leftX,  y, colW, SettingsLabels.BillDefaults!);
            QjUi.MiniHeader(rightX, y, colW, SettingsLabels.ConstructionDefaults!);
            y += 30f;

            // Row 1: Manage new bills (left) / Manage new construction (right).
            {
                Rect leftRow  = new Rect(leftX,  y, colW, CheckboxH);
                Rect rightRow = new Rect(rightX, y, colW, CheckboxH);

                // Left: manage new bills (dual-pattern).
                if (activeStore != null)
                {
                    bool manageNew = activeSettings!.ManageNewBills;
                    Widgets.CheckboxLabeled(leftRow, SettingsLabels.ManageNewBills!, ref manageNew);
                    if (manageNew != activeSettings.ManageNewBills)
                        Commands.SetManageNewBillsDefault(manageNew);
                }
                else
                {
                    Widgets.CheckboxLabeled(leftRow, SettingsLabels.ManageNewBills!, ref Settings.defaultManageNewBills);
                }
                WrTips.Key("QJ_SettingsManageNewBillsTip").Region(leftRow);

                // Right: manage new construction (dual-pattern).
                if (activeStore != null)
                {
                    bool manageNewC = activeSettings!.ManageNewConstruction;
                    Widgets.CheckboxLabeled(rightRow, SettingsLabels.ManageNewConstruction!, ref manageNewC);
                    if (manageNewC != activeSettings.ManageNewConstruction)
                        Commands.SetManageNewConstructionDefault(manageNewC);
                }
                else
                {
                    Widgets.CheckboxLabeled(rightRow, SettingsLabels.ManageNewConstruction!, ref Settings.defaultManageNewConstruction);
                }
                WrTips.Key("QJ_SettingsManageNewConstructionTip").Region(rightRow);

                y += CheckboxH + RowGap;
            }

            // Row 2: Require inspired (dual-pattern, both columns).
            {
                Rect leftRow  = new Rect(leftX,  y, colW, CheckboxH);
                Rect rightRow = new Rect(rightX, y, colW, CheckboxH);

                // Left: bill require inspired.
                if (activeStore != null)
                {
                    bool inspired = activeSettings!.RequireInspired;
                    Widgets.CheckboxLabeled(leftRow, SettingsLabels.RequireInspired!, ref inspired);
                    if (inspired != activeSettings.RequireInspired)
                        Commands.SetRequireInspiredDefault(inspired);
                }
                else
                {
                    Widgets.CheckboxLabeled(leftRow, SettingsLabels.RequireInspired!, ref Settings.defaultRequireInspired);
                }
                WrTips.Key("QJ_RequireInspiredTip").Region(leftRow);

                // Right: construction require inspired.
                if (activeStore != null)
                {
                    bool inspired = activeSettings!.ConstructionRequireInspired;
                    Widgets.CheckboxLabeled(rightRow, SettingsLabels.RequireInspired!, ref inspired);
                    if (inspired != activeSettings.ConstructionRequireInspired)
                        Commands.SetConstructionRequireInspiredDefault(inspired);
                }
                else
                {
                    Widgets.CheckboxLabeled(rightRow, SettingsLabels.RequireInspired!, ref Settings.defaultConstructionRequireInspired);
                }
                WrTips.Key("QJ_RequireInspiredTip").Region(rightRow);

                y += CheckboxH + RowGap;
            }

            // Row 3: Require specialist (Ideology-gated; both sides consume identical height).
            {
                if (ModsConfig.IdeologyActive)
                {
                    Rect leftRow  = new Rect(leftX,  y, colW, CheckboxH);
                    Rect rightRow = new Rect(rightX, y, colW, CheckboxH);

                    // Left: bill require specialist.
                    if (activeStore != null)
                    {
                        bool specialist = activeSettings!.RequireSpecialist;
                        Widgets.CheckboxLabeled(leftRow, SettingsLabels.RequireSpecialist!, ref specialist);
                        if (specialist != activeSettings.RequireSpecialist)
                            Commands.SetRequireSpecialistDefault(specialist);
                    }
                    else
                    {
                        Widgets.CheckboxLabeled(leftRow, SettingsLabels.RequireSpecialist!, ref Settings.defaultRequireSpecialist);
                    }
                    WrTips.Key("QJ_RequireSpecialistTip").Region(leftRow);

                    // Right: construction require specialist.
                    if (activeStore != null)
                    {
                        bool specialist = activeSettings!.ConstructionRequireSpecialist;
                        Widgets.CheckboxLabeled(rightRow, SettingsLabels.RequireSpecialist!, ref specialist);
                        if (specialist != activeSettings.ConstructionRequireSpecialist)
                            Commands.SetConstructionRequireSpecialistDefault(specialist);
                    }
                    else
                    {
                        Widgets.CheckboxLabeled(rightRow, SettingsLabels.RequireSpecialist!, ref Settings.defaultConstructionRequireSpecialist);
                    }
                    WrTips.Key("QJ_RequireSpecialistTip").Region(rightRow);
                }
                // else: both rows are blank — identical consumed height = CheckboxH + RowGap.
                y += CheckboxH + RowGap;
            }

            // Row 4: Auto-adjust finisher skill (dual-pattern, both columns).
            // The skill sliders below stay visible even when the auto default is
            // on: they still seed the manual threshold for bills and plans where
            // auto is later turned off.
            {
                Rect leftRow  = new Rect(leftX,  y, colW, CheckboxH);
                Rect rightRow = new Rect(rightX, y, colW, CheckboxH);

                // Left: bill auto-best default.
                if (activeStore != null)
                {
                    bool auto = activeSettings!.AutoBest;
                    Widgets.CheckboxLabeled(leftRow, SettingsLabels.AutoBest!, ref auto);
                    if (auto != activeSettings.AutoBest)
                        Commands.SetAutoBestDefault(auto);
                }
                else
                {
                    Widgets.CheckboxLabeled(leftRow, SettingsLabels.AutoBest!, ref Settings.defaultAutoBest);
                }
                WrTips.Key("QJ_AutoBestTip").Region(leftRow);

                // Right: construction auto-best default.
                if (activeStore != null)
                {
                    bool autoC = activeSettings!.ConstructionAutoBest;
                    Widgets.CheckboxLabeled(rightRow, SettingsLabels.AutoBest!, ref autoC);
                    if (autoC != activeSettings.ConstructionAutoBest)
                        Commands.SetConstructionAutoBestDefault(autoC);
                }
                else
                {
                    Widgets.CheckboxLabeled(rightRow, SettingsLabels.AutoBest!, ref Settings.defaultConstructionAutoBest);
                }
                WrTips.Key("QJ_AutoBestTip").Region(rightRow);

                y += CheckboxH + RowGap;
            }

            // Row 5: Finisher skill sliders (dual-pattern, both columns).
            // A column with its auto default on draws its slider dimmed: the
            // value stays editable as a seed for later manual use, but auto
            // mode does not read it, and full brightness would suggest it does.
            {
                bool leftAuto = activeStore != null
                    ? activeSettings!.AutoBest : Settings.defaultAutoBest;
                bool rightAuto = activeStore != null
                    ? activeSettings!.ConstructionAutoBest : Settings.defaultConstructionAutoBest;
                Color rowColor = GUI.color;

                // Left: bill finisher skill.
                int leftSkill = activeStore != null ? activeSettings!.MinSkill : Settings.defaultMinSkill;
                if (leftSkill != SettingsLabels.MinSkillValue)
                {
                    SettingsLabels.MinSkillLabel = "QJ_FinisherSkill".Translate(leftSkill);
                    SettingsLabels.MinSkillValue = leftSkill;
                }
                if (leftAuto)
                    GUI.color = new Color(rowColor.r, rowColor.g, rowColor.b, rowColor.a * 0.55f);
                int newLeftSkill = DrawSliderRow(leftX, y, colW,
                    SettingsLabels.MinSkillLabel!, leftSkill, 0f, 20f);
                GUI.color = rowColor;
                WrTips.Key("QJ_SettingsFinisherSkillTip")
                    .Region(new Rect(leftX, y, colW, SliderH));
                if (newLeftSkill != leftSkill)
                {
                    if (activeStore != null)
                        Commands.SetMinSkillDefault(newLeftSkill);
                    else
                        Settings.defaultMinSkill = newLeftSkill;
                }

                // Right: construction finisher skill.
                int rightSkill = activeStore != null ? activeSettings!.ConstructionMinSkill : Settings.defaultConstructionMinSkill;
                if (rightSkill != SettingsLabels.ConstructionMinSkillValue)
                {
                    SettingsLabels.ConstructionMinSkillLabel = "QJ_FinisherSkill".Translate(rightSkill);
                    SettingsLabels.ConstructionMinSkillValue = rightSkill;
                }
                if (rightAuto)
                    GUI.color = new Color(rowColor.r, rowColor.g, rowColor.b, rowColor.a * 0.55f);
                int newRightSkill = DrawSliderRow(rightX, y, colW,
                    SettingsLabels.ConstructionMinSkillLabel!, rightSkill, 0f, 20f);
                GUI.color = rowColor;
                WrTips.Key("QJ_SettingsFinisherSkillTip")
                    .Region(new Rect(rightX, y, colW, SliderH));
                if (newRightSkill != rightSkill)
                {
                    if (activeStore != null)
                        Commands.SetConstructionMinSkillDefault(newRightSkill);
                    else
                        Settings.defaultConstructionMinSkill = newRightSkill;
                }

                y += SliderH + RowGap;
            }

            // Row 6: Target quality pickers (dual-pattern, both columns).
            {
                Rect leftRow  = new Rect(leftX,  y, colW, CheckboxH);
                Rect rightRow = new Rect(rightX, y, colW, CheckboxH);
                DrawBillQualityPickerRow(leftRow, activeStore);
                DrawQualityPickerRow(rightRow, activeStore);
                y += CheckboxH + RowGap;
            }

            // Row 7: Stock cap (left) / blank (right).
            {
                int capVal = activeStore != null ? activeSettings!.ProductCap : Settings.defaultProductCap;
                // I4: rebuild interpolated cap label only when the displayed value changes.
                if (capVal != SettingsLabels.DefaultCapValue)
                {
                    SettingsLabels.DefaultCapLabel = "QJ_SettingsDefaultCap".Translate(capVal);
                    SettingsLabels.DefaultCapValue = capVal;
                }
                int newCap = DrawSliderRow(leftX, y, colW,
                    SettingsLabels.DefaultCapLabel!, capVal, 0f, 50f);
                WrTips.Key("QJ_SettingsDefaultCapTip")
                    .Region(new Rect(leftX, y, colW, SliderH));
                if (newCap != capVal)
                {
                    if (activeStore != null)
                        Commands.SetProductCapDefault(newCap);
                    else
                        Settings.defaultProductCap = newCap;
                }
                // right side: blank row (identical height consumed).
                y += SliderH + RowGap;
            }

            // Row 8: "0 = unlimited" dimmed hint (left) / blank (right).
            {
                Color savedColor = GUI.color;
                GUI.color = new Color(savedColor.r, savedColor.g, savedColor.b, savedColor.a * 0.6f);
                Rect hintRect = new Rect(leftX, y, colW, CheckboxH);
                TextAnchor savedAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(hintRect, SettingsLabels.UnlimitedHint!);
                Text.Anchor = savedAnchor;
                GUI.color = savedColor;

                // No trailing RowGap — last row in the grid.
                y += CheckboxH;
            }

            y += 6f; // small buffer before global options section.

            // ── Global options section ────────────────────────────────────────────
            // MiniHeader and toggles constrained to the LEFT column width only
            // (not full body width), matching the spec.
            QjUi.MiniHeader(bodyX, y, colW, SettingsLabels.GlobalOptions!);
            y += 30f;

            // Share toggle: shows the default when no game is loaded; shows the
            // per-save (store-backed) version when a store is active.
            if (Current.Game == null)
            {
                Rect shareRow = new Rect(bodyX, y, colW, CheckboxH);
                Widgets.CheckboxLabeled(shareRow, SettingsLabels.ShareWork!, ref Settings.defaultShareUnfinishedWork);
                WrTips.Key("QJ_SettingsShareWorkTip").Region(shareRow);
                y += CheckboxH + RowGap;
            }
            else
            {
                QualityJobsStore? store = QualityJobsStore.Active;
                if (store != null)
                {
                    // Per-save share toggle (synced).
                    Rect shareRow = new Rect(bodyX, y, colW, CheckboxH);
                    StoreSettingsSnapshot snapshot = store.SettingsPresentation;
                    bool share = snapshot.ShareUnfinishedWork;
                    Widgets.CheckboxLabeled(shareRow, SettingsLabels.ShareWork!, ref share);
                    WrTips.Key("QJ_SettingsShareWorkTip").Region(shareRow);
                    if (share != snapshot.ShareUnfinishedWork)
                        Commands.SetShareUnfinishedWork(share);
                }
                // If store is null the mod is disabled for this save;
                // the Enable button in the header panel handles re-enabling.
                y += CheckboxH + RowGap;
            }

            // Toolbar button toggle: a per-player presentation preference, so
            // it binds the global Settings field directly in every state.
            {
                Rect toolbarRow = new Rect(bodyX, y, colW, CheckboxH);
                Widgets.CheckboxLabeled(toolbarRow, SettingsLabels.ShowToolbarButton!, ref Settings.showToolbarButton);
                WrTips.Key("QJ_SettingsShowToolbarButtonTip").Region(toolbarRow);
                y += CheckboxH + RowGap;
            }

            if (Current.Game == null)
            {
                Rect noGameRow = new Rect(bodyX, y + 4f, colW, CheckboxH);
                Widgets.Label(noGameRow, SettingsLabels.NoGameLoaded!);
            }
        }

        internal static void ResetPresentationCaches() => SettingsLabels.Reset();

        /// Draws a manual slider row (label left 50%, slider right 50%) without
        /// Listing_Standard. Mirrors SliderLabeled's default labelPct = 0.5f.
        /// Returns the new integer value. No allocations on cache-hit paths.
        ///
        /// Owner: called from DoSettingsWindowContents (render path, pre-cached label).
        private static int DrawSliderRow(float x, float y, float width, string label,
            int current, float min, float max)
        {
            TextAnchor prev = Text.Anchor;
            Rect lRect = new Rect(x,               y, width * 0.5f, SliderH);
            Rect sRect = new Rect(x + width * 0.5f, y, width * 0.5f, SliderH);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(lRect, label);
            Text.Anchor = prev;
            return (int)Widgets.HorizontalSlider(sRect, current, min, max, middleAlignment: true);
        }

        /// Draws the target-quality button-picker row for construction defaults.
        /// Layout: label left 50%, button right 50% — matching the SliderLabeled
        /// 50/50 split so columns stay aligned.
        /// Menu allocation occurs only on click, not per frame.
        /// Dual-pattern: reads/writes store when activeStore != null, else Settings.
        private static void DrawQualityPickerRow(Rect row, QualityJobsStore? activeStore)
        {
            Rect lRect = new Rect(row.x,               row.y, row.width * 0.5f, row.height);
            Rect bRect = new Rect(row.x + row.width * 0.5f, row.y, row.width * 0.5f, row.height);

            TextAnchor prev = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(lRect, SettingsLabels.TargetQualityLabel!);
            Text.Anchor = prev;
            WrTips.Key("QJ_RetriedUntilTip").Region(row);

            int curQ = activeStore != null
                ? activeStore.SettingsPresentation.ConstructionTargetQuality
                : Settings.defaultConstructionTargetQuality;

            string btnCaption = curQ <= 0
                ? SettingsLabels.NoRetriesLabel!
                : SettingsLabels.QualityLabels![curQ];

            if (Widgets.ButtonText(bRect, btnCaption))
            {
                // Build options list only on click — allocation on interaction, not per frame.
                // Capture activeStore locally so the closure holds the right reference.
                QualityJobsStore? capturedStore = activeStore;
                var options = new List<FloatMenuOption>();
                options.Add(new FloatMenuOption(SettingsLabels.NoRetriesLabel!, () =>
                {
                    if (capturedStore != null)
                        Commands.SetConstructionTargetQualityDefault(0);
                    else
                        Settings.defaultConstructionTargetQuality = 0;
                }));
                for (int q = 1; q <= 6; q++)
                {
                    int capturedQ = q;
                    options.Add(new FloatMenuOption(SettingsLabels.QualityLabels![q], () =>
                    {
                        if (capturedStore != null)
                            Commands.SetConstructionTargetQualityDefault(capturedQ);
                        else
                            Settings.defaultConstructionTargetQuality = capturedQ;
                    }));
                }
                var menu = new FloatMenu(options) { vanishIfMouseDistant = false };
                Find.WindowStack.Add(menu);
            }
        }

        /// Draws the target-quality picker row for BILL defaults: label left 50%,
        /// button right 50%. 0 shows "Any" (a below-target finish raises the bill
        /// count instead of retrying a build). Menu allocation on click only.
        /// Dual-pattern: reads/writes store when activeStore != null, else Settings.
        private static void DrawBillQualityPickerRow(Rect row, QualityJobsStore? activeStore)
        {
            Rect lRect = new Rect(row.x,               row.y, row.width * 0.5f, row.height);
            Rect bRect = new Rect(row.x + row.width * 0.5f, row.y, row.width * 0.5f, row.height);

            TextAnchor prev = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(lRect, SettingsLabels.TargetQualityLabel!);
            Text.Anchor = prev;
            WrTips.Key("QJ_BillTargetQualityTip").Region(row);

            int curQ = activeStore != null
                ? activeStore.SettingsPresentation.TargetQuality
                : Settings.defaultTargetQuality;

            string btnCaption = curQ <= 0
                ? SettingsLabels.AnyQualityLabel!
                : SettingsLabels.QualityLabels![curQ];

            if (Widgets.ButtonText(bRect, btnCaption))
            {
                // Menu built on click only; capture the store for the closures.
                QualityJobsStore? capturedStore = activeStore;
                var options = new List<FloatMenuOption>();
                options.Add(new FloatMenuOption(SettingsLabels.AnyQualityLabel!, () =>
                {
                    if (capturedStore != null)
                        Commands.SetTargetQualityDefault(0);
                    else
                        Settings.defaultTargetQuality = 0;
                }));
                for (int q = 1; q <= 6; q++)
                {
                    int capturedQ = q;
                    options.Add(new FloatMenuOption(SettingsLabels.QualityLabels![q], () =>
                    {
                        if (capturedStore != null)
                            Commands.SetTargetQualityDefault(capturedQ);
                        else
                            Settings.defaultTargetQuality = capturedQ;
                    }));
                }
                var menu = new FloatMenu(options) { vanishIfMouseDistant = false };
                Find.WindowStack.Add(menu);
            }
        }

        /// I4: language-keyed label cache for the settings window. Constant labels
        /// are rebuilt when the active language changes; interpolated labels are
        /// keyed by value and rebuilt only when the displayed value changes.
        ///
        /// Owner: process. Key: LanguageDatabase.activeLanguage (constant labels);
        ///   value integer (interpolated labels).
        /// Dependencies: language change; value change.
        /// Equality policy: identity not required (strings are value-typed).
        /// Teardown: Reset on game disposal.
        private static class SettingsLabels
        {
            private static LoadedLanguage? _builtForLanguage;

            // Constant labels (rebuilt on language change).
            public static string? Title;
            public static string? BillDefaults;
            public static string? ConstructionDefaults;
            public static string? GlobalOptions;
            public static string? ManageNewBills;
            public static string? ManageNewConstruction;
            public static string? RequireInspired;
            public static string? RequireSpecialist;
            public static string? AutoBest;
            public static string? UnlimitedHint;
            public static string? ShareWork;
            public static string? ShowToolbarButton;
            public static string? NoGameLoaded;
            public static string? EnabledNote;
            public static string? DisableWarning;
            public static string? DisableButton;
            public static string? EnableButton;
            public static string? TargetQualityLabel;
            public static string? NoRetriesLabel;
            public static string? AnyQualityLabel;

            // Quality name cache: 7 entries (Awful=0 .. Legendary=6).
            // Owner: process. Key: language. Value: string[] immutable after build.
            // Dependencies: language change. Refresh: immediate on language change.
            // Equality policy: N/A (strings; identity not required).
            // Teardown: rebuilt on next language change.
            public static string[]? QualityLabels;

            // Interpolated labels (rebuilt when value changes).
            public static string? MinSkillLabel;
            public static int MinSkillValue = -1;
            public static string? ConstructionMinSkillLabel;
            public static int ConstructionMinSkillValue = -1;
            public static string? DefaultCapLabel;
            public static int DefaultCapValue = -1;

            public static void Ensure()
            {
                if (LanguageDatabase.activeLanguage == _builtForLanguage) return;
                _builtForLanguage    = LanguageDatabase.activeLanguage;
                Title                = "EPrime's Quality Jobs"; // SettingsCategory() string — not translated
                BillDefaults         = "QJ_SettingsBillDefaults".Translate();
                ConstructionDefaults = "QJ_SettingsConstructionDefaults".Translate();
                GlobalOptions        = "QJ_SettingsGlobalOptions".Translate();
                ManageNewBills       = "QJ_SettingsManageNewBills".Translate();
                ManageNewConstruction = "QJ_SettingsManageNewConstruction".Translate();
                RequireInspired      = "QJ_RequireInspired".Translate();
                RequireSpecialist    = "QJ_RequireSpecialist".Translate();
                AutoBest             = "QJ_AutoBest".Translate();
                UnlimitedHint        = "QJ_SettingsUnlimitedHint".Translate();
                ShareWork            = "QJ_SettingsShareWork".Translate();
                ShowToolbarButton    = "QJ_SettingsShowToolbarButton".Translate();
                NoGameLoaded         = "QJ_NoGameLoaded".Translate();
                EnabledNote          = "QJ_SettingsEnabledNote".Translate();
                DisableWarning       = "QJ_DisableWarning".Translate();
                DisableButton        = "QJ_DisableButton".Translate();
                EnableButton         = "QJ_EnableButton".Translate();
                TargetQualityLabel   = "QJ_MinQualityLabel".Translate();
                NoRetriesLabel       = "QJ_NoRetries".Translate();
                AnyQualityLabel      = "QJ_AnyQuality".Translate();
                // Quality names — 7 entries (Awful=0 .. Legendary=6).
                QualityLabels = new string[7];
                for (int q = 0; q <= 6; q++)
                    QualityLabels[q] = ((QualityCategory)q).GetLabel().CapitalizeFirst();
                // Force rebuild of interpolated labels at new language.
                MinSkillValue             = -1;
                ConstructionMinSkillValue = -1;
                DefaultCapValue           = -1;
            }

            public static void Reset()
            {
                _builtForLanguage = null;
                QualityLabels = null;
                MinSkillValue = -1;
                ConstructionMinSkillValue = -1;
                DefaultCapValue = -1;
            }
        }
    }
}
