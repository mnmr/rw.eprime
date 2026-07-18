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
        public RoleHolderMode holderMode;
        public bool holderRangeSet;
        public int minHolders;
        public int maxHolders = RoleHolderRange.Uncapped;
        public List<JobEntry> entries = new List<JobEntry>();

        public const int AllHours = 0xFFFFFF;
    }

    /// One training path as the file carries it: role NAMES with skill bands
    /// (resolved to ids on import), plus the optional assignment anchor.
    public class FileTrainingPath
    {
        public string name;
        public string colorRef;   // color NAME, like a role's; null = no override
        public string anchorRole; // null = no anchor
        public bool anchorBefore = true;
        public List<(string role, int min, int max)> entries =
            new List<(string, int, int)>();
    }

    public class RoleFileDocument
    {
        public List<(string name, ColorRgb color)> palette = new List<(string, ColorRgb)>();
        /// User group names in display order (the Default group is never listed).
        public List<string> groups = new List<string>();
        public List<FileRole> roles = new List<FileRole>();
        public List<FileTrainingPath> trainingPaths = new List<FileTrainingPath>();
        /// The stored recommendation-order template as role names; empty = the
        /// derived default (never exported).
        public List<string> recommendationOrder = new List<string>();
        public string error; // set when nothing usable could be parsed
    }

    /// The export file format: human-readable, hand-editable XML, versioned at the
    /// root, independent of the mod's save/sync internals. Parsing is LENIENT —
    /// malformed roles or palette entries are skipped, not fatal — and XML
    /// comments are ignored everywhere (only elements are read).
    public static class RoleFile
    {
        /// v2 added <Training> and <Holders>; v3 <TrainingPaths> and
        /// <RecommendationOrder>; v4 retired <Training> (paths own training)
        /// and gave <Holders> an inTraining attribute; v5 replaces the old
        /// holder floor/allowance with an explicit Auto/Never/Custom range.
        /// Parsing is lenient across versions (older readers ignore unknown
        /// elements, newer ones default absentees and skip retired ones).
        public const string FormatVersion = "5";

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
  - <Holders mode=""custom"" min=""2"" max=""4""/> sets an inclusive holder
    range. mode may be auto, never, or custom. Auto is the default when the
    element is absent. A max of 256 is displayed in-game as Uncapped.
  - A <TrainingPaths> <Path> lists <Role min=""0"" max=""8"">name</Role> skill bands
    on the 0..21 axis (21 = open top, spans at least 4 levels); the entry order
    is the assignment order. An optional color=""name"" attribute colors the
    path's chip (same color names as roles). <Anchor>name</Anchor> is where
    members slot into a colonist's list — before that role unless before=""false"".
  - <RecommendationOrder> lists <Role> names: importing it replaces the stored
    recommendation order (unlisted roles keep placing dynamically).
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

            if (doc.trainingPaths.Count > 0)
            {
                var paths = new XElement("TrainingPaths");
                foreach (var path in doc.trainingPaths)
                {
                    var el = new XElement("Path", new XAttribute("name", path.name ?? ""));
                    if (!string.IsNullOrEmpty(path.colorRef))
                        el.Add(new XAttribute("color", path.colorRef));
                    if (!string.IsNullOrEmpty(path.anchorRole))
                    {
                        var anchor = new XElement("Anchor", path.anchorRole);
                        if (!path.anchorBefore) anchor.Add(new XAttribute("before", "false"));
                        el.Add(anchor);
                    }
                    foreach (var (role, min, max) in path.entries)
                        el.Add(new XElement("Role",
                            new XAttribute("min", min), new XAttribute("max", max), role));
                    paths.Add(el);
                }
                root.Add(paths);
            }

            if (doc.recommendationOrder.Count > 0)
            {
                var order = new XElement("RecommendationOrder");
                foreach (var label in doc.recommendationOrder)
                    order.Add(new XElement("Role", label));
                root.Add(order);
            }
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
            if (role.holderMode != RoleHolderMode.Auto || role.holderRangeSet
                || role.minHolders != 0 || role.maxHolders != RoleHolderRange.Uncapped)
            {
                var holders = new XElement("Holders",
                    new XAttribute("mode", role.holderMode.ToString().ToLowerInvariant()));
                if (role.holderMode == RoleHolderMode.Custom || role.holderRangeSet
                    || role.minHolders != 0 || role.maxHolders != RoleHolderRange.Uncapped)
                {
                    holders.Add(new XAttribute("min", role.minHolders));
                    holders.Add(new XAttribute("max", role.maxHolders));
                }
                options.Add(holders);
            }
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
            bool v5Holders = int.TryParse(root.Attribute("version")?.Value, out int version)
                && version >= 5;
            foreach (var roleEl in root.Element("Roles")?.Elements("Role")
                     ?? Enumerable.Empty<XElement>())
            {
                var role = ParseRole(roleEl, v5Holders);
                if (role != null) doc.roles.Add(role);
            }
            foreach (var pathEl in root.Element("TrainingPaths")?.Elements("Path")
                     ?? Enumerable.Empty<XElement>())
            {
                var path = ParseTrainingPath(pathEl);
                if (path != null) doc.trainingPaths.Add(path);
            }
            foreach (var roleEl in root.Element("RecommendationOrder")?.Elements("Role")
                     ?? Enumerable.Empty<XElement>())
            {
                string label = roleEl.Value?.Trim();
                if (!string.IsNullOrEmpty(label)) doc.recommendationOrder.Add(label);
            }
            if (doc.roles.Count == 0 && doc.palette.Count == 0 && doc.error == null)
                doc.error = "empty document";
            return doc;
        }

        private static FileTrainingPath ParseTrainingPath(XElement el)
        {
            string name = el.Attribute("name")?.Value?.Trim();
            if (string.IsNullOrEmpty(name)) return null;
            var path = new FileTrainingPath { name = name };
            string colorRef = el.Attribute("color")?.Value?.Trim();
            if (!string.IsNullOrEmpty(colorRef)) path.colorRef = colorRef;
            var anchor = el.Element("Anchor");
            if (!string.IsNullOrEmpty(anchor?.Value?.Trim()))
            {
                path.anchorRole = anchor.Value.Trim();
                path.anchorBefore = anchor.Attribute("before")?.Value?.Trim() != "false";
            }
            foreach (var roleEl in el.Elements("Role"))
            {
                string label = roleEl.Value?.Trim();
                int.TryParse(roleEl.Attribute("min")?.Value, out int min);
                int.TryParse(roleEl.Attribute("max")?.Value, out int max);
                // Bands the geometry rules reject are skipped, not fatal.
                if (string.IsNullOrEmpty(label)
                    || min < 0 || max > SkillProgressionMath.MaxLevel
                    || max - min < SkillProgressionMath.MinSpan) continue;
                path.entries.Add((label, min, max));
            }
            return path;
        }

        /// Resolves a path's entry names via idOf (null = unknown role):
        /// unresolved or duplicate entries drop, their bands ride along.
        public static (List<int> ids, List<int> mins, List<int> maxes) ResolvePathEntries(
            FileTrainingPath path, Func<string, int?> idOf)
        {
            var ids = new List<int>();
            var mins = new List<int>();
            var maxes = new List<int>();
            foreach (var (role, min, max) in path.entries)
            {
                int? id = idOf(role);
                if (id == null || ids.Contains(id.Value)) continue;
                ids.Add(id.Value);
                mins.Add(min);
                maxes.Add(max);
            }
            return (ids, mins, maxes);
        }

        private static FileRole ParseRole(XElement el, bool v5Holders)
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
                // <Training> (v2/v3) is retired: skipped, never read.
                var holders = options.Element("Holders");
                if (holders != null && v5Holders)
                {
                    string mode = holders.Attribute("mode")?.Value?.Trim();
                    if (string.Equals(mode, "never", StringComparison.OrdinalIgnoreCase))
                        role.holderMode = RoleHolderMode.Never;
                    else if (string.Equals(mode, "custom", StringComparison.OrdinalIgnoreCase))
                        role.holderMode = RoleHolderMode.Custom;
                    if (int.TryParse(holders.Attribute("min")?.Value, out int min))
                        role.minHolders = RoleHolderRange.Clamp(min);
                    if (int.TryParse(holders.Attribute("max")?.Value, out int max))
                        role.maxHolders = RoleHolderRange.Clamp(max);
                    role.holderRangeSet = holders.Attribute("min") != null
                        && holders.Attribute("max") != null;
                    if (role.minHolders > role.maxHolders)
                        role.maxHolders = role.minHolders;
                }
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
