using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Implanner.UI
{
    /// Structured stat tooltips for the plan editor's selection tree and the
    /// rankings rows: title, key implant stats, then the description. Built
    /// lazily for the hovered row only.
    // Cache contract:
    // Owner: process/current UI presentation.
    // Key: implant definition name (only implant tips are stored).
    // Value: the immutable formatted tip string.
    // Dependencies: the loaded definition set (static per session) and
    //   UiVersion.LanguageCurrent for labels and stat names.
    // Refresh policy: cleared on the next lookup after the language revision
    //   moves; entries build on first hover.
    // Equality policy: hits return the cached string.
    // Teardown: Reset clears all entries (world teardown).
    internal static class PlannerTips
    {
        private static readonly Dictionary<string, string> tips =
            new Dictionary<string, string>(System.StringComparer.Ordinal);
        private static int languageStamp = -1;

        internal static void Reset()
        {
            tips.Clear();
            languageStamp = -1;
        }

        private static void EnsureCurrent()
        {
            if (languageStamp == UiVersion.LanguageCurrent) return;
            tips.Clear();
            languageStamp = UiVersion.LanguageCurrent;
        }

        internal static string ForImplant(ImplantCatalogEntry entry)
        {
            EnsureCurrent();
            // The defName IS the key: hover hits this every pass and a
            // cache hit must not allocate a composite key string.
            string key = entry.Def.defName;
            if (!tips.TryGetValue(key, out string tip))
                tips[key] = tip = BuildImplantTip(entry);
            return tip;
        }

        /// The body part groups the implant occupies, named by the part rather
        /// than the capacity so a modded part reads sensibly.
        private static string CapacityLabel(ImplantCatalogEntry entry)
        {
            List<BodyPartDef> parts = entry.FixedParts;
            if (parts.Count == 0) return "-";
            var text = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(parts[i].LabelCap);
            }
            return text.ToString();
        }

        private static string BuildImplantTip(ImplantCatalogEntry entry)
        {
            var text = new StringBuilder();
            text.AppendLine(entry.Label.CapitalizeFirst());
            text.AppendLine();
            if (entry.Def.addedPartProps != null)
                text.AppendLine("IMP_TipPartEfficiency".Translate(
                    entry.Def.addedPartProps.partEfficiency.ToStringPercent()));
            // The capacity the replaced part drives — what the Movement and
            // Consciousness priorities sort on.
            text.AppendLine("IMP_TipCapacity".Translate(CapacityLabel(entry)));
            ThingDef? item = entry.Def.spawnThingOnRemoved;
            if (item != null)
            {
                float value = item.GetStatValueAbstract(StatDefOf.MarketValue);
                if (value > 0f)
                {
                    text.Append(StatDefOf.MarketValue.LabelCap);
                    text.Append(": ");
                    text.AppendLine(value.ToStringByStyle(
                        StatDefOf.MarketValue.toStringStyle));
                }
            }
            string? description = entry.Def.description;
            if (description.NullOrEmpty() && entry.Def.spawnThingOnRemoved != null)
                description = entry.Def.spawnThingOnRemoved.description;
            if (!description.NullOrEmpty())
            {
                text.AppendLine();
                text.Append(description);
            }
            return text.ToString();
        }
    }
}
