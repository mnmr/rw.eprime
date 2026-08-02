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
        private int minSkill;
        private bool requireInspired;
        private bool requireSpecialist;
        private int cap;
        private bool loaded;

        // Odds caches — keyed (minSkill, inspired, roleOffset); rebuilt on mismatch.
        // Owner: dialog (transient). Dependencies: condition fields only.
        // Teardown: dies with the window.
        private OddsRows? thresholdOdds;
        private OddsRows? bestOdds;

        // Best-candidate throttle cache.
        // Owner: dialog (transient). Key: none (single pawn). Value: pawn + odds.
        // Dependencies: best-candidate selection, re-evaluated every BestCandidateInterval ticks.
        // Refresh: tick-throttled at BestCandidateInterval; also reset on LoadFromStore.
        // Teardown: dies with the window.
        private const int BestCandidateInterval = 60;
        private int lastBestTick = -BestCandidateInterval; // force first evaluation
        private int cachedBestSkill;
        private bool cachedBestInspired;
        private int cachedBestRoleOffset;
        private bool cachedBestValid; // false = no eligible pawn found

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
        private string? _stockCapTooltip;
        private string? _finisherSkillTooltip;

        // I4: interpolated slider labels, rebuilt only when the displayed value changes.
        // Owner: dialog instance. Teardown: dies with the window.
        private string? _minSkillLabel;
        private int _minSkillLabelValue = -1;
        private string? _capLabel;
        private int _capLabelValue = -1;

        // I1: stock-cap status line cache — rebuilt only when (count, cap) changes.
        // Drawn only when count >= cap && cap > 0.
        // Owner: dialog instance. Teardown: dies with the window.
        private int _cachedStatusCount = -1;
        private int _cachedStatusCap = -1;
        private string? _statusLabel;

        public Dialog_QualityBillConfig(Bill_ProductionWithUft bill, IntVec3 billGiverPos)
            : base(bill, billGiverPos)
        {
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

            // Hoist odds results before Begin so any early return inside the
            // listing body cannot skip the finally that restores Text.Font.
            OddsRows thresholdRows = EnsureThresholdOdds();
            OddsRows? bestRows = EnsureBestOdds();

            var listing = new Listing_Standard();
            listing.Begin(rect);
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Medium;
                listing.Label(_panelTitleLabel!);
                Text.Font = GameFont.Small;

                // A3: compute options-region height exactly from verified Listing metrics.
                // Verified from Decompiled\Verse\Listing_Standard.cs:
                //   CheckboxLabeled → GetRect(Text.CalcHeight(label, ColumnWidth)) + Gap(2f)
                //     Text.CalcHeight for a single-line label at GameFont.Small = Text.LineHeight = 22f
                //     → consumed = 22f height + 2f gap
                //   SliderLabeled   → GetRect(30f) + Gap(2f)
                //     → consumed = 30f height + 2f gap
                //   verticalSpacing = 2f  (Listing.cs line 8)
                // Content height = sum of GetRect heights + (N-1) gaps (no trailing gap).
                // Rows drawn: manage checkbox, min-skill slider, inspired checkbox,
                //   [specialist checkbox if Ideology], cap slider.
                // No-Ideology: 22 + 30 + 22 + 30 = 104f heights; 3 gaps = 6f → 110f
                // Ideology:    22 + 30 + 22 + 22 + 30 = 126f heights; 4 gaps = 8f → 134f
                const float CheckH = 22f;  // Text.LineHeight for GameFont.Small
                const float SliderH = 30f; // SliderLabeled GetRect height (Listing_Standard.cs line 381)
                const float Gap = 2f;      // verticalSpacing (Listing.cs line 8)
                float optionsContentH = ModsConfig.IdeologyActive
                    ? CheckH + Gap + SliderH + Gap + CheckH + Gap + CheckH + Gap + SliderH
                    : CheckH + Gap + SliderH + Gap + CheckH + Gap + SliderH;
                float sectionBoxH = optionsContentH + SectionPad * 2f;
                // CurHeight is the public property exposing protected curY (Listing.cs line 30).
                float sectionBoxY = listing.CurHeight;

                // Draw the section box (background + border) first, behind the controls.
                // IMPORTANT: listing.Begin(rect) opened a GUI group, so ALL coordinates
                // here are RELATIVE to the panel origin — using rect.x/rect.y again
                // would double-offset and push everything outside the visible panel.
                Widgets.DrawMenuSection(new Rect(0f, sectionBoxY, rect.width, sectionBoxH));

                // Run a child listing inside the padded inner rect (group-relative).
                Rect innerRect = new Rect(
                    SectionPad,
                    sectionBoxY + SectionPad,
                    rect.width - SectionPad * 2f,
                    optionsContentH);
                var innerListing = new Listing_Standard();
                innerListing.Begin(innerRect);
                try
                {
                    // Read current UI state into locals; mutate locals; push changes on
                    // actual difference. OnGUI is multi-pass; every pass must be idempotent.
                    bool newManaged = managed;
                    innerListing.CheckboxLabeled(_manageBillLabel!, ref newManaged);

                    // I4: rebuild interpolated label only when the displayed value changes.
                    if (minSkill != _minSkillLabelValue)
                    {
                        _minSkillLabel = "QJ_FinisherSkill".Translate(minSkill);
                        _minSkillLabelValue = minSkill;
                    }
                    int newMinSkill = (int)innerListing.SliderLabeled(_minSkillLabel!, minSkill, 0f, 20f,
                        tooltip: _finisherSkillTooltip!);

                    bool newInspired = requireInspired;
                    innerListing.CheckboxLabeled(_requireInspiredLabel!, ref newInspired);

                    bool newSpecialist = requireSpecialist;
                    if (ModsConfig.IdeologyActive)
                        innerListing.CheckboxLabeled(_requireSpecialistLabel!, ref newSpecialist);

                    // I4: rebuild interpolated cap label only when the displayed value changes.
                    if (cap != _capLabelValue)
                    {
                        _capLabel = "QJ_StockCapLabel".Translate(cap);
                        _capLabelValue = cap;
                    }
                    int newCap = (int)innerListing.SliderLabeled(_capLabel!, cap, 0f, 50f, tooltip: _stockCapTooltip!);

                    PushChanges(newManaged, newMinSkill, newInspired, newSpecialist, newCap);
                }
                finally
                {
                    innerListing.End();
                }

                // Advance the outer listing past the section box.
                listing.GetRect(sectionBoxH);

                // I1: stock-cap status line — drawn only when product is at/over cap.
                // Use the bench map (same resolution as EnsureBestOdds).
                {
                    Map? map = (bill.billStack?.billGiver as Thing)?.MapHeld;
                    if (map != null && cap > 0)
                    {
                        string? product = ManagedRecipes.ProductDefName(bill.recipe);
                        int count = store.SpawnedUftCount(map, product);
                        if (count >= cap)
                        {
                            if (count != _cachedStatusCount || cap != _cachedStatusCap)
                            {
                                _statusLabel = "QJ_StockCapStatus".Translate(count, cap);
                                _cachedStatusCount = count;
                                _cachedStatusCap = cap;
                            }
                            listing.Label(_statusLabel!);
                        }
                    }
                }

                // Odds section: mini-header via QjUi.MiniHeader (group-relative coords —
                // listing.Begin(rect) opened a GUI group so x=0 is the panel left edge).
                // listing.CurHeight is the next available y within the group.
                // MiniHeader returns y + 30f; we advance the listing past that height.
                float headerY = listing.CurHeight + 4f;
                QjUi.MiniHeader(0f, headerY, rect.width, _oddsHeaderLabel!);
                // Advance past header block: 4f pre-gap + 30f header = 34f total.
                listing.GetRect(34f);

                DrawOddsTable(listing, thresholdRows, bestRows);
            }
            finally
            {
                Text.Anchor = prevAnchor;
                Text.Font = prevFont;
                listing.End();
            }
        }

        private void LoadFromStore(QualityJobsStore store)
        {
            BillConfig config = store.ConfigFor(bill);
            managed = config.Managed;
            minSkill = config.Condition.MinSkill;
            requireInspired = config.Condition.RequireInspired;
            // ConfigFor already coerces specialist via the Ideology gate; mirror here
            // so the local copy is clean and PushChanges never sends true without Ideology.
            requireSpecialist = config.Condition.RequireSpecialist && ModsConfig.IdeologyActive;
            cap = store.CapFor(ManagedRecipes.ProductDefName(bill.recipe));
            // Force re-evaluation of best-candidate on next draw pass.
            lastBestTick = -BestCandidateInterval;
            cachedBestValid = false;

            // Build all instance-scoped label caches once per dialog open.
            // Reopening the dialog always constructs a fresh instance, so these
            // are always built from the language active at open time.
            _qualityLabels = new string[7];
            for (int q = 0; q < 7; q++)
                _qualityLabels[q] = ((QualityCategory)q).GetLabel().CapitalizeFirst();
            _panelTitleLabel = "QJ_QualityPanelTitle".Translate();
            _manageBillLabel = "QJ_ManageBill".Translate();
            _requireInspiredLabel = "QJ_RequireInspired".Translate();
            _requireSpecialistLabel = "QJ_RequireSpecialist".Translate();
            _oddsHeaderLabel = "QJ_OddsHeader".Translate();
            _oddsColConfigLabel = "QJ_OddsColConfig".Translate();
            _oddsColBestLabel = "QJ_OddsColBest".Translate();
            _stockCapTooltip = "QJ_StockCapTooltip".Translate();
            _finisherSkillTooltip = "QJ_FinisherSkillTip".Translate();

            loaded = true;
        }

        private void PushChanges(bool newManaged, int newMinSkill, bool newInspired,
            bool newSpecialist, int newCap)
        {
            // Local compare first (fast path). Commands perform a second no-op
            // compare against store truth — two defense layers per AGENTS.md.
            if (newManaged != managed)
            {
                managed = newManaged;
                Commands.SetBillManaged(BillIds.IdOf(bill), newManaged);
            }

            if (newMinSkill != minSkill)
            {
                minSkill = newMinSkill;
                Commands.SetBillMinSkill(BillIds.IdOf(bill), newMinSkill);
            }

            if (newInspired != requireInspired)
            {
                requireInspired = newInspired;
                Commands.SetBillRequireInspired(BillIds.IdOf(bill), newInspired);
            }

            if (newSpecialist != requireSpecialist)
            {
                requireSpecialist = newSpecialist;
                Commands.SetBillRequireSpecialist(BillIds.IdOf(bill), newSpecialist);
            }

            if (newCap != cap)
            {
                cap = newCap;
                string? product = ManagedRecipes.ProductDefName(bill.recipe);
                if (product != null) Commands.SetProductCap(product, newCap);
            }
        }

        private OddsRows EnsureThresholdOdds()
        {
            // requireSpecialist implies a roleOffset of +1 per spec §11 display logic.
            int roleOffset = requireSpecialist ? 1 : 0;
            if (thresholdOdds == null || !thresholdOdds.Matches(minSkill, requireInspired, roleOffset))
                thresholdOdds = OddsRows.Build(minSkill, requireInspired, roleOffset);
            return thresholdOdds;
        }

        private OddsRows? EnsureBestOdds()
        {
            // Throttle: re-evaluate SelectFinisher at most once per BestCandidateInterval
            // ticks, not per frame. SelectFinisher iterates all free colonists — doing
            // it in the render path every frame violates AGENTS.md §render-path-rule
            // (traversal and aggregation in a steady render pass).
            //
            // Cache contract — Owner: dialog (transient). Key: none.
            // Value: cached pawn stats (skill, inspired, roleOffset) + valid flag.
            // Dependencies: current colonist pool; re-evaluated every
            // BestCandidateInterval game ticks. Refresh: tick-throttled.
            // Equality: Matches() on new stats preserves bestOdds identity.
            // Teardown: dies with the window.
            int now = Find.TickManager.TicksGame;
            if (now - lastBestTick < BestCandidateInterval && lastBestTick >= 0)
            {
                // Return cached result.
                return cachedBestValid ? EnsureBestOddsFromCache() : null;
            }

            lastBestTick = now;

            // Use the bench's map: it is the semantically correct candidate scope
            // (the pawn must be on the same map as the workbench, not the camera map).
            Map? map = (bill.billStack?.billGiver as Thing)?.MapHeld;
            if (map == null)
            {
                cachedBestValid = false;
                bestOdds = null;
                return null;
            }

            Pawn? best = Dispatcher.SelectFinisher(map, bill.recipe, default, relaxed: true);
            if (best == null)
            {
                cachedBestValid = false;
                bestOdds = null;
                return null;
            }

            cachedBestSkill = Dispatcher.SkillOf(best, bill.recipe);
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
                float rowHeight = Text.LineHeight;
                float colWidth = listing.ColumnWidth;
                float col1w = colWidth * 0.50f;
                float col2w = colWidth * 0.25f;
                // col3w implicitly fills the remainder; rect computed from col1w+col2w.

                // Header row.
                {
                    Rect headerRow = listing.GetRect(rowHeight);
                    // Column 1: blank (quality-name column).
                    // Column 2: Best header (dimmed, right-aligned).
                    Rect hCol2 = new Rect(headerRow.x + col1w, headerRow.y, col2w, rowHeight);
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
                        headerRow.width - col1w - col2w, rowHeight);
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(hCol3, _oddsColConfigLabel!);
                    Text.Anchor = prevAnchor;
                }

                // Data rows: Legendary (6) down to Awful (0).
                for (int q = 6; q >= 0; q--)
                {
                    Rect dataRow = listing.GetRect(rowHeight);
                    Rect dCol1 = new Rect(dataRow.x, dataRow.y, col1w, rowHeight);
                    Rect dCol2 = new Rect(dataRow.x + col1w, dataRow.y, col2w, rowHeight);
                    Rect dCol3 = new Rect(dataRow.x + col1w + col2w, dataRow.y,
                        dataRow.width - col1w - col2w, rowHeight);
                    // Quality name (left-aligned, default anchor).
                    Widgets.Label(dCol1, _qualityLabels![q]);
                    // Best column (dimmed, right-aligned).
                    if (best != null)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.55f);
                        Text.Anchor = TextAnchor.MiddleRight;
                        Widgets.Label(dCol2, best.Percents[q]);
                        GUI.color = prevColor;
                        Text.Anchor = prevAnchor;
                    }
                    // Config column (full brightness, right-aligned).
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(dCol3, config.Percents[q]);
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
