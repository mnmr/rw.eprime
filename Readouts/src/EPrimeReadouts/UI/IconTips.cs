using System;
using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Structured hover tips for resource icons: label + count badge,
    /// description prose, threshold facts. Models are cached per token and
    /// rebuilt only when their shared render-data snapshot changes.
    public static class IconTips
    {
        private readonly struct TipRevision : IEquatable<TipRevision>
        {
            private readonly RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData;
            private readonly int thresholdsVersion;

            public TipRevision(
                RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> renderData,
                int thresholdsVersion)
            {
                this.renderData = renderData;
                this.thresholdsVersion = thresholdsVersion;
            }

            public bool Equals(TipRevision other) =>
                ReferenceEquals(renderData, other.renderData)
                && thresholdsVersion == other.thresholdsVersion;

            public override bool Equals(object obj) =>
                obj is TipRevision other && Equals(other);

            public override int GetHashCode() =>
                ((renderData != null ? renderData.GetHashCode() : 0) * 397)
                ^ thresholdsVersion;
        }

        private struct BuildState
        {
            public ThingDef Def;
            public int Count;
            public string Token;
            public ReadoutStore Store;
            public RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> RenderData;
        }

        private static readonly RevisionedCache<string, TipRevision, StructuredTip> cache =
            new RevisionedCache<string, TipRevision, StructuredTip>();
        private static readonly Func<BuildState, StructuredTip> buildTip = Build;

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
            StructuredTip tip;
            var store = ReadoutStore.Current;
            var state = new BuildState
            {
                Def = def,
                Count = count,
                Token = token,
                Store = store,
                RenderData = renderData,
            };
            if (renderData != null)
                tip = cache.Get(
                    cacheKey,
                    new TipRevision(renderData, store != null ? store.ThresholdsVersion : 0),
                    state,
                    buildTip);
            else
                tip = Build(state);
            TooltipHandler.TipRegion(rect, new TipSignal(tip.Activate(), def.shortHash));
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
                // Pool: per-member count breakdown from the shared count snapshot.
                var breakdown = model.AddSection();
                for (int m = 0; m < poolMembers.Count; m++)
                {
                    var memberDef = DefDatabase<ThingDef>.GetNamedSilentFail(poolMembers[m]);
                    if (memberDef == null) continue;
                    int memberCount = state.RenderData != null
                        && state.RenderData.Counts.Counts.TryGetValue(
                            memberDef.defName, out int cachedCount)
                        ? cachedCount : 0;
                    breakdown.Fact(memberDef.LabelCap, memberCount.ToString());
                }
            }

            var tipStore = state.Store;
            if (tipStore != null && tipStore.Model.Thresholds.TryGetValue(canonical, out var spec))
            {
                var levels = model.AddSection("EPR.Thresholds".Translate());
                levels.Fact("EPR.Low".Translate(), spec.Low.ToString());
                levels.Fact("EPR.Critical".Translate(), spec.Critical.ToString());
            }
            return new StructuredTip("EPR.Tip." + canonical, model);
        }
    }
}
