using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimShared.Tests.HelpShots
{
    public sealed class Scene
    {
        /// The frame's name; null = a step without a capture.
        public string Name;
        public Action Act;
        /// Region clicked (a real left click at its centre) after Act,
        /// e.g. the button that opens a dialog; "right <region>" clicks
        /// with the right button.
        public string Click;
        /// Region whose centre the pointer rests on before the capture
        /// ("region>dx,dy": that offset from its top-left corner instead).
        public string Hover;
        /// Polled every frame after Act; the scene waits (at most 10 s)
        /// until it answers true.
        public Func<bool> Until;
        /// Extra frames to let the scene settle after Act.
        public int Frames;
        /// Seconds the pointer rests before the capture; 0 = long enough
        /// for a tooltip with Hover, a moment without.
        public float Wait;
    }

    /// The scene walker and its file protocol with the driving script
    /// (Shared/tools/automation/help-shots/run-help-shots.ps1), all in the
    /// run's profile folder:
    ///   help-shots-scale.txt        the wanted effective UI scale (script)
    ///   help-shots-variant.txt      the run's mod set (script)
    ///   help-shots-hover.txt        "<n> <x> <y>": rest the pointer there;
    ///                               "<n> <x> <y> click|rclick": click there
    ///   help-shots-capture.txt      "<n> <scene>": save the frame
    ///   help-shots-*-ack.txt        "<n>" once the script has done it
    ///   help-shots-manifest.jsonl   one line per capture with its regions
    ///   help-shots-stage.txt        SUCCESS or FAILED <why>, ends the run
    public static class HelpShots
    {
        private static Harmony harmony;
        private static Action setup;
        private static readonly List<Scene> scenes = new List<Scene>();
        private static int index = -1, step, settle, sequence;
        private static bool clicked;
        private static DateTime notBefore, giveUp;
        private static Vector2 pointer = new Vector2(5f, 5f);
        private static string Root => GenFilePaths.SaveDataFolderPath;

        public static Harmony Harmony => harmony;

        /// The run's mod set, for a mod whose images need more than one run.
        public static string Variant => File.ReadAllText(Path.Combine(Root, "help-shots-variant.txt")).Trim();

        /// Call from the probe's static constructor. modHooks installs the
        /// mod's own recorder hooks; setup runs once in the loaded game,
        /// paused, at the wanted UI scale with stray popups closed: it puts
        /// the mod in its starting state and adds the scenes.
        public static void Run(string harmonyId, Action<Harmony> modHooks, Action setup)
        {
            try
            {
                harmony = new Harmony(harmonyId);
                HelpShots.setup = setup;
                VanillaHooks.Install(harmony);
                modHooks?.Invoke(harmony);
                harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"),
                    postfix: new HarmonyMethod(typeof(HelpShots), nameof(Update)));
            }
            catch (Exception ex)
            {
                // A hook that no longer fits ends the run at once.
                Write("ERROR " + ex);
                File.WriteAllText(Path.Combine(Root, "help-shots-stage.txt"), "FAILED " + ex.Message);
            }
        }

        public static Scene Add(string name, Action act, string hover = null, string click = null)
        {
            var scene = new Scene { Name = name, Act = act, Hover = hover, Click = click };
            scenes.Add(scene);
            return scene;
        }

        public static void Write(string message)
        {
            Log.Message("[HelpShots] " + message);
            File.AppendAllText(Path.Combine(Root, "help-shots.txt"), message + Environment.NewLine);
        }

        /// Closes every window except main tabs and those of the kept types.
        public static void CloseWindows(params Type[] keep)
        {
            foreach (Window open in Find.WindowStack.Windows.ToArray())
                if (!(open is MainTabWindow) && !keep.Contains(open.GetType())
                    && open.GetType().Name != "ImmediateWindow") open.Close(false);
        }

        /// A pointer target: a region's centre, or "spec>dx,dy" = that offset
        /// in pixels from the region's top-left corner.
        private static Vector2? Point(string spec)
        {
            int at = spec.LastIndexOf('>');
            Rect? region = Recorder.Find(at < 0 ? spec : spec.Substring(0, at));
            if (region == null) return null;
            if (at < 0) return region.Value.center;
            string[] offset = spec.Substring(at + 1).Split(',');
            return region.Value.position + new Vector2(
                float.Parse(offset[0], CultureInfo.InvariantCulture),
                float.Parse(offset[1], CultureInfo.InvariantCulture));
        }

        private static string Num(float value) =>
            Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);

        private static void WriteManifest(Scene scene)
        {
            var line = new StringBuilder();
            line.Append("{\"capture\":\"").Append(scene.Name).Append("\",\"utc\":\"")
                .Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
                .Append("\",\"screen\":[").Append(Screen.width).Append(',').Append(Screen.height)
                .Append("],\"uiScale\":").Append(Prefs.UIScale.ToString("R", CultureInfo.InvariantCulture))
                .Append(",\"pointer\":[").Append(Num(pointer.x)).Append(',').Append(Num(pointer.y))
                .Append("],\"hover\":").Append(scene.Hover == null ? "null" : "\"" + scene.Hover + "\"")
                .Append(",\"regions\":{");
            List<Recorder.Region> regions = Recorder.Last;
            // "tooltip": of the tooltips on screen, the one at the pointer
            // (the game may show others, e.g. for an alert).
            Rect? tooltip = null;
            float nearest = float.MaxValue;
            for (int i = 0; i < regions.Count; i++)
            {
                if (!regions[i].Id.StartsWith("tip")) continue;
                Rect px = regions[i].Px;
                float dx = Mathf.Max(px.xMin - pointer.x, 0f, pointer.x - px.xMax);
                float dy = Mathf.Max(px.yMin - pointer.y, 0f, pointer.y - px.yMax);
                if (dx * dx + dy * dy >= nearest) continue;
                nearest = dx * dx + dy * dy;
                tooltip = px;
            }
            if (tooltip != null)
                line.Append("\"tooltip\":[").Append(Num(tooltip.Value.x)).Append(',')
                    .Append(Num(tooltip.Value.y)).Append(',').Append(Num(tooltip.Value.xMax)).Append(',')
                    .Append(Num(tooltip.Value.yMax)).Append("],");
            for (int i = 0; i < regions.Count; i++)
            {
                Rect px = regions[i].Px;
                if (i > 0) line.Append(',');
                line.Append('"').Append(regions[i].Id).Append("\":[").Append(Num(px.x)).Append(',')
                    .Append(Num(px.y)).Append(',').Append(Num(px.xMax)).Append(',').Append(Num(px.yMax)).Append(']');
            }
            line.Append("}}");
            File.AppendAllText(Path.Combine(Root, "help-shots-manifest.jsonl"), line + "\n");
        }

        private static bool Acked(string file)
        {
            try
            {
                string path = Path.Combine(Root, file);
                return File.Exists(path) && File.ReadAllText(path).Trim() == sequence.ToString();
            }
            catch (IOException) { return false; }
        }

        private static void Update()
        {
            if (index == 999 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (settle++ < 8) return;
                if (index < 0)
                {
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                    foreach (Window popup in Find.WindowStack.Windows.ToArray())
                        if (!(popup is MainTabWindow)) popup.Close(false);
                    // The effective UI scale is the preference times the
                    // desktop's DPI factor; the setter divides that factor
                    // out, so the frame is drawn at exactly the wanted scale.
                    float wanted = float.Parse(File.ReadAllText(
                        Path.Combine(Root, "help-shots-scale.txt")).Trim(), CultureInfo.InvariantCulture);
                    Write("before: dpi " + Screen.dpi + " dpiScale " + ResolutionUtility.DpiScale
                        + " preference " + Prefs.UIScalePreference + " respectSystemScaling "
                        + Prefs.RespectSystemScaling + " effective " + Prefs.UIScale);
                    Prefs.UIScale = wanted;
                    Write("screen " + Screen.width + "x" + Screen.height + " uiScale "
                        + Prefs.UIScale.ToString("R", CultureInfo.InvariantCulture));
                    if (Mathf.Abs(Prefs.UIScale - wanted) > 0.0001f)
                        throw new InvalidOperationException("UI scale is " + Prefs.UIScale + ", wanted " + wanted);
                    setup();
                    index = 0; step = 0; settle = 0;
                    return;
                }
                if (index >= scenes.Count)
                {
                    index = 999;
                    File.WriteAllText(Path.Combine(Root, "help-shots-stage.txt"), "SUCCESS");
                    return;
                }
                Scene scene = scenes[index];
                switch (step)
                {
                    case 0:
                        scene.Act?.Invoke();
                        clicked = scene.Click == null;
                        giveUp = DateTime.UtcNow.AddSeconds(10);
                        step = 1; settle = -scene.Frames;
                        return;
                    case 1:
                    {
                        if (scene.Until != null && !scene.Until())
                        {
                            if (DateTime.UtcNow > giveUp) throw new InvalidOperationException(
                                (scene.Name ?? "step " + index) + ": its condition never held");
                            return;
                        }
                        if (!clicked)
                        {
                            bool right = scene.Click.StartsWith("right ");
                            Vector2? button = Point(right ? scene.Click.Substring(6) : scene.Click);
                            if (button == null) throw new InvalidOperationException(
                                (scene.Name ?? scene.Click) + ": no region " + scene.Click);
                            sequence++;
                            File.WriteAllText(Path.Combine(Root, "help-shots-hover.txt"), sequence + " "
                                + Num(button.Value.x) + " " + Num(button.Value.y) + (right ? " rclick" : " click"));
                            step = 5;
                            return;
                        }
                        // The pointer rests on the scene's target, or on empty
                        // chrome; the script sends each request once.
                        pointer = new Vector2(5f, 5f);
                        if (scene.Hover != null)
                        {
                            Vector2? target = Point(scene.Hover);
                            if (target == null) throw new InvalidOperationException(
                                (scene.Name ?? "step " + index) + ": no region " + scene.Hover);
                            pointer = target.Value;
                        }
                        sequence++;
                        File.WriteAllText(Path.Combine(Root, "help-shots-hover.txt"),
                            sequence + " " + Num(pointer.x) + " " + Num(pointer.y));
                        step = 2;
                        return;
                    }
                    case 2:
                        if (!Acked("help-shots-hover-ack.txt")) return;
                        // A tooltip opens after a continuous hover.
                        notBefore = DateTime.UtcNow.AddSeconds(scene.Wait > 0f ? scene.Wait : scene.Hover != null ? 1.2 : 0.15);
                        step = 3;
                        return;
                    case 5:
                        if (!Acked("help-shots-hover-ack.txt")) return;
                        clicked = true;
                        giveUp = DateTime.UtcNow.AddSeconds(10);
                        step = 1; settle = 0;
                        return;
                    case 3:
                        if (DateTime.UtcNow < notBefore) return;
                        if (scene.Name == null) { index++; step = 0; settle = 0; return; }
                        File.WriteAllText(Path.Combine(Root, "help-shots-capture.txt"),
                            sequence + " " + scene.Name);
                        step = 4;
                        return;
                    case 4:
                        if (!Acked("help-shots-capture-ack.txt")) return;
                        WriteManifest(scene);
                        index++; step = 0; settle = 0;
                        return;
                }
            }
            catch (Exception ex)
            {
                Write("ERROR " + ex);
                File.WriteAllText(Path.Combine(Root, "help-shots-stage.txt"), "FAILED " + ex.Message);
                index = 999;
            }
        }
    }
}
