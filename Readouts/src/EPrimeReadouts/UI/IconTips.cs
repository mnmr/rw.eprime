using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Structured hover tips for resource icons: label + count badge,
    /// description prose, pool breakdown, threshold facts. Hovering only
    /// records intent (a dictionary read plus field writes); gathering is
    /// deferred until the owned tooltip window opens after its hover delay.
    public static class IconTips
    {
        /// Pool breakdowns use up to this many columns. Rows are balanced from
        /// the visible, non-zero members before the immutable tip is published.
        private const int MaxBreakdownColumns = 3;
        private const int MaxPlannedWorkRows = 8;

        private static readonly TipColumnAlignment[] singleWorkAlignments =
        {
            TipColumnAlignment.Left,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
        };

        private static readonly TipColumnAlignment[] pooledWorkAlignments =
        {
            TipColumnAlignment.Left,
            TipColumnAlignment.Left,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
        };

        private readonly struct TipRevision : IEquatable<TipRevision>
        {
            private readonly RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData;
            private readonly int thresholdsVersion;
            private readonly int languageVersion;
            // Count-basis options narrow the badge and the pool breakdown, so
            // toggling them must rebuild the tip on its next display session.
            private readonly bool storageOnly;
            private readonly bool hideForbidden;
            // The planned-work debt itself arrives with renderData; only the
            // negative-display choice is an independent presentation input.
            private readonly bool showNegative;

            public TipRevision(
                RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData,
                int thresholdsVersion,
                int languageVersion,
                bool storageOnly,
                bool hideForbidden,
                bool showNegative)
            {
                this.renderData = renderData;
                this.thresholdsVersion = thresholdsVersion;
                this.languageVersion = languageVersion;
                this.storageOnly = storageOnly;
                this.hideForbidden = hideForbidden;
                this.showNegative = showNegative;
            }

            public bool Equals(TipRevision other) =>
                ReferenceEquals(renderData, other.renderData)
                && thresholdsVersion == other.thresholdsVersion
                && languageVersion == other.languageVersion
                && storageOnly == other.storageOnly
                && hideForbidden == other.hideForbidden
                && showNegative == other.showNegative;

            public override bool Equals(object obj) =>
                obj is TipRevision other && Equals(other);

            public override int GetHashCode() =>
                ((renderData != null ? renderData.GetHashCode() : 0) * 397)
                ^ thresholdsVersion
                ^ languageVersion
                ^ (storageOnly ? 1 << 30 : 0)
                ^ (hideForbidden ? 1 << 29 : 0)
                ^ (showNegative ? 1 << 28 : 0);
        }

        private struct BuildState
        {
            public ThingDef Def;
            public int Count;
            public string? Token;
            public ReadoutStore? Store;
            public RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot>? RenderData;
        }

        // Cache contract:
        // Owner: current world/store presentation session.
        // Key: canonical token.
        // Value: immutable StructuredTip/TipModel graph.
        // Dependencies: shared render snapshot identity, ThresholdsVersion,
        // language revision, and the storage-only, hide-forbidden, and
        // show-negative count-basis/presentation options.
        // Refresh policy: probed only at display-session start; a dependency
        // change rebuilds on the next display, never mid-display.
        // Equality policy: cache hits preserve StructuredTip identity.
        // Teardown: Reset clears all models on world teardown.
        private static readonly RevisionedCache<string, TipRevision, StructuredTip> cache =
            new RevisionedCache<string, TipRevision, StructuredTip>();
        private static readonly Func<BuildState, StructuredTip> buildTip = Build;

        // Cache contract:
        // Owner: current world/store presentation session.
        // Key: slot token (defName fallback) — same key as the model cache.
        // Value: mutable DeferredTip carrying the latest hovered state.
        // Dependencies: hover state fields are overwritten on every hovered
        // frame; the presenter owns the frozen display session.
        // Refresh policy: gathering runs on the first frame of each display
        // session, through the revisioned model cache above.
        // Equality policy: entry identity is stable per key.
        // Teardown: Reset clears entries and the presenter session.
        private static readonly Dictionary<string, DeferredTip> deferredTips =
            new Dictionary<string, DeferredTip>();

        /// One per hovered token, so hover passes build no closures, models,
        /// or strings. Resolve runs only when the presenter opens the window.
        private sealed class DeferredTip : IStructuredTipSource
        {
            internal readonly string CacheKey;
            internal ThingDef Def = null!; // assigned by Tip before any Resolve
            internal int Count;
            internal string? Token;
            internal RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot>? RenderData;

            internal DeferredTip(string cacheKey)
            {
                CacheKey = cacheKey;
            }

            string IStructuredTipSource.StableKey => CacheKey;

            /// The presenter invokes this once when a display session opens.
            StructuredTip IStructuredTipSource.Resolve()
            {
                UiVersion.ObserveCurrentMetrics();
                var store = ReadoutStore.Current;
                var state = new BuildState
                {
                    Def = Def,
                    Count = Count,
                    Token = Token,
                    Store = store,
                    RenderData = RenderData,
                };
                var settings = EPrimeReadoutsMod.Settings;
                return RenderData != null
                    ? cache.Get(
                        CacheKey,
                        new TipRevision(RenderData,
                            store != null ? store.ThresholdsVersion : 0,
                            UiVersion.LanguageCurrent,
                            settings.searchStorageOnly,
                            settings.searchHideForbidden,
                            settings.showNegativeCounts),
                        state,
                        buildTip)
                    : Build(state);
            }
        }

        public static void Tip(
            Rect rect,
            ThingDef def,
            int count,
            Band band,
            string? token,
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot>? renderData)
        {
            // Use token as cache key (null-safe fallback to defName for plain slots)
            string cacheKey = token ?? def.defName;
            if (!deferredTips.TryGetValue(cacheKey, out var deferred))
            {
                deferred = new DeferredTip(cacheKey);
                deferredTips.Add(cacheKey, deferred);
            }
            deferred.Def = def;
            deferred.Count = count;
            deferred.Token = token;
            deferred.RenderData = renderData;
            StructuredTipPresenter.TipRegion(rect, deferred);
        }

        private static StructuredTip Build(BuildState state)
        {
            var def = state.Def;
            int count = state.Count;
            string? token = state.Token;
            string canonical = token != null ? SlotToken.Canonical(token) : def.defName;
            bool isLegacyPool = token != null && SlotToken.IsPool(token);
            bool isPoolRef = token != null && SlotToken.IsPoolRef(token);
            bool pooled = isLegacyPool || isPoolRef;

            string title;
            System.Collections.Generic.IReadOnlyList<string>? poolMembers = null;

            if (isPoolRef)
            {
                // First-class pool: look up snapshot for name and members
                int poolId = SlotToken.PoolId(token!); // isPoolRef implies a token
                if (state.RenderData != null
                    && state.RenderData.Structure.TryGet(
                        poolId, out poolMembers, out _, out string? poolName))
                {
                    title = poolName!; // set on the true path
                }
                else title = canonical;
            }
            else if (isLegacyPool)
            {
                string member = SlotToken.MemberName(token!); // isLegacyPool implies a token
                title = GameResourceCatalog.Instance.CategoryLabelOf(member).CapitalizeFirst();
                poolMembers = GameResourceCatalog.Instance.CountedDefsIn(member);
            }
            else
            {
                title = def.LabelCap;
            }

            string badge = count.ToString();
            if (!pooled && def != null && state.RenderData != null)
            {
                int inStock = StockOf(state.RenderData.Counts,
                    EPrimeReadoutsMod.Settings, def.defName);
                PlannedWorkTipLayout headerLayout = PlannedWorkTipLayout.For(
                    pooled: false, available: count, inStock: inStock);
                if (headerLayout.ShowStockInHeader)
                    badge = "EPR.TipAvailableStock"
                        .Translate(count, inStock).ToString();
            }

            var model = new TipModel
            {
                Title = title,
                Badge = badge,
            };

            if (!isLegacyPool && !isPoolRef)
            {
                var body = model.AddSection();
                body.Text(def!.description); // icon tips always carry the slot's def
            }
            else if (poolMembers != null && poolMembers.Count > 0)
            {
                // Pool: per-member count breakdown from the shared count
                // snapshot. Zero-count members are omitted; long lists wrap
                // into extra columns so the tooltip widens instead of growing.
                var memberLabels = new System.Collections.Generic.List<string>();
                var memberValues = new System.Collections.Generic.List<string>();
                var settings = EPrimeReadoutsMod.Settings;
                for (int m = 0; m < poolMembers.Count; m++)
                {
                    var memberDef = DefDatabase<ThingDef>.GetNamedSilentFail(poolMembers[m]);
                    if (memberDef == null) continue;
                    // Same narrowed basis as the slot sums (CountBasis), so
                    // the breakdown always adds up to the badge. An empty
                    // breakdown map falls back to the raw counts, mirroring
                    // ReadoutLayoutEngine.ResolveSearchCount.
                    int memberCount = 0;
                    if (state.RenderData != null)
                    {
                        var counts = state.RenderData.Counts;
                        if (counts.SearchCounts.Count == 0)
                            counts.Counts.TryGetValue(memberDef.defName, out memberCount);
                        else if (counts.SearchCounts.TryGetValue(
                            memberDef.defName, out SearchCount search))
                            memberCount = CountBasis.Displayed(search,
                                settings.searchStorageOnly, settings.searchHideForbidden,
                                counts.DebtOf(memberDef.defName).Total,
                                settings.showNegativeCounts);
                    }
                    if (memberCount == 0) continue;
                    memberLabels.Add(memberDef.LabelCap);
                    memberValues.Add(memberCount.ToString());
                }
                if (memberLabels.Count > 0)
                {
                    int rowsPerColumn = ColumnGrid.RowsPerColumn(
                        memberLabels.Count, MaxBreakdownColumns);
                    model.AddSection().FactGrid(
                        memberLabels, memberValues, rowsPerColumn);
                }
            }

            AddPlannedWorkSection(state, model, def, poolMembers,
                pooled);

            var tipStore = state.Store;
            if (tipStore != null && tipStore.Model.Thresholds.TryGetValue(canonical, out var spec))
            {
                var levels = model.AddSection(UiText.Get("EPR.Thresholds"));
                levels.Fact(UiText.Get("EPR.Low"), spec.Low.ToString());
                levels.Fact(UiText.Get("EPR.Critical"), spec.Critical.ToString());
            }
            return new StructuredTip("EPR.Tip." + canonical, model);
        }

        /// Work/resource rows come from the shared immutable count snapshot.
        /// Pool tips filter the rows to their own members; single-resource tips
        /// use the same table with the implicit Resource column omitted.
        private static void AddPlannedWorkSection(
            BuildState state, TipModel model, ThingDef? def,
            System.Collections.Generic.IReadOnlyList<string>? poolMembers,
            bool pooled)
        {
            if (state.RenderData == null) return;
            var counts = state.RenderData.Counts;
            if (counts.PlannedWork.Count == 0) return;

            System.Collections.Generic.IReadOnlyList<string> resources;
            if (pooled)
            {
                if (poolMembers == null || poolMembers.Count == 0) return;
                resources = poolMembers;
            }
            else
            {
                if (def == null) return;
                resources = new[] { def.defName };
            }

            PlannedWorkSelection selection = PlannedWorkSelection.ForResources(
                counts.PlannedWork, resources, MaxPlannedWorkRows);
            if (selection.Rows.Count == 0 && selection.OverflowCount == 0) return;

            var settings = EPrimeReadoutsMod.Settings;
            PlannedWorkTipLayout layout = PlannedWorkTipLayout.For(
                pooled, available: 0, inStock: 0);
            TipColumnAlignment[] alignments = pooled
                ? pooledWorkAlignments : singleWorkAlignments;
            var section = model.AddSection();
            section.Columns(Headers(layout), TipText.DimColor,
                alignments: alignments);
            section.Rule();

            for (int i = 0; i < selection.Rows.Count; i++)
            {
                PlannedWorkEntry entry = selection.Rows[i];
                section.Columns(Cells(counts, settings,
                        entry, layout),
                    color: entry.Source == PlannedWorkSource.QualityJob
                        ? EprStyle.SelectionTint : (Color?)null,
                    alignments: alignments);
            }

            if (selection.OverflowCount > 0)
                section.Columns(OverflowCells(
                        counts, settings, selection, layout),
                    color: selection.OverflowSource
                               == PlannedWorkSource.QualityJob
                        ? EprStyle.SelectionTint : (Color?)null,
                    alignments: alignments);
        }

        private static string[] Headers(PlannedWorkTipLayout layout)
        {
            if (layout.ShowResourceColumn && layout.ShowInStockColumn)
                return new[]
                {
                    UiText.Get("EPR.TipPlannedWork"),
                    UiText.Get("EPR.TipResource"),
                    UiText.Get("EPR.TipQueued"),
                    UiText.Get("EPR.TipEach"),
                    UiText.Get("EPR.TipDrain"),
                    UiText.Get("EPR.TipInStock"),
                };
            return new[]
            {
                UiText.Get("EPR.TipPlannedWork"),
                UiText.Get("EPR.TipQueued"),
                UiText.Get("EPR.TipEach"),
                UiText.Get("EPR.TipDrain"),
            };
        }

        private static string[] Cells(
            RenderCountSnapshot counts,
            ReadoutSettings settings,
            PlannedWorkEntry entry,
            PlannedWorkTipLayout layout)
        {
            if (layout.ShowResourceColumn && layout.ShowInStockColumn)
            {
                string stock = StockOf(counts, settings,
                    entry.ResourceDefName).ToString();
                return new[]
                {
                    WorkLabel(entry),
                    ResourceLabel(entry.ResourceDefName),
                    entry.Queued.ToString(),
                    entry.UnitCost.ToString(),
                    "-" + entry.Drain.ToString(),
                    stock,
                };
            }
            return new[]
            {
                WorkLabel(entry),
                entry.Queued.ToString(),
                entry.UnitCost.ToString(),
                "-" + entry.Drain.ToString(),
            };
        }

        private static string[] OverflowCells(
            RenderCountSnapshot counts,
            ReadoutSettings settings,
            PlannedWorkSelection selection,
            PlannedWorkTipLayout layout)
        {
            string? resource = selection.OverflowResourceDefName;
            string label = "EPR.TipOtherPlannedWork"
                .Translate(selection.OverflowCount).ToString();
            if (layout.ShowResourceColumn && layout.ShowInStockColumn)
            {
                string stock = resource != null
                    ? StockOf(counts, settings, resource).ToString() : "—";
                return new[]
                {
                    label,
                    resource != null
                        ? ResourceLabel(resource)
                        : UiText.Get("EPR.TipMixedResources"),
                    resource != null
                        ? selection.OverflowQueued.ToString() : "—",
                    "—",
                    "-" + selection.OverflowDrain.ToString(),
                    stock,
                };
            }
            return new[]
            {
                label,
                selection.OverflowQueued.ToString(),
                "—",
                "-" + selection.OverflowDrain.ToString(),
            };
        }

        private static string WorkLabel(PlannedWorkEntry entry)
        {
            if (entry.Kind == PlannedWorkKind.Bill)
            {
                ThingDef product = DefDatabase<ThingDef>.GetNamedSilentFail(
                    entry.WorkDefName);
                if (product != null) return product.LabelCap;
                RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(
                    entry.WorkDefName);
                return recipe != null ? recipe.LabelCap : entry.WorkDefName;
            }

            BuildableDef buildable = DefDatabase<ThingDef>.GetNamedSilentFail(
                entry.WorkDefName);
            if (buildable == null)
                buildable = DefDatabase<TerrainDef>.GetNamedSilentFail(
                    entry.WorkDefName);
            ThingDef? stuff = entry.StuffDefName != null
                ? DefDatabase<ThingDef>.GetNamedSilentFail(entry.StuffDefName)
                : null;
            return buildable != null
                ? GenLabel.ThingLabel(buildable, stuff).CapitalizeFirst()
                : entry.WorkDefName;
        }

        private static string ResourceLabel(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def != null ? def.LabelCap : defName;
        }

        private static int StockOf(
            RenderCountSnapshot counts,
            ReadoutSettings settings,
            string defName)
        {
            if (counts.SearchCounts.Count == 0)
            {
                counts.Counts.TryGetValue(defName, out int raw);
                return raw;
            }
            return counts.SearchCounts.TryGetValue(
                       defName, out SearchCount search)
                ? CountBasis.Displayed(search,
                    settings.searchStorageOnly,
                    settings.searchHideForbidden)
                : 0;
        }

        internal static void Reset()
        {
            cache.Clear();
            deferredTips.Clear();
            StructuredTipPresenter.Reset();
        }
    }
}
