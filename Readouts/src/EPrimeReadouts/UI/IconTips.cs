using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Structured hover tips for resource icons: label + count badge,
    /// description prose, threshold facts. Models are cached per token and
    /// rebuilt only when count or band changes.
    public static class IconTips
    {
        private struct CachedTip
        {
            public int Count;
            public Band Band;
            public StructuredTip Tip;
        }

        private static readonly Dictionary<string, CachedTip> cache =
            new Dictionary<string, CachedTip>();

        public static void Tip(Rect rect, ThingDef def, int count, Band band, string token)
        {
            // Use token as cache key (null-safe fallback to defName for plain slots)
            string cacheKey = token ?? def.defName;
            if (!cache.TryGetValue(cacheKey, out var cached)
                || cached.Count != count || cached.Band != band)
            {
                cached = new CachedTip { Count = count, Band = band,
                    Tip = Build(def, count, band, token) };
                cache[cacheKey] = cached;
            }
            TooltipHandler.TipRegion(rect, new TipSignal(cached.Tip.Activate(), def.shortHash));
        }

        private static StructuredTip Build(ThingDef def, int count, Band band, string token)
        {
            string canonical = token != null ? SlotToken.Canonical(token) : def.defName;
            bool isLegacyPool = token != null && SlotToken.IsPool(token);
            bool isPoolRef = token != null && SlotToken.IsPoolRef(token);

            string title;
            System.Collections.Generic.IReadOnlyList<string> poolMembers = null;

            if (isPoolRef)
            {
                // First-class pool: look up snapshot for name and members
                int poolId = SlotToken.PoolId(token);
                var store = ReadoutStore.Current;
                var pool = store?.Model.PoolById(poolId);
                title = pool != null ? pool.Name : canonical;
                // Expand members on hover (acceptable — hover-only)
                if (pool != null)
                {
                    var snapshot = PoolSnapshot.Build(
                        new System.Collections.Generic.List<ResourcePool> { pool },
                        GameResourceCatalog.Instance);
                    snapshot.TryGet(poolId, out poolMembers, out _, out _);
                }
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
                // Pool: per-member count breakdown (LiveCount also covers
                // extra-counted defs like stone chunks)
                var breakdown = model.AddSection();
                var map = Find.CurrentMap;
                var breakdownStore = ReadoutStore.Current;
                for (int m = 0; m < poolMembers.Count; m++)
                {
                    var memberDef = DefDatabase<ThingDef>.GetNamedSilentFail(poolMembers[m]);
                    if (memberDef == null) continue;
                    int memberCount = map != null
                        ? GameCounts.LiveCount(map, breakdownStore, memberDef) : 0;
                    breakdown.Fact(memberDef.LabelCap, memberCount.ToString());
                }
            }

            var tipStore = ReadoutStore.Current;
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
