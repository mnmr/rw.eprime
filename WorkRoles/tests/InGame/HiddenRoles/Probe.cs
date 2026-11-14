using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.UI;

// Hidden roles (user report 2026-11-14): a role can be hidden from the
// Colonists tab's role palette while still working everywhere else.
//  H1 the role editor shows a "Hidden role" checkbox; a real click on it
//     (through Shared automation) sets Role.hidden.
//  H2 the hidden role is gone from the palette in both modes (skills and
//     groups); every other role is still there.
//  H3 the row's + menu still lists the hidden role for a colonist without it,
//     and existing holders keep it.
//  H4 unhiding (another real click) brings it back to the palette.
namespace WorkRolesHiddenRolesVerification
{
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static readonly Harmony Patches = new Harmony("tests.workroles.hiddenroles");
        private static MainTabWindow_WorkRoles window;
        private static RoleStore store;
        private static Role role;
        private static Pawn pawn;
        private static int stage, settle, holders;
        private static HashSet<int> before;
        private static readonly Dictionary<string, string> Points = new Dictionary<string, string>();
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static;

        static Probe()
        {
            Patches.Patch(AccessTools.Method(typeof(Root_Play), "Update"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            Patches.Patch(AccessTools.Method(AccessTools.TypeByName("WorkRoles.UI.RolesTabView"), "DrawEditorChecks"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(Checks)));
        }

        private static void Log(string message)
        {
            Verse.Log.Message("[HiddenRolesProbe] " + message);
            File.AppendAllText(Path.Combine(Root, "hidden-roles.txt"), message + Environment.NewLine);
        }
        private static void Assert(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Log("PASS " + message);
        }
        private static void Mark(string value) => File.WriteAllText(Path.Combine(Root, "hidden-stage.txt"), value);

        /// The Hidden role checkbox is the fifth row of the editor's check
        /// column; its box sits at the right edge.
        private static void Checks(Rect rect, float rowH)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 local = new Vector2(rect.xMax - 12f, rect.y + rowH * 4.5f);
            Vector2 point = GUIUtility.GUIToScreenPoint(local);
            point += GUIUtility.GUIToScreenPoint(Vector2.zero) * (Prefs.UIScale - 1f);
            string value = Math.Round(point.x).ToString(CultureInfo.InvariantCulture) + ","
                + Math.Round(point.y).ToString(CultureInfo.InvariantCulture);
            if (Points.TryGetValue("hidden", out string previous) && previous == value) return;
            Points["hidden"] = value;
            File.WriteAllText(Path.Combine(Root, "hidden-point.txt"), value);
        }

        private static object Field(object instance, string name) =>
            AccessTools.Field(instance.GetType(), name).GetValue(instance);

        private static void SelectTab(string name)
        {
            FieldInfo field = AccessTools.Field(window.GetType(), "curTab");
            field.SetValue(window, Enum.Parse(field.FieldType, name));
        }

        /// Role ids shown in the palette for a mode, from a fresh catalog build.
        private static HashSet<int> Palette(PaletteMode mode)
        {
            Type type = AccessTools.TypeByName("WorkRoles.UI.ColonistsRosterCatalogSnapshot");
            object catalog = type.GetMethod("Build", All).Invoke(null,
                new object[] { store, null, true, true, true });
            var ids = new HashSet<int>();
            int clusters = (int)type.GetMethod("PaletteClusterCount", All).Invoke(catalog, new object[] { mode });
            for (int c = 0; c < clusters; c++)
            {
                object cluster = type.GetMethod("PaletteClusterAt", All).Invoke(catalog, new object[] { mode, c });
                int count = (int)AccessTools.Property(cluster.GetType(), "Count").GetValue(cluster);
                for (int r = 0; r < count; r++)
                {
                    object paletteRole = cluster.GetType().GetMethod("RoleAt", All).Invoke(cluster, new object[] { r });
                    object chip = AccessTools.Property(paletteRole.GetType(), "Chip").GetValue(paletteRole);
                    ids.Add((int)AccessTools.Property(chip.GetType(), "RoleId").GetValue(chip));
                }
            }
            return ids;
        }

        private static bool IsMember(int id) =>
            (bool)typeof(RoleStore).GetMethod("IsCompositeMember", All).Invoke(store, new object[] { id });
        private static string Shown(Role r) =>
            (string)AccessTools.Property(typeof(Role), "DisplayLabel").GetValue(r);

        private static int Holders() =>
            store.pawnSets.Values.Count(set => set.assignments.Any(a => a.roleId == role.id));

        private static void Update()
        {
            if (stage == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (settle++ < 6) return;
                switch (stage)
                {
                    case 0:
                    {
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                        foreach (Window popup in Find.WindowStack.Windows.ToArray())
                            if (!(popup is MainTabWindow)) popup.Close(false);
                        store = RoleStore.Current;
                        Assert(store != null, "real save loaded");
                        HashSet<int> skills = before = Palette(PaletteMode.Skills);
                        // Prefer a composite member, the case the report is about.
                        role = store.roles.FirstOrDefault(r => IsMember(r.id) && skills.Contains(r.id))
                            ?? store.roles.First(r => skills.Contains(r.id) && !r.composite);
                        Assert(!role.hidden, "'" + role.label + "' starts visible (composite member: "
                            + IsMember(role.id) + ")");
                        Assert(Palette(PaletteMode.Groups).Contains(role.id), "'" + role.label + "' starts in the group palette");
                        pawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned.First(p =>
                            !store.pawnSets.TryGetValue(p, out var set) || set.assignments.All(a => a.roleId != role.id));
                        RoleCommands.AssignRole(Find.CurrentMap.mapPawns.FreeColonistsSpawned.First(p => p != pawn), role.id);
                        holders = Holders();
                        var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                        window = (MainTabWindow_WorkRoles)button.TabWindow;
                        Find.MainTabsRoot.SetCurrentTab(button);
                        SelectTab("Colonists");
                        stage = 1; settle = 0; Mark("palette-before");
                        return;
                    }
                    case 1:
                        if (File.ReadAllText(Path.Combine(Root, "hidden-stage.txt")).Trim() != "go-editor") return;
                        SelectTab("Roles");
                        object rolesTab = Field(window, "rolesTab");
                        rolesTab.GetType().GetMethod("SelectRole", All).Invoke(rolesTab, new object[] { role.id });
                        stage = 2; settle = 0;
                        return;
                    case 2:
                        if (!File.Exists(Path.Combine(Root, "hidden-point.txt"))) return;
                        stage = 3; Mark("editor-before");
                        return;
                    case 3:
                        if (!role.hidden) return;
                        Assert(true, "a real click on the editor's Hidden role checkbox hides '" + role.label + "'");
                        Assert(!Palette(PaletteMode.Skills).Contains(role.id), "hidden role is gone from the skill palette");
                        Assert(!Palette(PaletteMode.Groups).Contains(role.id), "hidden role is gone from the group palette");
                        var expected = new HashSet<int>(before);
                        expected.Remove(role.id);
                        Assert(Palette(PaletteMode.Skills).SetEquals(expected),
                            "every other role stays in the palette (" + expected.Count + ")");
                        Assert(Holders() == holders && holders > 0, "existing holders keep the hidden role (" + holders + ")");
                        stage = 4; settle = 0; Mark("editor-hidden");
                        return;
                    case 4:
                        if (File.ReadAllText(Path.Combine(Root, "hidden-stage.txt")).Trim() != "go-colonists") return;
                        SelectTab("Colonists");
                        stage = 5; settle = 0;
                        return;
                    case 5:
                        stage = 6; Mark("palette-hidden");
                        return;
                    case 6:
                        if (File.ReadAllText(Path.Combine(Root, "hidden-stage.txt")).Trim() != "go-menu") return;
                        typeof(ColonistsTabView).GetMethod("OpenAddMenu", All).Invoke(null, new object[] { pawn, store });
                        var menu = Find.WindowStack.WindowOfType<FloatMenu>();
                        var options = (List<FloatMenuOption>)Field(menu, "options");
                        Assert(options.Any(o => o.Label == Shown(role)),
                            "the + menu for " + pawn.LabelShort + " still lists hidden '" + Shown(role) + "'");
                        stage = 7; settle = 0;
                        return;
                    case 7:
                        stage = 8; Mark("menu");
                        return;
                    case 8:
                        if (File.ReadAllText(Path.Combine(Root, "hidden-stage.txt")).Trim() != "go-unhide") return;
                        Find.WindowStack.WindowOfType<FloatMenu>()?.Close(false);
                        SelectTab("Roles");
                        stage = 9; settle = 0;
                        return;
                    case 9:
                        stage = 10; Mark("editor-unhide");
                        return;
                    case 10:
                        if (role.hidden) return;
                        Assert(Palette(PaletteMode.Skills).Contains(role.id) && Palette(PaletteMode.Groups).Contains(role.id),
                            "unhiding brings '" + role.label + "' back to both palettes");
                        stage = 99; Mark("SUCCESS");
                        return;
                }
            }
            catch (Exception ex)
            {
                Log("ERROR " + ex);
                stage = 99;
                Mark("FAILED " + ex.Message);
            }
        }
    }
}
