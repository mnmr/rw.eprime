using System.Collections.Generic;
using QualityJobs.Core;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// Fold-out per-plan config (spec §10, B2). Anchors its bottom edge to the
    /// top of the gizmo button rect. Transient window; all mutations go through
    /// synced Commands; per-field pushes (MP last-writer-wins per field, same as
    /// the bill dialog). Labels cached per open; interpolated labels cached by
    /// value; odds rows cached by condition key.
    ///
    /// Implicit creation semantics: a plan exists IFF at least one option is
    /// non-neutral. Editing any control fires its setter, which implicitly creates
    /// the plan when needed (neutral auto-create handled by SetPlan* commands).
    /// Controls always show (no manage checkbox). A [Clear] button appears only
    /// when a plan exists and removes it entirely.
    ///
    /// Fix 2: The "Retried until" caption is removed entirely. The target-quality
    /// row now shows: label on the left portion, button flush to the RIGHT edge
    /// of the row. Its tooltip is restricted to the non-interactive label area.
    ///
    /// FloatMenu for quality picker uses vanishIfMouseDistant = false
    /// (verified field at Decompiled\Verse\FloatMenu.cs line 14) to prevent the
    /// menu from instantly self-closing when spawned near the screen edge.
    ///
    /// Fix 5: Operates on thing IDs captured at dialog open and follows their
    /// deterministic replacement IDs, so editing one field pushes the synced
    /// command for every eligible selected thing throughout construction.
    ///
    /// SetInitialSizeAndPosition verified against Decompiled\Verse\Window.cs line 249:
    ///   Rect rect3 = rect.ContractedBy(Margin);   // Margin = 18f
    ///   DoWindowContents(rect3.AtZero());
    /// So inRect.height = windowRect.height - 2*Margin. InitialSize.y is computed
    /// from content height + 2*Margin so no pixel is wasted.
    ///
    /// Layout (the window itself still rises from the gizmo button; content is
    /// TOP-anchored inside it). The title is lifted by TitleLift so its visual
    /// top margin matches the 18f side margin (Medium glyphs carry internal top
    /// bearing). The RIGHT column starts at the lifted title y so the odds
    /// mini-header aligns with the title, leaving room for the Status block
    /// below the odds rows:
    ///
    ///   inRect (title drawn TitleLift above inRect.y)
    ///   ┌─────────────────────────────────────────────────────────┐
    ///   │  Title "Quality Job"       │ ← Quality odds (MiniHeader) │
    ///   │  ┌──────────────────────┐  │   Legendary   xx.x%         │
    ///   │  │ LEFT panel           │  │   ...  7 rows × 20f         │
    ///   │  │ [DrawMenuSection]    │  │   Awful       xx.x%         │
    ///   │  │  [Clear]   ← TOP     │  │ ← Status (MiniHeader)       │
    ///   │  │  ↕ flexible space    │  │   Eligible finishers: N     │
    ///   │  │  ── options block ── │  │   names / stall line        │
    ///   │  │  ... controls ...    │  │                             │
    ///   │  └──────────────────────┘  └─────────────────────────────┘
    ///   └─────────────────────────────────────────────────────────┘
    public class Dialog_ConstructionPlanConfig : Window
    {
        private readonly TrackedTargetIds targetIds;
        private readonly Map? primaryMap;
        private readonly Rect _anchor;
        private QualityJobsStore? subscribedStore;
        private FloatMenu? qualityMenu;

        // Layout constants derived from verified Listing metrics:
        //   Text.LineHeight (GameFont.Small) = 22f (compile-time constant;
        //     confirmed via Listing_Standard which uses it throughout, and the
        //     task brief).
        //   SliderLabeled GetRect height = 30f (Listing_Standard.cs line 381:
        //     GetRect(30f)).
        //   verticalSpacing = 2f (Listing.cs line 8).
        //   QjUi.MiniHeader height = 30f (label 22f + rule at y+24f; returns y+30f).
        //   Window.Margin = 18f (Window.cs line 104).
        private const float SmallLineH  = 22f; // Text.LineHeight at GameFont.Small
        private const float SliderH     = 30f; // SliderLabeled GetRect height
        private const float GapH        =  2f; // verticalSpacing
        private const float MiniHeaderH = 27f; // QjUi.MiniHeader consumed height (label 22f, rule at y+21f, returns y+27f)
        private const float WinMargin   = 18f; // Window.Margin

        // Compact data rows for the odds/status blocks (QjUi.TightRowH); the
        // 22f SmallLineH stays for control rows (checkboxes, labels).
        private const float TightRowH = QjUi.TightRowH; // 20f

        // Right column content height:
        //   MiniHeader (27f) + 5 odds rows × 20f = 127f (Legendary..Good plus
        //   the collapsed "Normal or worse" row — OddsRows.RowCount).
        private const float RightContentH = MiniHeaderH + OddsRows.RowCount * TightRowH; // 27 + 100 = 127

        // Title lift: GameFont.Medium glyphs render with ~4px internal top
        // bearing, so the label rect is raised above inRect.y by this amount to
        // make the VISUAL top margin equal the 18f side margin (owner request).
        private const float TitleLift = 4f;
        // Gap between the title line box and the body below it.
        private const float TitleGap = 2f;

        // Status block below the odds rows (right column):
        //   gap (8f — a header with content above it gets 4f extra padding,
        //   owner request; the odds header is first and gets none)
        //   + MiniHeader (27f) + finishers row + names/stall row.
        private const float StatusGapH = 8f;
        private const float StatusContentH = MiniHeaderH + 2f * TightRowH; // 27 + 40 = 67

        // Left panel frame height: pad + [Clear] row + gap + the tallest
        // options block (manual mode slider; Ideology adds a row). Sized for
        // the tallest variant so toggling auto-best never resizes the frame.
        // No longer tied to the odds column height — the collapsed odds table
        // (5 rows) is shorter than the options block.
        private static float LeftPanelH()
        {
            float options = SmallLineH + GapH   // inspired
                + SmallLineH + GapH             // auto-best
                + SliderH + GapH                // manual skill slider (tallest)
                + SmallLineH;                   // target-quality row
            if (ModsConfig.IdeologyActive) options += SmallLineH + GapH;
            return 2f * PanelPad + ClearRowH + GapH + options;
        }

        // Left options block heights (no trailing gap on the last element):
        //   inspired(22) + gap + [specialist(22) + gap] + auto(22) + gap
        //   + skillRow(30 slider, or 22 current-best label) + gap + qualityRow(22)
        // All four combos (Ideology × autoBest):
        //   no Ideology, manual: 22+2 + 22+2 + 30+2 + 22 = 102f
        //   no Ideology, auto:   22+2 + 22+2 + 22+2 + 22 =  94f
        //   Ideology, manual:    22+2 + 22+2 + 22+2 + 30+2 + 22 = 126f
        //   Ideology, auto:      22+2 + 22+2 + 22+2 + 22+2 + 22 = 118f
        private float OptionsBlockH()
        {
            float skillRowH = autoBest ? SmallLineH : SliderH;
            float h = SmallLineH + GapH        // inspired
                    + SmallLineH + GapH        // auto-best checkbox
                    + skillRowH + GapH         // slider or current-best label
                    + SmallLineH;              // target-quality row
            if (ModsConfig.IdeologyActive) h += SmallLineH + GapH;
            return h;
        }

        // [Clear] button row reserved unconditionally at the top of the left panel inner area:
        //   row height = SmallLineH; followed by gap when drawing (so the button is drawn at top).
        private const float ClearRowH = SmallLineH;

        // Left panel frame padding (same as DrawMenuSection inner padding used in bill dialog).
        private const float PanelPad = 6f;

        // Fix 5: quality-picker button width = 50% of the row (midpoint to right edge),
        // matching SliderLabeled's default labelPct of 0.5f so the button aligns with
        // the control region of the finisher-skill slider above it.
        private const float QualityBtnWidthFraction = 0.5f;

        // Per-frame local edit copies; pushed via Commands only on actual change.
        private int minSkill;
        private bool requireInspired;
        private bool requireSpecialist;
        private int minQuality;
        private bool autoBest;

        // Constant translated strings cached as instance fields built on first draw.
        // Reopening the dialog always constructs a fresh instance (AGENTS.md tooltip-session note).
        // Owner: dialog instance. Teardown: dies with the window.
        private string? title;
        private string? requireInspiredLabel;
        private string? requireSpecialistLabel;
        private string? oddsHeaderLabel;
        private string? clearLabel;
        private string? noRetriesLabel;
        private string? targetQualityLabel;
        private string? anyQualityLabel;
        private string? autoBestLabel;
        private string? autoBestNoneLabel;

        // I4: interpolated slider label, rebuilt only when the displayed value changes.
        // Owner: dialog instance. Teardown: dies with the window.
        private string? minSkillLabel;
        private int minSkillLabelValue = -1;

        // Quality name cache: built once per dialog open. 7 entries (Awful..Legendary)
        // for the target-quality picker.
        // Owner: dialog instance. Teardown: dies with the window.
        private string[]? qualityLabels;

        // Odds table row labels (Legendary..Good, then "Normal or worse").
        // Owner: dialog instance. Teardown: dies with the window.
        private string[]? oddsRowLabels;

        // Odds rows — keyed (minSkill, inspired, roleOffset); rebuilt on mismatch.
        // Owner: dialog (transient). Dependencies: condition fields only.
        // Teardown: dies with the window.
        private OddsRows? odds;

        // Auto current-best cache (auto spec §5) — same contract as the bill
        // dialog's: owner dialog (transient), key none, value resolved best
        // (id, skill, inspired, roleOffset) + built label string; dependencies
        // colony pool contents + the dialog's filter fields; refresh at open,
        // on filter edits, and when ExternalPawnFactsRevision advances;
        // equality — same resolved
        // pawn+stats reuses the label; teardown — dies with the window.
        private int autoBestFactsRevision = int.MinValue;
        private int cachedAutoBestId = -1;
        private int cachedAutoSkill;
        private bool cachedAutoInspired;
        private int cachedAutoRoleOffset;
        private bool cachedAutoValid;
        private string? autoBestCurrentLabel;

        // Status cache (spec §11 status display) — same contract as the bill
        // dialog's: owner dialog (transient); key none; value = the
        // eligible-finisher id list, built labels, and the stall flag;
        // dependencies = map colonist pool, the dialog's condition/autoBest
        // edit fields, and the plan state; refresh gated by PlanStatusRevision
        // and forced on Push edits that affect eligibility;
        // equality — element-wise id compare preserves label identity;
        // teardown — dies with the window (scratch cleared each refresh).
        private int statusRevision = int.MinValue;
        private bool statusValid; // false = target off-map; status block hidden
        private bool statusStalled;
        private int statusEligibleCount = -1;
        private readonly List<int> statusEligibleIds = new List<int>(8);
        private readonly List<Pawn> statusPawnScratch = new List<Pawn>(16);
        private string? statusHeaderLabel;
        private string? statusFinishersLabel;
        private string? statusNamesLabel;
        private string? statusStalledLabel;

        // InitialSize.y computed from content + 2×Margin so the window fits exactly.
        // Text.LineHeightOf is a static array lookup initialized at startup — safe to
        // call from a property getter (before any rendering has started).
        // Left column height:  header (medLineH - TitleLift + TitleGap) + panel.
        // Right column height: odds + status, starting TitleLift ABOVE inRect.y
        //   (aligned with the lifted title), so TitleLift is subtracted.
        // Full window height = max(left, right) + 2×Margin.
        public override Vector2 InitialSize
        {
            get
            {
                float medLineH = Text.LineHeightOf(GameFont.Medium);
                float headerH  = medLineH - TitleLift + TitleGap;
                float leftH    = headerH + LeftPanelH();
                float rightH   = RightContentH + StatusGapH + StatusContentH - TitleLift;
                return new Vector2(520f, Mathf.Max(leftH, rightH) + 2f * WinMargin);
            }
        }

        /// Constructor takes target thing IDs, the primary map identity, and the anchor Rect (the
        /// gizmo button rect from GizmoOnGUI). The first thing in the list is the
        /// primary (values displayed come from its plan). When anchor == Rect.zero
        /// the window falls back to centered positioning.
        public Dialog_ConstructionPlanConfig(List<int> thingIds, Map? primaryMap,
            Rect anchor)
        {
            targetIds = new TrackedTargetIds(thingIds);
            this.primaryMap = primaryMap;
            _anchor = anchor;
            doCloseX = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            draggable = false;

            QualityJobsStore? store = QualityJobsStore.Active;
            PlanPresentationSnapshot? plan = null;
            store?.TryGetPlanPresentation(targetIds.Primary, out plan);
            int planMinSkill = plan?.MinSkill ?? 0;
            bool planInspired = plan?.RequireInspired ?? false;
            bool planSpecialist = plan?.RequireSpecialist ?? false;
            int planQuality = plan?.MinQuality ?? 0;
            bool planAutoBest = plan?.AutoBest ?? false;
            if (minSkill != planMinSkill) minSkill = planMinSkill;
            if (requireInspired != planInspired) requireInspired = planInspired;
            if (requireSpecialist != planSpecialist)
                requireSpecialist = planSpecialist;
            if (minQuality != planQuality) minQuality = planQuality;
            if (autoBest != planAutoBest) autoBest = planAutoBest;
            LoadLabels(plan);
        }

        public override void PreOpen()
        {
            base.PreOpen();
            subscribedStore = QualityJobsStore.Active;
            if (subscribedStore != null)
                subscribedStore.PlanRetargeted += OnPlanRetargeted;
        }

        public override void PostClose()
        {
            if (subscribedStore != null)
            {
                subscribedStore.PlanRetargeted -= OnPlanRetargeted;
                subscribedStore = null;
            }

            FloatMenu? menu = qualityMenu;
            qualityMenu = null;
            if (menu != null)
            {
                menu.onCloseCallback = null;
                StructuredTipPresenter.EndSuppression();
                if (menu.IsOpen) menu.Close(doCloseSound: false);
            }
            StructuredTipPresenter.Reset();
            base.PostClose();
        }

        private void OnPlanRetargeted(int previousThingId, int replacementThingId) =>
            targetIds.Retarget(previousThingId, replacementThingId);

        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;

            float x, y;
            if (_anchor == Rect.zero)
            {
                // Fallback: center on screen.
                x = (Verse.UI.screenWidth - size.x) / 2f;
                y = (Verse.UI.screenHeight - size.y) / 2f;
            }
            else
            {
                // Bottom edge of window aligns with top edge of gizmo button.
                x = _anchor.x;
                y = _anchor.y - size.y;
            }

            // Clamp to screen bounds.
            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Verse.UI.screenWidth - size.x));
            y = Mathf.Clamp(y, 0f, Mathf.Max(0f, Verse.UI.screenHeight - size.y));

            windowRect = new Rect(x, y, size.x, size.y).Rounded();
        }

        public override void DoWindowContents(Rect inRect)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            PlanPresentationSnapshot? plan = null;
            store?.TryGetPlanPresentation(targetIds.Primary, out plan);
            int currentMinSkill = plan?.MinSkill ?? 0;
            bool currentInspired = plan?.RequireInspired ?? false;
            bool currentSpecialist = plan?.RequireSpecialist ?? false;
            int currentQuality = plan?.MinQuality ?? 0;
            bool currentAutoBest = plan?.AutoBest ?? false;
            if (minSkill != currentMinSkill) minSkill = currentMinSkill;
            if (requireInspired != currentInspired) requireInspired = currentInspired;
            if (requireSpecialist != currentSpecialist)
                requireSpecialist = currentSpecialist;
            if (minQuality != currentQuality) minQuality = currentQuality;
            if (autoBest != currentAutoBest) autoBest = currentAutoBest;

            // Single EnsureAutoBest call site per pass, BEFORE DrawRightPanel,
            // so the odds mirror reads a fresh auto cache in the same pass
            // (DrawRightPanel runs before DrawLeftPanel below).
            if (autoBest) EnsureAutoBest();
            EnsureStatus(plan);

            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // Header: title in medium font, lifted so the visual top margin
                // matches the side margin (see TitleLift).
                Text.Font = GameFont.Medium;
                float medLineH = Text.LineHeight;
                float titleY = inRect.y - TitleLift;
                Widgets.Label(new Rect(inRect.x, titleY, inRect.width, medLineH), title!);
                Text.Font = GameFont.Small;

                // Columns, separated by the window's side padding (18f, owner
                // request): LEFT holds the options panel below the title; RIGHT
                // starts at the lifted title y so the odds header aligns with
                // the title, with the status block below the odds.
                float halfW = (inRect.width - WinMargin) / 2f;
                Rect leftRect = new Rect(inRect.x, titleY + medLineH + TitleGap,
                    halfW, LeftPanelH());
                Rect rightRect = new Rect(inRect.x + halfW + WinMargin, titleY,
                    halfW, inRect.yMax - titleY);

                DrawRightPanel(rightRect, plan);
                DrawLeftPanel(leftRect, plan);
            }
            finally
            {
                Text.Anchor = prevAnchor;
                Text.Font = prevFont;
            }
        }

        /// Right panel: odds table then the status block, top-anchored, no frame.
        /// Layout: MiniHeader (30f), 7 rows of 22f, gap, status MiniHeader + rows.
        private void DrawRightPanel(Rect rect, PlanPresentationSnapshot? plan)
        {
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            try
            {
                // Top-anchor: rect.y is the lifted title y (odds header aligns
                // with the window title). Rows ADVANCE by TightRowH; labels
                // DRAW in TightRowDrawH rects so descenders are not clipped
                // (see QjUi).
                float contentTop = rect.y;
                float rowH = TightRowH;
                float drawH = QjUi.TightRowDrawH;

                // MiniHeader: group-relative x=0 means the left edge of rect.
                // We draw with GUI absolute coords here (no BeginGroup), so x = rect.x.
                float afterHeader = QjUi.MiniHeader(rect.x, contentTop, rect.width, oddsHeaderLabel!);

                // Build odds for current displayed values.
                int roleOffset = requireSpecialist ? 1 : 0;
                int oddsSkill = minSkill;
                bool oddsInspired = requireInspired;
                if (autoBest && cachedAutoValid)
                {
                    // Auto mode (auto spec §5): odds show the pawn the gate
                    // currently demands. With no eligible colonist we fall back
                    // to the manual-threshold odds deliberately.
                    oddsSkill = cachedAutoSkill;
                    oddsInspired = cachedAutoInspired;
                    roleOffset = cachedAutoRoleOffset;
                }
                if (odds == null || !odds.Matches(oddsSkill, oddsInspired, roleOffset))
                    odds = OddsRows.Build(oddsSkill, oddsInspired, roleOffset);

                // Odds rows: Legendary down to Good, then "Normal or worse"
                // (bottom three qualities collapsed — OddsRows display order).
                float rowY = afterHeader;
                for (int r = 0; r < OddsRows.RowCount; r++)
                {
                    Rect row = new Rect(rect.x, rowY, rect.width, drawH);
                    Widgets.Label(row.LeftHalf(), oddsRowLabels![r]);
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(row.RightHalf(), odds.Percents[r]);
                    Text.Anchor = prevAnchor;
                    rowY += rowH;
                }

                // Status block (spec §11), below the odds rows: drawn only from
                // strings EnsureStatus (revision-gated) cached before the pass.
                if (statusValid)
                {
                    float afterStatusHeader = QjUi.MiniHeader(rect.x, rowY + StatusGapH,
                        rect.width, statusHeaderLabel!);
                    Rect finRow = new Rect(rect.x, afterStatusHeader, rect.width, drawH);
                    Widgets.Label(finRow, statusFinishersLabel!);
                    WrTips.Key("QJ_StatusFinishersTip").Region(finRow);

                    Rect infoRow = new Rect(rect.x, afterStatusHeader + rowH, rect.width, drawH);
                    if (statusEligibleCount > 0 && statusNamesLabel != null)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.55f);
                        Widgets.Label(infoRow, statusNamesLabel);
                        GUI.color = prevColor;
                    }
                    else if (statusStalled)
                    {
                        GUI.color = QjUi.WarnColor;
                        Widgets.Label(infoRow, statusStalledLabel!);
                        GUI.color = prevColor;
                        WrTips.Key("QJ_StatusStalledConstructionTip").Region(infoRow);
                    }
                }
            }
            finally
            {
                Text.Anchor = prevAnchor;
                GUI.color = prevColor;
            }
        }

        /// Left panel: framed (DrawMenuSection), top-anchored below the title
        /// (rect IS the panel rect, height LeftPanelH()).
        /// Inside: [Clear] row at the TOP (reserved unconditionally), flexible
        /// space, then options block anchored at the BOTTOM.
        private void DrawLeftPanel(Rect rect, PlanPresentationSnapshot? plan)
        {
            Color prevColor = GUI.color;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Rect panelRect = rect;
                Widgets.DrawMenuSection(panelRect);

                Rect inner = panelRect.ContractedBy(PanelPad);

                // Options block height (no trailing gap on last element).
                float optionsH = OptionsBlockH();

                // [Clear] button row: reserved unconditionally at the TOP of inner rect.
                // Height = SmallLineH. Only drawn when ANY selected thing has a plan.
                Rect clearRowRect = new Rect(inner.x, inner.y, inner.width, ClearRowH);
                if (AnyHasPlan())
                {
                    if (Widgets.ButtonText(clearRowRect, clearLabel!))
                    {
                        // Fix 5: Clear for every selected thing that has a plan.
                        for (int i = 0; i < targetIds.Count; i++)
                        {
                            QualityJobsStore? s = QualityJobsStore.Active;
                            int thingId = targetIds[i];
                            if (s != null && s.TryGetPlanPresentation(thingId, out _))
                                Commands.RemovePlan(thingId);
                        }
                    }
                }

                // Options block: anchored at the BOTTOM of inner rect.
                float optionsTop = inner.yMax - optionsH;
                DrawOptionsBlock(inner.x, optionsTop, inner.width, plan);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Anchor = prevAnchor;
            }
        }

        /// Returns true when any of the selected things has an active plan.
        /// Used to decide whether to show the [Clear] button (Fix 5).
        private bool AnyHasPlan()
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return false;
            for (int i = 0; i < targetIds.Count; i++)
                if (store.TryGetPlanPresentation(targetIds[i], out _)) return true;
            return false;
        }

        /// Draws the options controls (inspired checkbox, [specialist], auto-best
        /// checkbox, skill slider — or the current-best label when auto is on —
        /// and the target-quality row) laid out top-to-bottom starting at (x, startY).
        /// Uses manual rects (no Listing) so we can anchor from a computed bottom position.
        /// No trailing gap after the last element.
        ///
        /// Fix 2: The "Retried until" caption is removed. The target-quality button
        /// is right-aligned (occupies the right 40% of the row); the label is left.
        ///
        /// Rect-overlap audit (manual mode):
        ///   The slider occupies [y .. y+SliderH] = [y .. y+30f].
        ///   The target-quality row starts at y+SliderH+GapH = y+32f, which is
        ///   at least 30f (SliderH) below the slider's top. No overlap.
        ///   In auto mode the slider is replaced by a 22f label row; the
        ///   target-quality row then starts at y+SmallLineH+GapH = y+24f.
        private void DrawOptionsBlock(float x, float startY, float width,
            PlanPresentationSnapshot? plan)
        {
            Color prevColor = GUI.color;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // Read current values from plan; sync local copies when plan changes identity.
                int curMinSkill    = plan?.MinSkill       ?? 0;
                bool curInspired   = plan?.RequireInspired  ?? false;
                bool curSpecialist = plan?.RequireSpecialist ?? false;
                int curMinQuality  = plan?.MinQuality       ?? 0;
                bool curAutoBest   = plan?.AutoBest         ?? false;

                if (minSkill       != curMinSkill)    minSkill       = curMinSkill;
                if (requireInspired  != curInspired)  requireInspired  = curInspired;
                if (requireSpecialist != curSpecialist) requireSpecialist = curSpecialist;
                if (minQuality     != curMinQuality)  minQuality     = curMinQuality;
                if (autoBest       != curAutoBest)    autoBest       = curAutoBest;

                float y = startY;

                // (1) Require inspired checkbox.
                bool newInspired = requireInspired;
                Rect inspiredRect = new Rect(x, y, width, SmallLineH);
                Widgets.CheckboxLabeled(inspiredRect, requireInspiredLabel!, ref newInspired);
                y += SmallLineH + GapH;

                // (2) Require specialist checkbox (Ideology-gated).
                bool newSpecialist = requireSpecialist;
                if (ModsConfig.IdeologyActive)
                {
                    Rect specialistRect = new Rect(x, y, width, SmallLineH);
                    Widgets.CheckboxLabeled(specialistRect, requireSpecialistLabel!, ref newSpecialist);
                    y += SmallLineH + GapH;
                }

                // (2b) Auto-best checkbox.
                bool newAutoBest = autoBest;
                Rect autoRect = new Rect(x, y, width, SmallLineH);
                Widgets.CheckboxLabeled(autoRect, autoBestLabel!, ref newAutoBest);
                WrTips.Key("QJ_AutoBestTip").Region(autoRect);
                y += SmallLineH + GapH;

                // (3) Finisher-skill slider — or current-best label in auto mode.
                // EnsureAutoBest is NOT called here: the single per-pass call
                // site lives in DoWindowContents (before DrawRightPanel).
                int newMinSkill = minSkill;
                if (autoBest)
                {
                    Rect autoRow = new Rect(x, y, width, SmallLineH);
                    Color prevRowColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.55f);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(autoRow, autoBestCurrentLabel ?? autoBestNoneLabel!);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = prevRowColor;
                    y += SmallLineH + GapH;
                }
                else
                {
                    // Slider occupies: y .. y+SliderH (30f). No overlap with the
                    // row below, which starts at y+SliderH+GapH = y+32f.
                    if (minSkill != minSkillLabelValue)
                    {
                        minSkillLabel = "QJ_FinisherSkill".Translate(minSkill);
                        minSkillLabelValue = minSkill;
                    }
                    Rect sliderRowRect = new Rect(x, y, width, SliderH);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(sliderRowRect.LeftHalf(), minSkillLabel!);
                    WrTips.Key("QJ_FinisherSkillTip").Region(sliderRowRect.LeftHalf());
                    Text.Anchor = TextAnchor.UpperLeft;
                    newMinSkill = (int)Widgets.HorizontalSlider(
                        sliderRowRect.RightHalf(), minSkill, 0f, 20f, middleAlignment: true);
                    y += SliderH + GapH;
                }

                // (4) Target-quality row (Fix 2): label on the left, button RIGHT-aligned.
                // The "Retried until" caption is removed entirely.
                // Row starts at y (the skill row above ended at y - GapH = y - 2f,
                // whether it was the 30f slider or the 22f current-best label).
                Rect qualityRowRect = new Rect(x, y, width, SmallLineH);
                float btnW = width * QualityBtnWidthFraction;
                Rect qualityBtnRect  = new Rect(qualityRowRect.xMax - btnW, qualityRowRect.y, btnW, SmallLineH);
                Rect qualityLabelRect = new Rect(qualityRowRect.x, qualityRowRect.y,
                    qualityRowRect.width - btnW, SmallLineH);
                Widgets.Label(qualityLabelRect, targetQualityLabel!);
                string btnCaption = minQuality <= 0 ? noRetriesLabel! : qualityLabels![minQuality];
                if (Widgets.ButtonText(qualityBtnRect, btnCaption))
                {
                    // Build options list on click only — allocation on interaction, not per frame.
                    // Fix 3: set vanishIfMouseDistant = false so the menu does not self-close
                    // when spawned clamped away from the mouse near the screen edge.
                    // Verified: FloatMenu.vanishIfMouseDistant field at
                    //   Decompiled\Verse\FloatMenu.cs line 14 (public bool vanishIfMouseDistant = true).
                    var options = new List<FloatMenuOption>();
                    options.Add(new FloatMenuOption(anyQualityLabel!, () =>
                        PushMinQuality(0)));
                    for (int q = 1; q <= 6; q++)
                    {
                        int capturedQ = q;
                        options.Add(new FloatMenuOption(qualityLabels![q], () =>
                            PushMinQuality(capturedQ)));
                    }
                    var menu = new FloatMenu(options)
                    {
                        vanishIfMouseDistant = false,
                        onCloseCallback = OnQualityMenuClosed,
                    };
                    qualityMenu = menu;
                    StructuredTipPresenter.BeginSuppression();
                    try
                    {
                        Find.WindowStack.Add(menu);
                    }
                    catch
                    {
                        menu.onCloseCallback = null;
                        qualityMenu = null;
                        try
                        {
                            if (menu.IsOpen) menu.Close(doCloseSound: false);
                        }
                        finally
                        {
                            StructuredTipPresenter.EndSuppression();
                        }
                        throw;
                    }
                }
                if (qualityMenu == null)
                    WrTips.Key("QJ_RetriedUntilTip").Region(
                        qualityLabelRect, qualityBtnRect);
                // y not advanced after last row (no trailing gap).

                Push(newMinSkill, newInspired, newSpecialist, newAutoBest);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Anchor = prevAnchor;
            }
        }

        private void LoadLabels(PlanPresentationSnapshot? plan)
        {
            // Sync local edit copies from plan (if exists).
            if (plan != null)
            {
                minSkill       = plan.MinSkill;
                requireInspired  = plan.RequireInspired;
                requireSpecialist = plan.RequireSpecialist;
                minQuality     = plan.MinQuality;
                autoBest       = plan.AutoBest;
            }
            // else: local copies stay at neutral defaults (0/false/false/0).

            // At-open refresh of the auto current-best cache (matches the bill
            // dialog's LoadFromStore reset; the field initializer covers the
            // very first pass — this keeps the contract's "refresh at open"
            // true by mechanism as well).
            autoBestFactsRevision = int.MinValue;
            cachedAutoValid = false;
            cachedAutoBestId = -1;
            autoBestCurrentLabel = null;

            // Tooltips are not cached here: they render through WrTips, which
            // owns its own caching and language invalidation.
            title                = "QJ_ConstructionPanelTitle".Translate();
            requireInspiredLabel = "QJ_RequireInspired".Translate();
            requireSpecialistLabel = "QJ_RequireSpecialist".Translate();
            oddsHeaderLabel      = "QJ_OddsHeader".Translate();
            clearLabel           = "QJ_Clear".Translate();
            noRetriesLabel       = "QJ_NoRetries".Translate();
            targetQualityLabel   = "QJ_MinQualityLabel".Translate();
            anyQualityLabel      = "QJ_AnyQuality".Translate();
            autoBestLabel        = "QJ_AutoBest".Translate();
            autoBestNoneLabel    = "QJ_AutoBestNone".Translate();
            statusHeaderLabel    = "QJ_StatusHeader".Translate();
            statusStalledLabel   = "QJ_StatusStalledConstruction".Translate();

            qualityLabels = new string[7];
            for (int q = 0; q <= 6; q++)
                qualityLabels[q] = ((QualityCategory)q).GetLabel().CapitalizeFirst();
            oddsRowLabels = new string[OddsRows.RowCount];
            for (int r = 0; r < 4; r++)
                oddsRowLabels[r] = qualityLabels[6 - r];
            oddsRowLabels[4] = "QJ_NormalOrWorse".Translate();
        }

        /// Pushes minQuality to all selected things (Fix 5).
        /// Plain per-field setter; auto-creates the plan when needed
        /// (SetPlanMinQuality handles neutral auto-create/remove).
        private void PushMinQuality(int value)
        {
            minQuality = value;
            for (int i = 0; i < targetIds.Count; i++)
                Commands.SetPlanMinQuality(targetIds[i], value);
        }

        private void OnQualityMenuClosed()
        {
            qualityMenu = null;
            StructuredTipPresenter.EndSuppression();
        }

        /// <summary>Revision-gated auto current-best evaluation (auto spec §5).
        /// The store observes unpatched external pawn facts at its explicitly
        /// named responsiveness boundary; steady render passes compare one int.</summary>
        private void EnsureAutoBest()
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            int revision = store?.ExternalPawnFactsRevision ?? 0;
            if (autoBestFactsRevision == revision) return;
            autoBestFactsRevision = revision;
            Pawn? best = Dispatcher.ResolveAutoBestFacts(null,
                requireInspired, requireSpecialist,
                out int skill, out bool inspired, out int roleOffset);
            if (best == null)
            {
                cachedAutoValid = false;
                cachedAutoBestId = -1;
                autoBestCurrentLabel = null;
                return;
            }
            // Equality: same resolved identity + stats keeps the label instance.
            if (!cachedAutoValid || best.thingIDNumber != cachedAutoBestId
                || skill != cachedAutoSkill || inspired != cachedAutoInspired
                || roleOffset != cachedAutoRoleOffset)
            {
                cachedAutoBestId = best.thingIDNumber;
                cachedAutoSkill = skill;
                cachedAutoInspired = inspired;
                cachedAutoRoleOffset = roleOffset;
                autoBestCurrentLabel = "QJ_AutoBestCurrent".Translate(best.LabelShort);
            }
            cachedAutoValid = true;
        }

        /// <summary>Revision-gated status refresh (spec §11). Steady render
        /// passes compare PlanStatusRevision and reuse the immutable presentation
        /// strings/IDs built on the last invalidation.</summary>
        private void EnsureStatus(PlanPresentationSnapshot? plan)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            int revision = store?.PlanStatusRevision ?? 0;
            if (statusRevision == revision) return;
            statusRevision = revision;

            Map? map = plan?.Map ?? primaryMap;
            if (map == null)
            {
                statusValid = false;
                return;
            }
            statusValid = true;

            // Eligibility mirrors construction dispatch: recipe == null ranks
            // by Construction skill; auto mode replaces MinSkill with the
            // dynamic threshold, so only the filters travel with it.
            ResumeCondition condition = autoBest
                ? new ResumeCondition(0, requireInspired, requireSpecialist)
                : new ResumeCondition(minSkill, requireInspired, requireSpecialist);
            Dispatcher.CollectEligibleFinishers(map, null, condition, autoBest,
                statusPawnScratch);

            bool sameIds = statusPawnScratch.Count == statusEligibleIds.Count;
            if (sameIds)
                for (int i = 0; i < statusPawnScratch.Count; i++)
                    if (statusPawnScratch[i].thingIDNumber != statusEligibleIds[i])
                    {
                        sameIds = false;
                        break;
                    }
            if (!sameIds)
            {
                statusEligibleIds.Clear();
                for (int i = 0; i < statusPawnScratch.Count; i++)
                    statusEligibleIds.Add(statusPawnScratch[i].thingIDNumber);
                int n = statusPawnScratch.Count;
                statusFinishersLabel = n == 0
                    ? "QJ_StatusFinishersNone".Translate()
                    : "QJ_StatusFinishers".Translate(n);
                statusNamesLabel = n == 0 ? null : QjUi.NamesLine(statusPawnScratch);
            }
            statusEligibleCount = statusPawnScratch.Count;
            statusPawnScratch.Clear();

            // Paused plan with nobody eligible = the silent stall the status
            // block exists to surface.
            statusStalled = plan != null && plan.State == ConstructionPlanState.Paused
                && statusEligibleCount == 0;
        }

        /// Pushes per-field changes to all selected things (Fix 5).
        /// Plain per-field setters; each auto-creates the plan when needed
        /// and auto-removes it if all fields become neutral.
        private void Push(int newMinSkill, bool newInspired, bool newSpecialist, bool newAutoBest)
        {
            bool skillChanged     = newMinSkill   != minSkill;
            bool inspiredChanged  = newInspired   != requireInspired;
            bool specChanged      = newSpecialist != requireSpecialist;
            bool autoChanged      = newAutoBest   != autoBest;

            if (skillChanged)    minSkill          = newMinSkill;
            if (inspiredChanged) requireInspired   = newInspired;
            if (specChanged)     requireSpecialist = newSpecialist;
            if (autoChanged)     autoBest          = newAutoBest;
            if (inspiredChanged || specChanged || autoChanged)
                autoBestFactsRevision = int.MinValue;
            if (skillChanged || inspiredChanged || specChanged || autoChanged)
                statusRevision = int.MinValue;

            if (!skillChanged && !inspiredChanged && !specChanged && !autoChanged) return;

            for (int i = 0; i < targetIds.Count; i++)
            {
                int thingId = targetIds[i];
                if (skillChanged)    Commands.SetPlanMinSkill(thingId, newMinSkill);
                if (inspiredChanged) Commands.SetPlanRequireInspired(thingId, newInspired);
                if (specChanged)     Commands.SetPlanRequireSpecialist(thingId, newSpecialist);
                if (autoChanged)     Commands.SetPlanAutoBest(thingId, newAutoBest);
            }
        }
    }
}
