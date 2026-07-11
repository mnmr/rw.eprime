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
    /// swatch or palette entry) or a #hex literal; location is the raw enum word.
    public class FileRole
    {
        public string label;
        public string templateDef;
        public string colorRef;
        public bool autoAssign;
        public bool blocker;
        public bool enabled = true;
        public int activeHours = AllHours;
        public string location;
        public List<JobEntry> entries = new List<JobEntry>();

        public const int AllHours = 0xFFFFFF;
    }

    public class RoleFileDocument
    {
        public List<(string name, ColorRgb color)> palette = new List<(string, ColorRgb)>();
        public List<FileRole> roles = new List<FileRole>();
        public string error; // set when nothing usable could be parsed
    }

    /// The export file format: human-readable, hand-editable XML, versioned at the
    /// root, independent of the mod's save/sync internals. Parsing is LENIENT —
    /// malformed roles or palette entries are skipped, not fatal.
    public static class RoleFile
    {
        public const string FormatVersion = "1";

        public static string Build(RoleFileDocument doc)
        {
            var root = new XElement("Roles", new XAttribute("version", FormatVersion));
            if (doc.palette.Count > 0)
            {
                var palette = new XElement("Palette");
                foreach (var (name, color) in doc.palette)
                    palette.Add(new XElement("Color", new XAttribute("name", name), color.Hex()));
                root.Add(palette);
            }
            foreach (var role in doc.roles)
                root.Add(Encode(role));
            return root.ToString();
        }

        private static XElement Encode(FileRole role)
        {
            var element = new XElement("Role", new XAttribute("name", role.label ?? ""));
            if (!string.IsNullOrEmpty(role.templateDef))
                element.Add(new XAttribute("id", role.templateDef));

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
            if (!string.IsNullOrEmpty(role.location) && role.location != "Any")
                options.Add(new XElement("Location", role.location));
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
            foreach (var roleEl in root.Elements("Role"))
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
            var role = new FileRole { label = label, templateDef = el.Attribute("id")?.Value };
            var options = el.Element("Options");
            if (options != null)
            {
                role.colorRef = options.Element("Color")?.Value.Trim();
                role.autoAssign = options.Element("AutoAssign")?.Value.Trim() == "true";
                role.blocker = options.Element("Blocker")?.Value.Trim() == "true";
                role.enabled = options.Element("Enabled")?.Value.Trim() != "false";
                role.location = options.Element("Location")?.Value.Trim();
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
