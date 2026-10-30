using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core
{
    /// Detached portable options. A missing section preserves its destination;
    /// a present section replaces its domain, with defaults for omitted keys.
    public sealed class SetupOptions
    {
        public Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<string>? skillColumns;

        public XElement ToXml(string section)
        {
            var result = new XElement(section);
            foreach (string key in values.Keys.OrderBy(k => k, StringComparer.Ordinal))
                result.Add(new XElement("Option", new XAttribute("name", key), values[key]));
            if (skillColumns != null)
                result.Add(new XElement("SkillColumns", skillColumns.Select(s => new XElement("Skill", s))));
            return result;
        }

        public static SetupOptions? Read(XElement? section, bool player)
        {
            if (section == null) return null;
            var result = new SetupOptions();
            foreach (XElement item in section.Elements("Option"))
            {
                string key = ((string?)item.Attribute("name") ?? "").Trim();
                if (key.Length == 0 || result.values.ContainsKey(key))
                    throw new FormatException("Invalid or duplicate setup option: " + key);
                result.values.Add(key, Normalize(key, item.Value.Trim(), player));
            }
            if (player && section.Element("SkillColumns") is XElement skills)
                result.skillColumns = skills.Elements("Skill").Select(s => s.Value.Trim())
                    .Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            return result;
        }

        private static string Normalize(string key, string value, bool player)
        {
            string[] booleans = player ? PlayerBooleans : GameBooleans;
            if (Array.IndexOf(booleans, key) >= 0)
            {
                if (!bool.TryParse(value, out bool flag)) throw new FormatException("Invalid boolean: " + key);
                return flag ? "true" : "false";
            }
            if (!player && key.StartsWith("recommendation.", StringComparison.Ordinal)
                && RecommendationsTuningOptions.TryOption(key.Substring(15), out var option))
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    throw new FormatException("Invalid recommendation option: " + key);
                foreach (var descriptor in RecommendationsTuningOptions.Descriptors)
                    if (descriptor.Option == option)
                        return Math.Max(descriptor.MinimumValue, Math.Min(descriptor.MaximumValue, number)).ToString(CultureInfo.InvariantCulture);
            }
            int max = key == "chipGridNames" ? 4 : key == "chipDisplay" ? 2
                : key == "colonistOrder" || key == "paletteMode" ? 1 : -1;
            if (player && max >= 0)
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                    || number < 0 || number > max) throw new FormatException("Invalid display option: " + key);
                return number.ToString(CultureInfo.InvariantCulture);
            }
            if (player && (key == "windowWidth" || key == "windowHeight"))
            {
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                    || float.IsNaN(number) || float.IsInfinity(number) || number < 0)
                    throw new FormatException("Invalid window dimension: " + key);
                return number.ToString("R", CultureInfo.InvariantCulture);
            }
            return value; // Future options survive older readers; bridges apply only known keys.
        }

        public static readonly string[] GameBooleans = { "useWorkPriorities", "reportVanillaPriorities",
            "rolePriorityEmergencyRule", "autoOptimize", "mechanicalWorkersEnabled", "mechanicalRecommendationsEnabled" };
        public static readonly string[] PlayerBooleans = { "chipGrid", "nestedRoleTree", "verdictsOnColonistChips",
            "verdictsInPalette", "verdictsOnRecommendationChips", "colonistSkillCaptions" };
    }
}
