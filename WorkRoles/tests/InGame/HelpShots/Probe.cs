using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using RimShared.Tests.HelpShots;
using WorkRoles.UI;

// WorkRoles help image capture: the scenes and the mod's own recorder hooks.
// The scene walker, the recorder and the game-widget hooks are shared source
// (Shared/Tests/InGame/HelpShots); crops.json names the images cut from the
// frames (Shared/tools/automation/help-shots/crop-help.py).
namespace WorkRolesHelpShots
{
    public static class Hooks
    {
        private static PropertyInfo chipLabel;

        public static void Install(Harmony harmony)
        {
            Type view = typeof(ColonistsTabView);
            foreach (string name in new[] { "DrawPalettePanel", "DrawFilterRow", "DrawPawnTable",
                         "DrawTableHeader", "DrawStatsPanel", "DrawGroupHeader" })
                Patch(harmony, AccessTools.Method(view, name), nameof(Named));
            Patch(harmony, AccessTools.Method(view, "DrawRow"), nameof(Row));

            MethodInfo chip = AccessTools.GetDeclaredMethods(typeof(RoleChipUI)).First(m =>
                m.Name == "Draw" && m.GetParameters()[1].ParameterType.Name == "RoleChipRenderData");
            chipLabel = AccessTools.Property(chip.GetParameters()[1].ParameterType, "Label");
            Patch(harmony, chip, nameof(Chip));

            Patch(harmony, AccessTools.Method(AccessTools.TypeByName("WorkRoles.UI.OptionsPreviewState"), "Draw"), nameof(Preview));
            Patch(harmony, AccessTools.Method(typeof(OptionsTabView), "DisplayToggle"), nameof(Option));
            Patch(harmony, AccessTools.Method(typeof(OptionsTabView), "SegmentRow"), nameof(Segment));
            Patch(harmony, AccessTools.Method(AccessTools.TypeByName("WorkRoles.UI.WrLabels"), "HeaderLabel",
                new[] { typeof(Rect), typeof(string) }), nameof(Header));

            // Roles tab, Recommendations tab and the dialogs: "<prefix>:<Draw method>".
            VanillaHooks.PatchDrawMethods(harmony, typeof(RolesTabView), "roles");
            VanillaHooks.PatchDrawMethods(harmony, typeof(RecommendationsTabView), "recs");
            foreach (string dialog in new[] { "PriorityGrid", "ChangesPreview", "PreviewBase", "ImportSource",
                         "ExportPreview", "RestorePreview", "RoleIconPicker", "RoleFilePicker" })
                VanillaHooks.PatchDrawMethods(harmony, AccessTools.TypeByName("WorkRoles.UI.Dialog_" + dialog), dialog);
        }

        private static void Patch(Harmony harmony, MethodInfo target, string prefix) =>
            VanillaHooks.Patch(harmony, target, prefix, typeof(Hooks));

        public static void Named(Rect rect, MethodBase __originalMethod)
        {
            switch (__originalMethod.Name)
            {
                case "DrawPalettePanel": Recorder.Add("palette", rect); break;
                case "DrawFilterRow": Recorder.Add("filter", rect); break;
                case "DrawPawnTable": Recorder.Add("table", rect); break;
                case "DrawTableHeader": Recorder.Add("header", rect); break;
                case "DrawStatsPanel": Recorder.Add("panel", rect); break;
                case "DrawGroupHeader": Recorder.Add("group", rect); break;
            }
        }

        public static void Row(Rect rect, Pawn pawn) => Recorder.Add("row:" + pawn.LabelShort, rect);

        public static void Chip(object[] __args)
        {
            var pawn = __args[4] as Pawn;
            string label = (string)chipLabel.GetValue(__args[1]);
            Recorder.Add("chip:" + (pawn == null ? "" : pawn.LabelShort) + ":" + label, (Rect)__args[0]);
        }

        public static void Preview(Rect rect) => Recorder.Add("preview", rect);
        public static void Option(Rect rect, string label) => Recorder.Add("opt:" + Recorder.Clean(label), rect);
        public static void Segment(float x, float y, float width, string label) =>
            Recorder.Add("opt:" + Recorder.Clean(label), new Rect(x, y, width, 28f));
        public static void Header(Rect rect, string text) => Recorder.Add("head:" + Recorder.Clean(text), rect);
    }

    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static MainTabWindow_WorkRoles window;
        private static Pawn koen;

        static Probe() => HelpShots.Run("tests.workroles.helpshots", Hooks.Install, Setup);

        private static void SelectTab(string name)
        {
            FieldInfo field = AccessTools.Field(window.GetType(), "curTab");
            field.SetValue(window, Enum.Parse(field.FieldType, name));
        }

        private static object Selection()
        {
            object tab = AccessTools.Field(window.GetType(), "colonistsTab").GetValue(window);
            return AccessTools.Field(tab.GetType(), "selection").GetValue(tab);
        }

        private static void Click(Pawn pawn)
        {
            object selection = Selection();
            AccessTools.Method(selection.GetType(), "Click").Invoke(selection, new object[] { pawn });
        }

        private static void Toggle(Pawn pawn)
        {
            object selection = Selection();
            AccessTools.Method(selection.GetType(), "Toggle").Invoke(selection, new object[] { pawn });
        }

        private static Pawn Colonist(string name)
        {
            Pawn pawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.LabelShort == name);
            if (pawn == null) throw new InvalidOperationException("no colonist " + name);
            return pawn;
        }

        private static int RoleId(string label)
        {
            RoleStore store = RoleStore.Current;
            foreach (RoleAssignment a in store.pawnSets[koen].assignments)
                if (store.RoleById(a.roleId).label == label) return a.roleId;
            throw new InvalidOperationException(koen.LabelShort + " has no role " + label);
        }

        private static void State(string label, AssignmentState state) =>
            RoleCommands.SetAssignmentState(koen, RoleId(label), (int)state);

        private static void GlobalToggle(string label) => RoleCommands.ToggleRoleGlobal(RoleId(label));

        private static void Pin(string label, bool wanted)
        {
            int id = RoleId(label);
            RoleAssignment a = RoleStore.Current.pawnSets[koen].assignments.First(x => x.roleId == id);
            if (a.pinned != wanted) RoleCommands.ToggleAssignmentPin(koen, id);
        }

        private static Scene Add(string name, Action act, string hover = null, string click = null) =>
            HelpShots.Add(name, act, hover, click);

        private static Role RoleNamed(string label)
        {
            Role role = RoleStore.Current.roles.FirstOrDefault(r => r.label == label);
            if (role == null) throw new InvalidOperationException("no role " + label);
            return role;
        }

        private static object RolesTab => AccessTools.Field(window.GetType(), "rolesTab").GetValue(window);

        private static void EditRole(string label)
        {
            SelectTab("Roles");
            AccessTools.Method(RolesTab.GetType(), "SelectRole").Invoke(RolesTab, new object[] { RoleNamed(label).id });
        }

        private static object EditorState => AccessTools.Field(RolesTab.GetType(), "editorState").GetValue(RolesTab);

        private static object RecsTab => AccessTools.Field(window.GetType(), "recommendationsTab").GetValue(window);

        private static void ExpandRecRole(int roleId) =>
            AccessTools.Field(RecsTab.GetType(), "expandedRoleId").SetValue(RecsTab, roleId);

        /// Scrolls the Recommendations tab until a region sits 60 px below
        /// the tab's top (or as near as the end of the page lets it come).
        private static Func<bool> RecsScrolledTo(string region)
        {
            int calls = 0;
            float last = float.NaN;
            return () =>
            {
                // Recorded regions lag the scroll change by a frame or two.
                if (calls++ % 4 != 0) return false;
                FieldInfo field = AccessTools.Field(RecsTab.GetType(), "tabScroll");
                var position = (Vector2)field.GetValue(RecsTab);
                Rect? tab = Recorder.Find("recs:Draw");
                Rect? found = Recorder.Find(region);
                if (tab == null) return false;
                float wanted = tab.Value.y + 60f;
                if (found == null) position.y += 300f;
                else if (Mathf.Abs(found.Value.y - wanted) < 1f || found.Value.y == last) return true;
                else { last = found.Value.y; position.y += found.Value.y - wanted; }
                field.SetValue(RecsTab, position);
                return false;
            };
        }

        private static Bill cookBill;

        private static void OpenCookBill()
        {
            Role cook = RoleNamed("Cook");
            foreach (Building building in Find.CurrentMap.listerBuildings.allBuildingsColonist)
                if (building is Building_WorkTable table)
                    foreach (Bill bill in table.BillStack.Bills)
                        if (bill is Bill_Production production && BillRoles.EligibleRoles(bill).Contains(cook))
                        {
                            cookBill = bill;
                            RoleCommands.SetBillRole(bill, cook.id);
                            Find.WindowStack.Add(new Dialog_BillConfig(production, table.Position));
                            return;
                        }
            throw new InvalidOperationException("no bill the Cook role can work");
        }

        private static void BuildScenes()
        {
            WorkRolesSettings s = WorkRolesMod.Settings;
            // Colonist rows: Koen's row is the subject and stays unselected.
            Add("default", () => { });
            Add("basics-off", () => State("Basics", AssignmentState.Disabled));
            Add("basics-forced", () => State("Basics", AssignmentState.ForceOn));
            Add("global-off", () => { State("Basics", AssignmentState.Enabled); GlobalToggle("Basics"); });
            Add("both-off", () => State("Basics", AssignmentState.Disabled));
            Add("pinned", () =>
            {
                State("Basics", AssignmentState.Enabled); GlobalToggle("Basics");
                Pin("Core", true);
            });
            Add("chip-tip", () => Pin("Core", false), hover: "chip:Koen:Handler");
            Add("palette-tip", () => { }, hover: "chip::Butcher@palette");
            Add("initials", () => s.chipDisplay = ChipDisplay.Compact);
            Add("icons", () => s.chipDisplay = ChipDisplay.Icons);
            Add("initials-grid", () => { s.chipGrid = true; s.chipDisplay = ChipDisplay.Compact; });
            Add("icons-grid", () => s.chipDisplay = ChipDisplay.Icons);
            Add("multi", () =>
            {
                s.chipGrid = false; s.chipDisplay = ChipDisplay.Normal;
                Click(Colonist("Tontrool"));
                Toggle(Colonist("Bashy"));
                Toggle(Colonist("Flow"));
            });
            // Selected-colonist panel: a colonist with every kind of star.
            Add("panel", () => Click(Colonist("Hasonove")));
            Add("skill-tip", () => { }, hover: "label:Shooting@panel");
            Add("options", () => SelectTab("Options"));
            Add("grouped", () =>
            {
                SelectTab("Colonists");
                Click(Colonist("Madcat"));
                s.groupBy = "gender";
            });
            Add("skill-column", () =>
            {
                s.groupBy = "none";
                s.skillColumns = new List<string> { "Medicine" };
                s.sortColumn = "Medicine";
            });
            // Dialogs opened from the Colonists tab.
            Add("priority-grid", () => { s.skillColumns = new List<string>(); s.sortColumn = ""; }, click: "img:PriorityGrid");
            Add("fix-preview", () => HelpShots.CloseWindows(), click: "btn:Fix My Colony");
            // A cooking bill restricted to the Cook role, its worker menu open.
            Add("bill-role", () => { HelpShots.CloseWindows(); OpenCookBill(); },
                hover: "window:FloatMenu>60,130", click: "btn:Role: Cook");

            // Roles tab: the role list and the editor of an ordinary role.
            Add("roles-core", () =>
            {
                HelpShots.CloseWindows();
                RoleCommands.SetBillRole(cookBill, -1);
                s.nestedRoleTree = true;
                EditRole("Core");
            });
            Add("roles-jobs", () => AccessTools.Method(EditorState.GetType(), "EnsureWorkTypeExpanded")
                .Invoke(EditorState, new object[] { "Doctor" }));
            Add("roles-multi", () =>
            {
                object selection = AccessTools.Field(RolesTab.GetType(), "entrySelection").GetValue(RolesTab);
                AccessTools.Method(selection.GetType(), "SelectAll").Invoke(selection, new object[] { RoleNamed("Core").entries });
            });
            Add("roles-blocker", () => EditRole("Pyrophobia"));
            Add("roles-rules", () => EditRole("Miner Away"));
            Add("roles-members", () => EditRole("Rancher"));
            Add("icon-picker", () => EditRole("Core"), click: "label:Core@roles:DrawEditor>-17,15");
            Add("export-dialog", () => HelpShots.CloseWindows(), click: "btn:Export");
            Add("import-dialog", () =>
            {
                HelpShots.CloseWindows();
                var dialog = (Window)Activator.CreateInstance(AccessTools.TypeByName("WorkRoles.UI.Dialog_ImportSource"));
                Find.WindowStack.Add(dialog);
                // The dialog samples the clipboard when it opens; the image
                // shows it holding an export, whatever it holds in this run.
                AccessTools.Field(dialog.GetType(), "clip").SetValue(dialog, "<WorkRoles/>");
                AccessTools.Field(dialog.GetType(), "clipUsable").SetValue(dialog, true);
            });
            Add("restore-dialog", () => HelpShots.CloseWindows(), click: "btn:Restore Defaults");

            // Recommendations tab.
            Add("recs", () => { HelpShots.CloseWindows(); SelectTab("Recommendations"); ExpandRecRole(-1); });
            Add("recs-path", () => ExpandRecRole(RoleNamed("Caretaker").id));
            // Smith sits far down the list: scroll its panel to the top.
            Add("recs-role", () => ExpandRecRole(RoleNamed("Smith").id)).Until = RecsScrolledTo("chip::Smith@recs:Draw");
            Add(null, () =>
            {
                ExpandRecRole(-1);
                AccessTools.Field(RecsTab.GetType(), "tabScroll").SetValue(RecsTab, Vector2.zero);
                SelectTab("Colonists");
            });
            // Last: it changes a colonist for the rest of the run.
            Add("downed", () =>
            {
                s.skillColumns = new List<string>();
                s.sortColumn = "";
                // Anesthesia downs a colonist the same way every run
                // (damage would be random).
                Colonist("Hysott").health.AddHediff(HediffDefOf.Anesthetic);
            });
            // Every chip in a downed colonist's Recommended Roles says so.
            Add("downed-tip", () => Click(Colonist("Hysott")), hover: "chip::Core@panel");
        }

        private static void Setup()
        {
            WorkRolesSettings s = WorkRolesMod.Settings;
            // Reproducible starting state: the mod's defaults, stacked chips.
            s.chipGrid = false;
            s.chipDisplay = ChipDisplay.Normal;
            s.chipMarkers = ChipMarkers.Dots;
            s.paletteMode = PaletteMode.Skills;
            s.groupBy = "none";
            s.skillColumns = new List<string>();
            s.sortColumn = "";
            s.removeRoleIcon = true;
            s.recommendationsEnabled = true;
            s.verdictsOnColonistChips = true;
            s.verdictsInPalette = true;
            s.colonistSkillCaptions = true;
            if (RoleStore.Current == null) throw new InvalidOperationException("no role store");
            koen = Colonist("Koen");
            var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
            window = (MainTabWindow_WorkRoles)button.TabWindow;
            Find.MainTabsRoot.SetCurrentTab(button);
            SelectTab("Colonists");
            BuildScenes();
        }
    }
}
