using System.Collections.Generic;
using QualityJobs.Core;
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
    /// Implicit creation semantics (Fix 3): a plan exists IFF at least one option
    /// is non-neutral. Editing any control fires its setter, which implicitly
    /// creates the plan when needed. Controls always show (no manage checkbox).
    /// A [Clear] button appears only when a plan exists and removes it entirely.
    ///
    /// Fix 2: The "Retried until" caption is removed entirely. The target-quality
    /// row now shows: label on the left portion, button flush to the RIGHT edge
    /// of the row. The whole-row tooltip (QJ_RetriedUntilTip) is kept.
    ///
    /// Fix 3: FloatMenu for quality picker uses vanishIfMouseDistant = false
    /// (verified field at Decompiled\Verse\FloatMenu.cs line 14) to prevent the
    /// menu from instantly self-closing when spawned near the screen edge.
    ///
    /// Fix 5: Operates on a List<Thing> captured at dialog open, so editing one
    /// field pushes the synced command for every eligible selected thing.
    ///
    /// SetInitialSizeAndPosition verified against Decompiled\Verse\Window.cs line 249:
    ///   Rect rect3 = rect.ContractedBy(Margin);   // Margin = 18f
    ///   DoWindowContents(rect3.AtZero());
    /// So inRect.height = windowRect.height - 2*Margin. InitialSize.y is computed
    /// from content height + 2*Margin so no pixel is wasted.
    ///
    /// Layout (bottom-anchored — the window rises from the gizmo button):
    ///
    ///   inRect
    ///   ┌─────────────────────────────────────────────────────────┐
    ///   │  Title "Quality job"  (GameFont.Medium)                  │
    ///   │  ───────────────────────────────────────────────────     │
    ///   │  bodyRect (inRect minus title+gap)                       │
    ///   │  ┌──────────────────────┐  ┌──────────────────────────┐ │
    ///   │  │ LEFT panel           │  │ RIGHT panel (no frame)   │ │
    ///   │  │ [DrawMenuSection]    │  │ ← MiniHeader (30f)       │ │
    ///   │  │                      │  │   Legendary   xx.x%      │ │
    ///   │  │  [Clear]   ← TOP     │  │   ...  7 rows × 22f      │ │
    ///   │  │  ↕ flexible space    │  │   Awful       xx.x%      │ │
    ///   │  │  ── options block ── │  │                          │ │
    ///   │  │  Require inspired    │  │                          │ │
    ///   │  │  [Require specialist]│  │                          │ │
    ///   │  │  Finisher skill: 0   │  │                          │ │
    ///   │  │  Target quality [btn]│  │                          │ │
    ///   │  └──────────────────────┘  └──────────────────────────┘ │
    ///   └─────────────────────────────────────────────────────────┘
    ///
    /// Both columns have their BOTTOM EDGE at inRect.yMax, so content
    /// hugs the bottom (the window rises from the gizmo).
    public class Dialog_ConstructionPlanConfig : Window
    {
        // Primary thing (first in list) — used for plan lookup and display values.
        private readonly Thing _primaryThing;
        // All eligible selected things — commands are pushed to each of them.
        private readonly List<Thing> _things;
        private readonly Rect _anchor;

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
        private const float MiniHeaderH = 30f; // QjUi.MiniHeader consumed height
        private const float WinMargin   = 18f; // Window.Margin

        // Right column content height:
        //   MiniHeader (30f) + 7 quality rows × 22f = 184f.
        private const float RightContentH = MiniHeaderH + 7f * SmallLineH; // 30 + 154 = 184

        // Left options block heights (no trailing gap on the last element):
        //   Without Ideology: inspired(22) + gap(2) + minSkill(30) + gap(2) + qualityRow(22) = 78f
        //   With Ideology:    inspired(22) + gap(2) + specialist(22) + gap(2)
        //                     + minSkill(30) + gap(2) + qualityRow(22) = 102f
        private const float OptionsBlockBaseH     = SmallLineH + GapH + SliderH + GapH + SmallLineH; // 78f
        private const float OptionsBlockIdeologyH = SmallLineH + GapH + SmallLineH + GapH            // 102f
                                                  + SliderH + GapH + SmallLineH;

        // [Clear] button row reserved unconditionally at the top of the left panel inner area:
        //   row height = SmallLineH; followed by gap when drawing (so the button is drawn at top).
        private const float ClearRowH = SmallLineH;

        // Left panel frame padding (same as DrawMenuSection inner padding used in bill dialog).
        private const float PanelPad = 6f;

        // Fix 2: button width for the quality picker = 40% of the row width.
        // The button is right-aligned; the label occupies the remaining left portion.
        private const float QualityBtnWidthFraction = 0.40f;

        // Per-frame local edit copies; pushed via Commands only on actual change.
        private int minSkill;
        private bool requireInspired;
        private bool requireSpecialist;
        private int minQuality;
        private bool labelsLoaded; // true after first draw

        // Constant translated strings cached as instance fields built on first draw.
        // Reopening the dialog always constructs a fresh instance (AGENTS.md tooltip-session note).
        // Owner: dialog instance. Teardown: dies with the window.
        private string? title;
        private string? requireInspiredLabel;
        private string? requireSpecialistLabel;
        private string? oddsHeaderLabel;
        private string? clearLabel;
        private string? noRetriesLabel;
        private string? retriedUntilTip;
        private string? targetQualityLabel;
        private string? anyQualityLabel;
        private string? finisherSkillTooltip;

        // I4: interpolated slider label, rebuilt only when the displayed value changes.
        // Owner: dialog instance. Teardown: dies with the window.
        private string? minSkillLabel;
        private int minSkillLabelValue = -1;

        // Quality name cache: built once per dialog open. 7 entries (Awful..Legendary).
        // Owner: dialog instance. Teardown: dies with the window.
        private string[]? qualityLabels;

        // Odds rows — keyed (minSkill, inspired, roleOffset); rebuilt on mismatch.
        // Owner: dialog (transient). Dependencies: condition fields only.
        // Teardown: dies with the window.
        private OddsRows? odds;

        // InitialSize.y computed from content + 2×Margin so the window fits exactly.
        // Text.LineHeightOf is a static array lookup initialized at startup — safe to
        // call from a property getter (before any rendering has started).
        // Body height = header + body = (medLineH + 4f) + RightContentH.
        // Full window height = body + 2×Margin.
        public override Vector2 InitialSize
        {
            get
            {
                float medLineH = Text.LineHeightOf(GameFont.Medium);
                float headerH  = medLineH + 4f;
                float bodyH    = RightContentH;
                return new Vector2(520f, headerH + bodyH + 2f * WinMargin);
            }
        }

        /// Constructor takes the list of target Things and the anchor Rect (the
        /// gizmo button rect from GizmoOnGUI). The first thing in the list is the
        /// primary (values displayed come from its plan). When anchor == Rect.zero
        /// the window falls back to centered positioning.
        public Dialog_ConstructionPlanConfig(List<Thing> things, Rect anchor)
        {
            _things = things;
            _primaryThing = things[0];
            _anchor = anchor;
            doCloseX = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            draggable = false;
        }

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
            ConstructionPlan? plan = store?.FindPlanById(_primaryThing.thingIDNumber);

            // Initialise label caches once per dialog open (on first draw).
            if (!labelsLoaded) LoadLabels(plan);

            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // Header: title in medium font.
                Text.Font = GameFont.Medium;
                float medLineH = Text.LineHeight;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, medLineH), title!);
                Text.Font = GameFont.Small;

                float headerH = medLineH + 4f;
                Rect bodyRect = new Rect(inRect.x, inRect.y + headerH, inRect.width, inRect.height - headerH);

                // Split into LEFT and RIGHT halves (4f gap between them).
                float halfW = (bodyRect.width - 4f) / 2f;
                Rect leftRect  = new Rect(bodyRect.x,              bodyRect.y, halfW, bodyRect.height);
                Rect rightRect = new Rect(bodyRect.x + halfW + 4f, bodyRect.y, halfW, bodyRect.height);

                DrawRightPanel(rightRect, plan);
                DrawLeftPanel(leftRect, plan);
            }
            finally
            {
                Text.Anchor = prevAnchor;
                Text.Font = prevFont;
            }
        }

        /// Right panel: odds table, bottom-anchored, no frame.
        /// Layout: MiniHeader (30f) then 7 rows of 22f, bottom edge at rect.yMax.
        private void DrawRightPanel(Rect rect, ConstructionPlan? plan)
        {
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            try
            {
                // Bottom-anchor: top of content block = rect.yMax - RightContentH.
                float contentTop = rect.yMax - RightContentH;
                float rowH = SmallLineH;

                // MiniHeader: group-relative x=0 means the left edge of rect.
                // We draw with GUI absolute coords here (no BeginGroup), so x = rect.x.
                float afterHeader = QjUi.MiniHeader(rect.x, contentTop, rect.width, oddsHeaderLabel!);

                // Build odds for current displayed values.
                int roleOffset = requireSpecialist ? 1 : 0;
                if (odds == null || !odds.Matches(minSkill, requireInspired, roleOffset))
                    odds = OddsRows.Build(minSkill, requireInspired, roleOffset);

                // 7 quality rows, Legendary (6) down to Awful (0).
                float rowY = afterHeader;
                for (int q = 6; q >= 0; q--)
                {
                    Rect row = new Rect(rect.x, rowY, rect.width, rowH);
                    Widgets.Label(row.LeftHalf(), qualityLabels![q]);
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(row.RightHalf(), odds.Percents[q]);
                    Text.Anchor = prevAnchor;
                    rowY += rowH;
                }
            }
            finally
            {
                Text.Anchor = prevAnchor;
                GUI.color = prevColor;
            }
        }

        /// Left panel: framed (DrawMenuSection), bottom edge at rect.yMax.
        /// Height = RightContentH (so top aligns with the odds header).
        /// Inside: [Clear] row at the TOP (reserved unconditionally), flexible
        /// space, then options block anchored at the BOTTOM.
        private void DrawLeftPanel(Rect rect, ConstructionPlan? plan)
        {
            Color prevColor = GUI.color;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // Frame: same height as the right column content, bottom-anchored.
                Rect panelRect = new Rect(rect.x, rect.yMax - RightContentH, rect.width, RightContentH);
                Widgets.DrawMenuSection(panelRect);

                Rect inner = panelRect.ContractedBy(PanelPad);

                // Options block height (no trailing gap on last element).
                float optionsH = ModsConfig.IdeologyActive ? OptionsBlockIdeologyH : OptionsBlockBaseH;

                // [Clear] button row: reserved unconditionally at the TOP of inner rect.
                // Height = SmallLineH. Only drawn when ANY selected thing has a plan.
                Rect clearRowRect = new Rect(inner.x, inner.y, inner.width, ClearRowH);
                if (AnyHasPlan())
                {
                    if (Widgets.ButtonText(clearRowRect, clearLabel!))
                    {
                        // Fix 5: Clear for every selected thing that has a plan.
                        foreach (Thing t in _things)
                        {
                            QualityJobsStore? s = QualityJobsStore.Active;
                            if (s?.FindPlanById(t.thingIDNumber) != null)
                                Commands.RemovePlan(t.thingIDNumber);
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
            foreach (Thing t in _things)
                if (store.FindPlanById(t.thingIDNumber) != null) return true;
            return false;
        }

        /// Draws the options controls (inspired checkbox, [specialist], skill slider,
        /// target-quality row) laid out top-to-bottom starting at (x, startY).
        /// Uses manual rects (no Listing) so we can anchor from a computed bottom position.
        /// No trailing gap after the last element.
        ///
        /// Fix 2: The "Retried until" caption is removed. The target-quality button
        /// is right-aligned (occupies the right 40% of the row); the label is left.
        ///
        /// Rect-overlap audit:
        ///   The slider occupies [y .. y+SliderH] = [y .. y+30f].
        ///   The target-quality row starts at y+SliderH+GapH = y+32f, which is
        ///   at least 30f (SliderH) below the slider's top. No overlap.
        private void DrawOptionsBlock(float x, float startY, float width, ConstructionPlan? plan)
        {
            Color prevColor = GUI.color;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // Read current values from plan; sync local copies when plan changes identity.
                int curMinSkill    = plan?.minSkill       ?? 0;
                bool curInspired   = plan?.requireInspired  ?? false;
                bool curSpecialist = plan?.requireSpecialist ?? false;
                int curMinQuality  = plan?.minQuality       ?? 0;

                if (minSkill       != curMinSkill)    minSkill       = curMinSkill;
                if (requireInspired  != curInspired)  requireInspired  = curInspired;
                if (requireSpecialist != curSpecialist) requireSpecialist = curSpecialist;
                if (minQuality     != curMinQuality)  minQuality     = curMinQuality;

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

                // (3) Finisher-skill slider.
                // Slider occupies: y .. y+SliderH (30f). No overlap with row below
                // because row (4) starts at y+SliderH+GapH = y+32f.
                if (minSkill != minSkillLabelValue)
                {
                    minSkillLabel = "QJ_FinisherSkill".Translate(minSkill);
                    minSkillLabelValue = minSkill;
                }
                Rect sliderRowRect = new Rect(x, y, width, SliderH);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(sliderRowRect.LeftHalf(), minSkillLabel!);
                TooltipHandler.TipRegion(sliderRowRect.LeftHalf(), finisherSkillTooltip!);
                Text.Anchor = TextAnchor.UpperLeft;
                int newMinSkill = (int)Widgets.HorizontalSlider(
                    sliderRowRect.RightHalf(), minSkill, 0f, 20f, middleAlignment: true);
                y += SliderH + GapH;

                // (4) Target-quality row (Fix 2): label on the left, button RIGHT-aligned.
                // The "Retried until" caption is removed entirely.
                // Row starts at y (= startY + SliderH + GapH + optionalIdeologyRows).
                // No overlap with slider (which ended at y - GapH = y - 2f above this).
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
                    var menu = new FloatMenu(options) { vanishIfMouseDistant = false };
                    Find.WindowStack.Add(menu);
                }
                TooltipHandler.TipRegion(qualityRowRect, retriedUntilTip!);
                // y not advanced after last row (no trailing gap).

                Push(newMinSkill, newInspired, newSpecialist);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Anchor = prevAnchor;
            }
        }

        private void LoadLabels(ConstructionPlan? plan)
        {
            labelsLoaded = true;

            // Sync local edit copies from plan (if exists).
            if (plan != null)
            {
                minSkill       = plan.minSkill;
                requireInspired  = plan.requireInspired;
                requireSpecialist = plan.requireSpecialist;
                minQuality     = plan.minQuality;
            }
            // else: local copies stay at neutral defaults (0/false/false/0).

            title                = "QJ_ConstructionPanelTitle".Translate();
            requireInspiredLabel = "QJ_RequireInspired".Translate();
            requireSpecialistLabel = "QJ_RequireSpecialist".Translate();
            oddsHeaderLabel      = "QJ_OddsHeader".Translate();
            clearLabel           = "QJ_Clear".Translate();
            noRetriesLabel       = "QJ_NoRetries".Translate();
            // Fix 2: QJ_RetriedUntil label is removed; QJ_RetriedUntilTip stays.
            retriedUntilTip      = "QJ_RetriedUntilTip".Translate();
            targetQualityLabel   = "QJ_MinQualityLabel".Translate();
            anyQualityLabel      = "QJ_AnyQuality".Translate();
            finisherSkillTooltip = "QJ_FinisherSkillTip".Translate();

            qualityLabels = new string[7];
            for (int q = 0; q <= 6; q++)
                qualityLabels[q] = ((QualityCategory)q).GetLabel().CapitalizeFirst();
        }

        /// Pushes minQuality to all selected things (Fix 5).
        private void PushMinQuality(int value)
        {
            minQuality = value;
            foreach (Thing t in _things)
                Commands.SetPlanMinQuality(t.thingIDNumber, value);
        }

        /// Pushes per-field changes to all selected things (Fix 5).
        private void Push(int newMinSkill, bool newInspired, bool newSpecialist)
        {
            bool skillChanged     = newMinSkill   != minSkill;
            bool inspiredChanged  = newInspired   != requireInspired;
            bool specChanged      = newSpecialist != requireSpecialist;

            if (skillChanged)    minSkill          = newMinSkill;
            if (inspiredChanged) requireInspired   = newInspired;
            if (specChanged)     requireSpecialist = newSpecialist;

            if (!skillChanged && !inspiredChanged && !specChanged) return;

            foreach (Thing t in _things)
            {
                int thingId = t.thingIDNumber;
                if (skillChanged)    Commands.SetPlanMinSkill(thingId, newMinSkill);
                if (inspiredChanged) Commands.SetPlanRequireInspired(thingId, newInspired);
                if (specChanged)     Commands.SetPlanRequireSpecialist(thingId, newSpecialist);
            }
        }
    }
}
