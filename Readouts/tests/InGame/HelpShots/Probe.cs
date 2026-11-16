using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EPrimeReadouts;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;
using HarmonyLib;
using RimShared.Tests.HelpShots;
using RimWorld;
using UnityEngine;
using Verse;

// Readouts help image capture: the scenes and the mod's own recorder hooks.
// The scene walker, the recorder and the game-widget hooks are shared source
// (Shared/Tests/InGame/HelpShots); crops.json names the images cut from the
// frames (Shared/tools/automation/help-shots/crop-help.py).
namespace ReadoutsHelpShots
{
    public static class Hooks
    {
        private static readonly Assembly Mod = typeof(ReadoutStore).Assembly;
        private static FieldInfo drawField;

        public static void Install(Harmony harmony)
        {
            // "<prefix>:<Draw method>" for the editor window's parts and the dialogs.
            foreach (var pair in new Dictionary<string, string>
                     {
                         { "Dialog_ReadoutConfig", "config" }, { "GroupListView", "groups" }, { "EditorView", "editor" },
                         { "PoolListView", "pools" }, { "ResourcePanelView", "resources" }, { "OptionsTabView", "options" },
                         { "ItemPickerFilterBar", "filter" }, { "GroupResourceTreeBody", "tree" },
                         { "PoolResourceTreeBody", "pooltree" }, { "Dialog_ExportReadouts", "export" },
                         { "Dialog_ImportReadouts", "import" }, { "Dialog_SetupChoice", "setup" },
                     })
                VanillaHooks.PatchDrawMethods(harmony, Mod.GetType("EPrimeReadouts.UI." + pair.Key), pair.Value);
            VanillaHooks.Region(harmony, Mod.GetType("RimShared.UiLib.SegmentedControl"), "Row", "segments");

            // The readout panel draws from cached surfaces; its geometry is
            // published per frame by EnsureHotRects.
            drawField = AccessTools.Field(typeof(ReadoutPanel), "draw");
            harmony.Patch(AccessTools.Method(typeof(ReadoutPanel), "EnsureHotRects"),
                postfix: new HarmonyMethod(typeof(Hooks), nameof(Panel)));
            titleWidth = AccessTools.Field(typeof(ReadoutPanel), "cachedTitleWidth");
            // The group editor draws each tier as its own band.
            harmony.Patch(AccessTools.Method(Mod.GetType("EPrimeReadouts.UI.CellRenderer"), "Draw", new[] { typeof(DrawModel) }),
                prefix: new HarmonyMethod(typeof(Hooks), nameof(EditorBand)));
        }

        private static FieldInfo titleWidth;

        public static void EditorBand(DrawModel __0) =>
            Recorder.Add("editor-band", new Rect(0f, 0f, __0.Model.TotalWidth, __0.Model.TotalHeight));

        /// panel-header, panel-title (the mod name shown without the search
        /// field), panel (the bands' viewport), band:<group> (whole
        /// band), rail:<group> (its tier marker), slot:<group>:<token>.
        public static void Panel(float x, float headerY, float contentTop, Rect outRect, float scrollY)
        {
            var draw = (DrawModel)drawField.GetValue(null);
            ReadoutStore store = ReadoutStore.Current;
            if (draw == null || store == null) return;
            Recorder.Add("panel-header", new Rect(x, headerY, EPrimeReadoutsMod.Settings.panelWidth, contentTop - headerY), screen: true);
            Recorder.Add("panel", outRect, screen: true);
            if (!EPrimeReadoutsMod.Settings.showSearchFilter && EPrimeReadoutsMod.Settings.showModNameWhenNoSearch)
                Recorder.Add("panel-title", new Rect(x + 26f, headerY, (float)titleWidth.GetValue(null), contentTop - headerY), screen: true);
            RenderModel model = draw.Model;
            foreach (RenderBand band in model.Bands)
            {
                string group = store.Model.Groups.FirstOrDefault(g => g.Id == band.GroupId)?.Name ?? "results";
                Recorder.Add("band:" + group, Screen(band.Rect, x, contentTop - scrollY), screen: true);
                for (int i = band.MarkerStart; i < band.MarkerStart + band.MarkerCount; i++)
                    Recorder.Add("rail:" + group, Screen(model.MarkerHits[i].Rect, x, contentTop - scrollY), screen: true);
                for (int i = band.SlotStart; i < band.SlotStart + band.SlotCount; i++)
                    Recorder.Add("slot:" + group + ":" + model.SlotHits[i].Token,
                        Screen(model.SlotHits[i].Rect, x, contentTop - scrollY), screen: true);
            }
        }

        private static Rect Screen(RimShared.Common.RectF rect, float x, float y) =>
            new Rect(x + rect.X, y + rect.Y, rect.W, rect.H);
    }

    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static Dialog_ReadoutConfig config;

        static Probe() => HelpShots.Run("tests.readouts.helpshots", Hooks.Install, Setup);

        private static ReadoutSettings S => EPrimeReadoutsMod.Settings;
        private static ReadoutStore Store => ReadoutStore.Current;

        private static Scene Add(string name, Action act, string hover = null, string click = null) =>
            HelpShots.Add(name, act, hover, click);

        private static ReadoutGroup Group(string name)
        {
            ReadoutGroup group = Store.Model.Groups.FirstOrDefault(g => g.Name == name);
            if (group == null) throw new InvalidOperationException("no group " + name);
            return group;
        }

        private static void Bump() => ReadoutPanel.BumpView();

        /// Tooltips off: a hovered band is pictured without the tip its
        /// slots would open.
        private static void Tips(bool shown) => AccessTools.Method(
            typeof(ReadoutStore).Assembly.GetType("RimShared.UiLib.StructuredTipPresenter"), "SetSuppressed")
            .Invoke(null, new object[] { !shown });

        private static void Depth(string group, int depth)
        {
            S.tierDepths[Store.DepthKey(Group(group).Id)] = depth;
            Bump();
        }

        private static void OpenConfig(string tab, float width = 960f)
        {
            // Closing the previous window stores its size; set ours after.
            HelpShots.CloseWindows();
            S.dialogW = width;
            S.dialogH = 720f;
            FieldInfo field = AccessTools.Field(typeof(Dialog_ReadoutConfig), "curTab");
            field.SetValue(null, Enum.Parse(field.FieldType, tab));
            config = new Dialog_ReadoutConfig();
            config.selectedGroupId = Group("Food").Id;
            Find.WindowStack.Add(config);
        }

        private static void Mode(string mode)
        {
            FieldInfo field = AccessTools.Field(typeof(Dialog_ReadoutConfig), "centerMode");
            field.SetValue(config, Enum.Parse(field.FieldType, mode));
        }

        /// The resources panel's search filter.
        private static void Filter(string query)
        {
            object resources = AccessTools.Field(typeof(Dialog_ReadoutConfig), "resources").GetValue(config);
            ((ItemPickerState)AccessTools.Field(resources.GetType(), "filters").GetValue(resources)).Query = query;
            AccessTools.Method(resources.GetType(), "OnFilterChanged").Invoke(resources, null);
        }

        private static Window Dialog(string type, params object[] arguments)
        {
            HelpShots.CloseWindows();
            var dialog = (Window)Activator.CreateInstance(typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.UI." + type),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null);
            Find.WindowStack.Add(dialog);
            return dialog;
        }

        private static void BuildScenes()
        {
            ReadoutSettings s = S;
            // The readout panel on the map.
            Add("panel", () => { });
            Add("food-2", () => Depth("Food", 2));
            Add("food-3", () => Depth("Food", 3));
            Add("drugs-2", () => { Depth("Food", 1); Depth("Drugs", 2); });
            Add("drugs-vertical", () => { s.verticalTiers = true; Bump(); });
            Add("storage-off", () => { s.verticalTiers = false; Depth("Drugs", 1); s.searchStorageOnly = false; Bump(); });
            Add("negatives-on", () =>
            {
                s.searchStorageOnly = true;
                s.reserveForBills = s.reserveForBuildables = s.showNegativeCounts = true;
                Bump();
            });
            Add("negatives-off", () => { s.showNegativeCounts = false; Bump(); });
            Add("modname", () => { s.reserveForBills = s.reserveForBuildables = false; s.showSearchFilter = false; Bump(); });
            Add("search", () => { s.showSearchFilter = true; ReadoutPanel.SearchText = "meal"; Bump(); });
            Add("rail-tip", () => { ReadoutPanel.SearchText = ""; Bump(); }, hover: "rail:Food");
            Add("slot-tip", () => { }, hover: "slot:Food:#5");
            Add("hover-expanded", () => { s.expandOnHover = true; Tips(false); Bump(); }, hover: "band:Drugs");
            Add("collapse-idle", () => { s.collapseWhenIdle = true; Bump(); });
            Add("collapse-hover", () => { }, hover: "band:Drugs");

            // The editor window.
            Add("editor", () =>
            {
                s.expandOnHover = s.collapseWhenIdle = false;
                Tips(true);
                Bump();
                s.helpGroupsFolded = true;
                OpenConfig("Overview");
            });
            // Not pictured in the help: the slot's options in the minimum
            // window, where the threshold row has to fit a 332-wide column.
            Add("slot-options-narrow", () =>
            {
                s.helpEditorFolded = true;
                config.selectedCanonical = SlotToken.Canonical("#5");
            });
            // The panels' details are pictured in a wider window, where a
            // slot's options fit on their rows; they also need the room the
            // editor's help text takes.
            Add("slot-options", () =>
            {
                s.helpEditorFolded = true;
                OpenConfig("Overview", 1236f);
                config.selectedCanonical = SlotToken.Canonical("#5");
            });
            Add("resources-tree", () => { s.helpEditorFolded = false; config.selectedCanonical = null; }, click: "label:Foods@resources:Draw>4,12");
            Add("resources-search", () => Filter("meal"));
            Add("item-filter-menu", () => Filter(""), hover: "window:FloatMenu", click: "btn:Resources");
            Add("pools", () => { HelpShots.CloseWindows(typeof(Dialog_ReadoutConfig)); Mode("ResourcePools"); });
            Add("pool-members", () => config.selectedPoolId = Store.Model.Pools.First(p => p.Name == "Meals").Id);
            // Options with the choices on that reveal their sub-options.
            Add("options", () =>
            {
                s.reserveForBills = s.reserveForBuildables = s.showNegativeCounts = true;
                s.showSearchFilter = false;
                s.expandOnHover = true;
                Bump();
                OpenConfig("Options");
            });

            // Dialogs.
            Add("default-settings", () =>
            {
                s.reserveForBills = s.reserveForBuildables = s.showNegativeCounts = false;
                s.showSearchFilter = true;
                s.expandOnHover = false;
                Bump();
                // Pictured with automatic updates on.
                s.autoUpdateDefault = true;
                Dialog("Dialog_SetupChoice", false);
            });
            Add("default-conflict", () => { s.autoUpdateDefault = false; Dialog("Dialog_SetupChoice", true); });
            Add("export-setup", () => Dialog("Dialog_ExportReadouts"));
            Add("import-selection", () =>
            {
                // The preview of this colony's own setup, as an import of
                // an exported file shows it.
                Window import = Dialog("Dialog_ImportReadouts");
                AccessTools.Method(import.GetType(), "TryEnterPreview").Invoke(import,
                    new object[] { SetupDocument.Capture(Store.Model, new PlayerSetup()).ToXml() });
            });
        }

        /// The panel is translucent, and open ground (here: clouds under a
        /// ship in orbit) keeps moving while the game is paused. The camera
        /// goes where the panel's corner of the screen shows a roofed room,
        /// so the same pixels come out of every run.
        private static void StillBackground()
        {
            Map map = Find.CurrentMap;
            const int Wide = 13, High = 11;
            const float Size = 12f; // 24 cells down the screen: 45 px a cell
            // Of the roofed blocks, the emptiest: bare floor reads best
            // behind the bands.
            int bestX = -1, bestZ = -1, fewest = int.MaxValue;
            for (int z = map.Size.z - High; z >= 0; z--)
                for (int x = 0; x + Wide <= map.Size.x; x++)
                {
                    int things = 0;
                    for (int dz = 0; dz < High && things < fewest; dz++)
                        for (int dx = 0; dx < Wide && things < fewest; dx++)
                        {
                            var cell = new IntVec3(x + dx, 0, z + dz);
                            things += map.roofGrid.Roofed(cell) ? cell.GetThingList(map).Count : 100000;
                        }
                    if (things >= fewest) continue;
                    bestX = x; bestZ = z; fewest = things;
                }
            if (fewest < 100000)
            {
                float aspect = (float)Verse.UI.screenWidth / Verse.UI.screenHeight;
                Find.CameraDriver.SetRootPosAndSize(new Vector3(bestX + Size * aspect, 0f, bestZ + High - Size), Size);
                HelpShots.Write("camera over the roofed block at " + bestX + "," + bestZ + " holding " + fewest + " things");
                return;
            }
            throw new InvalidOperationException("no roofed block for the panel's background");
        }

        private static void Setup()
        {
            if (Store == null) throw new InvalidOperationException("no readout store");
            // Reproducible starting state: the mod's defaults, every group
            // shown at its first tier, the editor window at its default size.
            ReadoutSettings s = S;
            s.useVanillaReadout = false;
            s.offsetX = 7f; s.offsetY = 7f; s.panelWidth = 140f; s.bottomMargin = 200f;
            s.dialogW = 960f; s.dialogH = 720f;
            s.helpGroupsFolded = s.helpResourcesFolded = s.helpEditorFolded = s.helpPoolsFolded = false;
            s.showSearchFilter = true; s.showModNameWhenNoSearch = true;
            s.searchHideZero = true; s.searchStorageOnly = true; s.searchHideForbidden = true;
            s.selectJumpCamera = true; s.expandOnHover = false; s.collapseWhenIdle = false;
            s.keepReadoutToggle = false; s.verticalTiers = false; s.renderCountersWithTinyFont = true;
            s.reserveForBills = false; s.reserveForBuildables = false; s.showNegativeCounts = false;
            s.qualityJobsRework = false; s.autoUpdateDefault = false;
            foreach (ReadoutGroup group in Store.Model.Groups)
            {
                HelpShots.Write("group " + group.Id + " '" + group.Name + "' tiers " + group.Tiers.Count);
                s.enabledGroups[Store.DepthKey(group.Id)] = true;
                s.tierDepths[Store.DepthKey(group.Id)] = 1;
            }
            ReadoutPanel.BumpView();
            Find.MainTabsRoot.EscapeCurrentTab(false);
            StillBackground();

            BuildScenes();
        }
    }
}
