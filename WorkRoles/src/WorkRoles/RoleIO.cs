using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimShared.Common;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Store-side half of role import/export: color-reference resolution, role
    /// matching, merge/overwrite planning and application. The FILE FORMAT itself
    /// (parse/serialize) lives in Core (RoleFile) with round-trip tests.
    public static class RoleIO
    {
        public const string DefaultFileName = "WorkRoles.xml";

        // Session-fixed paths; the properties are read per frame from tooltips.
        private static string? gameDataDir;
        private static string? exportFile;

        /// Our folder under the game's per-user data root (beside Saves\, Config\).
        public static string GameDataDir =>
            gameDataDir ??= Path.Combine(GenFilePaths.SaveDataFolderPath, "WorkRoles");

        public static string ExportFile =>
            exportFile ??= Path.Combine(GameDataDir, DefaultFileName);

        public static RoleFileDocument Parse(string xml) => RoleFile.Parse(xml);

        /// The full export document — the custom palette slots the exported roles
        /// actually USE, plus every role — as indented XML text.
        public static string BuildXml(RoleStore store)
        {
            store.SyncSwatchNames();
            var doc = new RoleFileDocument();
            var usedSlots = new SortedSet<int>();
            foreach (var role in store.roles)
                if (role.hasCustomColor)
                {
                    int slot = CustomSlotOf(role.color, store);
                    if (slot >= 0) usedSlots.Add(slot);
                }
            foreach (int slot in usedSlots)
                doc.palette.Add((store.customSwatchNames[slot], ToRgb(store.customSwatches[slot])));
            foreach (var group in store.groups)
                if (group.id != RoleGroup.DefaultId)
                {
                    doc.groups.Add(group.label);
                    doc.groupsWithIds.Add(new FileGroup
                    {
                        fileId = GroupFileId(group.id),
                        name = group.label,
                    });
                }
            foreach (var role in store.roles)
            {
                // Runtime ids become document-local ids and are resolved only
                // through this document on import; labels remain fallbacks.
                var training = new List<FileTrainingPathEntry>();
                for (int i = 0; i < role.trainingRoleIds.Count; i++)
                {
                    string? memberLabel = store.RoleById(role.trainingRoleIds[i])?.label;
                    if (memberLabel != null)
                        training.Add(new FileTrainingPathEntry(
                            RoleFileId(role.trainingRoleIds[i]), memberLabel,
                            role.trainingMins[i], role.trainingMaxes[i]));
                }
                doc.roles.Add(new FileRole
                {
                    fileId = RoleFileId(role.id),
                    label = role.label,
                    templateDef = role.templateDefName,
                    group = role.groupId == RoleGroup.DefaultId ? null
                        : store.GroupById(role.groupId)?.label,
                    groupId = role.groupId == RoleGroup.DefaultId ? null
                        : GroupFileId(role.groupId),
                    colorRef = role.hasCustomColor ? EncodeColorRef(role.color, store, doc) : null,
                    autoAssign = role.autoAssign,
                    blocker = role.blocker,
                    enabled = role.enabled,
                    activeHours = role.activeHours,
                    locations = role.locationTokens.Select(FileLocationToken)
                        .Where(t => t != null).Select(t => t!).ToList(),
                    hasTuning = role.tuningSeeded
                        || role.requiredSkills.Count > 0,
                    category = role.category,
                    time = role.time,
                    championPenalty = role.championPenalty,
                    minAge = role.minAge,
                    maxAge = role.maxAge,
                    colonyMin = role.colonyMin,
                    coverage = role.coverage,
                    requiredSkills = role.requiredSkills.ToList(),
                    training = training,
                    entries = role.entries.ToList(),
                    composite = role.composite,
                    members = role.memberRoleIds
                        .Select(id => store.RoleById(id))
                        .OfType<Role>()
                        .Select(member => new FileRoleReference(
                            RoleFileId(member.id), member.label))
                        .ToList(),
                });
            }
            // Only the stored template travels (empty = the derived default).
            doc.recommendationOrderWithIds = store.recommendationOrder
                .Select(id => store.RoleById(id))
                .OfType<Role>()
                .Select(role => new FileRoleReference(RoleFileId(role.id), role.label))
                .ToList();
            doc.recommendationOrder = doc.recommendationOrderWithIds
                .Select(reference => reference.label).OfType<string>().ToList();
            return RoleFile.Build(doc);
        }

        private static string RoleFileId(int roleId) => "role-" + roleId;
        private static string GroupFileId(int groupId) => "group-" + groupId;

        /// Specific locations export by NAME (ids are save-local); stale tokens
        /// (a location that no longer exists) drop out of the export.
        private static string? FileLocationToken(string token)
        {
            if (token == LocationRules.Settlements
                || token == LocationRules.Caravans
                || token == LocationRules.Nowhere)
                return token;
            bool ship = token.StartsWith(LocationRules.ShipPrefix);
            string id = token.Substring(token.IndexOf(':') + 1);
            var loc = ColonyScope.Locations().FirstOrDefault(l => l.Id == id);
            if (loc == null) return null;
            return (ship ? LocationRules.ShipPrefix : LocationRules.SettlementPrefix) + loc.Label;
        }

        /// Resolves every portable location name before the synced import is
        /// issued. The parallel lists travel with the command, including empty
        /// values for unresolved names, so remote clients never consult their
        /// language- or faction-local location view.
        internal static void ResolveImportLocations(ImportSelection selection,
            RoleFileDocument doc, IReadOnlyList<WorkRoles.Core.LocationInfo> locations)
        {
            if (selection == null || doc?.roles == null) return;
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var role in doc.roles)
                if (role?.locations != null)
                    foreach (string fileToken in role.locations)
                    {
                        if (fileToken.NullOrEmpty() || !seen.Add(fileToken)) continue;
                        selection.locationFileTokens.Add(fileToken);
                        selection.locationRuntimeTokens.Add(
                            ImportLocationResolver.Resolve(fileToken, locations) ?? "");
                    }
        }

        /// Writes xml to path, creating directories; returns an error or null.
        public static string? SaveTo(string path, string xml)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!dir.NullOrEmpty()) Directory.CreateDirectory(dir);
                File.WriteAllText(path, xml, Encoding.UTF8);
                return null;
            }
            catch (System.Exception e)
            {
                return e.Message;
            }
        }

        // ----- Colors -----

        internal static Color ToUnity(ColorRgb c) => new Color(c.R, c.G, c.B);
        private static ColorRgb ToRgb(Color c) => new ColorRgb(c.r, c.g, c.b);

        /// Built-in swatch -> its Tailwind name ("red-800"); custom-slot color ->
        /// the slot's palette name; PaletteDef color (only differs from the
        /// swatch grid when a mod re-hexes a shipped entry) -> its defName.
        /// Anything else (colors from pre-palette saves) joins the export's
        /// palette under a generated name: roles ALWAYS reference colors by
        /// name, hex lives only in <Palette>.
        private static string EncodeColorRef(Color color, RoleStore store, RoleFileDocument doc)
        {
            var swatches = SwatchPalette.Swatches;
            for (int i = 0; i < swatches.Length; i++)
                if (color.IndistinguishableFrom(swatches[i]))
                    return SwatchPalette.ExportName(i);
            int slot = CustomSlotOf(color, store);
            if (slot >= 0)
                return store.customSwatchNames[slot];
            foreach (var def in DefDatabase<PaletteDef>.AllDefsListForReading)
                if (color.IndistinguishableFrom(def.color))
                    return def.defName;
            foreach (var (name, rgb) in doc.palette)
                if (ToUnity(rgb).IndistinguishableFrom(color))
                    return name; // same unnamed color twice -> one entry
            string generated = NextCustomName(store, doc);
            doc.palette.Add((generated, ToRgb(color)));
            return generated;
        }

        /// First free "custom-N" name (a user slot could legitimately be named
        /// custom-1; slot names outrank the file palette on import, so skip those).
        private static string NextCustomName(RoleStore store, RoleFileDocument doc)
        {
            for (int i = 1; ; i++)
            {
                string name = "custom-" + i;
                if (!doc.palette.Any(p => p.Item1 == name)
                    && !store.customSwatchNames.Contains(name))
                    return name;
            }
        }

        /// The custom slot exactly matching the color, or -1. Built-in swatches
        /// win (a color equal to both exports as its Tailwind name).
        private static int CustomSlotOf(Color color, RoleStore store)
        {
            foreach (var swatch in SwatchPalette.Swatches)
                if (color.IndistinguishableFrom(swatch))
                    return -1;
            for (int i = 0; i < store.customSwatches.Count; i++)
                if (store.customSwatches[i].a >= 0.5f && color.IndistinguishableFrom(store.customSwatches[i]))
                    return i;
            return -1;
        }

        private static readonly Color FallbackColor = SwatchPalette.Swatches[2 * 19]; // slate-600

        /// Resolves a role's color reference: built-in Tailwind name, then a store
        /// slot name, then the file's palette, then a PaletteDef, then hex
        /// (undocumented leniency for hand-edited files); unknown names fall back
        /// to slate-600. Null = no custom color.
        internal static (bool has, Color color) ResolveColor(string? colorRef, RoleStore store, RoleFileDocument doc)
        {
            if (colorRef is not { Length: > 0 }) return (false, default);
            var swatches = SwatchPalette.Swatches;
            for (int i = 0; i < swatches.Length; i++)
                if (SwatchPalette.ExportName(i) == colorRef)
                    return (true, swatches[i]);
            int slot = store.customSwatchNames.IndexOf(colorRef);
            if (slot >= 0 && slot < store.customSwatches.Count && store.customSwatches[slot].a >= 0.5f)
                return (true, store.customSwatches[slot]);
            foreach (var (name, color) in doc.palette)
                if (name == colorRef)
                    return (true, ToUnity(color));
            var paletteDef = DefDatabase<PaletteDef>.GetNamedSilentFail(colorRef);
            if (paletteDef != null)
                return (true, paletteDef.color);
            if (ColorRgb.TryParseHex(colorRef, out var hex))
                return (true, ToUnity(hex));
            return (true, FallbackColor);
        }

        // ----- Import plan + apply -----
        // Rows are derived deterministically from (xml, store), so the preview's
        // row indices are a valid MP sync payload: every client rebuilds the same
        // plan and applies the same selection.

        public class PaletteRow
        {
            public int sourceIndex;
            public string name = null!; // set at construction
            public Color color;
            public bool isNew;                 // false = same-name slot changes color
            public List<string> recolors = new List<string>(); // role labels affected
        }

        public class RoleRow
        {
            public FileRole role = null!;      // set at construction
            public Role? existing;             // null = new role
            public string? displayLabel;
            public bool preservesExistingLabel;
        }

        /// Existing role a file role maps onto: template id first, then label.
        /// File-local ids are deliberately not world ids and never participate.
        public static Role MatchRole(RoleStore store, FileRole imported)
        {
            if (!imported.templateDef.NullOrEmpty())
            {
                var byTemplate = store.RoleByTemplate(imported.templateDef!); // guarded by NullOrEmpty above
                if (byTemplate != null) return byTemplate;
            }
            return store.roles.FirstOrDefault(r => string.Equals(
                r.label, imported.label, System.StringComparison.OrdinalIgnoreCase));
        }

        /// Palette rows for MERGE mode: identical name+color entries are omitted.
        public static List<PaletteRow> PaletteMergeRows(RoleStore store, RoleFileDocument doc)
        {
            var rows = new List<PaletteRow>();
            for (int i = 0; i < doc.palette.Count; i++)
            {
                var (name, rgb) = doc.palette[i];
                var color = ToUnity(rgb);
                int slot = EffectiveCustomSlotOfName(store, name);
                bool defined = slot >= 0 && slot < store.customSwatches.Count
                    && store.customSwatches[slot].a >= 0.5f;
                if (defined && store.customSwatches[slot].IndistinguishableFrom(color))
                    continue; // identical: nothing to preview
                var row = new PaletteRow
                {
                    sourceIndex = i,
                    name = name,
                    color = color,
                    isNew = !defined,
                };
                if (defined)
                    foreach (var role in store.roles)
                        if (role.hasCustomColor && role.color.IndistinguishableFrom(store.customSwatches[slot]))
                            row.recolors.Add(role.label);
                rows.Add(row);
            }
            return rows;
        }

        /// Pure preview equivalent of SyncSwatchNames + IndexOf. Missing or
        /// blank persisted names are projected as their deterministic defaults
        /// without mutating authoritative store state merely to render a dialog.
        private static int EffectiveCustomSlotOfName(RoleStore store, string name)
        {
            for (int i = 0; i < store.customSwatches.Count; i++)
            {
                string? current = i < store.customSwatchNames.Count
                    ? store.customSwatchNames[i]
                    : null;
                if (current.NullOrEmpty()) current = $"custom-{i + 1}";
                if (string.Equals(current, name, System.StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        public static List<RoleRow> RoleRows(RoleStore store, RoleFileDocument doc,
            bool overwrite = false)
        {
            var importedRoles = doc?.roles ?? new List<FileRole>();
            var imports = importedRoles.Select(role => new ImportIdentitySource(
                role?.label, role?.templateDef)).ToList();
            var existing = store.roles.Select(role => new ImportIdentityExisting(
                role?.label, role?.templateDefName)).ToList();
            IReadOnlyList<ImportIdentityDecision> decisions =
                ImportIdentityPlanner.Plan(imports, existing,
                    discardUnmatchedExistingLabels: overwrite);
            var rows = new List<RoleRow>(importedRoles.Count);
            for (int i = 0; i < importedRoles.Count; i++)
            {
                ImportIdentityDecision decision = decisions[i];
                rows.Add(new RoleRow
                {
                    role = importedRoles[i],
                    existing = decision.ExistingIndex < 0
                        ? null : store.roles[decision.ExistingIndex],
                    displayLabel = decision.DisplayLabel,
                    preservesExistingLabel = decision.ExistingIndex >= 0
                        && string.Equals(
                            store.roles[decision.ExistingIndex].label?.Trim(),
                            decision.DisplayLabel?.Trim(),
                            System.StringComparison.OrdinalIgnoreCase),
                });
            }
            return rows;
        }

        /// Roles deleted by an overwrite: in the catalog but not in the file.
        public static List<Role> OverwriteDeletes(RoleStore store, RoleFileDocument doc)
        {
            return OverwriteDeletes(store, RoleRows(store, doc, overwrite: true));
        }

        internal static List<Role> OverwriteDeletes(RoleStore store, List<RoleRow> rows)
        {
            var kept = new HashSet<Role>(rows.Where(r => r.existing != null)
                .Select(r => r.existing!));
            return store.roles.Where(r => !kept.Contains(r)).ToList();
        }

    }

    public static partial class RoleCommands
    {
        private readonly struct ImportApplyResult
        {
            internal ImportApplyResult(string summary, bool changed,
                bool compilationChanged)
            {
                Summary = summary;
                Changed = changed;
                CompilationChanged = compilationChanged;
            }

            internal string Summary { get; }
            internal bool Changed { get; }
            internal bool CompilationChanged { get; }
        }

        /// Exact before/after value used only by the explicit import command.
        /// It prevents a semantically identical import from publishing UI or
        /// compiled-order invalidations; it is not retained as a render cache.
        private sealed class ImportStoreState
        {
            private readonly List<Color> swatches;
            private readonly List<string> swatchNames;
            private readonly List<GroupState> groups;
            private readonly List<RoleState> roles;
            private readonly List<int> recommendationOrder;

            internal ImportStoreState(RoleStore store)
            {
                swatches = new List<Color>(store.customSwatches);
                swatchNames = new List<string>(store.customSwatchNames);
                groups = store.groups.Select(group => new GroupState(group)).ToList();
                roles = store.roles.Select(role => new RoleState(role)).ToList();
                recommendationOrder = new List<int>(store.recommendationOrder);
            }

            internal bool ContentEquals(ImportStoreState other)
            {
                if (!swatches.SequenceEqual(other.swatches)
                    || !swatchNames.SequenceEqual(other.swatchNames)
                    || !recommendationOrder.SequenceEqual(other.recommendationOrder)
                    || groups.Count != other.groups.Count
                    || roles.Count != other.roles.Count)
                    return false;
                for (int i = 0; i < groups.Count; i++)
                    if (!groups[i].ContentEquals(other.groups[i])) return false;
                for (int i = 0; i < roles.Count; i++)
                    if (!roles[i].ContentEquals(other.roles[i])) return false;
                return true;
            }

            internal bool CompilationEquals(ImportStoreState other)
            {
                if (roles.Count != other.roles.Count) return false;
                for (int i = 0; i < roles.Count; i++)
                    if (!roles[i].CompilationEquals(other.roles[i])) return false;
                return true;
            }
        }

        private readonly struct GroupState
        {
            private readonly int id;
            private readonly string label;

            internal GroupState(RoleGroup group)
            {
                id = group.id;
                label = group.label;
            }

            internal bool ContentEquals(GroupState other) =>
                id == other.id && label == other.label;
        }

        private sealed class RoleState
        {
            private readonly int id;
            private readonly string label;
            private readonly bool enabled;
            private readonly bool hasCustomColor;
            private readonly Color color;
            private readonly string? templateDefName;
            private readonly bool autoAssign;
            private readonly bool blocker;
            private readonly RoleCategory category;
            private readonly RoleTime time;
            private readonly bool championPenalty;
            private readonly int minAge;
            private readonly int maxAge;
            private readonly int colonyMin;
            private readonly int coverage;
            private readonly bool tuningSeeded;
            private readonly bool skillGatesSeeded;
            private readonly int groupId;
            private readonly int activeHours;
            private readonly bool composite;
            private readonly List<string> requiredSkills;
            private readonly List<string> locationTokens;
            private readonly List<JobEntry> entries;
            private readonly List<int> memberRoleIds;
            private readonly List<int> trainingRoleIds;
            private readonly List<int> trainingMins;
            private readonly List<int> trainingMaxes;
            private readonly Dictionary<string, List<string>> workTypeSnapshots;

            internal RoleState(Role role)
            {
                id = role.id;
                label = role.label;
                enabled = role.enabled;
                hasCustomColor = role.hasCustomColor;
                color = role.color;
                templateDefName = role.templateDefName;
                autoAssign = role.autoAssign;
                blocker = role.blocker;
                category = role.category;
                time = role.time;
                championPenalty = role.championPenalty;
                minAge = role.minAge;
                maxAge = role.maxAge;
                colonyMin = role.colonyMin;
                coverage = role.coverage;
                tuningSeeded = role.tuningSeeded;
                skillGatesSeeded = role.skillGatesSeeded;
                groupId = role.groupId;
                activeHours = role.activeHours;
                composite = role.composite;
                requiredSkills = new List<string>(role.requiredSkills);
                locationTokens = new List<string>(role.locationTokens);
                entries = new List<JobEntry>(role.entries);
                memberRoleIds = new List<int>(role.memberRoleIds);
                trainingRoleIds = new List<int>(role.trainingRoleIds);
                trainingMins = new List<int>(role.trainingMins);
                trainingMaxes = new List<int>(role.trainingMaxes);
                workTypeSnapshots = role.workTypeSnapshots.ToDictionary(
                    pair => pair.Key, pair => new List<string>(pair.Value));
            }

            internal bool ContentEquals(RoleState other) =>
                id == other.id
                && label == other.label
                && enabled == other.enabled
                && hasCustomColor == other.hasCustomColor
                && color.Equals(other.color)
                && templateDefName == other.templateDefName
                && autoAssign == other.autoAssign
                && blocker == other.blocker
                && category == other.category
                && time == other.time
                && championPenalty == other.championPenalty
                && minAge == other.minAge
                && maxAge == other.maxAge
                && colonyMin == other.colonyMin
                && coverage == other.coverage
                && tuningSeeded == other.tuningSeeded
                && skillGatesSeeded == other.skillGatesSeeded
                && groupId == other.groupId
                && activeHours == other.activeHours
                && composite == other.composite
                && requiredSkills.SequenceEqual(other.requiredSkills)
                && locationTokens.SequenceEqual(other.locationTokens)
                && entries.SequenceEqual(other.entries)
                && memberRoleIds.SequenceEqual(other.memberRoleIds)
                && trainingRoleIds.SequenceEqual(other.trainingRoleIds)
                && trainingMins.SequenceEqual(other.trainingMins)
                && trainingMaxes.SequenceEqual(other.trainingMaxes)
                && SnapshotContentEquals(other);

            internal bool CompilationEquals(RoleState other) =>
                id == other.id
                && enabled == other.enabled
                && blocker == other.blocker
                && activeHours == other.activeHours
                && composite == other.composite
                && locationTokens.SequenceEqual(other.locationTokens)
                && entries.SequenceEqual(other.entries)
                && memberRoleIds.SequenceEqual(other.memberRoleIds)
                && SnapshotContentEquals(other);

            private bool SnapshotContentEquals(RoleState other)
            {
                if (workTypeSnapshots.Count != other.workTypeSnapshots.Count)
                    return false;
                foreach (var pair in workTypeSnapshots)
                    if (!other.workTypeSnapshots.TryGetValue(pair.Key, out var value)
                        || !pair.Value.SequenceEqual(value))
                        return false;
                return true;
            }
        }

        /// Applies an import. Palette selections are stable source indices into
        /// doc.palette; role/path selections are source indices into RoleRows and
        /// doc.trainingPaths. Selections are ignored in overwrite modes.
        /// Returns the exact observable change state and summary.
        private static ImportApplyResult ApplyImportToStore(RoleStore store,
            RoleFileDocument doc,
            bool paletteInclude, bool paletteOverwrite, List<int> paletteRows,
            bool rolesInclude, bool rolesOverwrite, List<int> roleRows,
            bool pathsInclude, bool pathsOverwrite, List<int> pathRows,
            bool orderInclude,
            IReadOnlyDictionary<string, string> resolvedLocations)
        {
            var before = new ImportStoreState(store);
            if (!rolesInclude)
            {
                pathsInclude = false;
                orderInclude = false;
            }

            int paletteChanges = 0, updated = 0, added = 0, deleted = 0, pathsAdded = 0;
            Dictionary<FileRole, Role>? runtimeRoles = null;
            store.SyncSwatchNames();

            if (paletteInclude && paletteOverwrite)
            {
                var oldColors = store.customSwatches.ToList();
                var oldNames = store.customSwatchNames.ToList();
                store.customSwatches.Clear();
                store.customSwatchNames.Clear();
                foreach (var (name, rgb) in doc.palette.Take(RoleStore.MaxCustomSwatches))
                {
                    store.customSwatches.Add(RoleIO.ToUnity(rgb));
                    store.customSwatchNames.Add(name);
                    paletteChanges++;
                }
                // A slot name surviving the overwrite recolors the roles that used
                // its old color; roles of dropped names keep their color and the
                // coverage sweep below re-slots or snaps it.
                for (int i = 0; i < oldColors.Count; i++)
                {
                    if (oldColors[i].a < 0.5f || i >= oldNames.Count) continue;
                    var replacement = NamedColor(doc, oldNames[i]);
                    if (replacement != null && !replacement.Value.IndistinguishableFrom(oldColors[i]))
                        RecolorRoles(store, oldColors[i], replacement);
                }
            }
            else if (paletteInclude)
            {
                foreach (int sourceIndex in paletteRows ?? new List<int>())
                {
                    if (sourceIndex < 0 || sourceIndex >= doc.palette.Count) continue;
                    var (name, rgb) = doc.palette[sourceIndex];
                    Color color = RoleIO.ToUnity(rgb);
                    int slot = store.customSwatchNames.IndexOf(name);
                    if (slot >= 0 && slot < store.customSwatches.Count && store.customSwatches[slot].a >= 0.5f)
                    {
                        if (store.customSwatches[slot].IndistinguishableFrom(color))
                            continue;
                        var old = store.customSwatches[slot];
                        store.customSwatches[slot] = color;
                        RecolorRoles(store, old, color);
                    }
                    else
                    {
                        int free = FreeSlot(store);
                        // No capacity: the coverage sweep below snaps affected roles.
                        if (free < 0) continue;
                        while (store.customSwatches.Count <= free) store.customSwatches.Add(Color.clear);
                        store.SyncSwatchNames();
                        store.customSwatches[free] = color;
                        store.customSwatchNames[free] = name;
                    }
                    paletteChanges++;
                }
            }

            if (rolesInclude)
            {
                var plannedRoles = RoleIO.RoleRows(store, doc, rolesOverwrite);
                runtimeRoles = plannedRoles
                    .Where(row => row.existing != null)
                    .ToDictionary(row => row.role, row => row.existing!);

                // Groups travel with the roles section: merge appends missing
                // groups at the end; overwrite adopts the file's order (Default
                // stays pinned, groups the file doesn't know keep their spot).
                IReadOnlyList<FileGroup> fileGroups = RoleFile.GroupsWithStableIds(doc);
                var existingGroups = store.groups.ToList();
                var groupImports = fileGroups.Select(group =>
                    new ImportIdentitySource(group?.name, null)).ToList();
                var groupTargets = existingGroups.Select(group =>
                    new ImportIdentityExisting(group?.label, null)).ToList();
                IReadOnlyList<ImportIdentityDecision> groupPlan =
                    ImportIdentityPlanner.Plan(groupImports, groupTargets);
                var runtimeGroups = new Dictionary<FileGroup, RoleGroup>();
                for (int groupIndex = 0; groupIndex < fileGroups.Count; groupIndex++)
                {
                    FileGroup fileGroup = fileGroups[groupIndex];
                    ImportIdentityDecision decision = groupPlan[groupIndex];
                    RoleGroup? group = decision.ExistingIndex < 0
                        ? null : existingGroups[decision.ExistingIndex];
                    if (group == null && GroupNameRules.IsDefault(decision.DisplayLabel))
                        group = store.EnsureDefaultGroup();
                    if (group == null && !decision.DisplayLabel.NullOrEmpty())
                    {
                        group = new RoleGroup
                        {
                            id = store.NextGroupId(),
                            label = decision.DisplayLabel!, // guarded by NullOrEmpty above
                        };
                        store.groups.Add(group);
                    }
                    if (group != null) runtimeGroups[fileGroup] = group;
                }
                if (rolesOverwrite)
                {
                    var ordered = new List<RoleGroup>();
                    var defaultGroup = store.GroupById(RoleGroup.DefaultId);
                    if (defaultGroup != null) ordered.Add(defaultGroup);
                    foreach (var fileGroup in fileGroups)
                        if (runtimeGroups.TryGetValue(fileGroup, out var group)
                            && !ordered.Contains(group))
                            ordered.Add(group);
                    foreach (var group in store.groups)
                        if (!ordered.Contains(group)) ordered.Add(group);
                    store.groups.Clear();
                    store.groups.AddRange(ordered);
                }

                var rows = plannedRoles;
                var appliedRows = new List<RoleIO.RoleRow>();
                var selected = rolesOverwrite
                    ? Enumerable.Range(0, rows.Count).ToList()
                    : (roleRows ?? new List<int>());
                if (rolesOverwrite)
                {
                    foreach (var role in RoleIO.OverwriteDeletes(store, plannedRoles))
                    {
                        RoleCommands.DeleteRole(role.id);
                        deleted++;
                    }
                    // Matched renames may swap names. Remove every changing
                    // target from the live name namespace before final labels
                    // are validated and assigned in deterministic file order.
                    foreach (RoleIO.RoleRow row in plannedRoles)
                        if (row.existing != null && !row.preservesExistingLabel
                            && !row.displayLabel.NullOrEmpty())
                            row.existing.label = null!; // transiently vacated; reassigned below in file order
                }
                foreach (int index in selected)
                {
                    if (index < 0 || index >= rows.Count) continue;
                    var row = rows[index];
                    bool unchangedLegacyDuplicate = row.preservesExistingLabel;
                    if (!unchangedLegacyDuplicate && !CatalogNameRules.IsAvailable(
                            row.displayLabel, store.roles,
                            existing => existing.label, row.existing))
                        continue;
                    var (hasColor, color) = RoleIO.ResolveColor(row.role.colorRef, store, doc);
                    var locationTokens = row.role.locations
                        .Select(token => ImportLocationResolver.FromMap(
                            token, resolvedLocations))
                        .OfType<string>().ToList();
                    var importedEntries = row.role.composite
                        ? new List<JobEntry>()
                        : row.role.entries.Distinct().ToList();
                    Role? target = row.existing;
                    bool clearWorkSnapshots = target == null
                        || target.composite != row.role.composite
                        || !target.entries.SequenceEqual(importedEntries);
                    if (target == null)
                    {
                        target = new Role { id = store.NextId() };
                        target.templateDefName = !row.role.templateDef.NullOrEmpty()
                            && store.RoleByTemplate(row.role.templateDef!) == null ? row.role.templateDef : null;
                        store.roles.Add(target);
                        store.InvalidateRoleIndex();
                        added++;
                    }
                    else
                    {
                        updated++;
                    }
                    target.label = row.displayLabel!; // planner always yields a label for applied rows
                    target.hasCustomColor = hasColor;
                    if (hasColor) target.color = color;
                    target.autoAssign = row.role.autoAssign;
                    target.blocker = row.role.blocker;
                    target.enabled = row.role.enabled;
                    target.activeHours = row.role.activeHours;
                    target.locationTokens = locationTokens;
                    target.category = row.role.category;
                    target.time = row.role.time;
                    target.championPenalty = row.role.championPenalty;
                    // Pre-minAge files carry -1; the migration below derives it.
                    target.minAge = row.role.minAge < 0
                        ? -1 : Mathf.Clamp(row.role.minAge, 0, 18);
                    target.maxAge = Mathf.Clamp(row.role.maxAge, 0, 18);
                    target.colonyMin = Mathf.Clamp(row.role.colonyMin, 0, 30);
                    target.coverage = Mathf.Clamp(row.role.coverage, 0, 100);
                    target.requiredSkills = row.role.requiredSkills.ToList();
                    target.skillGatesSeeded = true;
                    // Pre-tuning files leave this false; the migration below
                    // derives their classification like a pre-tuning save.
                    target.tuningSeeded = row.role.hasTuning;
                    target.groupId = GroupIdFor(
                        row.role.groupId, row.role.group, doc, runtimeGroups, store);
                    // Hand-edited files can repeat an entry; first occurrence wins.
                    target.composite = row.role.composite;
                    target.entries = importedEntries;
                    if (clearWorkSnapshots)
                        target.workTypeSnapshots.Clear();
                    row.existing = target;
                    runtimeRoles[row.role] = target;
                    appliedRows.Add(row);
                }
                // Training resolves AFTER every role landed: entries may
                // reference roles this same import just added.
                foreach (RoleIO.RoleRow row in appliedRows)
                {
                    var ids = new List<int>();
                    var mins = new List<int>();
                    var maxes = new List<int>();
                    foreach (var entry in row.role.training)
                    {
                        Role? member = RuntimeRole(doc, runtimeRoles!, entry.role);
                        if (member == null || ids.Contains(member.id)) continue;
                        ids.Add(member.id);
                        mins.Add(entry.min);
                        maxes.Add(entry.max);
                    }
                    row.existing!.trainingRoleIds = ids; // applied rows always carry a target
                    row.existing.trainingMins = mins;
                    row.existing.trainingMaxes = maxes;
                    store.SanitizeRoleTraining(row.existing);
                }
                // Composite members resolve AFTER every role landed, like
                // training: references may point at roles this import added.
                foreach (RoleIO.RoleRow row in appliedRows)
                {
                    if (!row.existing!.composite) // applied rows always carry a target
                    {
                        row.existing.memberRoleIds.Clear();
                        continue;
                    }
                    var memberIds = new List<int>();
                    foreach (var reference in row.role.members)
                    {
                        Role? member = RuntimeRole(doc, runtimeRoles!, reference);
                        if (member == null || memberIds.Contains(member.id)) continue;
                        memberIds.Add(member.id);
                    }
                    row.existing.memberRoleIds = memberIds;
                }
                // An import can flip a role composite or hand it rules; every
                // member list re-validates against the final imported state.
                foreach (var role in store.roles)
                    store.SanitizeCompositeMembers(role);
                RoleCommands.SweepEmptyGroups();
                Seeding.RefreshWorkTypeSnapshots();
                store.MigrateRoleTuning();
            }

            // Legacy stand-alone <TrainingPaths> (v10 and older) resolve AFTER
            // roles landed and fold into their unique target role; names,
            // colors and anchors retire. A target that already owns training
            // keeps it (importing the same file twice must not churn).
            if (pathsInclude)
            {
                if (pathsOverwrite)
                    foreach (var role in store.roles)
                    {
                        role.trainingRoleIds.Clear();
                        role.trainingMins.Clear();
                        role.trainingMaxes.Clear();
                    }
                var selectedPaths = pathsOverwrite
                    ? Enumerable.Range(0, doc.trainingPaths.Count).ToList()
                    : (pathRows ?? new List<int>());
                foreach (int index in selectedPaths)
                {
                    if (index < 0 || index >= doc.trainingPaths.Count) continue;
                    var filePath = doc.trainingPaths[index];
                    var (ids, mins, maxes) = RoleFile.ResolvePathEntries(filePath, doc,
                        fileRole => runtimeRoles!.TryGetValue(fileRole, out var runtime) // paths imply rolesInclude
                            ? runtime.id : (int?)null);
                    if (ids.Count < 2
                        || !SkillProgressionMath.Validate(ids.Count, mins, maxes))
                        continue;
                    Role? owner = store.RoleById(UniqueTargetOf(ids, mins));
                    if (owner == null || owner.trainingRoleIds.Count > 0) continue;
                    owner.trainingRoleIds = ids;
                    owner.trainingMins = mins;
                    owner.trainingMaxes = maxes;
                    store.SanitizeRoleTraining(owner);
                    pathsAdded++;
                }
            }
            IReadOnlyList<FileRoleReference> effectiveOrder =
                RoleFile.RecommendationOrderWithStableIds(doc);
            if (orderInclude && effectiveOrder.Count > 0)
            {
                store.recommendationOrder = effectiveOrder
                    .Select(reference => RuntimeRole(doc, runtimeRoles!, reference)?.id) // order implies rolesInclude
                    .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            }

            // Invariant close-out: no import outcome (skipped palette rows,
            // dropped slots, hex or PaletteDef colors, capacity misses) may
            // leave a role color outside the palette.
            EnforcePaletteCoverage(store);

            var after = new ImportStoreState(store);
            return new ImportApplyResult(
                "WR_ImportSummary".Translate(
                    added, updated, deleted, paletteChanges, pathsAdded),
                !before.ContentEquals(after),
                !before.CompilationEquals(after));
        }

        /// The unique highest-band-minimum entry of a legacy path; -1 on ties.
        private static int UniqueTargetOf(List<int> ids, List<int> mins)
        {
            int highest = int.MinValue;
            int at = -1;
            bool unique = true;
            for (int i = 0; i < ids.Count; i++)
            {
                if (mins[i] > highest)
                {
                    highest = mins[i];
                    at = i;
                    unique = true;
                }
                else if (mins[i] == highest)
                    unique = false;
            }
            return unique && at >= 0 ? ids[at] : -1;
        }

        /// Orphan role colors claim free custom slots; past capacity they snap
        /// to the nearest palette color. Deterministic (store order), so it is
        /// safe inside the synced import apply and load normalization.
        internal static void EnforcePaletteCoverage(RoleStore store)
        {
            var custom = new List<Rgba>(store.customSwatches.Count);
            for (int i = 0; i < store.customSwatches.Count; i++)
                custom.Add(PaletteColors.ToRgba(store.customSwatches[i]));
            var colored = new List<(int id, Rgba color)>();
            foreach (var role in store.roles)
                if (role != null && role.hasCustomColor)
                    colored.Add((role.id, PaletteColors.ToRgba(role.color)));

            var plan = PaletteCoverageEnforcer.Plan(PaletteColors.StandardRgba(),
                custom, RoleStore.MaxCustomSwatches, colored);
            if (plan.IsEmpty) return;
            foreach (var (slot, color) in plan.DefineSlots)
            {
                while (store.customSwatches.Count <= slot)
                    store.customSwatches.Add(Color.clear);
                store.customSwatches[slot] = PaletteColors.ToColor(color);
            }
            store.SyncSwatchNames();
            foreach (var (roleId, color) in plan.SnapRoles)
            {
                var role = store.RoleById(roleId);
                if (role != null) role.color = PaletteColors.ToColor(color);
            }
        }

        private static Role? RuntimeRole(RoleFileDocument document,
            Dictionary<FileRole, Role> runtimeRoles, string? fileId, string? label)
        {
            var fileRole = RoleFile.ResolveRole(document, fileId, label);
            return fileRole != null && runtimeRoles.TryGetValue(fileRole, out var runtime)
                ? runtime : null;
        }

        private static Role? RuntimeRole(RoleFileDocument document,
            Dictionary<FileRole, Role> runtimeRoles, FileRoleReference? reference) =>
            reference == null ? null : RuntimeRole(
                document, runtimeRoles, reference.fileId, reference.label);

        /// Resolves a file group name to a group id, creating unknown groups
        /// (leniency: a role may reference a name missing from <Groups>).
        private static int GroupIdFor(string? fileId, string? name, RoleFileDocument document,
            Dictionary<FileGroup, RoleGroup> runtimeGroups, RoleStore store)
        {
            var fileGroup = RoleFile.ResolveGroup(document, fileId, name);
            if (fileGroup != null && runtimeGroups.TryGetValue(fileGroup, out var mapped))
                return mapped.id;
            // Hand-edited files may name Default explicitly (we never write it).
            if (GroupNameRules.IsDefault(name)) return store.EnsureDefaultGroup().id;
            if (name.NullOrEmpty()) return RoleGroup.DefaultId;
            var group = store.GroupByName(name!); // guarded by NullOrEmpty above
            if (group == null && GroupNameRules.IsAvailable(
                    name, store.groups, existing => existing.label))
            {
                group = new RoleGroup { id = store.NextGroupId(), label = name!.Trim() };
                store.groups.Add(group);
            }
            return group?.id ?? RoleGroup.DefaultId;
        }

        private static void RecolorRoles(RoleStore store, Color from, Color? to)
        {
            if (to == null) return;
            foreach (var role in store.roles)
                if (role.hasCustomColor && role.color.IndistinguishableFrom(from))
                    role.color = to.Value;
        }

        private static Color? NamedColor(RoleFileDocument doc, string name)
        {
            if (name.NullOrEmpty()) return null;
            foreach (var (n, c) in doc.palette)
                if (n == name) return RoleIO.ToUnity(c);
            return null;
        }

        private static int FreeSlot(RoleStore store)
        {
            for (int i = 0; i < RoleStore.MaxCustomSwatches; i++)
                if (i >= store.customSwatches.Count || store.customSwatches[i].a < 0.5f)
                    return i;
            return -1;
        }
    }
}
