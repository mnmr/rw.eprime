using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using EPrimeReadouts.Patches;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Marks this mod's deferred tooltip getters so Patch_ActiveTip can
    /// recognize them by delegate target without invoking foreign getters.
    internal interface IDeferredTipSource
    {
    }

    /// Structured hover tips for resource icons: label + count badge,
    /// description prose, pool breakdown, threshold facts. Hovering only
    /// records intent (a dictionary read plus field writes); gathering is
    /// deferred into a per-token TipSignal getter that vanilla invokes only
    /// once the tooltip actually renders after its hover delay.
    public static class IconTips
    {
        /// Pool breakdown rows per tooltip column before a new column starts.
        private const int MaxBreakdownRowsPerColumn = 20;

        private readonly struct TipRevision : IEquatable<TipRevision>
        {
            private readonly RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData;
            private readonly int thresholdsVersion;
            private readonly int uiVersion;
            // Count-basis options narrow the badge and the pool breakdown, so
            // toggling them must rebuild the tip on its next display session.
            private readonly bool storageOnly;
            private readonly bool hideForbidden;

            public TipRevision(
                RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData,
                int thresholdsVersion,
                int uiVersion,
                bool storageOnly,
                bool hideForbidden)
            {
                this.renderData = renderData;
                this.thresholdsVersion = thresholdsVersion;
                this.uiVersion = uiVersion;
                this.storageOnly = storageOnly;
                this.hideForbidden = hideForbidden;
            }

            public bool Equals(TipRevision other) =>
                ReferenceEquals(renderData, other.renderData)
                && thresholdsVersion == other.thresholdsVersion
                && uiVersion == other.uiVersion
                && storageOnly == other.storageOnly
                && hideForbidden == other.hideForbidden;

            public override bool Equals(object obj) =>
                obj is TipRevision other && Equals(other);

            public override int GetHashCode() =>
                ((renderData != null ? renderData.GetHashCode() : 0) * 397)
                ^ thresholdsVersion
                ^ uiVersion
                ^ (storageOnly ? 1 << 30 : 0)
                ^ (hideForbidden ? 1 << 29 : 0);
        }

        private struct BuildState
        {
            public ThingDef Def;
            public int Count;
            public string Token;
            public ReadoutStore Store;
            public RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> RenderData;
        }

        // Cache contract:
        // Owner: current world/store presentation session.
        // Key: canonical token.
        // Value: immutable StructuredTip/TipModel graph.
        // Dependencies: shared render snapshot identity, ThresholdsVersion,
        // UiVersion, and the storage-only/hide-forbidden count-basis options.
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
        // Value: mutable DeferredTip carrying a once-built getter delegate,
        // the latest hovered state, and the frozen displayed tip.
        // Dependencies: hover state fields are overwritten on every hovered
        // frame; the frozen tip depends only on display-frame continuity.
        // Refresh policy: gathering runs on the first frame of each display
        // session, through the revisioned model cache above.
        // Equality policy: entry and getter identity are stable per key.
        // Teardown: Reset clears entries and the displayed registration.
        private static readonly Dictionary<string, DeferredTip> deferredTips =
            new Dictionary<string, DeferredTip>();

        /// One per hovered token: TipRegion hands vanilla the cached Getter,
        /// so hover passes build no closures, models, or strings. Gather runs
        /// only while vanilla renders the tooltip, after the hover delay.
        private sealed class DeferredTip : IDeferredTipSource
        {
            internal readonly string CacheKey;
            internal readonly Func<string> Getter;
            internal ThingDef Def;
            internal int Count;
            internal string Token;
            internal RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> RenderData;
            private StructuredTip Frozen;
            private int lastDisplayFrame = TipContinuity.NoFrame;

            internal DeferredTip(string cacheKey)
            {
                CacheKey = cacheKey;
                Getter = Gather;
            }

            /// The first invocation of a display session gathers through the
            /// revisioned cache and freezes the result; a broken frame
            /// continuity means the tip closed, so the next display regathers.
            /// Users leave and re-hover to see updated info.
            private string Gather()
            {
                int frame = Time.frameCount;
                if (Frozen == null || TipContinuity.IsBroken(lastDisplayFrame, frame))
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
                    Frozen = RenderData != null
                        ? cache.Get(
                            CacheKey,
                            new TipRevision(RenderData,
                                store != null ? store.ThresholdsVersion : 0,
                                UiVersion.Current,
                                settings.searchStorageOnly,
                                settings.searchHideForbidden),
                            state,
                            buildTip)
                        : Build(state);
                }
                lastDisplayFrame = frame;
                return Frozen.Activate();
            }
        }

        public static void Tip(
            Rect rect,
            ThingDef def,
            int count,
            Band band,
            string token,
            RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData)
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
            TooltipHandler.TipRegion(rect, new TipSignal(deferred.Getter, def.shortHash));
        }

        private static StructuredTip Build(BuildState state)
        {
            var def = state.Def;
            int count = state.Count;
            string token = state.Token;
            string canonical = token != null ? SlotToken.Canonical(token) : def.defName;
            bool isLegacyPool = token != null && SlotToken.IsPool(token);
            bool isPoolRef = token != null && SlotToken.IsPoolRef(token);

            string title;
            System.Collections.Generic.IReadOnlyList<string> poolMembers = null;

            if (isPoolRef)
            {
                // First-class pool: look up snapshot for name and members
                int poolId = SlotToken.PoolId(token);
                if (state.RenderData != null
                    && state.RenderData.Structure.TryGet(
                        poolId, out poolMembers, out _, out string poolName))
                {
                    title = poolName;
                }
                else title = canonical;
            }
            else if (isLegacyPool)
            {
                string member = SlotToken.MemberName(token);
                title = GameResourceCatalog.Instance.CategoryLabelOf(member).CapitalizeFirst();
                poolMembers = GameResourceCatalog.Instance.CountedDefsIn(member);
            }
            else
            {
                title = def.LabelCap;
            }

            var model = new TipModel
            {
                Title = title,
                Badge = count.ToString(),
            };

            if (!isLegacyPool && !isPoolRef)
            {
                var body = model.AddSection();
                body.Text(def.description);
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
                                settings.searchStorageOnly, settings.searchHideForbidden);
                    }
                    if (memberCount == 0) continue;
                    memberLabels.Add(memberDef.LabelCap);
                    memberValues.Add(memberCount.ToString());
                }
                if (memberLabels.Count > 0)
                    model.AddSection().FactGrid(
                        memberLabels, memberValues, MaxBreakdownRowsPerColumn);
            }

            var tipStore = state.Store;
            if (tipStore != null && tipStore.Model.Thresholds.TryGetValue(canonical, out var spec))
            {
                var levels = model.AddSection(UiText.Get("EPR.Thresholds"));
                levels.Fact(UiText.Get("EPR.Low"), spec.Low.ToString());
                levels.Fact(UiText.Get("EPR.Critical"), spec.Critical.ToString());
            }
            return new StructuredTip("EPR.Tip." + canonical, model);
        }

        internal static void Reset()
        {
            cache.Clear();
            deferredTips.Clear();
            Patch_ActiveTip_TipRect.ReleaseDisplayed();
        }
    }
}
