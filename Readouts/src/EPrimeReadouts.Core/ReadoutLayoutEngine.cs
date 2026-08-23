using System;
using System.Collections.Generic;
using RimShared.Common;

namespace EPrimeReadouts.Core
{
    public sealed class LayoutInput
    {
        public List<ReadoutGroup> Groups = new List<ReadoutGroup>();
        /// Per-player tier depth; out-of-range values mean "all tiers".
        public Func<ReadoutGroup, int> DepthOf = g => g.TierCount;
        /// The user-configured depth when DepthOf reflects a transient hover
        /// expansion; tiers visible beyond it render HoverLit triangles. Null
        /// means DepthOf IS the configured depth (no hover distinction).
        public Func<ReadoutGroup, int>? ConfiguredDepthOf;
        public IReadOnlyDictionary<string, int> Counts = null!; // Required input; set by every builder.
        public Dictionary<string, ThresholdSpec> Thresholds = null!; // Required input; set by every builder.
        /// Shared per-token count-basis overrides keyed by canonical token;
        /// null or a missing entry falls back to the global options below.
        /// Search results always use the globals — the search section's own
        /// filter toggles are those options.
        public IReadOnlyDictionary<string, CountRule>? CountRules;
        public string SearchText = "";
        /// Per-def search breakdown; null falls back to Counts with every
        /// stack treated as stored and unforbidden.
        public IReadOnlyDictionary<string, SearchCount>? SearchCounts;
        /// Per-player search-result filters (see BuildResults).
        public bool SearchHideZero;
        public bool SearchStorageOnly;
        public bool SearchHideForbidden;
        /// Per-def material already owed to planned work; null treated as empty.
        public IReadOnlyDictionary<string, PlannedWorkDebt>? Debts;
        /// Let a counter whose debt exceeds its stock show the overrun as a
        /// negative number instead of capping at zero.
        public bool AllowNegativeCounts;
        public float Width = 140f;
        public IResourceCatalog Catalog = null!; // Required input; set by every builder.
        public bool EditorMode;
        /// Font-resolved cell geometry; default reproduces the tiny-font
        /// baseline. See CellMetrics.
        public CellMetrics Metrics;
        /// Pool snapshot built at rebuild time; null treated as empty.
        public PoolSnapshot? Pools;
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
            public IReadOnlyList<string> Members;  // def members (1+ entries)
            public int Sum;
            public string? IconDefName;   // icon defName (pool snapshot icon for #tokens; first member otherwise)
            public string? HighlightName; // pool name (for #tokens) or null (use member labels for @tokens)
        }

        // Content inset: X is stripe + pad, Y is GroupPadY.
        private static float InsetX => LayoutMetrics.StripeW + LayoutMetrics.GroupPadX;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<
            string, IReadOnlyList<string>> singleMemberLists =
            new System.Collections.Concurrent.ConcurrentDictionary<
                string, IReadOnlyList<string>>(StringComparer.Ordinal);
        private static readonly Func<string, IReadOnlyList<string>> buildSingleMember =
            member => Array.AsReadOnly(new[] { member });

        private static IReadOnlyList<string> SingleMember(string member)
            => singleMemberLists.GetOrAdd(member, buildSingleMember);

        // Columns for the Results section (wraps at panel width, capped).
        private static int ResultsColumns(LayoutInput input) =>
            Math.Min(MaxResultColumns,
                Math.Max(1, (int)((input.Width - InsetX - LayoutMetrics.MarkerColW)
                    / input.Metrics.CellW)));

        // Width of a group container for a given slot count (never wraps).
        private static float GroupContainerWidth(int slotCount, CellMetrics metrics) =>
            LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
            + LayoutMetrics.MarkerColW + slotCount * metrics.CellW
            + LayoutMetrics.GroupPadX;

        // Markers keep the shipped 7x9 size and occupy stable tier positions
        // within a centered three-marker stack. The full icon+counter band is
        // the vertical constraint, not only its icon row.
        private static RectF MarkerRect(
            float insetX, float insetY, float rowPairH, int tier) =>
            new RectF(
                insetX + (LayoutMetrics.MarkerColW - LayoutMetrics.TriW) / 2f,
                insetY + (rowPairH - LayoutMetrics.MarkerStackH) / 2f
                    + tier * (LayoutMetrics.TriH + LayoutMetrics.TriGap),
                LayoutMetrics.TriW, LayoutMetrics.TriH);

        // Keep the slim visual column easy to click by treating the stripe,
        // leading pad, and marker column as one full-height interaction rail.
        private static RectF MarkerHitRect(float insetY, float rowPairH) =>
            new RectF(0f, insetY, InsetX + LayoutMetrics.MarkerColW, rowPairH);

        public static RenderModel Build(LayoutInput input)
        {
            var model = new RenderModel();
            float y = 0f;
            float maxGroupW = 0f;
            bool searching = !input.EditorMode && SearchMatcher.IsActive(input.SearchText);
            if (searching)
            {
                int cellStart = model.Cells.Count;
                int slotStart = model.SlotHits.Count;
                int markerStart = model.MarkerHits.Count;
                y = BuildResults(input, model, y);
                RecordBand(model, cellStart, slotStart, markerStart);
            }
            var slots = new List<ResolvedSlot>();
            // Stripe colors key on the group's position among the enabled
            // groups (the input list), NOT on its render position: a group
            // that renders nothing must not shift the colors of the groups
            // below it, and collapsed/expanded states must agree.
            for (int groupDisplayIndex = 0; groupDisplayIndex < input.Groups.Count;
                groupDisplayIndex++)
            {
                var group = input.Groups[groupDisplayIndex];
                if (input.EditorMode)
                {
                    if (y > 0f) y += LayoutMetrics.GroupGap;
                    float containerW = EditorGroupContainerWidth(group, input);
                    if (containerW > maxGroupW) maxGroupW = containerW;
                    int cellStart = model.Cells.Count;
                    int slotStart = model.SlotHits.Count;
                    int markerStart = model.MarkerHits.Count;
                    y = BuildEditorGroup(group, input, model, y, groupDisplayIndex, containerW);
                    RecordBand(model, cellStart, slotStart, markerStart);
                }
                else if (input.DepthOf(group) == 0)
                {
                    // Collapsed render mode: a thin identification band for
                    // every enabled group — including groups the expanded
                    // layout would omit for having no visible slots.
                    if (y > 0f) y += LayoutMetrics.GroupGap;
                    int cellStart = model.Cells.Count;
                    int slotStart = model.SlotHits.Count;
                    int markerStart = model.MarkerHits.Count;
                    y = BuildCollapsedGroup(group, input, model, y, groupDisplayIndex);
                    RecordBand(model, cellStart, slotStart, markerStart);
                }
                else
                {
                    slots.Clear();
                    CollectVisible(group, input, slots);
                    if (slots.Count == 0) continue;
                    if (y > 0f) y += LayoutMetrics.GroupGap;
                    float containerW = GroupContainerWidth(slots.Count, input.Metrics);
                    if (containerW > maxGroupW) maxGroupW = containerW;
                    int cellStart = model.Cells.Count;
                    int slotStart = model.SlotHits.Count;
                    int markerStart = model.MarkerHits.Count;
                    y = BuildGroup(group, input, model, slots, y, searching, groupDisplayIndex, containerW);
                    RecordBand(model, cellStart, slotStart, markerStart);
                }
            }
            model.TotalHeight = y;
            model.TotalWidth = maxGroupW > input.Width ? maxGroupW : input.Width;
            return model;
        }

        /// Updates count-derived cell payloads without rebuilding geometry.
        /// Returns false when the new counts change which slots/groups are
        /// present, or when active search makes result geometry count-dependent.
        public static bool TryRefreshCounts(LayoutInput input, RenderModel model)
        {
            if (input.EditorMode || SearchMatcher.IsActive(input.SearchText))
                return false;
            if (!HasSameVisibleSlots(input, model)) return false;

            for (int bandIndex = 0; bandIndex < model.Bands.Count; bandIndex++)
            {
                RenderBand band = model.Bands[bandIndex];
                int slotOffset = 0;
                int sum = 0;
                int cellEnd = band.CellStart + band.CellCount;
                for (int cellIndex = band.CellStart;
                     cellIndex < cellEnd;
                     cellIndex++)
                {
                    RenderCell cell = model.Cells[cellIndex];
                    if (cell.Kind == CellKind.Icon)
                    {
                        SlotHit hit = model.SlotHits[band.SlotStart + slotOffset];
                        sum = SumMembers(input, hit.Token, hit.Members);
                        cell.Count = sum;
                        model.Cells[cellIndex] = cell;
                    }
                    else if (cell.Kind == CellKind.Counter)
                    {
                        cell.Count = sum;
                        cell.Text = CountFormat.Compact(sum);
                        cell.Band = CounterBand(
                            input, SlotToken.Canonical(cell.Token!), sum);
                        model.Cells[cellIndex] = cell;
                        slotOffset++;
                    }
                }
            }
            return true;
        }

        private static bool HasSameVisibleSlots(
            LayoutInput input, RenderModel model)
        {
            int bandIndex = 0;
            for (int groupIndex = 0; groupIndex < input.Groups.Count; groupIndex++)
            {
                ReadoutGroup group = input.Groups[groupIndex];
                if (input.DepthOf(group) == 0)
                {
                    if (bandIndex >= model.Bands.Count
                        || model.Bands[bandIndex].GroupId != group.Id
                        || model.Bands[bandIndex].SlotCount != 0)
                        return false;
                    bandIndex++;
                    continue;
                }

                int visibleCount = 0;
                int depth = Markers.ClampDepth(
                    group.TierCount, input.DepthOf(group));
                for (int tier = 0; tier < depth; tier++)
                {
                    List<string> tokens = group.Tiers[tier];
                    for (int slot = 0; slot < tokens.Count; slot++)
                    {
                        string token = tokens[slot];
                        if (!ResolveToken(token, input, editorMode: false,
                                out _, out _, out _, out int sum))
                            continue;
                        if (!IsVisible(token, input, sum)) continue;

                        if (bandIndex >= model.Bands.Count)
                            return false;
                        RenderBand band = model.Bands[bandIndex];
                        if (band.GroupId != group.Id
                            || visibleCount >= band.SlotCount
                            || !string.Equals(
                                model.SlotHits[band.SlotStart + visibleCount].Token,
                                token, StringComparison.Ordinal))
                            return false;
                        visibleCount++;
                    }
                }

                if (visibleCount == 0)
                {
                    if (bandIndex < model.Bands.Count
                        && model.Bands[bandIndex].GroupId == group.Id)
                        return false;
                    continue;
                }
                if (visibleCount != model.Bands[bandIndex].SlotCount)
                    return false;
                bandIndex++;
            }
            return bandIndex == model.Bands.Count;
        }

        private static int SumMembers(
            LayoutInput input, string token, IReadOnlyList<string> members)
        {
            ResolveBasis(input, SlotToken.Canonical(token),
                out bool storageOnly, out bool hideForbidden);
            int sum = 0;
            for (int i = 0; i < members.Count; i++)
                sum += EffectiveCount(input, members[i],
                    storageOnly, hideForbidden);
            return sum;
        }

        private static void RecordBand(
            RenderModel model, int cellStart, int slotStart, int markerStart)
        {
            if (cellStart >= model.Cells.Count) return;
            RenderCell backing = model.Cells[cellStart];
            model.Bands.Add(new RenderBand
            {
                GroupId = backing.GroupId,
                Rect = backing.Rect,
                CellStart = cellStart,
                CellCount = model.Cells.Count - cellStart,
                SlotStart = slotStart,
                SlotCount = model.SlotHits.Count - slotStart,
                MarkerStart = markerStart,
                MarkerCount = model.MarkerHits.Count - markerStart,
            });
        }

        /// The storage-only / hide-forbidden basis for one slot token: the
        /// token's count rule where present, the global options otherwise.
        /// Rules are keyed by canonical token, so a pool slot resolves through
        /// its own "#id" key and never through its members' keys.
        private static void ResolveBasis(LayoutInput input, string canonical,
            out bool storageOnly, out bool hideForbidden)
        {
            storageOnly = input.SearchStorageOnly;
            hideForbidden = input.SearchHideForbidden;
            if (input.CountRules != null
                && input.CountRules.TryGetValue(canonical, out CountRule rule))
            {
                storageOnly = rule.ResolveStorageOnly(storageOnly);
                hideForbidden = rule.ResolveHideForbidden(hideForbidden);
            }
        }

        /// Displayed count for one def under the resolved storage-only /
        /// hide-forbidden basis and the planned-work options. The basis
        /// narrows every displayed count (group slots, pool sums, visibility,
        /// thresholds); search results resolve their global basis inline in
        /// BuildResults. A null SearchCounts input falls back to the raw
        /// group-count basis via ResolveSearchCount.
        private static int EffectiveCount(LayoutInput input, string defName,
            bool storageOnly, bool hideForbidden)
        {
            input.Counts.TryGetValue(defName, out int raw);
            SearchCount search = ResolveSearchCount(input, defName, raw);
            return CountBasis.Displayed(search,
                storageOnly, hideForbidden,
                ResolveDebt(input, defName), input.AllowNegativeCounts);
        }

        private static Band CounterBand(LayoutInput input, string canonical, int count)
        {
            if (count < 0) return Band.Critical;
            return input.Thresholds.TryGetValue(canonical, out ThresholdSpec spec)
                ? ThresholdBands.For(count, spec) : Band.Normal;
        }

        /// Planned-work debt for one def; a null Debts input means nothing is
        /// owed (every reservation option off).
        private static int ResolveDebt(LayoutInput input, string defName)
        {
            if (input.Debts == null) return 0;
            return input.Debts.TryGetValue(defName, out PlannedWorkDebt debt)
                ? debt.Total : 0;
        }

        /// Resolves a token to its members list, icon defName, highlight name, and count sum.
        /// Returns true when the token is resolvable (has ≥1 member), unless editorMode is true
        /// in which case pool-ref tokens with zero members are still included.
        private static bool ResolveToken(string token, LayoutInput input, bool editorMode,
            out IReadOnlyList<string>? members, out string? iconDefName,
            out string? highlightName, out int sum)
        {
            members = null;
            iconDefName = null;
            highlightName = null;
            sum = 0;
            ResolveBasis(input, SlotToken.Canonical(token),
                out bool storageOnly, out bool hideForbidden);

            if (SlotToken.IsPoolRef(token))
            {
                // First-class pool reference: #poolId
                int poolId = SlotToken.PoolId(token);
                var pools = input.Pools;
                if (pools == null || !pools.TryGet(poolId, out var poolMembers, out var poolIcon, out var poolName))
                {
                    // Unknown pool: skip in normal mode; in editor mode return an empty slot
                    if (!editorMode) return false;
                    members = Array.Empty<string>();
                    iconDefName = null;
                    highlightName = null;
                    sum = 0;
                    return true;
                }
                // Known pool
                if (poolMembers!.Count == 0) // TryGet true => members populated.
                {
                    // Zero members: skip in normal mode; in editor mode include with empty list
                    if (!editorMode) return false;
                    members = Array.Empty<string>();
                    iconDefName = poolIcon; // may be null
                    highlightName = poolName;
                    sum = 0;
                    return true;
                }
                members = poolMembers;
                iconDefName = poolIcon;
                highlightName = poolName;
                foreach (var m in members)
                    sum += EffectiveCount(input, m, storageOnly, hideForbidden);
                return true;
            }
            else if (SlotToken.IsPool(token))
            {
                // Legacy @Category token
                var cats = input.Catalog.CountedDefsIn(SlotToken.MemberName(token));
                if (cats.Count == 0) return false;
                members = cats;
                iconDefName = members[0];
                highlightName = null; // use member labels for legacy pools
                foreach (var m in members)
                    sum += EffectiveCount(input, m, storageOnly, hideForbidden);
                return true;
            }
            else
            {
                // Plain defName
                string defName = SlotToken.MemberName(token);
                if (!input.Catalog.Exists(defName)) return false;
                members = SingleMember(defName);
                iconDefName = defName;
                highlightName = null;
                sum = EffectiveCount(input, defName, storageOnly, hideForbidden);
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

                    // Visibility check. A negative sum means planned work has
                    // overrun the stock — exactly what the player needs to
                    // see, so hide-when-zero must not swallow it. With
                    // negatives off the sum is already clamped at zero, so
                    // this stays equivalent to the original sum > 0 rule.
                    if (!IsVisible(token, input, sum)) continue;

                    into.Add(new ResolvedSlot
                    {
                        Token = token,
                        Members = members!, // ResolveToken true => members set.
                        Sum = sum,
                        IconDefName = iconDefName,
                        HighlightName = highlightName,
                    });
                }
            }
        }

        private static bool IsVisible(
            string token, LayoutInput input, int sum)
            => sum != 0
                || SlotToken.ShowWhenZero(token)
                || input.Thresholds.ContainsKey(SlotToken.Canonical(token));

        /// Horizontally collapsed band: the normal single-row band height at
        /// the zero-slot container width — stripe backing and one dim
        /// triangle per tier so the group stays identifiable. No slots, slot
        /// hits, or marker hit — the band exists only while the pointer is
        /// elsewhere (hovering it restores tiers), so it is never
        /// interactable. The height matching BuildGroup exactly is what keeps
        /// per-band hover expansion from shifting the bands below.
        private static float BuildCollapsedGroup(ReadoutGroup group, LayoutInput input,
            RenderModel model, float yTop, int groupDisplayIndex)
        {
            float containerH = 2f * LayoutMetrics.GroupPadY + input.Metrics.RowPairH;
            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.GroupBack,
                GroupIndex = groupDisplayIndex,
                GroupId = group.Id,
                Rect = new RectF(0f, yTop,
                    GroupContainerWidth(0, input.Metrics), containerH),
            });
            float insetX = InsetX;
            float insetY = yTop + LayoutMetrics.GroupPadY;
            int tierCount = Math.Min(group.TierCount, TierOps.MaxTiers);
            for (int i = 0; i < tierCount; i++)
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Triangle,
                    Triangle = TriangleState.Dim,
                    Rect = MarkerRect(
                        insetX, insetY, input.Metrics.RowPairH, i),
                });
            return yTop + containerH;
        }

        private static float BuildGroup(ReadoutGroup group, LayoutInput input, RenderModel model,
            List<ResolvedSlot> slots, float yTop, bool searching, int groupDisplayIndex,
            float containerW)
        {
            // Single row always: one icon+counter row pair.
            float containerH = 2f * LayoutMetrics.GroupPadY + input.Metrics.RowPairH;

            // GroupBack cell spans the computed container width — emitted FIRST
            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.GroupBack,
                GroupIndex = groupDisplayIndex,
                GroupId = group.Id,
                Rect = new RectF(0f, yTop, containerW, containerH),
            });

            // Emit marker triangles (inset by stripe+pad on X, GroupPadY on Y).
            // Tiers visible only through hover expansion (beyond the
            // configured depth) show HoverLit instead of Lit.
            int depth = Markers.ClampDepth(group.TierCount, input.DepthOf(group));
            int configured = -1;
            if (input.ConfiguredDepthOf != null)
                configured = Markers.ClampDepth(
                    group.TierCount, input.ConfiguredDepthOf(group));
            float insetX = InsetX;
            float insetY = yTop + LayoutMetrics.GroupPadY;
            for (int i = 0; i < TierOps.MaxTiers; i++)
            {
                TriangleState state = Markers.StateAt(
                    group.TierCount, depth, i);
                if (configured >= 0 && i >= configured && i < depth
                    && state == TriangleState.Lit)
                    state = TriangleState.HoverLit;
                if (state == TriangleState.Absent) continue;
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Triangle,
                    Triangle = state,
                    Rect = MarkerRect(
                        insetX, insetY, input.Metrics.RowPairH, i),
                });
            }

            // The leading rail is clickable even though only 11px is reserved
            // between the inset and the first resource cell.
            model.MarkerHits.Add(new MarkerHit
            {
                GroupId = group.Id,
                Rect = MarkerHitRect(insetY, input.Metrics.RowPairH),
            });

            // Build the icon/counter row, inset (single row, no wrapping)
            BuildGroupGrid(model, slots, input, insetY, highlightMatches: searching);

            return yTop + containerH;
        }

        // All group slots rendered on a single row — no wrapping.
        private static void BuildGroupGrid(RenderModel model, List<ResolvedSlot> slots,
            LayoutInput input, float yInset, bool highlightMatches)
        {
            var metrics = input.Metrics;
            float insetX = InsetX;
            float y = yInset;
            for (int c = 0; c < slots.Count; c++)
            {
                var slot = slots[c];
                // Icon defName: snapshot icon for pool refs, first member otherwise.
                // May be null for empty pools in editor mode — cell still gets emitted.
                string? iconDefName = slot.IconDefName;
                // For DefName on cells: use iconDefName (may be null for zero-member pools)
                string? cellDefName = iconDefName ?? (slot.Members.Count > 0 ? slot.Members[0] : null);
                float x = insetX + LayoutMetrics.MarkerColW + c * metrics.CellW;
                var iconRect = new RectF(
                    x + (metrics.CellW - LayoutMetrics.IconSize) / 2f, y,
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

                int iconCellIndex = model.Cells.Count;
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
                    Band = CounterBand(input, canonical, slot.Sum),
                    Rect = new RectF(x,
                        y + LayoutMetrics.IconRowH - LayoutMetrics.CounterOverlap,
                        metrics.CellW, metrics.CounterRowH),
                });

                model.SlotHits.Add(new SlotHit
                {
                    Token = slot.Token,
                    Members = slot.Members,
                    Rect = new RectF(x, y, metrics.CellW, metrics.RowPairH),
                    CellIndex = iconCellIndex,
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
            float contentW = (tokenCount + (atCap ? 0 : 1)) * input.Metrics.CellW;
            return LayoutMetrics.StripeW + LayoutMetrics.GroupPadX
                   + LayoutMetrics.MarkerColW + contentW + LayoutMetrics.GroupPadX;
        }

        private static float BuildEditorGroup(ReadoutGroup group, LayoutInput input, RenderModel model,
            float yTop, int groupDisplayIndex, float containerW)
        {
            var metrics = input.Metrics;
            float containerH = 2f * LayoutMetrics.GroupPadY + metrics.RowPairH;

            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.GroupBack,
                GroupIndex = groupDisplayIndex,
                GroupId = group.Id,
                Rect = new RectF(0f, yTop, containerW, containerH),
            });

            var tiers = group.Tiers;
            int depth = EditorBand.ClampDepth(tiers, input.DepthOf(group));

            // Markers: count = min(3, max(tiers.Count, depth)), lit = depth
            int markerCount = Math.Min(3, Math.Max(tiers.Count, depth));
            float insetX = InsetX;
            float insetY = yTop + LayoutMetrics.GroupPadY;
            for (int i = 0; i < TierOps.MaxTiers; i++)
            {
                TriangleState state = Markers.StateAt(
                    markerCount, depth, i);
                if (state == TriangleState.Absent) continue;
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Triangle,
                    Triangle = state,
                    Rect = MarkerRect(
                        insetX, insetY, metrics.RowPairH, i),
                });
            }

            model.MarkerHits.Add(new MarkerHit
            {
                GroupId = group.Id,
                Rect = MarkerHitRect(insetY, metrics.RowPairH),
            });

            // Render exactly one tier: tier at index depth-1
            int t = depth - 1;
            List<string>? tierTokens = t < tiers.Count ? tiers[t] : null;
            int tokenCount = tierTokens != null ? tierTokens.Count : 0;

            float colX = insetX + LayoutMetrics.MarkerColW;

            // Emit existing token cells for the current tier
            for (int s = 0; s < tokenCount; s++)
            {
                string token = tierTokens![s]; // tokenCount > 0 => tier exists.
                // Resolve token in editor mode — pool refs with zero/no members still emit cells
                bool resolved = ResolveToken(token, input, editorMode: true,
                    out var members, out var iconDefName, out _, out int sum);
                if (!resolved) { colX += metrics.CellW; continue; }

                // cellDefName may be null for zero-member pools in editor (icon cell still occupies column)
                string? cellDefName = iconDefName ?? (members!.Count > 0 ? members[0] : null); // resolved => members set.

                var iconRect = new RectF(
                    colX + (metrics.CellW - LayoutMetrics.IconSize) / 2f, insetY,
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
                    Band = CounterBand(input, canonical, sum),
                    Rect = new RectF(colX,
                        insetY + LayoutMetrics.IconRowH - LayoutMetrics.CounterOverlap,
                        metrics.CellW, metrics.CounterRowH),
                });

                colX += metrics.CellW;
            }

            // One trailing EmptySlot (append position = tokenCount) — omitted when tier is at cap
            if (tokenCount < TierOps.MaxSlotsPerTier)
            {
                var emptyRect = new RectF(
                    colX + (metrics.CellW - LayoutMetrics.IconSize) / 2f, insetY,
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

        /// A search hit resolved against the active filters; Count is the
        /// displayed count under those filters.
        private struct ResultEntry
        {
            public string DefName;
            public int Count;
        }

        // Results section: plain counted defNames, per-def counts. The count
        // basis is the search breakdown (stored + scattered stacks), narrowed
        // by the per-player filters:
        // - storage-only drops defs with nothing in player storage and counts
        //   only stored stacks;
        // - hide-forbidden drops defs whose (basis) stacks are all forbidden
        //   and subtracts forbidden stacks from the displayed count;
        // - hide-zero drops rows whose displayed count is zero.
        // Defs with a count sort before zero rows; each half stays alphabetical.
        private static float BuildResults(LayoutInput input, RenderModel model, float y)
        {
            var matches = new List<ResultEntry>();
            foreach (var pair in input.Counts)
            {
                if (!SearchMatcher.Matches(input.Catalog.LabelOf(pair.Key), input.SearchText))
                    continue;
                SearchCount search = ResolveSearchCount(input, pair.Key, pair.Value);
                int basisTotal = input.SearchStorageOnly ? search.Stored : search.Total;
                int basisUnforbidden = input.SearchStorageOnly
                    ? search.StoredUnforbidden : search.Unforbidden;
                // Presence rules read the physical stock; the displayed number
                // then goes through the shared basis so planned-work debt is
                // subtracted here exactly as it is on group slots.
                if (input.SearchStorageOnly && basisTotal == 0) continue;
                if (input.SearchHideForbidden && basisTotal > 0 && basisUnforbidden == 0)
                    continue;
                int count = CountBasis.Displayed(search,
                    input.SearchStorageOnly, input.SearchHideForbidden,
                    ResolveDebt(input, pair.Key), input.AllowNegativeCounts);
                if (input.SearchHideZero && count == 0) continue;
                matches.Add(new ResultEntry { DefName = pair.Key, Count = count });
            }
            matches.Sort((a, b) =>
            {
                bool aZero = a.Count == 0, bZero = b.Count == 0;
                if (aZero != bZero) return aZero ? 1 : -1;
                return string.CompareOrdinal(
                    input.Catalog.LabelOf(a.DefName), input.Catalog.LabelOf(b.DefName));
            });

            var metrics = input.Metrics;
            float insetX = InsetX;
            float insetY = y + LayoutMetrics.GroupPadY;

            // Pre-compute container height so GroupBack spans the full container
            int columns = ResultsColumns(input);
            int shown = Math.Min(matches.Count, columns * MaxResultRows);
            int hidden = matches.Count - shown;
            if (hidden > 0)
                matches.RemoveRange(shown, hidden);
            int gridRows = matches.Count > 0 ? (matches.Count + columns - 1) / columns : 0;
            float gridH = gridRows * metrics.RowPairH;
            float containerH = 2f * LayoutMetrics.GroupPadY + metrics.LabelRowH + gridH
                + (hidden > 0 ? metrics.LabelRowH : 0f);

            // GroupBack with GroupIndex/GroupId = -1 emitted BEFORE label cell
            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.GroupBack,
                GroupIndex = -1,
                GroupId = -1,
                Rect = new RectF(0f, y, input.Width, containerH),
            });

            // Label cell (inset)
            model.Cells.Add(new RenderCell
            {
                Kind = CellKind.Label,
                Text = matches.Count == 0 ? NoMatchesLabelKey : ResultsLabelKey,
                Rect = new RectF(insetX, insetY, input.Width - insetX, metrics.LabelRowH),
            });
            insetY += metrics.LabelRowH;

            if (matches.Count > 0)
                BuildResultsGrid(input, model, matches, insetY);

            if (hidden > 0)
                model.Cells.Add(new RenderCell
                {
                    Kind = CellKind.Label,
                    Text = MoreResultsLabelKey,
                    Count = hidden,
                    Rect = new RectF(insetX, insetY + gridH,
                        input.Width - insetX, metrics.LabelRowH),
                });

            return y + containerH;
        }

        /// Search breakdown for one def; a null SearchCounts input falls back
        /// to the group-count basis with every stack stored and unforbidden.
        private static SearchCount ResolveSearchCount(
            LayoutInput input, string defName, int fallbackCount)
        {
            if (input.SearchCounts == null)
                return new SearchCount(fallbackCount, fallbackCount,
                    fallbackCount, fallbackCount);
            input.SearchCounts.TryGetValue(defName, out SearchCount search);
            return search;
        }

        private static void BuildResultsGrid(LayoutInput input, RenderModel model,
            List<ResultEntry> entries, float y)
        {
            var metrics = input.Metrics;
            float insetX = InsetX;
            int columns = ResultsColumns(input);
            for (int i = 0; i < entries.Count; i += columns)
            {
                int rowCount = Math.Min(columns, entries.Count - i);
                for (int c = 0; c < rowCount; c++)
                {
                    string defName = entries[i + c].DefName;
                    int count = entries[i + c].Count;
                    float x = insetX + LayoutMetrics.MarkerColW + c * metrics.CellW;
                    var iconRect = new RectF(
                        x + (metrics.CellW - LayoutMetrics.IconSize) / 2f, y,
                        LayoutMetrics.IconSize, LayoutMetrics.IconSize);
                    int iconCellIndex = model.Cells.Count;
                    model.Cells.Add(new RenderCell
                        { Kind = CellKind.Icon, DefName = defName, Count = count, Rect = iconRect });
                    model.Cells.Add(new RenderCell
                    {
                        Kind = CellKind.Counter,
                        DefName = defName,
                        Count = count,
                        Text = CountFormat.Compact(count),
                        Band = CounterBand(input, defName, count),
                        Rect = new RectF(x,
                            y + LayoutMetrics.IconRowH - LayoutMetrics.CounterOverlap,
                            metrics.CellW, metrics.CounterRowH),
                    });

                    // A result row is always a single plain def.
                    model.SlotHits.Add(new SlotHit
                    {
                        Token = defName,
                        Members = SingleMember(defName),
                        Rect = new RectF(x, y, metrics.CellW, metrics.RowPairH),
                        CellIndex = iconCellIndex,
                    });
                }
                y += metrics.RowPairH;
            }
        }
    }
}
