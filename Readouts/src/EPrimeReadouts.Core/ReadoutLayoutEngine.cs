using System;
using System.Collections.Generic;
using System.Globalization;

namespace EPrimeReadouts.Core
{
    public sealed class LayoutInput
    {
        public List<ReadoutGroup> Groups = new List<ReadoutGroup>();
        /// Per-player tier depth; out-of-range values mean "all tiers".
        public Func<ReadoutGroup, int> DepthOf = g => g.TierCount;
        public IReadOnlyDictionary<string, int> Counts;
        public Dictionary<string, ThresholdSpec> Thresholds;
        public string SearchText = "";
        public float Width = 140f;
        public IResourceCatalog Catalog;
        public bool EditorMode;
    }

    /// Builds the panel's complete draw plan from pure inputs. Runs only when
    /// something changed (store version, view state, counts); the game
    /// assembly caches the result and blits it every frame.
    public static class ReadoutLayoutEngine
    {
        public const string ResultsLabelKey = "EPR.Results";
        public const string NoMatchesLabelKey = "EPR.NoMatches";

        /// Private struct for resolved token data in group layout.
        private struct ResolvedSlot
        {
            public string Token;
            public List<string> Members;  // def members (1+ entries)
            public int Sum;
        }

        // Content inset: X is stripe + pad, Y is GroupPadY.
        private static float InsetX => LayoutMetrics.StripeW + LayoutMetrics.GroupPadX;

        // Columns for the Results section (wraps at panel width).
        private static int ResultsColumns(float width) =>
            Math.Max(1, (int)((width - InsetX - LayoutMetrics.MarkerColW) / LayoutMetrics.CellW));

        // Width of a group container for a given slot count (never wraps).
        private static float GroupContainerWidth(int slotCount) =>
            LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
            + LayoutMetrics.MarkerColW + slotCount * LayoutMetrics.CellW
            + LayoutMetrics.GroupPadX;

        public static RenderModel Build(LayoutInput input)
        {
            var model = new RenderModel();
            float y = 0f;
            float maxGroupW = 0f;
            bool searching = !input.EditorMode && SearchMatcher.IsActive(input.SearchText);
            if (searching) y = BuildResults(input, model, y);
            var slots = new List<ResolvedSlot>();
            int groupDisplayIndex = 0;
            foreach (var group in input.Groups)
            {
                if (input.EditorMode)
                {
                    if (y > 0f) y += LayoutMetrics.GroupGap;
                    float containerW = EditorGroupContainerWidth(group, input);
                    if (containerW > maxGroupW) maxGroupW = containerW;
                    y = BuildEditorGroup(group, input, model, y, groupDisplayIndex, containerW);
                    groupDisplayIndex++;
                }
                else
                {
                    slots.Clear();
                    CollectVisible(group, input, slots);
                    if (slots.Count == 0) continue;
                    if (y > 0f) y += LayoutMetrics.GroupGap;
                    float containerW = GroupContainerWidth(slots.Count);
                    if (containerW > maxGroupW) maxGroupW = containerW;
                    y = BuildGroup(group, input, model, slots, y, searching, groupDisplayIndex, containerW);
                    groupDisplayIndex++;
                }
            }
            model.TotalHeight = y;
            model.TotalWidth = maxGroupW > input.Width ? maxGroupW : input.Width;
            return model;
        }

        private static void CollectVisible(ReadoutGroup group, LayoutInput input,
            List<ResolvedSlot> into)
        {
            int depth = Markers.ClampDepth(group.TierCount, input.DepthOf(group));
            for (int t = 0; t < depth; t++)
            {
                foreach (var token in group.Tiers[t])
                {
                    // Resolve members
                    var members = new List<string>();
                    if (SlotToken.IsPool(token))
                    {
                        var cats = input.Catalog.CountedDefsIn(SlotToken.MemberName(token));
                        foreach (var m in cats) members.Add(m);
                    }
                    else
                    {
                        string defName = SlotToken.MemberName(token);
                        if (input.Catalog.Exists(defName)) members.Add(defName);
                    }

                    // Skip tokens with zero members (unknown def / empty category)
                    if (members.Count == 0) continue;

                    // Sum counts
                    int sum = 0;
                    foreach (var m in members)
                    {
                        input.Counts.TryGetValue(m, out int c);
                        sum += c;
                    }

                    // Visibility check
                    string canonical = SlotToken.Canonical(token);
                    bool visible = sum > 0
                        || SlotToken.ShowWhenZero(token)
                        || input.Thresholds.ContainsKey(canonical);
                    if (!visible) continue;

                    into.Add(new ResolvedSlot { Token = token, Members = members, Sum = sum });
                }
            }
        }

        private static float BuildGroup(ReadoutGroup group, LayoutInput input, RenderModel model,
            List<ResolvedSlot> slots, float yTop, bool searching, int groupDisplayIndex,
            float containerW)
        {
            // Single row always: one row of IconRowH + CounterRowH.
            float containerH = 2f * LayoutMetrics.GroupPadY
                + LayoutMetrics.IconRowH + LayoutMetrics.CounterRowH;

            // GroupBack cell spans the computed container width — emitted FIRST
            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.GroupBack,
                GroupIndex = groupDisplayIndex,
                Rect = new RectF(0f, yTop, containerW, containerH),
            });

            // Emit marker triangles (inset by stripe+pad on X, GroupPadY on Y)
            int depth = Markers.ClampDepth(group.TierCount, input.DepthOf(group));
            var states = new TriangleState[TierOps.MaxTiers];
            Markers.Compute(group.TierCount, depth, states);
            float insetX = InsetX;
            float insetY = yTop + LayoutMetrics.GroupPadY;
            for (int i = 0; i < TierOps.MaxTiers; i++)
            {
                if (states[i] == TriangleState.Absent) continue;
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Triangle,
                    Triangle = states[i],
                    Rect = new RectF(
                        insetX + i * (LayoutMetrics.TriW + LayoutMetrics.TriGap),
                        insetY + (LayoutMetrics.IconRowH - LayoutMetrics.TriH) / 2f,
                        LayoutMetrics.TriW, LayoutMetrics.TriH),
                });
            }

            // MarkerHit is the INSET clickable region (matches drawn triangles)
            model.MarkerHits.Add(new MarkerHit
            {
                GroupId = group.Id,
                Rect = new RectF(insetX, insetY, LayoutMetrics.MarkerColW,
                    LayoutMetrics.IconRowH + LayoutMetrics.CounterRowH),
            });

            // Build the icon/counter row, inset (single row, no wrapping)
            BuildGroupGrid(model, slots, input, insetY, highlightMatches: searching);

            return yTop + containerH;
        }

        // All group slots rendered on a single row — no wrapping.
        private static void BuildGroupGrid(RenderModel model, List<ResolvedSlot> slots,
            LayoutInput input, float yInset, bool highlightMatches)
        {
            float insetX = InsetX;
            float y = yInset;
            for (int c = 0; c < slots.Count; c++)
            {
                var slot = slots[c];
                string firstMember = slot.Members[0];
                float x = insetX + LayoutMetrics.MarkerColW + c * LayoutMetrics.CellW;
                var iconRect = new RectF(
                    x + (LayoutMetrics.CellW - LayoutMetrics.IconSize) / 2f, y,
                    LayoutMetrics.IconSize, LayoutMetrics.IconSize);

                // Highlight: any member label matches, or pool category label matches
                if (highlightMatches)
                {
                    bool match = false;
                    foreach (var m in slot.Members)
                        if (SearchMatcher.Matches(input.Catalog.LabelOf(m), input.SearchText))
                        { match = true; break; }
                    if (!match && SlotToken.IsPool(slot.Token))
                        match = SearchMatcher.Matches(
                            input.Catalog.CategoryLabelOf(SlotToken.MemberName(slot.Token)),
                            input.SearchText);
                    if (match)
                        model.Cells.Add(new RenderCell
                        {
                            Kind = CellKind.Highlight,
                            DefName = firstMember,
                            Token = slot.Token,
                            Rect = iconRect,
                        });
                }

                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Icon,
                    DefName = firstMember,
                    Token = slot.Token,
                    Rect = iconRect,
                });

                string canonical = SlotToken.Canonical(slot.Token);
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Counter,
                    DefName = firstMember,
                    Token = slot.Token,
                    Text = slot.Sum.ToString(CultureInfo.InvariantCulture),
                    Band = input.Thresholds.TryGetValue(canonical, out var spec)
                        ? ThresholdBands.For(slot.Sum, spec) : Band.Normal,
                    Rect = new RectF(x, y + LayoutMetrics.IconRowH,
                        LayoutMetrics.CellW, LayoutMetrics.CounterRowH),
                });
            }
        }

        // --- Editor mode helpers ---

        // Computes the container width for an editor group (one tier at a time).
        private static float EditorGroupContainerWidth(ReadoutGroup group, LayoutInput input)
        {
            var tiers = group.Tiers;
            int depth = EditorBand.ClampDepth(tiers, input.DepthOf(group));
            int tierIndex = depth - 1;
            int tokenCount = tierIndex < tiers.Count ? tiers[tierIndex].Count : 0;
            // tokens + 1 empty slot
            float contentW = (tokenCount + 1) * LayoutMetrics.CellW;
            return LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
                   + LayoutMetrics.MarkerColW + contentW + LayoutMetrics.GroupPadX;
        }

        private static float BuildEditorGroup(ReadoutGroup group, LayoutInput input, RenderModel model,
            float yTop, int groupDisplayIndex, float containerW)
        {
            float containerH = 2f * LayoutMetrics.GroupPadY
                + LayoutMetrics.IconRowH + LayoutMetrics.CounterRowH;

            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.GroupBack,
                GroupIndex = groupDisplayIndex,
                Rect = new RectF(0f, yTop, containerW, containerH),
            });

            var tiers = group.Tiers;
            int depth = EditorBand.ClampDepth(tiers, input.DepthOf(group));

            // Markers: count = min(3, max(tiers.Count, depth)), lit = depth
            int markerCount = Math.Min(3, Math.Max(tiers.Count, depth));
            var states = new TriangleState[TierOps.MaxTiers];
            Markers.Compute(markerCount, depth, states);

            float insetX = InsetX;
            float insetY = yTop + LayoutMetrics.GroupPadY;
            for (int i = 0; i < TierOps.MaxTiers; i++)
            {
                if (states[i] == TriangleState.Absent) continue;
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Triangle,
                    Triangle = states[i],
                    Rect = new RectF(
                        insetX + i * (LayoutMetrics.TriW + LayoutMetrics.TriGap),
                        insetY + (LayoutMetrics.IconRowH - LayoutMetrics.TriH) / 2f,
                        LayoutMetrics.TriW, LayoutMetrics.TriH),
                });
            }

            model.MarkerHits.Add(new MarkerHit
            {
                GroupId = group.Id,
                Rect = new RectF(insetX, insetY, LayoutMetrics.MarkerColW,
                    LayoutMetrics.IconRowH + LayoutMetrics.CounterRowH),
            });

            // Render exactly one tier: tier at index depth-1
            int t = depth - 1;
            List<string> tierTokens = t < tiers.Count ? tiers[t] : null;
            int tokenCount = tierTokens != null ? tierTokens.Count : 0;

            float colX = insetX + LayoutMetrics.MarkerColW;

            // Emit existing token cells for the current tier
            for (int s = 0; s < tokenCount; s++)
            {
                string token = tierTokens[s];
                // Resolve members; skip if not in catalog (advance colX regardless)
                List<string> members = null;
                if (SlotToken.IsPool(token))
                {
                    var cats = input.Catalog.CountedDefsIn(SlotToken.MemberName(token));
                    if (cats.Count > 0)
                    {
                        members = new List<string>();
                        foreach (var m in cats) members.Add(m);
                    }
                }
                else
                {
                    string defName = SlotToken.MemberName(token);
                    if (input.Catalog.Exists(defName))
                        members = new List<string> { defName };
                }
                if (members == null || members.Count == 0) { colX += LayoutMetrics.CellW; continue; }

                string firstMember = members[0];
                var iconRect = new RectF(
                    colX + (LayoutMetrics.CellW - LayoutMetrics.IconSize) / 2f, insetY,
                    LayoutMetrics.IconSize, LayoutMetrics.IconSize);

                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Icon,
                    DefName = firstMember,
                    Token = token,
                    Tier = t,
                    Slot = s,
                    Rect = iconRect,
                });

                int sum = 0;
                foreach (var m in members) { input.Counts.TryGetValue(m, out int c); sum += c; }
                string canonical = SlotToken.Canonical(token);
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Counter,
                    DefName = firstMember,
                    Token = token,
                    Tier = t,
                    Slot = s,
                    Text = sum.ToString(CultureInfo.InvariantCulture),
                    Band = input.Thresholds.TryGetValue(canonical, out var spec)
                        ? ThresholdBands.For(sum, spec) : Band.Normal,
                    Rect = new RectF(colX, insetY + LayoutMetrics.IconRowH,
                        LayoutMetrics.CellW, LayoutMetrics.CounterRowH),
                });

                colX += LayoutMetrics.CellW;
            }

            // One trailing EmptySlot (append position = tokenCount)
            var emptyRect = new RectF(
                colX + (LayoutMetrics.CellW - LayoutMetrics.IconSize) / 2f, insetY,
                LayoutMetrics.IconSize, LayoutMetrics.IconSize);
            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.EmptySlot,
                Tier = t,
                Slot = tokenCount,
                Rect = emptyRect,
            });

            return yTop + containerH;
        }

        // Results section: plain counted defNames, per-def counts.
        private static float BuildResults(LayoutInput input, RenderModel model, float y)
        {
            var matches = new List<string>();
            foreach (var pair in input.Counts)
                if (SearchMatcher.Matches(input.Catalog.LabelOf(pair.Key), input.SearchText))
                    matches.Add(pair.Key);
            matches.Sort((a, b) => string.CompareOrdinal(
                input.Catalog.LabelOf(a), input.Catalog.LabelOf(b)));

            float insetX = InsetX;
            float insetY = y + LayoutMetrics.GroupPadY;

            // Pre-compute container height so GroupBack spans the full container
            int columns = ResultsColumns(input.Width);
            int gridRows = matches.Count > 0 ? (matches.Count + columns - 1) / columns : 0;
            float gridH = gridRows * (LayoutMetrics.IconRowH + LayoutMetrics.CounterRowH);
            float containerH = 2f * LayoutMetrics.GroupPadY + LayoutMetrics.LabelRowH + gridH;

            // GroupBack with GroupIndex = -1 emitted BEFORE label cell
            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.GroupBack,
                GroupIndex = -1,
                Rect = new RectF(0f, y, input.Width, containerH),
            });

            // Label cell (inset)
            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.Label,
                Text = matches.Count == 0 ? NoMatchesLabelKey : ResultsLabelKey,
                Rect = new RectF(insetX, insetY, input.Width - insetX, LayoutMetrics.LabelRowH),
            });
            insetY += LayoutMetrics.LabelRowH;

            if (matches.Count > 0)
                BuildResultsGrid(input, model, matches, insetY);

            return y + containerH;
        }

        private static void BuildResultsGrid(LayoutInput input, RenderModel model,
            List<string> defNames, float y)
        {
            float insetX = InsetX;
            int columns = ResultsColumns(input.Width);
            for (int i = 0; i < defNames.Count; i += columns)
            {
                int rowCount = Math.Min(columns, defNames.Count - i);
                for (int c = 0; c < rowCount; c++)
                {
                    string defName = defNames[i + c];
                    float x = insetX + LayoutMetrics.MarkerColW + c * LayoutMetrics.CellW;
                    var iconRect = new RectF(
                        x + (LayoutMetrics.CellW - LayoutMetrics.IconSize) / 2f, y,
                        LayoutMetrics.IconSize, LayoutMetrics.IconSize);
                    model.Cells.Add(new RenderCell
                        { Kind = CellKind.Icon, DefName = defName, Rect = iconRect });
                    input.Counts.TryGetValue(defName, out int count);
                    model.Cells.Add(new RenderCell
                    {
                        Kind = CellKind.Counter,
                        DefName = defName,
                        Text = count.ToString(CultureInfo.InvariantCulture),
                        Band = input.Thresholds.TryGetValue(defName, out var spec)
                            ? ThresholdBands.For(count, spec) : Band.Normal,
                        Rect = new RectF(x, y + LayoutMetrics.IconRowH,
                            LayoutMetrics.CellW, LayoutMetrics.CounterRowH),
                    });
                }
                y += LayoutMetrics.IconRowH + LayoutMetrics.CounterRowH;
            }
        }
    }
}
