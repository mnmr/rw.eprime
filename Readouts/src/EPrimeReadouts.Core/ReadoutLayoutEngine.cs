using System;
using System.Collections.Generic;

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
        /// Pool snapshot built at rebuild time; null treated as empty.
        public PoolSnapshot Pools;
    }

    /// Builds the panel's complete draw plan from pure inputs. Runs only when
    /// something changed (store version, view state, counts); the game
    /// assembly caches the result and blits it every frame.
    public static class ReadoutLayoutEngine
    {
        public const string ResultsLabelKey = "EPR.Results";
        public const string NoMatchesLabelKey = "EPR.NoMatches";
        /// Truncation indicator below the results grid; the cell's Count
        /// carries the number of hidden matches for the "{0}" placeholder.
        public const string MoreResultsLabelKey = "EPR.MoreResults";

        // Results section display cap: at most 3 rows of up to 6 items each.
        public const int MaxResultColumns = 6;
        public const int MaxResultRows = 3;

        /// Private struct for resolved token data in group layout.
        private struct ResolvedSlot
        {
            public string Token;
            public List<string> Members;  // def members (1+ entries)
            public int Sum;
            public string IconDefName;    // icon defName (pool snapshot icon for #tokens; first member otherwise)
            public string HighlightName;  // pool name (for #tokens) or null (use member labels for @tokens)
        }

        // Content inset: X is stripe + pad, Y is GroupPadY.
        private static float InsetX => LayoutMetrics.StripeW + LayoutMetrics.GroupPadX;

        // Columns for the Results section (wraps at panel width, capped).
        private static int ResultsColumns(float width) =>
            Math.Min(MaxResultColumns,
                Math.Max(1, (int)((width - InsetX - LayoutMetrics.MarkerColW) / LayoutMetrics.CellW)));

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

        /// Resolves a token to its members list, icon defName, highlight name, and count sum.
        /// Returns true when the token is resolvable (has ≥1 member), unless editorMode is true
        /// in which case pool-ref tokens with zero members are still included.
        private static bool ResolveToken(string token, LayoutInput input, bool editorMode,
            out List<string> members, out string iconDefName, out string highlightName, out int sum)
        {
            members = null;
            iconDefName = null;
            highlightName = null;
            sum = 0;

            if (SlotToken.IsPoolRef(token))
            {
                // First-class pool reference: #poolId
                int poolId = SlotToken.PoolId(token);
                var pools = input.Pools;
                if (pools == null || !pools.TryGet(poolId, out var poolMembers, out var poolIcon, out var poolName))
                {
                    // Unknown pool: skip in normal mode; in editor mode return an empty slot
                    if (!editorMode) return false;
                    members = new List<string>();
                    iconDefName = null;
                    highlightName = null;
                    sum = 0;
                    return true;
                }
                // Known pool
                if (poolMembers.Count == 0)
                {
                    // Zero members: skip in normal mode; in editor mode include with empty list
                    if (!editorMode) return false;
                    members = new List<string>();
                    iconDefName = poolIcon; // may be null
                    highlightName = poolName;
                    sum = 0;
                    return true;
                }
                members = new List<string>(poolMembers);
                iconDefName = poolIcon;
                highlightName = poolName;
                foreach (var m in members) { input.Counts.TryGetValue(m, out int c); sum += c; }
                return true;
            }
            else if (SlotToken.IsPool(token))
            {
                // Legacy @Category token
                var cats = input.Catalog.CountedDefsIn(SlotToken.MemberName(token));
                if (cats.Count == 0) return false;
                members = new List<string>(cats);
                iconDefName = members[0];
                highlightName = null; // use member labels for legacy pools
                foreach (var m in members) { input.Counts.TryGetValue(m, out int c); sum += c; }
                return true;
            }
            else
            {
                // Plain defName
                string defName = SlotToken.MemberName(token);
                if (!input.Catalog.Exists(defName)) return false;
                members = new List<string> { defName };
                iconDefName = defName;
                highlightName = null;
                input.Counts.TryGetValue(defName, out sum);
                return true;
            }
        }

        private static void CollectVisible(ReadoutGroup group, LayoutInput input,
            List<ResolvedSlot> into)
        {
            int depth = Markers.ClampDepth(group.TierCount, input.DepthOf(group));
            for (int t = 0; t < depth; t++)
            {
                foreach (var token in group.Tiers[t])
                {
                    if (!ResolveToken(token, input, editorMode: false,
                        out var members, out var iconDefName, out var highlightName, out int sum))
                        continue;

                    // Visibility check
                    string canonical = SlotToken.Canonical(token);
                    bool visible = sum > 0
                        || SlotToken.ShowWhenZero(token)
                        || input.Thresholds.ContainsKey(canonical);
                    if (!visible) continue;

                    into.Add(new ResolvedSlot
                    {
                        Token = token,
                        Members = members,
                        Sum = sum,
                        IconDefName = iconDefName,
                        HighlightName = highlightName,
                    });
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
                // Icon defName: snapshot icon for pool refs, first member otherwise.
                // May be null for empty pools in editor mode — cell still gets emitted.
                string iconDefName = slot.IconDefName;
                // For DefName on cells: use iconDefName (may be null for zero-member pools)
                string cellDefName = iconDefName ?? (slot.Members.Count > 0 ? slot.Members[0] : null);
                float x = insetX + LayoutMetrics.MarkerColW + c * LayoutMetrics.CellW;
                var iconRect = new RectF(
                    x + (LayoutMetrics.CellW - LayoutMetrics.IconSize) / 2f, y,
                    LayoutMetrics.IconSize, LayoutMetrics.IconSize);

                // Highlight: pool name match (for #tokens) or any member label match
                // or legacy @category label match
                if (highlightMatches)
                {
                    bool match = false;
                    if (slot.HighlightName != null)
                    {
                        // #poolId: match pool name
                        match = SearchMatcher.Matches(slot.HighlightName, input.SearchText);
                    }
                    else
                    {
                        // Plain def or @category: match member labels
                        foreach (var m in slot.Members)
                            if (SearchMatcher.Matches(input.Catalog.LabelOf(m), input.SearchText))
                            { match = true; break; }
                        if (!match && SlotToken.IsPool(slot.Token))
                            match = SearchMatcher.Matches(
                                input.Catalog.CategoryLabelOf(SlotToken.MemberName(slot.Token)),
                                input.SearchText);
                    }
                    if (match)
                        model.Cells.Add(new RenderCell
                        {
                            Kind = CellKind.Highlight,
                            DefName = cellDefName,
                            Token = slot.Token,
                            Rect = iconRect,
                        });
                }

                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Icon,
                    DefName = cellDefName,
                    Token = slot.Token,
                    Count = slot.Sum,
                    Rect = iconRect,
                });

                string canonical = SlotToken.Canonical(slot.Token);
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Counter,
                    DefName = cellDefName,
                    Token = slot.Token,
                    Count = slot.Sum,
                    Text = CountFormat.Compact(slot.Sum),
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
            // tokens + 1 empty slot (omitted when tier is at cap)
            bool atCap = tokenCount >= TierOps.MaxSlotsPerTier;
            float contentW = (tokenCount + (atCap ? 0 : 1)) * LayoutMetrics.CellW;
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
                // Resolve token in editor mode — pool refs with zero/no members still emit cells
                bool resolved = ResolveToken(token, input, editorMode: true,
                    out var members, out var iconDefName, out _, out int sum);
                if (!resolved) { colX += LayoutMetrics.CellW; continue; }

                // cellDefName may be null for zero-member pools in editor (icon cell still occupies column)
                string cellDefName = iconDefName ?? (members.Count > 0 ? members[0] : null);

                var iconRect = new RectF(
                    colX + (LayoutMetrics.CellW - LayoutMetrics.IconSize) / 2f, insetY,
                    LayoutMetrics.IconSize, LayoutMetrics.IconSize);

                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Icon,
                    DefName = cellDefName,
                    Token = token,
                    Tier = t,
                    Slot = s,
                    Count = sum,
                    Rect = iconRect,
                });

                string canonical = SlotToken.Canonical(token);
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Counter,
                    DefName = cellDefName,
                    Token = token,
                    Tier = t,
                    Slot = s,
                    Count = sum,
                    Text = CountFormat.Compact(sum),
                    Band = input.Thresholds.TryGetValue(canonical, out var spec)
                        ? ThresholdBands.For(sum, spec) : Band.Normal,
                    Rect = new RectF(colX, insetY + LayoutMetrics.IconRowH,
                        LayoutMetrics.CellW, LayoutMetrics.CounterRowH),
                });

                colX += LayoutMetrics.CellW;
            }

            // One trailing EmptySlot (append position = tokenCount) — omitted when tier is at cap
            if (tokenCount < TierOps.MaxSlotsPerTier)
            {
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
            }

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
            int shown = Math.Min(matches.Count, columns * MaxResultRows);
            int hidden = matches.Count - shown;
            if (hidden > 0)
                matches.RemoveRange(shown, hidden);
            int gridRows = matches.Count > 0 ? (matches.Count + columns - 1) / columns : 0;
            float gridH = gridRows * (LayoutMetrics.IconRowH + LayoutMetrics.CounterRowH);
            float containerH = 2f * LayoutMetrics.GroupPadY + LayoutMetrics.LabelRowH + gridH
                + (hidden > 0 ? LayoutMetrics.LabelRowH : 0f);

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

            if (hidden > 0)
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Label,
                    Text = MoreResultsLabelKey,
                    Count = hidden,
                    Rect = new RectF(insetX, insetY + gridH,
                        input.Width - insetX, LayoutMetrics.LabelRowH),
                });

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
                    input.Counts.TryGetValue(defName, out int count);
                    model.Cells.Add(new RenderCell
                        { Kind = CellKind.Icon, DefName = defName, Count = count, Rect = iconRect });
                    model.Cells.Add(new RenderCell
                    {
                        Kind = CellKind.Counter,
                        DefName = defName,
                        Count = count,
                        Text = CountFormat.Compact(count),
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
