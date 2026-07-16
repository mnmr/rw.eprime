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

        /// Our folder under the game's per-user data root (beside Saves\, Config\).
        public static string GameDataDir =>
            Path.Combine(GenFilePaths.SaveDataFolderPath, "WorkRoles");

        public static string ExportFile => Path.Combine(GameDataDir, DefaultFileName);

        public static RoleFileDocument Parse(string xml) => RoleFile.Parse(xml);

        /// The full export document — the custom palette slots the exported roles
        /// actually USE, plus every non-managed role — as indented XML text.
        public static string BuildXml(RoleStore store)
        {
            store.SyncSwatchNames();
            var doc = new RoleFileDocument();
            var usedSlots = new SortedSet<int>();
            foreach (var role in store.roles)
                if (!role.managed && role.hasCustomColor)
                {
                    int slot = CustomSlotOf(role.color, store);
                    if (slot >= 0) usedSlots.Add(slot);
                }
            foreach (int slot in usedSlots)
                doc.palette.Add((store.customSwatchNames[slot], ToRgb(store.customSwatches[slot])));
            foreach (var group in store.groups)
                if (group.id != RoleGroup.DefaultId)
                    doc.groups.Add(group.label);
            foreach (var role in store.roles)
            {
                if (role.managed) continue;
                doc.roles.Add(new FileRole
                {
                    label = role.label,
                    templateDef = role.templateDefName,
                    group = role.groupId == RoleGroup.DefaultId ? null
                        : store.GroupById(role.groupId)?.label,
                    colorRef = role.hasCustomColor ? EncodeColorRef(role.color, store, doc) : null,
                    autoAssign = role.autoAssign,
                    blocker = role.blocker,
                    enabled = role.enabled,
                    activeHours = role.activeHours,
                    locations = role.locationTokens.Select(FileLocationToken).Where(t => t != null).ToList(),
                    trainSkill = role.trainSkill,
                    trainMin = role.trainMin,
                    trainMax = role.trainMax,
                    // Targets travel by role NAME (ids are save-local).
                    trainTargets = role.trainTargets
                        .Select(id => store.RoleById(id)?.label)
                        .Where(l => l != null).ToList(),
                    minHolders = role.minHolders,
                    entries = role.entries.ToList(),
                });
            }
            return RoleFile.Build(doc);
        }

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
        }

        /// Existing role a file role maps onto: template id first, then exact label.
        public static Role MatchRole(RoleStore store, FileRole imported)
        {
            if (!imported.templateDef.NullOrEmpty())
            {
                var byTemplate = store.RoleByTemplate(imported.templateDef);
                if (byTemplate != null) return byTemplate;
            }
            return store.roles.FirstOrDefault(r => !r.managed && r.label == imported.label);
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

        public static List<RoleRow> RoleRows(RoleStore store, RoleFileDocument doc)
        {
            var rows = new List<RoleRow>();
            foreach (var imported in doc.roles)
                rows.Add(new RoleRow { role = imported, existing = MatchRole(store, imported) });
            return rows;
        }

        /// Roles deleted by an overwrite: in the catalog (non-managed) but not in the file.
        public static List<Role> OverwriteDeletes(RoleStore store, RoleFileDocument doc)
        {
            var kept = new HashSet<Role>(RoleRows(store, doc).Where(r => r.existing != null).Select(r => r.existing));
            return store.roles.Where(r => !r.managed && !kept.Contains(r)).ToList();
        }

        /// Applies an import. Selections are row indices into PaletteMergeRows /
        /// RoleRows; ignored in overwrite modes (wholesale). Returns a summary.
        public static string Apply(RoleStore store, RoleFileDocument doc,
            bool paletteInclude, bool paletteOverwrite, List<int> paletteRows,
            bool rolesInclude, bool rolesOverwrite, List<int> roleRows)
        {
            int paletteChanges = 0, updated = 0, added = 0, deleted = 0;
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
                // Groups travel with the roles section: merge appends missing
                // groups at the end; overwrite adopts the file's order (Default
                // stays pinned, groups the file doesn't know keep their spot).
                if (rolesOverwrite)
                {
                    var ordered = new List<RoleGroup>();
                    var defaultGroup = store.GroupById(RoleGroup.DefaultId);
                    if (defaultGroup != null) ordered.Add(defaultGroup);
                    foreach (var name in doc.groups)
                        ordered.Add(store.GroupByName(name)
                            ?? new RoleGroup { id = store.NextGroupId(), label = name });
                    foreach (var group in store.groups)
                        if (!ordered.Contains(group)) ordered.Add(group);
                    store.groups.Clear();
                    store.groups.AddRange(ordered);
                }
                else
                {
                    foreach (var name in doc.groups)
                        if (store.GroupByName(name) == null)
                            store.groups.Add(new RoleGroup { id = store.NextGroupId(), label = name });
                }

                var rows = RoleRows(store, doc);
                var selected = rolesOverwrite
                    ? Enumerable.Range(0, rows.Count).ToList()
                    : (roleRows ?? new List<int>());
                if (rolesOverwrite)
                {
                    foreach (var role in OverwriteDeletes(store, doc))
                    {
                        RoleCommands.DeleteRole(role.id);
                        deleted++;
                    }
                }
                var pendingTrainTargets = new List<(Role role, List<string> names)>();
                foreach (int index in selected)
                {
                    if (index < 0 || index >= rows.Count) continue;
                    var row = rows[index];
                    var (hasColor, color) = ResolveColor(row.role.colorRef, store, doc);
                    var target = row.existing;
                    if (target == null)
                    {
                        target = new Role { id = store.NextId() };
                        target.templateDefName = !row.role.templateDef.NullOrEmpty()
                            && store.RoleByTemplate(row.role.templateDef) == null ? row.role.templateDef : null;
                        store.roles.Add(target);
                        added++;
                    }
                    else
                    {
                        updated++;
                    }
                    target.label = row.role.label;
                    target.hasCustomColor = hasColor;
                    if (hasColor) target.color = color;
                    target.autoAssign = row.role.autoAssign;
                    target.blocker = row.role.blocker;
                    target.enabled = row.role.enabled;
                    target.activeHours = row.role.activeHours;
                    target.locationTokens = row.role.locations
                        .Select(RuntimeLocationToken).Where(t => t != null).ToList();
                    target.trainSkill = row.role.trainSkill;
                    target.trainMin = row.role.trainMin;
                    target.trainMax = row.role.trainMax;
                    target.minHolders = row.role.minHolders;
                    pendingTrainTargets.Add((target, row.role.trainTargets));
                    target.groupId = GroupIdFor(row.role.group, store);
                    // Hand-edited files can repeat an entry; first occurrence wins.
                    target.entries = row.role.entries.Distinct().ToList();
                    target.workTypeSnapshots.Clear();
                }
                // Train targets resolve by NAME once every imported role exists
                // (a role may target one applied later in the file).
                foreach (var (role, names) in pendingTrainTargets)
                    role.trainTargets = names
                        .Select(n => store.roles.FirstOrDefault(r => r != role
                            && string.Equals(r.label, n, System.StringComparison.OrdinalIgnoreCase))?.id)
                        .Where(id => id.HasValue).Select(id => id.Value).Distinct().ToList();
                RoleCommands.SweepEmptyGroups();
                CompiledJobOrders.InvalidateAll();
                Seeding.RefreshWorkTypeSnapshots();
            }

            return "WR_ImportSummary".Translate(added, updated, deleted, paletteChanges);
        }

        /// Resolves a file group name to a group id, creating unknown groups
        /// (leniency: a role may reference a name missing from <Groups>).
        private static int GroupIdFor(string name, RoleStore store)
        {
            if (name.NullOrEmpty()) return RoleGroup.DefaultId;
            // Hand-edited files may name Default explicitly (we never write it).
            if (string.Equals(name.Trim(), "Default", System.StringComparison.Ordinal))
                return store.EnsureDefaultGroup().id;
            var group = store.GroupByName(name);
            if (group == null)
            {
                group = new RoleGroup { id = store.NextGroupId(), label = name.Trim() };
                store.groups.Add(group);
            }
            return group.id;
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
