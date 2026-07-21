using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private static string gameDataDir;
        private static string exportFile;

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
            foreach (var path in store.trainingPaths)
                if (path.hasCustomColor)
                {
                    int slot = CustomSlotOf(path.color, store);
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
                    locations = role.locationTokens.Select(FileLocationToken).Where(t => t != null).ToList(),
                    holderMode = role.holderMode,
                    holderRangeSet = role.holderRangeSet,
                    minHolders = role.minHolders,
                    maxHolders = role.maxHolders,
                    trainingWaivers = role.trainingWaivers,
                    entries = role.entries.ToList(),
                });
            }
            foreach (var path in store.trainingPaths)
            {
                // Runtime ids become document-local ids and are resolved only
                // through this document on import; labels remain fallbacks.
                var filePath = new FileTrainingPath
                {
                    name = path.name,
                    colorRef = path.hasCustomColor ? EncodeColorRef(path.color, store, doc) : null,
                    anchorRole = store.RoleById(path.anchorRoleId)?.label,
                    anchorRoleId = path.anchorRoleId < 0 ? null : RoleFileId(path.anchorRoleId),
                    anchorBefore = path.anchorBefore,
                };
                if (filePath.anchorRole != null)
                    filePath.anchorWithId = new FileRoleReference(
                        filePath.anchorRoleId, filePath.anchorRole);
                for (int i = 0; i < path.roleIds.Count; i++)
                {
                    string label = store.RoleById(path.roleIds[i])?.label;
                    if (label != null)
                    {
                        filePath.entries.Add((label, path.bandMins[i], path.bandMaxes[i]));
                        filePath.entriesWithIds.Add(new FileTrainingPathEntry(
                            RoleFileId(path.roleIds[i]), label,
                            path.bandMins[i], path.bandMaxes[i]));
                    }
                }
                doc.trainingPaths.Add(filePath);
            }
            // Only the stored template travels (empty = the derived default).
            doc.recommendationOrderWithIds = store.recommendationOrder
                .Select(id => store.RoleById(id))
                .Where(role => role != null)
                .Select(role => new FileRoleReference(RoleFileId(role.id), role.label))
                .ToList();
            doc.recommendationOrder = doc.recommendationOrderWithIds
                .Select(reference => reference.label).ToList();
            return RoleFile.Build(doc);
        }

        private static string RoleFileId(int roleId) => "role-" + roleId;
        private static string GroupFileId(int groupId) => "group-" + groupId;

        /// Specific locations export by NAME (ids are save-local); stale tokens
        /// (a location that no longer exists) drop out of the export.
        private static string FileLocationToken(string token)
        {
            if (token == LocationRules.Settlements || token == LocationRules.Caravans) return token;
            bool ship = token.StartsWith(LocationRules.ShipPrefix);
            string id = token.Substring(token.IndexOf(':') + 1);
            var loc = ColonyScope.Locations().FirstOrDefault(l => l.Id == id);
            if (loc == null) return null;
            return (ship ? LocationRules.ShipPrefix : LocationRules.SettlementPrefix) + loc.Label;
        }

        /// Import direction: resolve exported names against this save's
        /// locations (case-insensitive); unresolved names drop out.
        private static string RuntimeLocationToken(string fileToken)
        {
            if (fileToken == LocationRules.Settlements || fileToken == LocationRules.Caravans) return fileToken;
            bool ship = fileToken.StartsWith(LocationRules.ShipPrefix);
            string name = fileToken.Substring(fileToken.IndexOf(':') + 1);
            var loc = ColonyScope.Locations().FirstOrDefault(l => l.IsShip == ship
                && string.Equals(l.Label, name, System.StringComparison.OrdinalIgnoreCase));
            if (loc == null) return null;
            return (ship ? LocationRules.ShipPrefix : LocationRules.SettlementPrefix) + loc.Id;
        }

        /// Writes xml to path, creating directories; returns an error or null.
        public static string SaveTo(string path, string xml)
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

        private static Color ToUnity(ColorRgb c) => new Color(c.R, c.G, c.B);
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
        private static (bool has, Color color) ResolveColor(string colorRef, RoleStore store, RoleFileDocument doc)
        {
            if (colorRef.NullOrEmpty()) return (false, default);
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
            public string name;
            public Color color;
            public bool isNew;                 // false = same-name slot changes color
            public List<string> recolors = new List<string>(); // role labels affected
        }

        public class RoleRow
        {
            public FileRole role;
            public Role existing;              // null = new role
            public string displayLabel;
            public bool preservesExistingLabel;
        }

        /// Existing role a file role maps onto: template id first, then label.
        /// File-local ids are deliberately not world ids and never participate.
        public static Role MatchRole(RoleStore store, FileRole imported)
        {
            if (!imported.templateDef.NullOrEmpty())
            {
                var byTemplate = store.RoleByTemplate(imported.templateDef);
                if (byTemplate != null) return byTemplate;
            }
            return store.roles.FirstOrDefault(r => string.Equals(
                r.label, imported.label, System.StringComparison.OrdinalIgnoreCase));
        }

        /// Palette rows for MERGE mode: identical name+color entries are omitted.
        public static List<PaletteRow> PaletteMergeRows(RoleStore store, RoleFileDocument doc)
        {
            store.SyncSwatchNames();
            var rows = new List<PaletteRow>();
            foreach (var (name, rgb) in doc.palette)
            {
                var color = ToUnity(rgb);
                int slot = store.customSwatchNames.IndexOf(name);
                bool defined = slot >= 0 && slot < store.customSwatches.Count
                    && store.customSwatches[slot].a >= 0.5f;
                if (defined && store.customSwatches[slot].IndistinguishableFrom(color))
                    continue; // identical: nothing to preview
                var row = new PaletteRow { name = name, color = color, isNew = !defined };
                if (defined)
                    foreach (var role in store.roles)
                        if (role.hasCustomColor && role.color.IndistinguishableFrom(store.customSwatches[slot]))
                            row.recolors.Add(role.label);
                rows.Add(row);
            }
            return rows;
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

        private static List<Role> OverwriteDeletes(RoleStore store, List<RoleRow> rows)
        {
            var kept = new HashSet<Role>(rows.Where(r => r.existing != null)
                .Select(r => r.existing));
            return store.roles.Where(r => !kept.Contains(r)).ToList();
        }

        /// Applies an import. Selections are row indices into PaletteMergeRows /
        /// RoleRows / doc.trainingPaths; ignored in overwrite modes (wholesale).
        /// Returns a summary.
        public static string Apply(RoleStore store, RoleFileDocument doc,
            bool paletteInclude, bool paletteOverwrite, List<int> paletteRows,
            bool rolesInclude, bool rolesOverwrite, List<int> roleRows,
            bool pathsInclude, bool pathsOverwrite, List<int> pathRows,
            bool orderInclude)
        {
            if (!rolesInclude)
            {
                pathsInclude = false;
                orderInclude = false;
            }

            int paletteChanges = 0, updated = 0, added = 0, deleted = 0, pathsAdded = 0;
            Dictionary<FileRole, Role> runtimeRoles = null;
            store.SyncSwatchNames();

            if (paletteInclude && paletteOverwrite)
            {
                var oldColors = store.customSwatches.ToList();
                var oldNames = store.customSwatchNames.ToList();
                store.customSwatches.Clear();
                store.customSwatchNames.Clear();
                foreach (var (name, rgb) in doc.palette.Take(RoleStore.MaxCustomSwatches))
                {
                    store.customSwatches.Add(ToUnity(rgb));
                    store.customSwatchNames.Add(name);
                    paletteChanges++;
                }
                // A slot name surviving the overwrite recolors the roles that used
                // its old color; dropped names leave their roles' colors as-is.
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
                var rows = PaletteMergeRows(store, doc);
                foreach (int index in paletteRows ?? new List<int>())
                {
                    if (index < 0 || index >= rows.Count) continue;
                    var row = rows[index];
                    int slot = store.customSwatchNames.IndexOf(row.name);
                    if (slot >= 0 && slot < store.customSwatches.Count && store.customSwatches[slot].a >= 0.5f)
                    {
                        var old = store.customSwatches[slot];
                        store.customSwatches[slot] = row.color;
                        RecolorRoles(store, old, row.color);
                    }
                    else
                    {
                        int free = FreeSlot(store);
                        if (free < 0) continue; // no capacity: role colors resolve via file palette
                        while (store.customSwatches.Count <= free) store.customSwatches.Add(Color.clear);
                        store.SyncSwatchNames();
                        store.customSwatches[free] = row.color;
                        store.customSwatchNames[free] = row.name;
                    }
                    paletteChanges++;
                }
            }

            if (rolesInclude)
            {
                var plannedRoles = RoleRows(store, doc, rolesOverwrite);
                runtimeRoles = plannedRoles
                    .Where(row => row.existing != null)
                    .ToDictionary(row => row.role, row => row.existing);

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
                    RoleGroup group = decision.ExistingIndex < 0
                        ? null : existingGroups[decision.ExistingIndex];
                    if (group == null && GroupNameRules.IsDefault(decision.DisplayLabel))
                        group = store.EnsureDefaultGroup();
                    if (group == null && !decision.DisplayLabel.NullOrEmpty())
                    {
                        group = new RoleGroup
                        {
                            id = store.NextGroupId(),
                            label = decision.DisplayLabel,
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
                var selected = rolesOverwrite
                    ? Enumerable.Range(0, rows.Count).ToList()
                    : (roleRows ?? new List<int>());
                if (rolesOverwrite)
                {
                    foreach (var role in OverwriteDeletes(store, plannedRoles))
                    {
                        RoleCommands.DeleteRole(role.id);
                        deleted++;
                    }
                    // Matched renames may swap names. Remove every changing
                    // target from the live name namespace before final labels
                    // are validated and assigned in deterministic file order.
                    foreach (RoleRow row in plannedRoles)
                        if (row.existing != null && !row.preservesExistingLabel
                            && !row.displayLabel.NullOrEmpty())
                            row.existing.label = null;
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
                    var (hasColor, color) = ResolveColor(row.role.colorRef, store, doc);
                    var target = row.existing;
                    if (target == null)
                    {
                        target = new Role { id = store.NextId() };
                        target.templateDefName = !row.role.templateDef.NullOrEmpty()
                            && store.RoleByTemplate(row.role.templateDef) == null ? row.role.templateDef : null;
                        store.roles.Add(target);
                        store.InvalidateRoleIndex();
                        added++;
                    }
                    else
                    {
                        updated++;
                    }
                    target.label = row.displayLabel;
                    target.hasCustomColor = hasColor;
                    if (hasColor) target.color = color;
                    target.autoAssign = row.role.autoAssign;
                    target.blocker = row.role.blocker;
                    target.enabled = row.role.enabled;
                    target.activeHours = row.role.activeHours;
                    target.locationTokens = row.role.locations
                        .Select(RuntimeLocationToken).Where(t => t != null).ToList();
                    target.holderMode = row.role.holderMode;
                    target.holderRangeSet = row.role.holderRangeSet;
                    target.minHolders = row.role.minHolders;
                    target.maxHolders = row.role.maxHolders;
                    target.trainingWaivers = row.role.trainingWaivers;
                    target.groupId = GroupIdFor(
                        row.role.groupId, row.role.group, doc, runtimeGroups, store);
                    // Hand-edited files can repeat an entry; first occurrence wins.
                    target.entries = row.role.entries.Distinct().ToList();
                    target.workTypeSnapshots.Clear();
                    row.existing = target;
                    runtimeRoles[row.role] = target;
                }
                RoleCommands.SweepEmptyGroups();
                CompiledJobOrders.InvalidateAll();
                Seeding.RefreshWorkTypeSnapshots();
            }

            // Paths and the order resolve names AFTER roles landed (they may
            // reference roles this same import just added).
            if (pathsInclude)
            {
                if (pathsOverwrite) store.trainingPaths.Clear();
                var selectedPaths = pathsOverwrite
                    ? Enumerable.Range(0, doc.trainingPaths.Count).ToList()
                    : (pathRows ?? new List<int>());
                foreach (int index in selectedPaths)
                {
                    if (index < 0 || index >= doc.trainingPaths.Count) continue;
                    var filePath = doc.trainingPaths[index];
                    var (ids, mins, maxes) = RoleFile.ResolvePathEntries(filePath, doc,
                        fileRole => runtimeRoles.TryGetValue(fileRole, out var runtime)
                            ? runtime.id : (int?)null);
                    var (hasPathColor, pathColor) = ResolveColor(filePath.colorRef, store, doc);
                    // Always a NEW path (names are not identities); unknown names
                    // dropped already, an unknown anchor means no anchor.
                    store.trainingPaths.Add(new TrainingPath
                    {
                        id = store.NextPathId(),
                        name = filePath.name,
                        roleIds = ids,
                        bandMins = mins,
                        bandMaxes = maxes,
                        anchorRoleId = RuntimeRole(doc, runtimeRoles,
                            RoleFile.AnchorWithStableId(filePath))?.id ?? -1,
                        anchorBefore = filePath.anchorBefore,
                        hasCustomColor = hasPathColor,
                        color = hasPathColor ? pathColor : Color.white,
                    });
                    pathsAdded++;
                }
            }
            IReadOnlyList<FileRoleReference> effectiveOrder =
                RoleFile.RecommendationOrderWithStableIds(doc);
            if (orderInclude && effectiveOrder.Count > 0)
            {
                store.recommendationOrder = effectiveOrder
                    .Select(reference => RuntimeRole(doc, runtimeRoles, reference)?.id)
                    .Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
            }

            return "WR_ImportSummary".Translate(added, updated, deleted, paletteChanges, pathsAdded);
        }

        private static Role RuntimeRole(RoleFileDocument document,
            Dictionary<FileRole, Role> runtimeRoles, string fileId, string label)
        {
            var fileRole = RoleFile.ResolveRole(document, fileId, label);
            return fileRole != null && runtimeRoles.TryGetValue(fileRole, out var runtime)
                ? runtime : null;
        }

        private static Role RuntimeRole(RoleFileDocument document,
            Dictionary<FileRole, Role> runtimeRoles, FileRoleReference reference) =>
            reference == null ? null : RuntimeRole(
                document, runtimeRoles, reference.fileId, reference.label);

        /// Resolves a file group name to a group id, creating unknown groups
        /// (leniency: a role may reference a name missing from <Groups>).
        private static int GroupIdFor(string fileId, string name, RoleFileDocument document,
            Dictionary<FileGroup, RoleGroup> runtimeGroups, RoleStore store)
        {
            var fileGroup = RoleFile.ResolveGroup(document, fileId, name);
            if (fileGroup != null && runtimeGroups.TryGetValue(fileGroup, out var mapped))
                return mapped.id;
            // Hand-edited files may name Default explicitly (we never write it).
            if (GroupNameRules.IsDefault(name)) return store.EnsureDefaultGroup().id;
            if (name.NullOrEmpty()) return RoleGroup.DefaultId;
            var group = store.GroupByName(name);
            if (group == null && GroupNameRules.IsAvailable(
                    name, store.groups, existing => existing.label))
            {
                group = new RoleGroup { id = store.NextGroupId(), label = name.Trim() };
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
                if (n == name) return ToUnity(c);
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
