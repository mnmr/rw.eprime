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
        public string fileId;
        public string label;
        public string templateDef;
        public string group;   // role-list group name; null = the Default group
        public string groupId;
        public string colorRef;
        public bool autoAssign;
        public bool blocker;
        public bool enabled = true;
        public int activeHours = AllHours;
        public List<string> locations = new List<string>();
        /// Legacy named scale reference: parsed from old files for the
        /// colonyMin/coverage migration, never emitted.
        public string holderScale;
        /// Recommendation tuning; hasTuning=false marks a pre-tuning file whose
        /// roles derive their skill classification on import.
        public bool hasTuning;
        public RoleCategory category;
        public RoleTime time;
        public bool championPenalty = true;
        /// Minimum holding age in years; -1 = absent (pre-minAge file).
        public int minAge = -1;
        /// Assignment scaling inputs (v11): minimum assignment count and
        /// ideal colonist percentage.
        public int colonyMin;
        public int coverage;
        public List<string> requiredSkills = new List<string>();
        public List<string> optionalSkills = new List<string>();
        /// Role-owned training path (v11): the role itself plus its training
        /// roles with skill bands. Empty = the implicit self-only path.
        public List<FileTrainingPathEntry> training =
            new List<FileTrainingPathEntry>();
        public List<JobEntry> entries = new List<JobEntry>();

        public const int AllHours = 0xFFFFFF;
    }

    public class FileGroup
    {
        public string fileId;
        public string name;

        public static implicit operator FileGroup(string name) => new FileGroup { name = name };
        public override string ToString() => name ?? "";
    }

    public class FileRoleReference
    {
        public string fileId;
        public string label;

        public FileRoleReference() { }
        public FileRoleReference(string fileId, string label)
        {
            this.fileId = fileId;
            this.label = label;
        }

        public static implicit operator FileRoleReference(string label) =>
            new FileRoleReference(null, label);
        public override string ToString() => label ?? "";
    }

    public class FileTrainingPathEntry
    {
        public FileRoleReference role;
        public int min;
        public int max;

        public FileTrainingPathEntry(string label, int min, int max)
            : this(null, label, min, max) { }

        public FileTrainingPathEntry(string fileId, string label, int min, int max)
        {
            role = new FileRoleReference(fileId, label);
            this.min = min;
            this.max = max;
        }

        public static implicit operator FileTrainingPathEntry(
            (string role, int min, int max) entry) =>
            new FileTrainingPathEntry(entry.role, entry.min, entry.max);
    }

    /// One training path as the file carries it: document-local role ids plus
    /// display labels for legacy fallback, with skill bands and an anchor.
    public class FileTrainingPath
    {
        public string name;
        public string colorRef;   // color NAME, like a role's; null = no override
        public string anchorRole; // null = no anchor
        public string anchorRoleId;
        public FileRoleReference anchorWithId;
        public bool anchorBefore = true;
        // Compatibility surface shipped by formats 1-6. Keep this exact field
        // type: external callers compile direct field references against it.
        public List<(string role, int min, int max)> entries =
            new List<(string role, int min, int max)>();
        // Format-7 metadata is valid only while it remains index-for-index
        // aligned with entries. Public legacy fields stay authoritative.
        public List<FileTrainingPathEntry> entriesWithIds =
            new List<FileTrainingPathEntry>();
    }

    public class RoleFileDocument
    {
        public List<(string name, ColorRgb color)> palette = new List<(string, ColorRgb)>();
        /// User group names in display order (the Default group is never listed).
        public List<string> groups = new List<string>();
        public List<FileGroup> groupsWithIds = new List<FileGroup>();
        public List<FileRole> roles = new List<FileRole>();
        public List<FileTrainingPath> trainingPaths = new List<FileTrainingPath>();
        /// Legacy named strategies: parsed from old files so role references
        /// can convert to colonyMin/coverage on import, never emitted.
        public List<RoleAssignmentStrategy> scales =
            new List<RoleAssignmentStrategy>();
        /// The stored recommendation-order template as role names; empty = the
        /// derived default (never exported).
        public List<string> recommendationOrder = new List<string>();
        public List<FileRoleReference> recommendationOrderWithIds =
            new List<FileRoleReference>();
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
        /// holder floor/allowance with an explicit Auto/Never/Custom range;
        /// v6 adds the Custom training-waiver count; v7 adds document-local
        /// role/group ids and id-backed references while retaining labels;
        /// v8 adds named <Scales> (banded holder demand) and the Holders
        /// scale attribute referencing them; v9 adds the internal <Nowhere/>
        /// location used to preserve disabled migrated roles; v10 removes the
        /// obsolete scalar holder mode/range attributes; v11 moves training
        /// onto the owning role (<Tuning><Training>) and retires the
        /// stand-alone <TrainingPaths> section (still parsed for import).
        /// Parsing is lenient across versions (older readers ignore unknown
        /// elements, newer ones default absentees and skip retired ones).
        public const string FormatVersion = "11";

        // Hand-editing help, embedded in every export. Non-obvious parts only.
        private const string FormatNotes = @"
  Format notes:
  - Palette entries define custom colors (#rrggbb). Roles reference a color by
    NAME only: a palette entry or a built-in color name (e.g. ""red-800"").
  - A Role's <Options> only lists non-defaults. ActiveHours is a 24-character
    bitstring, hour 0 leftmost, 1 = active. <Locations> holds any of:
    <Settlements/> (any settlement), <Caravans/> (caravans and away maps),
    <Nowhere/> (an intentionally disabled migrated location rule),
    <Settlement name=""...""/> and <Ship name=""...""/> (matched by name).
  - The order of <Jobs> IS the priority order. <WorkType> covers every job of
    that work type, including jobs mods add later; <WorkGiver> is one job.
  - <Groups> lists role-list groups in display order; a Role joins one via its
    groupId reference (the group label remains for display and legacy fallback;
    no reference = Default). fileId values are local to this document only.
  - A Role's <Tuning> may hold <Training> with <Role roleId=""..."" min=""0"" max=""8"">name</Role>
    skill bands on the 0..21 axis (21 = open top, spans at least 4 levels): the
    role's own training path (the role itself plus its training roles).
    Assignment order uses band minimum descending, then the pawn's weakest role
    skill; entry order is the final tie-breaker. Legacy <TrainingPaths>
    sections still import and fold into their target role.
  - <RecommendationOrder> lists roleId references with labels: importing it
    replaces the stored recommendation order (unlisted roles place dynamically).
  - A Role's <Tuning> colonyMin and coverage attributes hold its assignment
    demand: the minimum assignment count and the ideal colonist percentage.
    Legacy <Scales> sections and <Holders scale=""name""/> references still
    import and convert to these numbers.
";
        private const string PaletteSample = @" <Color name=""ocean"">#0e7490</Color> ";

        public static string Build(RoleFileDocument doc)
        {
            doc ??= new RoleFileDocument();
            var root = new XElement("WorkRoles", new XAttribute("version", FormatVersion));
            root.Add(new XComment(FormatNotes));

            var palette = new XElement("Palette");
            if (doc.palette?.Count > 0)
                foreach (var (name, color) in doc.palette)
                    palette.Add(new XElement("Color", new XAttribute("name", name), color.Hex()));
            else
                palette.Add(new XComment(PaletteSample)); // syntax sample, not imported
            root.Add(palette);

            IReadOnlyList<FileGroup> effectiveGroups = GroupsWithStableIds(doc);
            if (effectiveGroups.Count > 0)
            {
                // Element form so future per-group options land as attributes.
                var groups = new XElement("Groups");
                foreach (var group in effectiveGroups)
                {
                    var element = new XElement("Group", new XAttribute("name", group.name ?? ""));
                    if (!string.IsNullOrEmpty(group.fileId))
                        element.Add(new XAttribute("fileId", group.fileId));
                    groups.Add(element);
                }
                root.Add(groups);
            }

            var roles = new XElement("Roles");
            if (doc.roles != null)
                foreach (var role in doc.roles)
                    if (role != null)
                        roles.Add(Encode(role));
            root.Add(roles);

            if (doc.trainingPaths?.Count > 0)
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
                        FileRoleReference anchorReference = AnchorWithStableId(path);
                        if (!string.IsNullOrEmpty(anchorReference?.fileId))
                            anchor.Add(new XAttribute("roleId", anchorReference.fileId));
                        if (!path.anchorBefore) anchor.Add(new XAttribute("before", "false"));
                        el.Add(anchor);
                    }
                    foreach (var entry in EntriesWithStableIds(path))
                    {
                        var role = new XElement("Role",
                            new XAttribute("min", entry.min), new XAttribute("max", entry.max),
                            entry.role?.label ?? "");
                        if (!string.IsNullOrEmpty(entry.role?.fileId))
                            role.Add(new XAttribute("roleId", entry.role.fileId));
                        el.Add(role);
                    }
                    paths.Add(el);
                }
                root.Add(paths);
            }

            IReadOnlyList<FileRoleReference> effectiveOrder =
                RecommendationOrderWithStableIds(doc);
            if (effectiveOrder.Count > 0)
            {
                var order = new XElement("RecommendationOrder");
                foreach (var reference in effectiveOrder)
                {
                    var role = new XElement("Role", reference?.label ?? "");
                    if (!string.IsNullOrEmpty(reference?.fileId))
                        role.Add(new XAttribute("roleId", reference.fileId));
                    order.Add(role);
                }
                root.Add(order);
            }
            return root.ToString();
        }

        private static XElement Encode(FileRole role)
        {
            var element = new XElement("Role", new XAttribute("name", role.label ?? ""));
            if (!string.IsNullOrEmpty(role.fileId))
                element.Add(new XAttribute("fileId", role.fileId));
            if (!string.IsNullOrEmpty(role.templateDef))
                element.Add(new XAttribute("id", role.templateDef));
            if (!string.IsNullOrEmpty(role.group))
                element.Add(new XAttribute("group", role.group));
            if (!string.IsNullOrEmpty(role.groupId))
                element.Add(new XAttribute("groupId", role.groupId));

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
            if (role.hasTuning)
            {
                // Present-but-empty still matters: it marks authored tuning.
                var tuning = new XElement("Tuning");
                if (role.category != RoleCategory.None)
                    tuning.Add(new XAttribute("category", role.category));
                if (role.time != RoleTime.None)
                    tuning.Add(new XAttribute("time", role.time));
                if (!role.championPenalty)
                    tuning.Add(new XAttribute("championPenalty", "false"));
                if (role.minAge >= 0)
                    tuning.Add(new XAttribute("minAge", role.minAge));
                if (role.colonyMin != 0)
                    tuning.Add(new XAttribute("colonyMin", role.colonyMin));
                if (role.coverage != 0)
                    tuning.Add(new XAttribute("coverage", role.coverage));
                if (role.requiredSkills.Count > 0)
                    tuning.Add(new XElement("RequiredSkills",
                        string.Join(",", role.requiredSkills)));
                if (role.optionalSkills.Count > 0)
                    tuning.Add(new XElement("OptionalSkills",
                        string.Join(",", role.optionalSkills)));
                if (role.training.Count > 0)
                {
                    var training = new XElement("Training");
                    foreach (var entry in role.training)
                    {
                        var entryEl = new XElement("Role",
                            new XAttribute("min", entry.min),
                            new XAttribute("max", entry.max),
                            entry.role?.label ?? "");
                        if (!string.IsNullOrEmpty(entry.role?.fileId))
                            entryEl.Add(new XAttribute("roleId", entry.role.fileId));
                        training.Add(entryEl);
                    }
                    tuning.Add(training);
                }
                options.Add(tuning);
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
                    else if (token == LocationRules.Nowhere)
                        locations.Add(new XElement("Nowhere"));
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
                string fileId = groupEl.Attribute("fileId")?.Value?.Trim();
                if (!string.IsNullOrEmpty(name)
                    && (!string.IsNullOrEmpty(fileId)
                        || !doc.groups.Any(group => string.Equals(
                            group, name, StringComparison.OrdinalIgnoreCase))))
                {
                    doc.groups.Add(name);
                    doc.groupsWithIds.Add(new FileGroup
                        { fileId = EmptyToNull(fileId), name = name });
                }
            }
            foreach (var roleEl in root.Element("Roles")?.Elements("Role")
                     ?? Enumerable.Empty<XElement>())
            {
                var role = ParseRole(roleEl);
                if (role != null) doc.roles.Add(role);
            }
            foreach (var scaleEl in root.Element("Scales")?.Elements("Scale")
                     ?? Enumerable.Empty<XElement>())
            {
                string name = scaleEl.Attribute("name")?.Value?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                bool preset = string.Equals(scaleEl.Attribute("preset")?.Value,
                    "true", StringComparison.OrdinalIgnoreCase);
                var bands = new HolderScale
                {
                    RequiredTotals = HolderScaleCodec.DecodeRow(
                        scaleEl.Element("Min")?.Value, 0),
                    TrainingWaivers = HolderScaleCodec.DecodeRow(
                        scaleEl.Element("Train")?.Value, 0),
                    Max = HolderScaleCodec.DecodeRow(
                        scaleEl.Element("Max")?.Value, RoleHolderRange.Uncapped),
                };
                doc.scales.Add(RoleAssignmentStrategy.FromRows(
                    name, preset, scaleEl.Attribute("mode")?.Value, bands));
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
                if (!string.IsNullOrEmpty(label))
                {
                    doc.recommendationOrder.Add(label);
                    doc.recommendationOrderWithIds.Add(new FileRoleReference(
                        EmptyToNull(roleEl.Attribute("roleId")?.Value?.Trim()), label));
                }
            }
            if (doc.roles.Count == 0)
                doc.error = "document contains no valid roles";
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
                path.anchorRoleId = EmptyToNull(anchor.Attribute("roleId")?.Value?.Trim());
                path.anchorWithId = new FileRoleReference(path.anchorRoleId, path.anchorRole);
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
                path.entriesWithIds.Add(new FileTrainingPathEntry(
                    EmptyToNull(roleEl.Attribute("roleId")?.Value?.Trim()), label, min, max));
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
            foreach (var entry in path.entries)
            {
                int? id = idOf(entry.role);
                if (id == null || ids.Contains(id.Value)) continue;
                ids.Add(id.Value);
                mins.Add(entry.min);
                maxes.Add(entry.max);
            }
            return (ids, mins, maxes);
        }

        public static (List<int> ids, List<int> mins, List<int> maxes) ResolvePathEntries(
            FileTrainingPath path, RoleFileDocument document, Func<FileRole, int?> idOf)
        {
            var ids = new List<int>();
            var mins = new List<int>();
            var maxes = new List<int>();
            foreach (var entry in EntriesWithStableIds(path))
            {
                var role = ResolveRole(document, entry.role.fileId, entry.role.label);
                int? id = role == null ? null : idOf(role);
                if (id == null || ids.Contains(id.Value)) continue;
                ids.Add(id.Value);
                mins.Add(entry.min);
                maxes.Add(entry.max);
            }
            return (ids, mins, maxes);
        }

        public static FileRole ResolveRole(RoleFileDocument document, string fileId, string label)
        {
            FileRole byId = UniqueById(document?.roles, fileId, role => role.fileId);
            if (byId != null) return byId;
            return document?.roles?.FirstOrDefault(role => role != null
                && string.Equals(role.label, label, StringComparison.OrdinalIgnoreCase));
        }

        public static FileGroup ResolveGroup(RoleFileDocument document, string fileId, string name)
        {
            IReadOnlyList<FileGroup> groups = GroupsWithStableIds(document);
            FileGroup byId = UniqueById(groups, fileId, group => group.fileId);
            if (byId != null) return byId;
            return groups.FirstOrDefault(group => group != null
                && string.Equals(group.name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// Rich format-7 metadata is advisory. It is used only while every item
        /// remains aligned with the legacy public collection callers may mutate.
        public static IReadOnlyList<FileGroup> GroupsWithStableIds(RoleFileDocument document)
        {
            List<string> legacy = document?.groups;
            if (legacy == null || legacy.Count == 0) return Array.Empty<FileGroup>();
            List<FileGroup> rich = document.groupsWithIds;
            if (rich != null && rich.Count == legacy.Count)
            {
                bool aligned = true;
                for (int i = 0; i < legacy.Count; i++)
                    if (rich[i] == null || !string.Equals(
                            rich[i].name, legacy[i], StringComparison.Ordinal))
                    {
                        aligned = false;
                        break;
                    }
                if (aligned) return rich;
            }

            var fallback = new List<FileGroup>(legacy.Count);
            for (int i = 0; i < legacy.Count; i++)
                fallback.Add(new FileGroup { name = legacy[i] });
            return fallback;
        }

        public static IReadOnlyList<FileRoleReference> RecommendationOrderWithStableIds(
            RoleFileDocument document)
        {
            List<string> legacy = document?.recommendationOrder;
            if (legacy == null || legacy.Count == 0)
                return Array.Empty<FileRoleReference>();
            List<FileRoleReference> rich = document.recommendationOrderWithIds;
            if (rich != null && rich.Count == legacy.Count)
            {
                bool aligned = true;
                for (int i = 0; i < legacy.Count; i++)
                    if (rich[i] == null || !string.Equals(
                            rich[i].label, legacy[i], StringComparison.Ordinal))
                    {
                        aligned = false;
                        break;
                    }
                if (aligned) return rich;
            }

            var fallback = new List<FileRoleReference>(legacy.Count);
            for (int i = 0; i < legacy.Count; i++)
                fallback.Add(new FileRoleReference(null, legacy[i]));
            return fallback;
        }

        public static IReadOnlyList<FileTrainingPathEntry> EntriesWithStableIds(
            FileTrainingPath path)
        {
            List<(string role, int min, int max)> legacy = path?.entries;
            if (legacy == null || legacy.Count == 0)
                return Array.Empty<FileTrainingPathEntry>();
            List<FileTrainingPathEntry> rich = path.entriesWithIds;
            if (rich != null && rich.Count == legacy.Count)
            {
                bool aligned = true;
                for (int i = 0; i < legacy.Count; i++)
                {
                    FileTrainingPathEntry candidate = rich[i];
                    if (candidate?.role == null
                        || !string.Equals(candidate.role.label,
                            legacy[i].role, StringComparison.Ordinal)
                        || candidate.min != legacy[i].min
                        || candidate.max != legacy[i].max)
                    {
                        aligned = false;
                        break;
                    }
                }
                if (aligned) return rich;
            }

            var fallback = new List<FileTrainingPathEntry>(legacy.Count);
            for (int i = 0; i < legacy.Count; i++)
                fallback.Add(new FileTrainingPathEntry(
                    legacy[i].role, legacy[i].min, legacy[i].max));
            return fallback;
        }

        public static FileRoleReference AnchorWithStableId(FileTrainingPath path)
        {
            if (path == null || string.IsNullOrEmpty(path.anchorRole)) return null;
            FileRoleReference rich = path.anchorWithId;
            if (rich != null
                && string.Equals(rich.label, path.anchorRole, StringComparison.Ordinal)
                && string.Equals(rich.fileId, path.anchorRoleId, StringComparison.Ordinal))
                return rich;
            return new FileRoleReference(null, path.anchorRole);
        }

        private static T UniqueById<T>(IEnumerable<T> items, string fileId, Func<T, string> idOf)
            where T : class
        {
            if (items == null || string.IsNullOrEmpty(fileId)) return null;
            T match = null;
            foreach (T item in items)
            {
                if (item == null) continue;
                if (!string.Equals(idOf(item), fileId, StringComparison.Ordinal)) continue;
                if (match != null) return null;
                match = item;
            }
            return match;
        }

        private static string EmptyToNull(string value) =>
            string.IsNullOrEmpty(value) ? null : value;

        private static FileRole ParseRole(XElement el)
        {
            string label = el.Attribute("name")?.Value?.Trim();
            if (string.IsNullOrEmpty(label)) return null;
            var role = new FileRole
            {
                fileId = EmptyToNull(el.Attribute("fileId")?.Value?.Trim()),
                label = label,
                templateDef = el.Attribute("id")?.Value,
                group = el.Attribute("group")?.Value?.Trim(),
                groupId = EmptyToNull(el.Attribute("groupId")?.Value?.Trim()),
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
                    else if (loc.Name == "Nowhere") role.locations.Add(LocationRules.Nowhere);
                    else if (loc.Name == "Settlement" && !string.IsNullOrEmpty(name))
                        role.locations.Add(LocationRules.SettlementPrefix + name);
                    else if (loc.Name == "Ship" && !string.IsNullOrEmpty(name))
                        role.locations.Add(LocationRules.ShipPrefix + name);
                }
                string bits = options.Element("ActiveHours")?.Value.Trim();
                if (bits != null && bits.Length == 24)
                    role.activeHours = BitsToHours(bits);
                // <Training> (v2/v3) is retired: skipped, never read.
                var tuningEl = options.Element("Tuning");
                if (tuningEl != null)
                {
                    role.hasTuning = true;
                    if (Enum.TryParse(tuningEl.Attribute("category")?.Value,
                            true, out RoleCategory category))
                        role.category = category;
                    if (Enum.TryParse(tuningEl.Attribute("time")?.Value,
                            true, out RoleTime time))
                        role.time = time;
                    role.championPenalty = !string.Equals(
                        tuningEl.Attribute("championPenalty")?.Value?.Trim(),
                        "false", StringComparison.OrdinalIgnoreCase);
                    if (int.TryParse(tuningEl.Attribute("minAge")?.Value,
                            out int minAge))
                        role.minAge = minAge;
                    if (int.TryParse(tuningEl.Attribute("colonyMin")?.Value,
                            out int colonyMin))
                        role.colonyMin = colonyMin;
                    if (int.TryParse(tuningEl.Attribute("coverage")?.Value,
                            out int coverage))
                        role.coverage = coverage;
                    role.requiredSkills = SplitSkills(
                        tuningEl.Element("RequiredSkills")?.Value);
                    role.optionalSkills = SplitSkills(
                        tuningEl.Element("OptionalSkills")?.Value);
                    foreach (var entryEl in tuningEl.Element("Training")?.Elements("Role")
                             ?? Enumerable.Empty<XElement>())
                    {
                        string entryLabel = entryEl.Value?.Trim();
                        int.TryParse(entryEl.Attribute("min")?.Value, out int min);
                        int.TryParse(entryEl.Attribute("max")?.Value, out int max);
                        // Bands the geometry rules reject are skipped, not fatal.
                        if (string.IsNullOrEmpty(entryLabel)
                            || min < 0 || max > SkillProgressionMath.MaxLevel
                            || max - min < SkillProgressionMath.MinSpan) continue;
                        role.training.Add(new FileTrainingPathEntry(
                            EmptyToNull(entryEl.Attribute("roleId")?.Value?.Trim()),
                            entryLabel, min, max));
                    }
                }
                var holders = options.Element("Holders");
                if (holders != null)
                {
                    string scaleName = holders.Attribute("scale")?.Value?.Trim();
                    if (!string.IsNullOrEmpty(scaleName)) role.holderScale = scaleName;
                    else if (string.Equals(
                        holders.Attribute("mode")?.Value?.Trim(), "never",
                        StringComparison.OrdinalIgnoreCase))
                        role.holderScale = "Never";
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

        private static List<string> SplitSkills(string joined) =>
            string.IsNullOrWhiteSpace(joined)
                ? new List<string>()
                : joined.Split(',').Select(s => s.Trim())
                    .Where(s => s.Length > 0).ToList();

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
