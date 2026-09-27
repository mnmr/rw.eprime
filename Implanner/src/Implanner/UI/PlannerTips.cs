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
    //   UiVersion.LanguageCurrent for labels and stat names. Quality Bionics
    //   Remastered's efficiency multipliers are read when a tip builds and
    //   observed through ImplantQualities.SettingsRevision (checked on the
    //   1020-tick boundary, since the mod sends no change notification).
    // Refresh policy: cleared on the next lookup after either revision
    //   moves; entries build on first hover.
    // Equality policy: hits return the cached string.
    // Teardown: Reset clears all entries (world teardown).
    internal static class PlannerTips
    {
        private static readonly Dictionary<string, string> tips =
            new Dictionary<string, string>(System.StringComparer.Ordinal);
        private static int languageStamp = -1;
        private static int qualityStamp = -1;

        internal static void Reset()
        {
            tips.Clear();
            languageStamp = -1;
            qualityStamp = -1;
        }

        private static void EnsureCurrent()
        {
            if (languageStamp == UiVersion.LanguageCurrent
                && qualityStamp == ImplantQualities.SettingsRevision)
                return;
            tips.Clear();
            languageStamp = UiVersion.LanguageCurrent;
            qualityStamp = ImplantQualities.SettingsRevision;
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
            // Part efficiency as installed: one value, or the span across
            // item qualities (Quality Bionics Remastered's multipliers,
            // Vanilla Genetics Expanded's quality stages).
            float[] byQuality = ImplantQualities.AfterInstall(entry);
            float lowest = byQuality[0], highest = byQuality[0];
            for (int q = 1; q < byQuality.Length; q++)
            {
                if (byQuality[q] < lowest) lowest = byQuality[q];
                if (byQuality[q] > highest) highest = byQuality[q];
            }
            if (highest - lowest > 0.0001f)
                text.AppendLine("IMP_TipPartEfficiencyRange".Translate(
                    lowest.ToStringPercent(), highest.ToStringPercent()));
            else if (entry.IsReplacement)
                text.AppendLine("IMP_TipPartEfficiency".Translate(lowest.ToStringPercent()));
            // The capacity the replaced part drives — what the Movement and
            // Consciousness priorities sort on.
            text.AppendLine("IMP_TipCapacity".Translate(CapacityLabel(entry)));
            // Elite Bionics Framework's body part HP change, as declared.
            if (ImplantQualities.TryMaxHpAdjustment(entry.Def, out int linear, out float scale))
            {
                string hp = "";
                if (scale != 1f) hp = scale.ToStringPercent();
                if (linear != 0)
                    hp = (hp.Length > 0 ? hp + " " : "") + linear.ToStringWithSign();
                text.AppendLine("IMP_TipMaxHp".Translate(hp));
            }
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
