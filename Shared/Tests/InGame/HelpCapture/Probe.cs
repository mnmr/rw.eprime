using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace HelpCaptureProbe
{
    /// Opens a mod's Help tab at a topic so the shared capture can photograph
    /// it, and reports the topic-list splitter so input automation can drag
    /// it. Reflection only, no mod references.
    ///   help-capture-request.txt: "<id>;<windowTypeName>;<slug>"  open Help at the slug
    ///                             "splitter;<id>"                  report the splitter
    ///   help-capture-<id>.txt:    written two frames after navigation.
    ///   help-splitter-<id>.txt:   "x1,y1,x2,y2;width;fit;cursor": the grab zone in
    ///                             physical pixels, the list width, the double-click
    ///                             width and whether the resize cursor is active.
    ///   help-capture-error.txt:   the failure, if any.
    /// WorkRoles: MainTabWindow_WorkRoles (field helpTab, tab enum field curTab).
    /// Implanner: MainTabWindow_Implanner (view field, its field help, static curTab).
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static readonly Harmony harmony = new Harmony("eprime.shared.helpcapture");
        private static string pendingId = "";
        private static string pendingSplitterId = "";
        private static int frames;
        private static object help;
        private static Type patchedHelpType;

        static Probe()
        {
            harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"),
                postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
        }

        private static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
                if (pendingId.Length > 0)
                {
                    if (++frames < 3) return;
                    File.WriteAllText(Path.Combine(Root, "help-capture-" + pendingId + ".txt"), "shown");
                    pendingId = "";
                    return;
                }
                string request = Path.Combine(Root, "help-capture-request.txt");
                if (!File.Exists(request)) return;
                string[] parts = File.ReadAllText(request).Trim().Split(';');
                File.Delete(request);
                if (parts[0] == "splitter")
                {
                    if (help == null) throw new InvalidOperationException("Open a help topic before asking for the splitter.");
                    pendingSplitterId = parts[1];
                    return;
                }
                foreach (Window popup in Find.WindowStack.Windows.ToArray())
                    if (!(popup is MainTabWindow)) popup.Close(false);
                Open(parts[1], parts[2]);
                pendingId = parts[0];
                frames = 0;
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(Root, "help-capture-error.txt"), exception.ToString());
            }
        }

        private static void Open(string windowTypeName, string slug)
        {
            MainButtonDef button = DefDatabase<MainButtonDef>.AllDefsListForReading
                .First(d => d.tabWindowClass != null && d.tabWindowClass.Name == windowTypeName);
            Find.MainTabsRoot.SetCurrentTab(button);
            Window window = button.TabWindow;
            if (!Find.WindowStack.Windows.Contains(window)) Find.WindowStack.Add(window);
            object owner = window;
            // Implanner hosts its view in a field; WorkRoles is the view.
            FieldInfo viewField = window.GetType().GetFields(All).FirstOrDefault(f => f.FieldType.Name == "ImplannerView");
            if (viewField != null) owner = viewField.GetValue(window);
            FieldInfo tabField = owner.GetType().GetFields(All).First(f => f.Name == "curTab" && f.FieldType.IsEnum);
            tabField.SetValue(tabField.IsStatic ? null : owner, Enum.Parse(tabField.FieldType, "Help"));
            FieldInfo helpField = owner.GetType().GetFields(All).First(f => f.FieldType.Name == "HelpTabView");
            help = helpField.GetValue(owner);
            AccessTools.Method(help.GetType(), "Reset").Invoke(help, null);
            AccessTools.Method(help.GetType(), "NavigateTo").Invoke(help, new object[] { slug });
            if (patchedHelpType != help.GetType())
            {
                harmony.Patch(AccessTools.Method(help.GetType(), "Draw"),
                    postfix: new HarmonyMethod(typeof(Probe), nameof(AfterDraw)));
                patchedHelpType = help.GetType();
            }
        }

        /// Runs inside the view's own GUI group, so its splitter rectangle
        /// converts to screen coordinates exactly.
        private static void AfterDraw(object __instance)
        {
            if (pendingSplitterId.Length == 0 || Event.current.type != EventType.Repaint || !ReferenceEquals(__instance, help)) return;
            try
            {
                Type type = __instance.GetType();
                var rect = (Rect)AccessTools.Field(type, "splitterRect").GetValue(__instance);
                // GUIToScreenPoint applies the UI matrix, so these are physical pixels already.
                Vector2 topLeft = GUIUtility.GUIToScreenPoint(rect.position);
                Vector2 bottomRight = GUIUtility.GUIToScreenPoint(rect.max);
                float width = (float)AccessTools.Field(type, "topicPanelWidth").GetValue(null);
                float fit = (float)AccessTools.Field(type, "topicFitPanelWidth").GetValue(__instance);
                Type cursor = type.Assembly.GetType("RimShared.UiLib.SplitterCursor");
                bool active = (bool)AccessTools.Property(cursor, "Active").GetValue(null, null);
                Vector2 mouse = Event.current.mousePosition;
                Vector2 origin = GUIUtility.GUIToScreenPoint(Vector2.zero);
                Vector3 input = Input.mousePosition;
                string line = string.Join(";", new[]
                {
                    string.Join(",", new[] { topLeft.x, topLeft.y, bottomRight.x, bottomRight.y }.Select(v => v.ToString("0", CultureInfo.InvariantCulture))),
                    width.ToString("0.##", CultureInfo.InvariantCulture),
                    fit.ToString("0.##", CultureInfo.InvariantCulture),
                    active ? "cursor" : "arrow",
                    // Diagnostics: the view-local pointer, the group origin and the raw input, all unscaled.
                    string.Join(",", new[] { mouse.x, mouse.y, origin.x, origin.y, input.x, input.y, rect.x, rect.y, Prefs.UIScale }
                        .Select(v => v.ToString("0.##", CultureInfo.InvariantCulture))),
                });
                File.WriteAllText(Path.Combine(Root, "help-splitter-" + pendingSplitterId + ".txt"), line);
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(Root, "help-capture-error.txt"), exception.ToString());
            }
            pendingSplitterId = "";
        }

        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    }
}
