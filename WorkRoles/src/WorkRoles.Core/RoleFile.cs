using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace WorkRoles.Core
{
    /// One color, game-engine-agnostic (0..1 channels).
    public readonly struct ColorRgb
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public ColorRgb(float r, float g, float b) { R = r; G = g; B = b; }

        public string Hex() =>
            "#" + ((int)Math.Round(R * 255)).ToString("x2")
                + ((int)Math.Round(G * 255)).ToString("x2")
                + ((int)Math.Round(B * 255)).ToString("x2");

        public static bool TryParseHex(string text, out ColorRgb color)
        {
            color = default;
            text = text?.Trim();
            if (text == null || text.Length != 7 || text[0] != '#') return false;
            if (!int.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return false;
            color = new ColorRgb((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f);
            return true;
        }
    }

    /// One role as the export file carries it. colorRef is a color NAME (built-in
    /// swatch or palette entry); locations hold LocationRules
    /// tokens with NAMES instead of save-local ids ("settlement:Boarwood").
    public class FileRole
    {
        public string label;
        public string templateDef;
        public string group;   // role-list group name; null = the Default group
        public string colorRef;
        public bool autoAssign;
        public bool blocker;
        public bool enabled = true;
        public int activeHours = AllHours;
        public List<string> locations = new List<string>();
        public List<JobEntry> entries = new List<JobEntry>();

        public const int AllHours = 0xFFFFFF;
    }

    public class RoleFileDocument
    {
        public List<(string name, ColorRgb color)> palette = new List<(string, ColorRgb)>();
        /// User group names in display order (the Default group is never listed).
        public List<string> groups = new List<string>();
        public List<FileRole> roles = new List<FileRole>();
        public string error; // set when nothing usable could be parsed
    }

    /// The export file format: human-readable, hand-editable XML, versioned at the
    /// root, independent of the mod's save/sync internals. Parsing is LENIENT —
    /// malformed roles or palette entries are skipped, not fatal — and XML
    /// comments are ignored everywhere (only elements are read).
    public static class RoleFile
    {
        public const string FormatVersion = "1";

        // Hand-editing help, embedded in every export. Non-obvious parts only.
        private const string FormatNotes = @"
  Format notes:
  - Palette entries define custom colors (#rrggbb). Roles reference a color by
    NAME only: a palette entry or a built-in color name (e.g. ""red-800"").
  - A Role's <Options> only lists non-defaults. ActiveHours is a 24-character
    bitstring, hour 0 leftmost, 1 = active. <Locations> holds any of:
    <Settlements/> (any settlement), <Caravans/> (caravans and away maps),
    <Settlement name=""...""/> and <Ship name=""...""/> (matched by name).
  - The order of <Jobs> IS the priority order. <WorkType> covers every job of
    that work type, including jobs mods add later; <WorkGiver> is one job.
  - <Groups> lists role-list groups in display order; a Role joins one via its
    group attribute (unlisted names still work; no attribute = Default).
";
        private const string PaletteSample = @" <Color name=""ocean"">#0e7490</Color> ";

        public static string Build(RoleFileDocument doc)
        {
            var root = new XElement("WorkRoles", new XAttribute("version", FormatVersion));
            root.Add(new XComment(FormatNotes));

            var palette = new XElement("Palette");
            if (doc.palette.Count > 0)
                foreach (var (name, color) in doc.palette)
                    palette.Add(new XElement("Color", new XAttribute("name", name), color.Hex()));
            else
                palette.Add(new XComment(PaletteSample)); // syntax sample, not imported
            root.Add(palette);

            if (doc.groups.Count > 0)
            {
                // Element form so future per-group options land as attributes.
                var groups = new XElement("Groups");
                foreach (var name in doc.groups)
                    groups.Add(new XElement("Group", new XAttribute("name", name)));
                root.Add(groups);
            }

            var roles = new XElement("Roles");
            foreach (var role in doc.roles)
                roles.Add(Encode(role));
            root.Add(roles);
            return root.ToString();
        }

        private static XElement Encode(FileRole role)
        {
            var element = new XElement("Role", new XAttribute("name", role.label ?? ""));
            if (!string.IsNullOrEmpty(role.templateDef))
                element.Add(new XAttribute("id", role.templateDef));
            if (!string.IsNullOrEmpty(role.group))
                element.Add(new XAttribute("group", role.group));

            var options = new XElement("Options");
            if (!string.IsNullOrEmpty(role.colorRef))
                options.Add(new XElement("Color", role.colorRef));
            if (role.autoAssign)
                options.Add(new XElement("AutoAssign", "true"));
            if (role.blocker)
                options.Add(new XElement("Blocker", "true"));
            if (!role.enabled)
                options.Add(new XElement("Enabled", "false"));
            if (role.activeHours != FileRole.AllHours)
                options.Add(new XElement("ActiveHours", HoursToBits(role.activeHours)));
            if (role.locations.Count > 0)
            {
                // Structured elements so names (XLinq-escaped) survive any
                // characters a player can type.
                var locations = new XElement("Locations");
                foreach (var token in role.locations)
                {
                    if (token == LocationRules.Settlements)
                        locations.Add(new XElement("Settlements"));
                    else if (token == LocationRules.Caravans)
                        locations.Add(new XElement("Caravans"));
                    else if (token.StartsWith(LocationRules.SettlementPrefix))
                        locations.Add(new XElement("Settlement",
                            new XAttribute("name", token.Substring(LocationRules.SettlementPrefix.Length))));
                    else if (token.StartsWith(LocationRules.ShipPrefix))
                        locations.Add(new XElement("Ship",
                            new XAttribute("name", token.Substring(LocationRules.ShipPrefix.Length))));
                }
                if (locations.HasElements)
                    options.Add(locations);
            }
            if (options.HasElements)
                element.Add(options);

            // One ordered list; the element name carries the entry kind (order IS
            // priority, so types and givers must not be split into separate lists).
            var jobs = new XElement("Jobs");
            foreach (var entry in role.entries)
                jobs.Add(new XElement(
                    entry.Kind == JobEntryKind.WorkType ? "WorkType" : "WorkGiver", entry.DefName));
            element.Add(jobs);
            return element;
        }

        public static RoleFileDocument Parse(string xml)
        {
            var doc = new RoleFileDocument();
            XElement root;
            try { root = XElement.Parse(xml); }
            catch (Exception e)
            {
                doc.error = e.Message;
                return doc;
            }
            foreach (var colorEl in root.Element("Palette")?.Elements("Color")
                     ?? Enumerable.Empty<XElement>())
            {
                string name = colorEl.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name) && ColorRgb.TryParseHex(colorEl.Value, out var color)
                    && doc.palette.All(p => p.name != name))
                    doc.palette.Add((name, color));
            }
            foreach (var groupEl in root.Element("Groups")?.Elements("Group")
                     ?? Enumerable.Empty<XElement>())
            {
                string name = groupEl.Attribute("name")?.Value?.Trim();
                if (!string.IsNullOrEmpty(name)
                    && !doc.groups.Contains(name, StringComparer.OrdinalIgnoreCase))
                    doc.groups.Add(name);
            }
            foreach (var roleEl in root.Element("Roles")?.Elements("Role")
                     ?? Enumerable.Empty<XElement>())
            {
                var role = ParseRole(roleEl);
                if (role != null) doc.roles.Add(role);
            }
            if (doc.roles.Count == 0 && doc.palette.Count == 0 && doc.error == null)
                doc.error = "empty document";
            return doc;
        }

        private static FileRole ParseRole(XElement el)
        {
            string label = el.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(label)) return null;
            var role = new FileRole
            {
                label = label,
                templateDef = el.Attribute("id")?.Value,
                group = el.Attribute("group")?.Value?.Trim(),
            };
            if (string.IsNullOrEmpty(role.group)) role.group = null;
            var options = el.Element("Options");
            if (options != null)
            {
                role.colorRef = options.Element("Color")?.Value.Trim();
                role.autoAssign = options.Element("AutoAssign")?.Value.Trim() == "true";
                role.blocker = options.Element("Blocker")?.Value.Trim() == "true";
                role.enabled = options.Element("Enabled")?.Value.Trim() != "false";
                foreach (var loc in options.Element("Locations")?.Elements()
                         ?? Enumerable.Empty<XElement>())
                {
                    string name = loc.Attribute("name")?.Value;
                    if (loc.Name == "Settlements") role.locations.Add(LocationRules.Settlements);
                    else if (loc.Name == "Caravans") role.locations.Add(LocationRules.Caravans);
                    else if (loc.Name == "Settlement" && !string.IsNullOrEmpty(name))
                        role.locations.Add(LocationRules.SettlementPrefix + name);
                    else if (loc.Name == "Ship" && !string.IsNullOrEmpty(name))
                        role.locations.Add(LocationRules.ShipPrefix + name);
                }
                string bits = options.Element("ActiveHours")?.Value.Trim();
                if (bits != null && bits.Length == 24)
                    role.activeHours = BitsToHours(bits);
            }
            foreach (var job in el.Element("Jobs")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                if (job.Name == "WorkType")
                    role.entries.Add(new JobEntry(JobEntryKind.WorkType, job.Value.Trim()));
                else if (job.Name == "WorkGiver")
                    role.entries.Add(new JobEntry(JobEntryKind.WorkGiver, job.Value.Trim()));
            }
            return role;
        }

        /// 24-char bitstring, hour 0 leftmost; '1' = active during that hour.
        public static string HoursToBits(int mask)
        {
            var bits = new StringBuilder(24);
            for (int hour = 0; hour < 24; hour++)
                bits.Append((mask >> hour & 1) == 1 ? '1' : '0');
            return bits.ToString();
        }

        public static int BitsToHours(string bits)
        {
            int mask = 0;
            for (int hour = 0; hour < 24 && hour < bits.Length; hour++)
                if (bits[hour] == '1')
                    mask |= 1 << hour;
            return mask;
        }
    }
}
