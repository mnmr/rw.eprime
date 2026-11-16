using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.UI;

// Role chip markers: the four-light dot stripe (default), the icon display
// and markers off, on the real Fisso colony. The first colonist with four
// roles gets one role off, one always on, one pinned and one globally off so
// every state lamp colour is on screen. The driving script captures each
// display and the Options row; the visuals are judged from those captures.
namespace WorkRolesChipMarkersVerification
{
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static readonly Harmony Patches = new Harmony("tests.workroles.chipmarkers");
        private static MainTabWindow_WorkRoles window;
        private static int stage, settle, assignments;
        private static int Assignments() =>
            RoleStore.Current.pawnSets.Values.Sum(set => set.assignments.Count);
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static string StageFile => Path.Combine(Root, "markers-stage.txt");

        static Probe()
        {
            Patches.Patch(AccessTools.Method(typeof(Root_Play), "Update"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
        }

        private static void Log(string message)
        {
            Verse.Log.Message("[ChipMarkersProbe] " + message);
            File.AppendAllText(Path.Combine(Root, "chip-markers.txt"), message + Environment.NewLine);
        }
        private static void Mark(string value) => File.WriteAllText(StageFile, value);
        private static bool Told(string value)
        {
            // The driving script may be writing the file this frame.
            try { return File.Exists(StageFile) && File.ReadAllText(StageFile).Trim() == value; }
            catch (IOException) { return false; }
        }

        private static void SelectTab(string name)
        {
            FieldInfo field = AccessTools.Field(window.GetType(), "curTab");
            field.SetValue(window, Enum.Parse(field.FieldType, name));
        }

        private static void Show(ChipMarkers markers, string mark)
        {
            WorkRolesMod.Settings.chipMarkers = markers;
            stage++; settle = 0; Mark(mark);
        }

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
                        RoleStore store = RoleStore.Current;
                        if (store == null) throw new InvalidOperationException("no role store");
                        Pawn pawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned.First(p =>
                            store.pawnSets.TryGetValue(p, out var set) && set.assignments.Count >= 4);
                        var roles = store.pawnSets[pawn].assignments.Select(a => a.roleId).ToList();
                        RoleCommands.SetAssignmentState(pawn, roles[0], (int)AssignmentState.Disabled);
                        RoleCommands.SetAssignmentState(pawn, roles[1], (int)AssignmentState.ForceOn);
                        if (!store.pawnSets[pawn].assignments[2].pinned)
                            RoleCommands.ToggleAssignmentPin(pawn, roles[2]);
                        if (store.RoleById(roles[3]).enabled)
                            RoleCommands.ToggleRoleGlobal(roles[3]);
                        Log(pawn.LabelShort + ": off=" + store.RoleById(roles[0]).label
                            + " always-on=" + store.RoleById(roles[1]).label
                            + " pinned=" + store.RoleById(roles[2]).label
                            + " global-off=" + store.RoleById(roles[3]).label);
                        var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                        window = (MainTabWindow_WorkRoles)button.TabWindow;
                        Find.MainTabsRoot.SetCurrentTab(button);
                        SelectTab("Colonists");
                        WorkRolesMod.Settings.chipMarkers = ChipMarkers.Dots;
                        stage = 1; settle = 0; Mark("dots");
                        return;
                    }
                    case 1:
                        if (Told("skip-previews"))
                        {
                            WorkRolesMod.Settings.paletteMode = PaletteMode.Groups;
                            stage = 12; settle = 0; Mark("groups");
                        }
                        else if (Told("go-grid"))
                        {
                            // Fixed-width chips: the script clicks a chip
                            // through its states and compares its neighbours.
                            WorkRolesMod.Settings.chipGrid = true;
                            stage = 20; settle = 0; Mark("grid");
                        }
                        else if (Told("go-icons")) Show(ChipMarkers.Icons, "icons");
                        return;
                    case 20:
                        if (!Told("go-done")) return;
                        WorkRolesMod.Settings.chipGrid = false;
                        stage = 99; Mark("SUCCESS");
                        return;
                    case 2: if (Told("go-off")) Show(ChipMarkers.Off, "off"); return;
                    case 3: if (Told("go-dots")) Show(ChipMarkers.Dots, "dots-again"); return;
                    case 4:
                        if (!Told("go-options")) return;
                        SelectTab("Options");
                        stage = 5; settle = 0; Mark("options");
                        return;
                    case 5:
                        // Remove icon off: no trash can, the right-click
                        // menu carries Remove role.
                        if (!Told("go-noicon")) return;
                        SelectTab("Colonists");
                        WorkRolesMod.Settings.removeRoleIcon = false;
                        assignments = Assignments();
                        stage = 6; settle = 0; Mark("noicon");
                        return;
                    case 6:
                    {
                        // The script right-clicks the first chip of the
                        // first row through the automation pipe.
                        // The click-free tour skips the menu.
                        if (Told("go-norecs")) { stage = 8; return; }
                        // The click-free tour skips the menu.
                        if (Told("go-norecs")) { stage = 8; return; }
                        FloatMenu menu = Find.WindowStack.WindowOfType<FloatMenu>();
                        if (menu == null) return;
                        var options = (System.Collections.Generic.List<FloatMenuOption>)
                            AccessTools.Field(typeof(FloatMenu), "options").GetValue(menu);
                        string remove = "WR_RemoveRole".Translate();
                        if (options.All(o => o.Label != remove))
                            throw new InvalidOperationException("menu has no '" + remove + "': "
                                + string.Join(", ", options.Select(o => o.Label)));
                        Log("PASS a real right-click opens a menu with '" + remove + "' ("
                            + string.Join(", ", options.Select(o => o.Label)) + ")");
                        stage = 7; settle = 0; Mark("menu");
                        return;
                    }
                    case 7:
                    {
                        if (!Told("go-remove")) return;
                        FloatMenu menu = Find.WindowStack.WindowOfType<FloatMenu>();
                        var options = (System.Collections.Generic.List<FloatMenuOption>)
                            AccessTools.Field(typeof(FloatMenu), "options").GetValue(menu);
                        string remove = "WR_RemoveRole".Translate();
                        options.First(o => o.Label == remove).action();
                        menu.Close(false);
                        if (Assignments() != assignments - 1)
                            throw new InvalidOperationException("Remove role left " + Assignments()
                                + " assignments, expected " + (assignments - 1));
                        Log("PASS the menu's Remove role removes exactly one assignment");
                        stage = 8; settle = 0; Mark("removed");
                        return;
                    }
                    case 8:
                        if (!Told("go-norecs")) return;
                        WorkRolesMod.Settings.removeRoleIcon = true;
                        WorkRolesMod.Settings.recommendationsEnabled = false;
                        stage = 9; settle = 0; Mark("norecs");
                        return;
                    case 9:
                        if (!Told("go-norecs-options")) return;
                        SelectTab("Options");
                        stage = 10; settle = 0; Mark("norecs-options");
                        return;
                    case 10:
                        if (!Told("go-recs")) return;
                        WorkRolesMod.Settings.recommendationsEnabled = true;
                        SelectTab("Colonists");
                        stage = 11; settle = 0; Mark("recs");
                        return;
                    case 11:
                        // Groups palette: no rules dot (Conditional says it).
                        if (!Told("go-groups")) return;
                        WorkRolesMod.Settings.paletteMode = PaletteMode.Groups;
                        // Selected-colonist verdicts off: no stars in the
                        // palette or the skill panel.
                        WorkRolesMod.Settings.verdictsInPalette = false;
                        stage = 12; settle = 0; Mark("groups");
                        return;
                    case 12:
                        // Options previews under the Groups palette: the row
                        // preview keeps its roles, the palette regroups them.
                        if (Told("go-done"))
                        {
                            WorkRolesMod.Settings.paletteMode = PaletteMode.Skills;
                            WorkRolesMod.Settings.verdictsInPalette = true;
                            stage = 99; Mark("SUCCESS");
                            return;
                        }
                        if (!Told("go-groups-options")) return;
                        WorkRolesMod.Settings.verdictsInPalette = true;
                        SelectTab("Options");
                        stage = 13; settle = 0; Mark("groups-options");
                        return;
                    case 13:
                        if (!Told("go-skills-options")) return;
                        WorkRolesMod.Settings.paletteMode = PaletteMode.Skills;
                        stage = 15; settle = 0; Mark("skills-options");
                        return;
                    case 15:
                    {
                        // Wrapping: a colonist with more than four traits and
                        // every role assigned, so the trait list becomes one
                        // wrapped text and the recommendations take rows.
                        if (!Told("go-wrap")) return;
                        RoleStore store = RoleStore.Current;
                        Pawn pawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned.First();
                        foreach (TraitDef def in DefDatabase<TraitDef>.AllDefsListForReading)
                        {
                            if (pawn.story.traits.allTraits.Count >= 6) break;
                            if (pawn.story.traits.HasTrait(def)
                                || pawn.story.traits.allTraits.Any(t => def.ConflictsWith(t)))
                                continue;
                            pawn.story.traits.GainTrait(new Trait(def, def.degreeDatas[0].degree, true));
                        }
                        RoleCommands.PasteRoleSet(pawn, store.roles.Select(r =>
                            new RoleAssignment { roleId = r.id }).ToList());
                        SelectTab("Colonists");
                        object tab = AccessTools.Field(window.GetType(), "colonistsTab").GetValue(window);
                        object selection = AccessTools.Field(tab.GetType(), "selection").GetValue(tab);
                        AccessTools.Method(selection.GetType(), "Click").Invoke(selection, new object[] { pawn });
                        Log(pawn.LabelShort + ": traits=" + string.Join(", ",
                            pawn.story.traits.allTraits.Select(t => t.LabelCap))
                            + " roles=" + store.pawnSets[pawn].assignments.Count);
                        stage = 14; settle = 0; Mark("wrap");
                        return;
                    }
                    case 14:
                        if (!Told("go-done")) return;
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
