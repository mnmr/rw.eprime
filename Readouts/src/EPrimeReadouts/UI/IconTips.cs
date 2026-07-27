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
            bool isPool = token != null && SlotToken.IsPool(token);
            string member = token != null ? SlotToken.MemberName(token) : def.defName;
            string title;
            if (isPool)
                title = GameResourceCatalog.Instance.CategoryLabelOf(member).CapitalizeFirst();
            else
                title = def.LabelCap;
            var model = new TipModel
            {
                Title = title,
                Badge = count.ToString(),
            };
            if (!isPool)
            {
                var body = model.AddSection();
                body.Text(def.description);
            }
            else
            {
                // Pool: per-member count breakdown
                var members = GameResourceCatalog.Instance.CountedDefsIn(member);
                if (members.Count > 0)
                {
                    var breakdown = model.AddSection();
                    var map = Find.CurrentMap;
                    for (int m = 0; m < members.Count; m++)
                    {
                        var memberDef = DefDatabase<ThingDef>.GetNamedSilentFail(members[m]);
                        if (memberDef == null) continue;
                        int memberCount = map?.resourceCounter.GetCount(memberDef) ?? 0;
                        breakdown.Fact(memberDef.LabelCap, memberCount.ToString());
                    }
                }
            }
            var store = ReadoutStore.Current;
            if (store != null && store.Model.Thresholds.TryGetValue(canonical, out var spec))
            {
                var levels = model.AddSection("EPR.Thresholds".Translate());
                levels.Fact("EPR.Low".Translate(), spec.Low.ToString());
                levels.Fact("EPR.Critical".Translate(), spec.Critical.ToString());
            }
            return new StructuredTip("EPR.Tip." + canonical, model);
        }
    }
}
