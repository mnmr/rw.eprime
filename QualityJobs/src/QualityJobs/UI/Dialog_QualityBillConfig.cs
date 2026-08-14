using System.Collections.Generic;
using QualityJobs.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// Owned bill dialog (spec §11): vanilla Dialog_BillConfig content intact in
    /// the left region, quality panel in an added right column. Transient window —
    /// zero save/MP footprint. All mutations go through Commands.
    public class Dialog_QualityBillConfig : Dialog_BillConfig
    {
        private const float PanelWidth = 280f;
        private const float PanelGap = 10f;

        // Verified against Dialog_BillConfig.DoWindowContents (Decompiled/RimWorld/Dialog_BillConfig.cs
        // lines 120-121): rect2 and rect3 both start at y=50f (the bill title label occupies
        // y=0..34f; vanilla leaves a gap before the column content begins at y=50f).
        private const float TitleOffset = 50f;

        // Inner padding for the DrawMenuSection options panel (A3).
        private const float SectionPad = 6f;

        // Per-frame local edit copies; pushed via Commands only on actual change.
        // The two-layer idempotency matches QualityJobsMod.cs: local compare here,
        // Commands no-op compare again — AGENTS.md §authoritative-state.
        private bool managed;
        private bool autoBest;
        private int minSkill;
        private bool requireInspired;
        private bool requireSpecialist;
        private int targetQuality;
        private int cap;
        private bool loaded;
        private readonly Listing_Standard listing = new Listing_Standard();
        private BillPresentationSnapshot? presentation;

        // Odds caches — keyed (minSkill, inspired, roleOffset); rebuilt on mismatch.
        // Owner: dialog (transient). Dependencies: condition fields only.
        // Teardown: dies with the window.
        private OddsRows? thresholdOdds;
        private OddsRows? bestOdds;

        // Best-candidate throttle cache.
        // Owner: dialog (transient). Key: none (single pawn). Value: pawn + odds.
        // Dependencies: external pawn facts. Refresh: revision-gated and reset
        // on LoadFromStore.
        // Teardown: dies with the window.
        private int bestFactsRevision = int.MinValue;
        private int cachedBestSkill;
        private bool cachedBestInspired;
        private int cachedBestRoleOffset;
        private bool cachedBestValid; // false = no eligible pawn found

        // Auto current-best cache (auto spec §5).
        // Owner: dialog (transient). Key: none (single value). Value: resolved
        // best (id, skill, inspired, roleOffset) + built label string.
        // Dependencies: colony pool contents and the dialog's condition filter
        // fields. Refresh: rebuilt at window open (LoadFromStore), when a filter
        // field is edited (PushChanges), and when ExternalPawnFactsRevision moves.
        // Equality: same (pawnId, skill, inspired, roleOffset) reuses the label.
        // Teardown: dies with the window.
        private int autoBestFactsRevision = int.MinValue;
        private int cachedAutoBestId = -1;
        private int cachedAutoSkill;
        private bool cachedAutoInspired;
        private int cachedAutoRoleOffset;
        private bool cachedAutoValid;
        private string? _autoBestCurrentLabel;

        // Odds table row labels (Legendary..Good, then "Normal or worse"),
        // built once per dialog open in LoadFromStore alongside _qualityLabels
        // (which stays full 7-entry for the target-quality picker).
        // Owner: dialog instance. Teardown: dies with the window.
        private string[]? _oddsRowLabels;

        // Quality label cache: built once per dialog open in LoadFromStore.
        // Language changes are not observable while the dialog is open — the dialog
        // is closed and reopened after a language switch, so a reopened dialog always
        // builds fresh instance fields from the current language.
        //
        // Owner: dialog instance. Teardown: dies with the window.
        private string[]? _qualityLabels;

        // Constant translated strings cached as instance fields built in LoadFromStore.
        // None of these have runtime-arg interpolation, so a single allocation per
        // dialog open is correct.
        // Owner: dialog instance. Teardown: dies with the window.
        private string? _panelTitleLabel;
        private string? _manageBillLabel;
        private string? _requireInspiredLabel;
        private string? _requireSpecialistLabel;
        private string? _oddsHeaderLabel;
        private string? _oddsColConfigLabel;
        private string? _oddsColBestLabel;
        private string? _autoBestLabel;
        private string? _autoBestNoneLabel;
        private string? _targetQualityLabel;
        private string? _anyQualityLabel;

        // I4: interpolated slider labels, rebuilt only when the displayed value changes.
        // Owner: dialog instance. Teardown: dies with the window.
        private string? _minSkillLabel;
        private int _minSkillLabelValue = -1;
        private string? _capLabel;
        private int _capLabelValue = -1;

        // Status section cache (spec §11 status display).
        // Owner: dialog (transient). Key: none. Value: pipeline counts
        // (shared/waiting/finishing), the eligible-finisher id list, and the
        // built display strings. Dependencies: store entries (updated by lifecycle
        // events and reconciliation boundaries), the map colonist pool, and the dialog's
        // condition/autoBest edit fields. Refresh: BillStatusRevision-gated;
        // forced on any PushChanges edit that affects
        // eligibility (immediate visibility, including while paused).
        // Equality: each label is rebuilt only when its source values change
        // (element-wise id compare preserves the names label identity).
        // Teardown: dies with the window (scratch list cleared each refresh).
        private int statusRevision = int.MinValue;
        private bool statusValid; // false = bench has no map or unmanaged recipe; section hidden
        private int statusWaiting = -1;
        private int statusFinishing = -1;
        private int statusShared = -1;
        private bool statusSharedShown;
        private int statusEligibleCount = -1;
        private readonly List<int> statusEligibleIds = new List<int>(8);
        private readonly List<Pawn> statusPawnScratch = new List<Pawn>(16);
        private string? _statusHeaderLabel;
        private string? _statusQueueLabel;
        private string? _statusFinishersLabel;
        private string? _statusNamesLabel;
        private string? _statusStalledLabel;

        public Dialog_QualityBillConfig(Bill_ProductionWithUft bill, IntVec3 billGiverPos)
            : base(bill, billGiverPos)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store != null) LoadFromStore(store);
        }

        public override Vector2 InitialSize
            => new Vector2(base.InitialSize.x + PanelWidth, base.InitialSize.y);

        public override void DoWindowContents(Rect inRect)
        {
            // Vanilla DoWindowContents uses (inRect.width - 34f) / 3 for column
            // widths, so it is fully relative to the rect passed in. Narrowing
            // inRect to the left region is safe and does not affect vanilla layout.
            Rect vanillaRect = inRect;
            vanillaRect.width -= PanelWidth;
            base.DoWindowContents(vanillaRect);
            DrawQualityPanel(new Rect(vanillaRect.xMax + PanelGap, inRect.y + TitleOffset,
                PanelWidth - PanelGap, inRect.height - TitleOffset));
        }

        private void DrawQualityPanel(Rect rect)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            if (!loaded) LoadFromStore(store);
            else
            {
                BillPresentationSnapshot current = store.BillPresentationFor(bill);
                if (!ReferenceEquals(presentation, current))
                    AdoptPresentation(current);
            }
            if (autoBest) EnsureAutoBest();
            EnsureStatus(store);

            // Hoist odds results before Begin so any early return inside the
            // listing body cannot skip the finally that restores Text.Font.
            OddsRows thresholdRows = EnsureThresholdOdds();
            OddsRows? bestRows = EnsureBestOdds();

            listing.Begin(rect);
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevPanelColor = GUI.color;
            try
            {
                Text.Font = GameFont.Medium;
                listing.Label(_panelTitleLabel!);
                Text.Font = GameFont.Small;

                // Options section, canonical row order shared with the
                // construction fold-out and the settings grid: manage,
                // inspired, [specialist], auto-adjust, skill row (slider, or
                // the current-best label in auto mode), target quality, cap.
                // Manual rects (like the fold-out) so rows, paddings, and the
                // 50% label/control split align across both panels. Metrics:
                // checkbox/label/button rows 22f, slider rows 30f, 2f gaps,
                // no trailing gap on the last row.
                const float RowH = 22f;    // Text.LineHeight for GameFont.Small
                const float SliderH = 30f; // slider row height
                const float Gap = 2f;      // vertical gap between rows
                float skillRowH = autoBest ? RowH : SliderH;
                float optionsContentH = RowH + Gap          // manage
                    + RowH + Gap                            // inspired
                    + (ModsConfig.IdeologyActive ? RowH + Gap : 0f)
                    + RowH + Gap                            // auto-adjust
                    + skillRowH + Gap                       // skill row
                    + RowH + Gap                            // target quality
                    + SliderH;                              // cap (last row)
                float sectionBoxH = optionsContentH + SectionPad * 2f;
                // CurHeight is the public property exposing protected curY (Listing.cs line 30).
                float sectionBoxY = listing.CurHeight;

                // Draw the section box (background + border) first, behind the controls.
                // IMPORTANT: listing.Begin(rect) opened a GUI group, so ALL coordinates
                // here are RELATIVE to the panel origin — using rect.x/rect.y again
                // would double-offset and push everything outside the visible panel.
                Widgets.DrawMenuSection(new Rect(0f, sectionBoxY, rect.width, sectionBoxH));

                {
                    // Read current UI state into locals; mutate locals; push changes on
                    // actual difference. OnGUI is multi-pass; every pass must be idempotent.
                    float x = SectionPad;
                    float w = rect.width - SectionPad * 2f;
                    float y = sectionBoxY + SectionPad;

                    // (1) Manage this bill.
                    bool newManaged = managed;
                    Widgets.CheckboxLabeled(new Rect(x, y, w, RowH), _manageBillLabel!, ref newManaged);
                    y += RowH + Gap;

                    // (2) Require inspired creativity.
                    bool newInspired = requireInspired;
                    Widgets.CheckboxLabeled(new Rect(x, y, w, RowH), _requireInspiredLabel!, ref newInspired);
                    y += RowH + Gap;

                    // (3) Require production specialist (Ideology only).
                    bool newSpecialist = requireSpecialist;
                    if (ModsConfig.IdeologyActive)
                    {
                        Widgets.CheckboxLabeled(new Rect(x, y, w, RowH), _requireSpecialistLabel!, ref newSpecialist);
                        y += RowH + Gap;
                    }

                    // (4) Auto-adjust finisher skill.
                    bool newAutoBest = autoBest;
                    Rect autoRect = new Rect(x, y, w, RowH);
                    Widgets.CheckboxLabeled(autoRect, _autoBestLabel!, ref newAutoBest);
                    WrTips.Key("QJ_AutoBestTip").Region(autoRect);
                    y += RowH + Gap;

                    // (5) Finisher skill: slider, or the current-best label in auto mode.
                    int newMinSkill = minSkill;
                    if (autoBest)
                    {
                        Rect autoRow = new Rect(x, y, w, RowH);
                        Color prevRowColor = GUI.color;
                        GUI.color = new Color(1f, 1f, 1f, 0.55f);
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Widgets.Label(autoRow, _autoBestCurrentLabel ?? _autoBestNoneLabel!);
                        Text.Anchor = prevAnchor;
                        GUI.color = prevRowColor;
                        y += RowH + Gap;
                    }
                    else
                    {
                        // I4: rebuild interpolated label only when the displayed value changes.
                        if (minSkill != _minSkillLabelValue)
                        {
                            _minSkillLabel = "QJ_FinisherSkill".Translate(minSkill);
                            _minSkillLabelValue = minSkill;
                        }
                        Rect skillLabel = new Rect(x, y, w * 0.5f, SliderH);
                        Text.Anchor = TextAnchor.MiddleLeft;
                        Widgets.Label(skillLabel, _minSkillLabel!);
                        Text.Anchor = prevAnchor;
                        WrTips.Key("QJ_FinisherSkillTip").Region(skillLabel);
                        newMinSkill = (int)Widgets.HorizontalSlider(
                            new Rect(x + w * 0.5f, y, w * 0.5f, SliderH), minSkill, 0f, 20f,
                            middleAlignment: true);
                        y += SliderH + Gap;
                    }

                    // (6) Target quality: label left, picker button right.
                    Rect qualityRow = new Rect(x, y, w, RowH);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(x, y, w * 0.5f, RowH), _targetQualityLabel!);
                    Text.Anchor = prevAnchor;
                    string qualityCaption = targetQuality <= 0
                        ? _anyQualityLabel! : _qualityLabels![targetQuality];
                    if (Widgets.ButtonText(new Rect(x + w * 0.5f, y, w * 0.5f, RowH), qualityCaption))
                    {
                        // Menu built on click only; allocation on interaction, not per frame.
                        var options = new System.Collections.Generic.List<FloatMenuOption>();
                        options.Add(new FloatMenuOption(_anyQualityLabel!, () => PushTargetQuality(0)));
                        for (int q = 1; q <= 6; q++)
                        {
                            int capturedQ = q;
                            options.Add(new FloatMenuOption(_qualityLabels![q],
                                () => PushTargetQuality(capturedQ)));
                        }
                        Find.WindowStack.Add(new FloatMenu(options) { vanishIfMouseDistant = false });
                    }
                    WrTips.Key("QJ_BillTargetQualityTip").Region(qualityRow);
                    y += RowH + Gap;

                    // (7) Stock cap slider (last row).
                    // I4: rebuild interpolated cap label only when the displayed value changes.
                    if (cap != _capLabelValue)
                    {
                        _capLabel = "QJ_StockCapLabel".Translate(cap);
                        _capLabelValue = cap;
                    }
                    Rect capLabel = new Rect(x, y, w * 0.5f, SliderH);
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(capLabel, _capLabel!);
                    Text.Anchor = prevAnchor;
                    WrTips.Key("QJ_StockCapTooltip").Region(capLabel);
                    int newCap = (int)Widgets.HorizontalSlider(
                        new Rect(x + w * 0.5f, y, w * 0.5f, SliderH), cap, 0f, 50f,
                        middleAlignment: true);

                    PushChanges(newManaged, newAutoBest, newMinSkill, newInspired, newSpecialist, newCap);
                }

                // Advance the outer listing past the section box.
                listing.GetRect(sectionBoxH);

                // Odds section: mini-header via QjUi.MiniHeader (group-relative coords —
                // listing.Begin(rect) opened a GUI group so x=0 is the panel left edge).
                // listing.CurHeight is the next available y within the group.
                // MiniHeader returns y + 27f; we advance the listing past that height.
                // 8f pre-gap: a header with content above it gets 4f extra
                // padding (owner request; a first header would get none).
                float headerY = listing.CurHeight + 8f;
                QjUi.MiniHeader(0f, headerY, rect.width, _oddsHeaderLabel!);
                // Advance past header block: 8f pre-gap + 27f header = 35f total.
                listing.GetRect(35f);

                DrawOddsTable(listing, thresholdRows, bestRows);

                // Status section (spec §11), below the odds: mini-header +
                // fixed-height rows drawn exclusively from strings EnsureStatus
                // (revision-gated) cached before Begin. The stall line is the
                // diagnostic for the silent no-eligible-finisher pipeline stop.
                if (statusValid)
                {
                    // 8f pre-gap: header with content above it (see odds header).
                    float statusHeaderY = listing.CurHeight + 8f;
                    QjUi.MiniHeader(0f, statusHeaderY, rect.width, _statusHeaderLabel!);
                    // 8f pre-gap + 27f header (MiniHeader returns y + 27f).
                    listing.GetRect(35f);

                    // Rows advance by TightRowH; labels draw in TightRowDrawH
                    // rects so descenders are not clipped (see QjUi).
                    const float StatusRowH = QjUi.TightRowH;
                    const float StatusDrawH = QjUi.TightRowDrawH;
                    Rect queueRow = listing.GetRect(StatusRowH);
                    queueRow.height = StatusDrawH;
                    Widgets.Label(queueRow, _statusQueueLabel!);
                    WrTips.Key("QJ_StatusQueueTip").Region(queueRow);

                    Rect finRow = listing.GetRect(StatusRowH);
                    finRow.height = StatusDrawH;
                    Widgets.Label(finRow, _statusFinishersLabel!);
                    WrTips.Key("QJ_StatusFinishersTip").Region(finRow);

                    if (statusEligibleCount > 0 && _statusNamesLabel != null)
                    {
                        Rect namesRow = listing.GetRect(StatusRowH);
                        namesRow.height = StatusDrawH;
                        GUI.color = new Color(1f, 1f, 1f, 0.55f);
                        Widgets.Label(namesRow, _statusNamesLabel);
                        GUI.color = prevPanelColor;
                    }
                    else if (statusWaiting > 0 && statusEligibleCount == 0)
                    {
                        Rect stallRow = listing.GetRect(StatusRowH);
                        stallRow.height = StatusDrawH;
                        GUI.color = QjUi.WarnColor;
                        Widgets.Label(stallRow, _statusStalledLabel!);
                        GUI.color = prevPanelColor;
                        WrTips.Key("QJ_StatusStalledTip").Region(stallRow);
                    }
                }
            }
            finally
            {
                Text.Anchor = prevAnchor;
                Text.Font = prevFont;
                GUI.color = prevPanelColor;
                listing.End();
            }
        }

        private void LoadFromStore(QualityJobsStore store)
        {
            AdoptPresentation(store.BillPresentationFor(bill));

            // Build all instance-scoped label caches once per dialog open.
            // Reopening the dialog always constructs a fresh instance, so these
            // are always built from the language active at open time.
            _qualityLabels = new string[7];
            for (int q = 0; q < 7; q++)
                _qualityLabels[q] = ((QualityCategory)q).GetLabel().CapitalizeFirst();
            _oddsRowLabels = new string[OddsRows.RowCount];
            for (int r = 0; r < 4; r++)
                _oddsRowLabels[r] = _qualityLabels[6 - r];
            _oddsRowLabels[4] = "QJ_NormalOrWorse".Translate();
            _panelTitleLabel = "QJ_QualityPanelTitle".Translate();
            _manageBillLabel = "QJ_ManageBill".Translate();
            _requireInspiredLabel = "QJ_RequireInspired".Translate();
            _requireSpecialistLabel = "QJ_RequireSpecialist".Translate();
            _oddsHeaderLabel = "QJ_OddsHeader".Translate();
            _oddsColConfigLabel = "QJ_OddsColConfig".Translate();
            _oddsColBestLabel = "QJ_OddsColBest".Translate();
            _autoBestLabel = "QJ_AutoBest".Translate();
            _autoBestNoneLabel = "QJ_AutoBestNone".Translate();
            _targetQualityLabel = "QJ_MinQualityLabel".Translate();
            _anyQualityLabel = "QJ_AnyQuality".Translate();
            _statusHeaderLabel = "QJ_StatusHeader".Translate();
            _statusStalledLabel = "QJ_StatusStalled".Translate();

            loaded = true;
        }

        private void AdoptPresentation(BillPresentationSnapshot current)
        {
            bool sourceChanged = presentation == null
                || !ReferenceEquals(presentation.Recipe, current.Recipe)
                || !ReferenceEquals(presentation.Map, current.Map)
                || !string.Equals(presentation.ProductDefName,
                    current.ProductDefName, System.StringComparison.Ordinal);
            BillConfig config = current.Config;
            bool eligibilityChanged = minSkill != config.Condition.MinSkill
                || requireInspired != config.Condition.RequireInspired
                || requireSpecialist != config.Condition.RequireSpecialist
                || autoBest != config.AutoBest;

            presentation = current;
            managed = config.Managed;
            minSkill = config.Condition.MinSkill;
            requireInspired = config.Condition.RequireInspired;
            // ConfigFor already coerces specialist via the Ideology gate; mirror here
            // so the local copy is clean and PushChanges never sends true without Ideology.
            requireSpecialist = config.Condition.RequireSpecialist && ModsConfig.IdeologyActive;
            autoBest = config.AutoBest;
            targetQuality = presentation.TargetQuality;
            cap = presentation.ProductCap;
            if (!sourceChanged && !eligibilityChanged) return;

            // Force re-evaluation only when inputs consumed by these builders
            // changed. Cap/target/managed-only updates retain the derived rows.
            bestFactsRevision = int.MinValue;
            autoBestFactsRevision = int.MinValue;
            statusRevision = int.MinValue;
            cachedBestValid = false;
            cachedAutoValid = false;
            cachedAutoBestId = -1;
            _autoBestCurrentLabel = null;
        }

        private void PushChanges(bool newManaged, bool newAutoBest, int newMinSkill,
            bool newInspired, bool newSpecialist, int newCap)
        {
            // Local compare first (fast path). Commands perform a second no-op
            // compare against store truth — two defense layers per AGENTS.md.
            if (newManaged != managed)
            {
                managed = newManaged;
                Commands.SetBillManaged(presentation!.BillId, newManaged);
            }

            if (newAutoBest != autoBest)
            {
                autoBest = newAutoBest;
                Commands.SetBillAutoBest(presentation!.BillId, newAutoBest);
                // Filter/mode edit: re-evaluate the current best immediately.
                autoBestFactsRevision = int.MinValue;
                statusRevision = int.MinValue;
            }

            if (newMinSkill != minSkill)
            {
                minSkill = newMinSkill;
                Commands.SetBillMinSkill(presentation!.BillId, newMinSkill);
                statusRevision = int.MinValue;
            }

            if (newInspired != requireInspired)
            {
                requireInspired = newInspired;
                Commands.SetBillRequireInspired(presentation!.BillId, newInspired);
                autoBestFactsRevision = int.MinValue;
                statusRevision = int.MinValue;
            }

            if (newSpecialist != requireSpecialist)
            {
                requireSpecialist = newSpecialist;
                Commands.SetBillRequireSpecialist(presentation!.BillId, newSpecialist);
                autoBestFactsRevision = int.MinValue;
                statusRevision = int.MinValue;
            }

            if (newCap != cap)
            {
                cap = newCap;
                string? product = presentation!.ProductDefName;
                if (product != null) Commands.SetProductCap(product, newCap);
            }
        }

        /// Pushes the target quality picked from the float menu (click path,
        /// not per frame). Same two-layer no-op discipline as PushChanges.
        private void PushTargetQuality(int value)
        {
            if (value == targetQuality) return;
            targetQuality = value;
            Commands.SetBillTargetQuality(presentation!.BillId, value);
        }

        private OddsRows EnsureThresholdOdds()
        {
            if (autoBest && cachedAutoValid)
            {
                // Auto mode (auto spec §5): the Config column shows the odds of
                // the pawn the gate currently demands.
                if (thresholdOdds == null || !thresholdOdds.Matches(
                        cachedAutoSkill, cachedAutoInspired, cachedAutoRoleOffset))
                    thresholdOdds = OddsRows.Build(
                        cachedAutoSkill, cachedAutoInspired, cachedAutoRoleOffset);
                return thresholdOdds;
            }
            // Auto mode with NO eligible colonist (cachedAutoValid false) falls
            // through here deliberately: the manual-threshold odds are the least
            // misleading display next to the "No eligible colonist" line.
            // requireSpecialist implies a roleOffset of +1 per spec §11 display logic.
            int roleOffset = requireSpecialist ? 1 : 0;
            if (thresholdOdds == null || !thresholdOdds.Matches(minSkill, requireInspired, roleOffset))
                thresholdOdds = OddsRows.Build(minSkill, requireInspired, roleOffset);
            return thresholdOdds;
        }

        private OddsRows? EnsureBestOdds()
        {
            // Revision gate: SelectFinisher iterates colonists only after the
            // store's external pawn-facts revision moves.
            //
            // Cache contract — Owner: dialog (transient). Key: none.
            // Value: cached pawn stats (skill, inspired, roleOffset) + valid flag.
            // Dependencies: current colonist pool. Refresh: revision-gated.
            // Equality: Matches() on new stats preserves bestOdds identity.
            // Teardown: dies with the window.
            QualityJobsStore? store = QualityJobsStore.Active;
            int revision = store?.ExternalPawnFactsRevision ?? 0;
            if (bestFactsRevision == revision)
            {
                // Return cached result.
                return cachedBestValid ? EnsureBestOddsFromCache() : null;
            }

            bestFactsRevision = revision;

            // Use the bench's map: it is the semantically correct candidate scope
            // (the pawn must be on the same map as the workbench, not the camera map).
            Map? map = presentation?.Map;
            if (map == null)
            {
                cachedBestValid = false;
                bestOdds = null;
                return null;
            }

            Pawn? best = Dispatcher.SelectFinisher(
                map, presentation!.Recipe, default, relaxed: true);
            if (best == null)
            {
                cachedBestValid = false;
                bestOdds = null;
                return null;
            }

            cachedBestSkill = Dispatcher.SkillOf(best, presentation!.Recipe);
            cachedBestInspired = best.InspirationDef == InspirationDefOf.Inspired_Creativity;
            cachedBestRoleOffset = Dispatcher.RoleOffsetOf(best);
            cachedBestValid = true;
            return EnsureBestOddsFromCache();
        }

        private OddsRows EnsureBestOddsFromCache()
        {
            if (bestOdds == null
                || !bestOdds.Matches(cachedBestSkill, cachedBestInspired, cachedBestRoleOffset))
                bestOdds = OddsRows.Build(cachedBestSkill, cachedBestInspired, cachedBestRoleOffset);
            return bestOdds;
        }

        /// <summary>Revision-gated auto current-best evaluation (auto spec §5).
        /// Steady render passes compare one external-facts revision.</summary>
        private void EnsureAutoBest()
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            int revision = store?.ExternalPawnFactsRevision ?? 0;
            if (autoBestFactsRevision == revision) return;
            autoBestFactsRevision = revision;

            // The auto pool honors the same filters the gate will apply; MinSkill
            // is ignored in auto mode, so pass 0.
            var condition = new ResumeCondition(0, requireInspired, requireSpecialist);
            Pawn? best = Dispatcher.AutoBestForDisplay(presentation!.Recipe, condition);
            if (best == null)
            {
                cachedAutoValid = false;
                cachedAutoBestId = -1;
                _autoBestCurrentLabel = null;
                return;
            }
            int skill = Dispatcher.SkillOf(best, presentation!.Recipe);
            bool inspired = best.InspirationDef == InspirationDefOf.Inspired_Creativity;
            int roleOffset = Dispatcher.RoleOffsetOf(best);
            // Equality: same resolved identity + stats keeps the label instance.
            if (!cachedAutoValid || best.thingIDNumber != cachedAutoBestId
                || skill != cachedAutoSkill || inspired != cachedAutoInspired
                || roleOffset != cachedAutoRoleOffset)
            {
                cachedAutoBestId = best.thingIDNumber;
                cachedAutoSkill = skill;
                cachedAutoInspired = inspired;
                cachedAutoRoleOffset = roleOffset;
                _autoBestCurrentLabel = "QJ_AutoBestCurrent".Translate(best.LabelShort);
            }
            cachedAutoValid = true;
        }

        /// <summary>Revision-gated status refresh (spec §11): pipeline counts and
        /// eligible finishers rebuild only after their exact store domain moves.</summary>
        private void EnsureStatus(QualityJobsStore store)
        {
            int revision = store.BillStatusRevision;
            if (statusRevision == revision) return;
            statusRevision = revision;

            Map? map = presentation?.Map;
            string? product = presentation?.ProductDefName;
            if (map == null || product == null)
            {
                statusValid = false;
                return;
            }
            statusValid = true;

            store.CountEntriesFor(map, product,
                out int waiting, out int finishing, out int shared);
            bool showShared = store.SettingsPresentation.ShareUnfinishedWork;
            if (waiting != statusWaiting || finishing != statusFinishing
                || shared != statusShared || showShared != statusSharedShown)
            {
                statusWaiting = waiting;
                statusFinishing = finishing;
                statusShared = shared;
                statusSharedShown = showShared;
                // Shared pool items lead the line as "Unfinished" (owner request).
                _statusQueueLabel = showShared
                    ? "QJ_StatusQueueShared".Translate(shared, waiting, finishing)
                    : "QJ_StatusQueue".Translate(waiting, finishing);
            }

            // Eligibility mirrors TryDispatch: the auto path replaces MinSkill
            // with the dynamic threshold, so only the filters travel with it.
            ResumeCondition condition = autoBest
                ? new ResumeCondition(0, requireInspired, requireSpecialist)
                : new ResumeCondition(minSkill, requireInspired, requireSpecialist);
            Dispatcher.CollectEligibleFinishers(map, presentation!.Recipe,
                condition, autoBest,
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
                _statusFinishersLabel = n == 0
                    ? "QJ_StatusFinishersNone".Translate()
                    : "QJ_StatusFinishers".Translate(n);
                _statusNamesLabel = n == 0 ? null : QjUi.NamesLine(statusPawnScratch);
            }
            statusEligibleCount = statusPawnScratch.Count;
            statusPawnScratch.Clear();
        }

        private void DrawOddsTable(Listing_Standard listing, OddsRows config, OddsRows? best)
        {
            // _qualityLabels, _oddsColConfigLabel, _oddsColBestLabel are built in
            // LoadFromStore (once per dialog open). Language changes are not observable
            // while the dialog is open — a reopened dialog always constructs fresh
            // instance fields from the current language (AGENTS.md tooltip-session note).
            //
            // Column layout (row width = listing.ColumnWidth):
            //   left  50% — quality name
            //   next  25% — Best percent (dimmed at 0.55 alpha; omitted when best is null)
            //   last  25% — Config percent (full brightness)
            //
            // A2: column order is now [name][Best][Config].
            // Header row: blank quality-name cell, then Best header (dimmed), then Config header.
            // Data rows:  seven rows Legendary (6) down to Awful (0).
            // A2: percent cells and header cells are right-aligned (MiddleRight).
            // GUI.color and Text.Anchor are saved/restored inside the try/finally that
            // also guards Text.Font in the caller; this method also restores them locally.
            Color prevColor = GUI.color;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // Compact rows (owner request): rows ADVANCE by TightRowH but
                // every label DRAWS in a TightRowDrawH rect so small-font
                // descenders are not clipped (see QjUi).
                float rowHeight = QjUi.TightRowH;
                float drawH = QjUi.TightRowDrawH;
                float colWidth = listing.ColumnWidth;
                float col1w = colWidth * 0.50f;
                float col2w = colWidth * 0.25f;
                // col3w implicitly fills the remainder; rect computed from col1w+col2w.

                // Header row.
                {
                    Rect headerRow = listing.GetRect(rowHeight);
                    // Column 1: blank (quality-name column).
                    // Column 2: Best header (dimmed, right-aligned).
                    Rect hCol2 = new Rect(headerRow.x + col1w, headerRow.y, col2w, drawH);
                    if (best != null)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.55f);
                        Text.Anchor = TextAnchor.MiddleRight;
                        Widgets.Label(hCol2, _oddsColBestLabel!);
                        GUI.color = prevColor;
                        Text.Anchor = prevAnchor;
                    }
                    // Column 3: Config header (full brightness, right-aligned).
                    Rect hCol3 = new Rect(headerRow.x + col1w + col2w, headerRow.y,
                        headerRow.width - col1w - col2w, drawH);
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(hCol3, _oddsColConfigLabel!);
                    Text.Anchor = prevAnchor;
                }

                // Data rows: Legendary down to Good, then "Normal or worse"
                // (bottom three qualities collapsed — OddsRows display order).
                for (int r = 0; r < OddsRows.RowCount; r++)
                {
                    Rect dataRow = listing.GetRect(rowHeight);
                    Rect dCol1 = new Rect(dataRow.x, dataRow.y, col1w, drawH);
                    Rect dCol2 = new Rect(dataRow.x + col1w, dataRow.y, col2w, drawH);
                    Rect dCol3 = new Rect(dataRow.x + col1w + col2w, dataRow.y,
                        dataRow.width - col1w - col2w, drawH);
                    // Row name (left-aligned, default anchor).
                    Widgets.Label(dCol1, _oddsRowLabels![r]);
                    // Best column (dimmed, right-aligned).
                    if (best != null)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.55f);
                        Text.Anchor = TextAnchor.MiddleRight;
                        Widgets.Label(dCol2, best.Percents[r]);
                        GUI.color = prevColor;
                        Text.Anchor = prevAnchor;
                    }
                    // Config column (full brightness, right-aligned).
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(dCol3, config.Percents[r]);
                    Text.Anchor = prevAnchor;
                }
            }
            finally
            {
                Text.Anchor = prevAnchor;
                GUI.color = prevColor;
            }
        }
    }
}
