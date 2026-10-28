using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using EPrimeReadouts;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReadoutsDisplayProbe
{
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static readonly Type Options = typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.UI.OptionsTabView");
        private static readonly Type Defaults = typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.SetupDefaults");
        private static readonly Func<long> Allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread"));
        private static readonly Action Drain = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(Defaults, "ProcessPending"));
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static readonly bool HelpMedia = File.Exists(Path.Combine(Root, "help-media.txt"));
        private static string lastRequest = "", id = "", scene = "";
        private static int width, height, stage, frames, repaints, setupBuilds, startBuilds, measuredBuilds;
        private static float scale;
        private static bool tiny, scope, inspectScope, measured, sampling, probeMeasurement;
        private static long beforeAllocated, allocated, beforeTicks, ticks;
        private static int samples, calcCalls;
        private static Window window;
        private static readonly List<XElement> labels = new List<XElement>();
        private static Rect content;

        static Probe()
        {
            var harmony = new Harmony("eprime.readouts.displayprobe");
            harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            harmony.Patch(AccessTools.Method(Options, "Draw"), prefix: new HarmonyMethod(typeof(Probe), nameof(BeforeOptions)), postfix: new HarmonyMethod(typeof(Probe), nameof(AfterOptions)));
            harmony.Patch(AccessTools.Method(typeof(Widgets), "Label", new[] { typeof(Rect), typeof(string) }), prefix: new HarmonyMethod(typeof(Probe), nameof(Label)));
            harmony.Patch(AccessTools.Method(typeof(Dialog_ImportReadouts), "DrawPreview"), prefix: new HarmonyMethod(typeof(Probe), nameof(BeforeImport)), postfix: new HarmonyMethod(typeof(Probe), nameof(AfterImport)));
            if (HelpMedia)
                foreach (Type type in new[] { typeof(Dialog_ReadoutConfig), typeof(Dialog_ExportReadouts), typeof(Dialog_ImportReadouts), typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.UI.Dialog_SetupChoice") })
                    harmony.Patch(AccessTools.Method(type, "DoWindowContents"), prefix: new HarmonyMethod(typeof(Probe), nameof(BeforeMediaWindow)), postfix: new HarmonyMethod(typeof(Probe), nameof(AfterMediaWindow)));
            harmony.Patch(AccessTools.Method(typeof(Text), "CalcSize", new[] { typeof(string) }), prefix: new HarmonyMethod(typeof(Probe), nameof(Calc)));
            harmony.Patch(AccessTools.Method(typeof(Text), "CalcHeight", new[] { typeof(string), typeof(float) }), prefix: new HarmonyMethod(typeof(Probe), nameof(Calc)));
            foreach (string method in new[] { "Capture", "ToXml", "TryRead" })
                harmony.Patch(AccessTools.Method(typeof(SetupDocument), method), prefix: new HarmonyMethod(typeof(Probe), nameof(Build)));
        }
        private static void Build() { setupBuilds++; }
        private static void BeforeMediaWindow(Window __instance) { inspectScope = ReferenceEquals(window, __instance) && stage == 2 && frames >= 2 && Event.current.type == EventType.Repaint; }
        private static void AfterMediaWindow() { if (inspectScope) measured = true; inspectScope = false; }
        private static void BeforeImport() { inspectScope = stage == 2 && frames >= 2 && Event.current.type == EventType.Repaint; }
        private static void AfterImport() { if (inspectScope) measured = true; inspectScope = false; }
        private static void Calc() { if (sampling && !probeMeasurement) calcCalls++; }
        private static void BeforeOptions(Rect rect)
        {
            scope = stage == 2 && frames >= 2 && window is Dialog_ReadoutConfig && scene != "overview" && Event.current.type == EventType.Repaint;
            if (!scope) return;
            content = rect;
            sampling = measured && repaints >= 4;
            if (sampling) { beforeTicks = Stopwatch.GetTimestamp(); beforeAllocated = Allocated(); }
        }
        private static void AfterOptions()
        {
            if (!scope) return;
            if (sampling) { allocated += Allocated() - beforeAllocated; ticks += Stopwatch.GetTimestamp() - beforeTicks; samples++; }
            scope = sampling = false;
            measured = true; repaints++;
        }
        private static void Label(Rect rect, string label)
        {
            if ((!scope && !inspectScope) || measured || string.IsNullOrEmpty(label)) return;
            probeMeasurement = true;
            try
            {
                Vector2 size = Text.CalcSize(label);
                float neededHeight = Text.WordWrap ? Text.CalcHeight(label, rect.width) : Mathf.Ceil(Text.LineHeight);
                Vector2 origin = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y));
                labels.Add(new XElement("Label", new XAttribute("text", label),
                    new XAttribute("x", origin.x), new XAttribute("y", origin.y),
                    new XAttribute("width", rect.width), new XAttribute("height", rect.height),
                    new XAttribute("neededWidth", size.x), new XAttribute("neededHeight", neededHeight),
                    new XAttribute("fits", (Text.WordWrap || size.x <= rect.width + .25f) && neededHeight <= rect.height + .25f)));
            }
            finally { probeMeasurement = false; }
        }
        private static object Field(object target, string name) => AccessTools.Field(target.GetType(), name).GetValue(target);
        private static void CloseWindows()
        { for (int i = Find.WindowStack.Count - 1; i >= 0; i--) Find.WindowStack[i].Close(false); }
        private static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
                EditWindow_Log.wantsToOpen = false;
                for (int i = Find.WindowStack.Count - 1; i >= 0; i--)
                    if (Find.WindowStack[i] is EditWindow_Log) Find.WindowStack[i].Close(false);
                string path = Path.Combine(Root, "display-request.txt");
                if (!File.Exists(path)) return;
                string request;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream)) request = reader.ReadToEnd();
                if (!request.EndsWith("\n")) return;
                if (request != lastRequest)
                {
                    lastRequest = request;
                    var parts = request.Trim().Split(';');
                    id = parts[0]; width = int.Parse(parts[1]); height = int.Parse(parts[2]);
                    scale = float.Parse(parts[3], CultureInfo.InvariantCulture); tiny = bool.Parse(parts[4]); scene = parts[5];
                    Prefs.UIScale = scale; Prefs.DisableTinyText = tiny;
                    Prefs.ScreenWidth = width; Prefs.ScreenHeight = height;
                    ResolutionUtility.SetResolutionRaw(width, height, false);
                    stage = 1; frames = repaints = samples = calcCalls = 0; allocated = ticks = 0; measured = false; labels.Clear();
                    return;
                }
                if (stage == 1)
                {
                    File.WriteAllText(Path.Combine(Root, "display-wait.txt"), "stage=resolution actual=" + Screen.width + "x" + Screen.height + " requested=" + width + "x" + height);
                    if (Screen.width != width || Screen.height != height || ++frames < 6) return;
                    var settings = EPrimeReadoutsMod.Settings;
                    if (HelpMedia) { settings.dialogW = 960f; settings.dialogH = 720f; }
                    if (!scene.StartsWith("inspect")) { settings.showSearchFilter = false; settings.expandOnHover = true; settings.autoUpdateDefault = false; }
                    if (HelpMedia && scene.StartsWith("inspect")) window = Find.WindowStack[Find.WindowStack.Count - 1];
                    if (!scene.StartsWith("inspect") && (!(window is Dialog_ReadoutConfig) || scene != "options"))
                    {
                        CloseWindows();
                        if (scene == "overview")
                        {
                            var tab = AccessTools.Field(typeof(Dialog_ReadoutConfig), "curTab");
                            tab.SetValue(null, Enum.Parse(tab.FieldType, "Overview"));
                            window = new Dialog_ReadoutConfig();
                            if (HelpMedia) ((Dialog_ReadoutConfig)window).selectedGroupId = ReadoutStore.Current.Model.Groups[0].Id;
                        }
                        else if (scene == "defaults") window = (Window)Activator.CreateInstance(typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.UI.Dialog_SetupChoice"), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { false }, null);
                        else if (scene == "conflict") window = (Window)Activator.CreateInstance(typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.UI.Dialog_SetupChoice"), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { true }, null);
                        else if (scene == "import") window = new Dialog_ImportReadouts();
                        else if (scene == "dependency")
                        {
                            ReadoutCommands.ImportAll("<Readouts><Pools><Pool Name='Old'><Member>Steel</Member></Pool></Pools><Groups><Group Name='Before'><Tier><Slot>pool:Old</Slot></Tier></Group></Groups><Thresholds/></Readouts>");
                            window = new Dialog_ImportReadouts();
                        }
                        else if (scene == "export") window = new Dialog_ExportReadouts();
                        else window = new Dialog_ReadoutConfig(true);
                        Find.WindowStack.Add(window);
                        if (scene == "import") AccessTools.Method(typeof(Dialog_ImportReadouts), "TryEnterPreview").Invoke(window, new object[] { SetupDocument.Capture(ReadoutStore.Current.Model, new PlayerSetup()).ToXml() });
                        if (scene == "dependency") AccessTools.Method(typeof(Dialog_ImportReadouts), "TryEnterPreview").Invoke(window, new object[] { "<Readouts><Pools><Pool Name='New'><Member>WoodLog</Member></Pool></Pools><Groups><Group Name='After'><Tier><Slot>pool:New</Slot></Tier></Group></Groups><Thresholds><Threshold Token='Steel' Low='25' Critical='5'/></Thresholds></Readouts>" });
                    }
                    if (window is Dialog_ReadoutConfig && scene != "overview" && !(HelpMedia && scene.StartsWith("inspect")))
                    {
                        var tab = AccessTools.Field(typeof(Dialog_ReadoutConfig), "curTab");
                        tab.SetValue(null, Enum.Parse(tab.FieldType, scene.StartsWith("help:") ? "Help" : "Options"));
                        if (scene.StartsWith("help:"))
                        {
                            object help = Field(window, "help");
                            AccessTools.Method(help.GetType(), "NavigateTo").Invoke(help, new object[] { scene.Substring(5) });
                        }
                    }
                    if (scene == "shrink") window.windowRect = new Rect(-100, -100, 300, 200);
                    measured = false; labels.Clear(); repaints = samples = calcCalls = 0; allocated = ticks = 0;
                    frames = 0; stage = 2; startBuilds = setupBuilds;
                }
                else if (stage == 2)
                {
                    File.WriteAllText(Path.Combine(Root, "display-wait.txt"), "stage=render frames=" + frames + " repaints=" + repaints + " samples=" + samples);
                    if (frames == 6) startBuilds = setupBuilds;
                    if (++frames < 10) return;
                    if (scene == "options" || scene == "shrink" || scene == "reopen") { if (samples < 20) return; }
                    measuredBuilds = setupBuilds - startBuilds;
                    WriteResult(); stage = 3;
                }
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(Root, "display-error.txt"), e.ToString()); stage = 3; }
        }
        private static void WriteResult()
        {
            Vector2 minimum = Vector2.zero;
            float actualHeight = 0;
            if (window is Dialog_ReadoutConfig config)
            {
                object options = Field(config, "options");
                minimum = (Vector2)AccessTools.Property(Options, "MinimumContentSize").GetValue(options, null);
                actualHeight = Mathf.Max(((Listing_Standard)Field(options, "left")).CurHeight, ((Listing_Standard)Field(options, "right")).CurHeight);
            }
            Drain();
            for (int i = 0; i < 1000; i++) Drain();
            Allocated(); Stopwatch.GetTimestamp();
            long pendingAllocated = Allocated();
            long pendingTicks = Stopwatch.GetTimestamp();
            for (int i = 0; i < 100000; i++) Drain();
            pendingTicks = Stopwatch.GetTimestamp() - pendingTicks; pendingAllocated = Allocated() - pendingAllocated;
            Rect w = window.windowRect;
            bool inScreen = w.x >= 0 && w.y >= 0 && w.xMax <= Verse.UI.screenWidth + .25f && w.yMax <= Verse.UI.screenHeight + .25f;
            var xml = new XElement("Display", new XAttribute("id", id), new XAttribute("scene", scene),
                new XAttribute("width", Screen.width), new XAttribute("height", Screen.height), new XAttribute("scale", Prefs.UIScale),
                new XAttribute("windowType", window.GetType().Name),
                new XAttribute("logicalWidth", Verse.UI.screenWidth), new XAttribute("logicalHeight", Verse.UI.screenHeight),
                new XAttribute("windowX", w.x), new XAttribute("windowY", w.y), new XAttribute("windowWidth", w.width), new XAttribute("windowHeight", w.height),
                new XAttribute("inScreen", inScreen), new XAttribute("contentWidth", content.width), new XAttribute("contentHeight", content.height),
                new XAttribute("minimumWidth", minimum.x), new XAttribute("minimumHeight", minimum.y), new XAttribute("actualHeight", actualHeight),
                new XAttribute("optionsFit", samples == 0 || (minimum.x <= content.width + .25f && actualHeight <= content.height + .25f)),
                new XAttribute("samples", samples), new XAttribute("drawAllocated", allocated), new XAttribute("drawUs", samples == 0 ? 0 : ticks * 1000000.0 / Stopwatch.Frequency / samples),
                new XAttribute("steadyMeasures", calcCalls), new XAttribute("setupBuilds", measuredBuilds),
                new XAttribute("idlePendingAllocated", pendingAllocated), new XAttribute("idlePendingNs", pendingTicks * 10000.0 / Stopwatch.Frequency), labels);
            xml.SetAttributeValue("searchShown", EPrimeReadoutsMod.Settings.showSearchFilter);
            if (scene.StartsWith("help:"))
            {
                object help = Field(window, "help");
                object helpContent = Field(help, "content");
                var textures = (System.Collections.IDictionary)Field(helpContent, "textures");
                int missing = 0;
                foreach (System.Collections.DictionaryEntry item in textures)
                    if (AccessTools.Property(item.Value.GetType(), "Texture").GetValue(item.Value, null) == null) missing++;
                xml.SetAttributeValue("helpImages", textures.Count);
                xml.SetAttributeValue("missingHelpImages", missing);
                int metadata = 0;
                foreach (XElement label in labels)
                    if (((string)label.Attribute("text")).Contains("l10n:")) metadata++;
                xml.SetAttributeValue("visibleHelpMetadata", metadata);
                var slugs = (string[])Field(help, "selectedSlugs");
                xml.SetAttributeValue("helpTopic", slugs[(int)Field(help, "activeChapter")]);
            }
            if (window is Dialog_ImportReadouts)
            {
                xml.SetAttributeValue("selected", (int)(SetupSections)Field(window, "selected"));
                xml.SetAttributeValue("canImport", Field(window, "canImport"));
            }
            xml.SetAttributeValue("firstGroup", ReadoutStore.Current.Model.Groups[0].Name);
            xml.SetAttributeValue("steelLow", ReadoutStore.Current.Model.Thresholds.TryGetValue("Steel", out var threshold) ? threshold.Low : -1);
            var presenter = typeof(ReadoutStore).Assembly.GetType("RimShared.UiLib.StructuredTipPresenter");
            object tip = AccessTools.Field(presenter, "frozen").GetValue(null);
            if (tip != null)
            {
                object model = AccessTools.Property(tip.GetType(), "Model")?.GetValue(tip, null) ?? AccessTools.Field(tip.GetType(), "Model").GetValue(tip);
                xml.SetAttributeValue("tip", AccessTools.Method(model.GetType(), "ToPlainText").Invoke(model, null));
            }
            string resultPath = Path.Combine(Root, "display-" + id + ".xml");
            xml.Save(resultPath + ".tmp");
            File.Move(resultPath + ".tmp", resultPath);
            File.WriteAllText(Path.Combine(Root, "display-ready.txt"), id);
        }
    }
}
