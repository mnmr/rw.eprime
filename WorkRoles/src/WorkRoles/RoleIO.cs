using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.UI;

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
            foreach (var role in store.roles)
            {
                if (role.managed) continue;
                doc.roles.Add(new FileRole
                {
                    label = role.label,
                    templateDef = role.templateDefName,
                    colorRef = role.hasCustomColor ? EncodeColorRef(role.color, store) : null,
                    autoAssign = role.autoAssign,
                    blocker = role.blocker,
                    enabled = role.enabled,
                    activeHours = role.activeHours,
                    location = role.location.ToString(),
                    entries = role.entries.ToList(),
                });
            }
            return RoleFile.Build(doc);
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
        /// the slot's palette name; anything else -> plain hex.
        private static string EncodeColorRef(Color color, RoleStore store)
        {
            var swatches = RolesTabView.Swatches;
            for (int i = 0; i < swatches.Length; i++)
                if (color.IndistinguishableFrom(swatches[i]))
                    return RolesTabView.ExportSwatchName(i);
            int slot = CustomSlotOf(color, store);
            if (slot >= 0)
                return store.customSwatchNames[slot];
            return ToRgb(color).Hex();
        }

        /// The custom slot exactly matching the color, or -1. Built-in swatches
        /// win (a color equal to both exports as its Tailwind name).
        private static int CustomSlotOf(Color color, RoleStore store)
        {
            foreach (var swatch in RolesTabView.Swatches)
                if (color.IndistinguishableFrom(swatch))
                    return -1;
            for (int i = 0; i < store.customSwatches.Count; i++)
                if (store.customSwatches[i].a >= 0.5f && color.IndistinguishableFrom(store.customSwatches[i]))
                    return i;
            return -1;
        }

        private static readonly Color FallbackColor = RolesTabView.Swatches[2 * 19]; // slate-600

        /// Resolves a role's color reference: built-in Tailwind name, then a store
        /// slot name, then the file's palette, then hex; unknown names fall back to
        /// slate-600. Null = no custom color.
        private static (bool has, Color color) ResolveColor(string colorRef, RoleStore store, RoleFileDocument doc)
        {
            if (colorRef.NullOrEmpty()) return (false, default);
            var swatches = RolesTabView.Swatches;
            for (int i = 0; i < swatches.Length; i++)
                if (RolesTabView.ExportSwatchName(i) == colorRef)
                    return (true, swatches[i]);
            int slot = store.customSwatchNames.IndexOf(colorRef);
            if (slot >= 0 && slot < store.customSwatches.Count && store.customSwatches[slot].a >= 0.5f)
                return (true, store.customSwatches[slot]);
            foreach (var (name, color) in doc.palette)
                if (name == colorRef)
                    return (true, ToUnity(color));
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
                foreach (var (name, rgb) in doc.palette.Take(32))
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
                    target.location = System.Enum.TryParse(row.role.location, out RoleLocation loc)
                        ? loc : RoleLocation.Any;
                    target.entries = row.role.entries.ToList();
                    target.workTypeSnapshots.Clear();
                }
                CompiledJobOrders.InvalidateAll();
                Seeding.RefreshWorkTypeSnapshots();
            }

            return "WR_ImportSummary".Translate(added, updated, deleted, paletteChanges);
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
            for (int i = 0; i < 32; i++)
                if (i >= store.customSwatches.Count || store.customSwatches[i].a < 0.5f)
                    return i;
            return -1;
        }
    }
}
