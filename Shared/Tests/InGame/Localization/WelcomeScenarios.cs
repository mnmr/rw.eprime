using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using LudeonTK;
using RimWorld.Automation.Core;
using UnityEngine;
using Verse;

namespace LocalizationProbe;

// Diagnostic-only controller. All files and input belong to the verified run.
// Root.Update keeps the lifecycle assertions alive across leaving/reloading a save.
[StaticConstructorOnStartup]
internal static class WelcomeScenarios
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly string[] assemblies = { "WorkRoles", "EPrimeReadouts", "QualityJobs", "Implanner" };
    private static readonly string[] packages = { "mnmr.workroles", "mnmr.eprimereadouts", "eprime.qualityjobs", "eprime.implanner" };
    private static readonly List<Probe.Result> results = new();
    private static readonly List<Window> windows = new();
    private static readonly List<string>[] seen = { new(), new(), new(), new() };
    private static readonly string profile = "";
    private static string pending = "", hiddenPath = "";
    private static int sequence, frames, originalWidth, originalHeight, revision;
    private static float originalScale;
    private static Rect originalBody;
    private static bool initialized, exiting, fontTest;
    private static int drawCount, buttonCount, closeCallbacks;
    private static Game? firstGame;

    static WelcomeScenarios()
    {
        string token = "";
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)) profile = arg.Substring(16).Trim('"');
            if (arg.StartsWith("-automationtoken=", StringComparison.Ordinal)) token = arg.Substring(17);
        }
        if (!RunIdentity.Matches(profile, token) || !File.Exists(Path.Combine(profile, "Config", "welcome-scenarios.enabled"))) return;
        var harmony = new Harmony("rimshared.tests.localization.welcome-scenarios");
        harmony.Patch(AccessTools.Method(typeof(Root), "Update"), postfix: new HarmonyMethod(typeof(WelcomeScenarios), nameof(Update)));
        foreach (string assembly in assemblies)
        {
            Type type = TypeOf(assembly);
            harmony.Patch(AccessTools.Method(type, "PostClose"), prefix: new HarmonyMethod(typeof(WelcomeScenarios), nameof(BeforeClose)));
            harmony.Patch(AccessTools.Method(type, "DoWindowContents"), prefix: new HarmonyMethod(typeof(WelcomeScenarios), nameof(BeforeDraw)),
                postfix: new HarmonyMethod(typeof(WelcomeScenarios), nameof(AfterDraw)));
        }
        MethodInfo button = AccessTools.GetDeclaredMethods(typeof(Widgets)).Single(m => m.Name == "ButtonText" && m.GetParameters().Length == 6 && m.GetParameters()[1].ParameterType == typeof(string));
        harmony.Patch(button, prefix: new HarmonyMethod(typeof(WelcomeScenarios), nameof(BeforeButton)));
    }

    private static Type TypeOf(string assembly) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == assembly).GetType("RimShared.UiLib.WelcomeDialog", true)!;
    private static object? Field(object value, string name) => value.GetType().GetField(name, All)!.GetValue(value);
    private static object Queue(Type type) => type.GetField("suspendedWindows", All)!.GetValue(null)!;
    private static object? Property(object value, string name) => value.GetType().GetProperty(name, All)!.GetValue(value);
    private static int Visible => windows.Count(w => Find.WindowStack.Windows.Contains(w));
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void Record(string name, Action check)
    {
        try { check(); results.Add(new(name, true, "")); }
        catch (Exception e) { results.Add(new(name, false, e.ToString())); }
        Publish();
    }
    private static void Publish() => File.WriteAllText(Path.Combine(profile, "welcome-scenarios-report.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    private static void Ready(string name)
    {
        File.WriteAllText(Path.Combine(profile, "welcome-scenario-" + sequence + ".json"), JsonSerializer.Serialize(new
        {
            name, width = Screen.width, height = Screen.height, scale = Prefs.UIScale,
            logicalWidth = UI.screenWidth, logicalHeight = UI.screenHeight,
            visible = Current.ProgramState == ProgramState.Playing ? Visible : 0,
            windows = windows.Select(w => new { assembly = w.GetType().Assembly.GetName().Name, rect = w.windowRect.ToString(),
                preview = Field(w, "preview") != null, body = Field(w, "bodyRect")!.ToString(), revision = Field(w, "metricRevision") })
        }, new JsonSerializerOptions { WriteIndented = true }));
        pending = "";
    }
    private static void QuietLog()
    {
        AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
        EditWindow_Log.wantsToOpen = false;
        Find.WindowStack.TryRemove(typeof(EditWindow_Log), false);
    }
    private static void Open(int index)
    {
        ModContentPack mod = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == packages[index]);
        var window = (Window)Activator.CreateInstance(TypeOf(assemblies[index]), assemblies[index],
            "Willkommen. Diese ausführliche Einführung erklärt die wichtigen Einstellungen und die ersten Schritte. Die Hinweise bleiben auch bei geänderter Bildschirmauflösung lesbar.",
            "Öffne die Einstellungen über die Schaltfläche in der Hauptleiste.", "Jetzt öffnen",
            Path.Combine(mod.RootDir, "About", "Preview.png"), new Rect(0, 0, 1280, 720), (Action)(() => { }))!;
        windows.Add(window);
        Find.WindowStack.Add(window);
    }
    private static void Update()
    {
        try
        {
            if (!initialized)
            {
                if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
                initialized = true;
                originalWidth = Screen.width; originalHeight = Screen.height; originalScale = Prefs.UIScale;
                firstGame = Current.Game;
                Prefs.DevMode = false;
                QuietLog();
                Ready("ready");
            }
            if (pending.Length == 0)
            {
                string path = Path.Combine(profile, "welcome-scenario-" + (sequence + 1) + ".request");
                if (!File.Exists(path)) return;
                pending = File.ReadAllText(path).Trim(); sequence++; frames = 0;
                Start();
            }
            if (pending.Length == 0) return;
            frames++;
            Observe();
        }
        catch (Exception error)
        {
            results.Add(new("controller-" + pending, false, error.ToString())); Publish();
            Ready("failed");
        }
    }
    private static void Start()
    {
        switch (pending)
        {
            case "four": for (int i = 0; i < 4; i++) Open(i); break;
            case "quality": Open(2); break;
            case "implanner": Open(3); break;
            case "shrink": Prefs.UIScale = 1f; Screen.SetResolution(640, 480, false); break;
            case "grow": Screen.SetResolution(originalWidth, originalHeight, false); Prefs.UIScale = originalScale; break;
            case "hide-preview":
                Window target = windows[3];
                Require(!Find.WindowStack.Windows.Contains(target) && Field(target, "preview") == null, "Target must be suspended and release its image before changing the run's file");
                hiddenPath = (string)Field(target, "previewPath")!;
                Require(Path.GetFullPath(hiddenPath).StartsWith(Path.GetFullPath(profile) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Preview must belong to this run");
                File.Move(hiddenPath, hiddenPath + ".hidden-for-probe");
                Ready("preview-hidden"); break;
            case "restore-preview":
                File.Move(hiddenPath + ".hidden-for-probe", hiddenPath); hiddenPath = ""; Ready("preview-restored"); break;
            case "text-only":
                Record("suspended-preview-missing-reopens-text-only", () =>
                {
                    Require(Visible == 4, "All four windows must resume");
                    Require(Field(windows[3], "preview") == null, "Missing art must be absent");
                    Require(((Rect)Field(windows[3], "bodyRect")!).y == 0f, "Text must reclaim the old image area");
                }); Ready(pending); break;
            case "image-restored":
                Record("suspended-preview-restored-rebuilds-image-layout", () =>
                {
                    Require(Visible == 4 && Field(windows[3], "preview") != null, "Same suspended window must reload restored image");
                    Require(((Rect)Field(windows[3], "bodyRect")!).y > 0f, "Body must follow restored preview");
                }); Ready(pending); break;
            case "metrics-change":
                Require(Visible == 1, "Metric observer must be tested with one welcome alone");
                revision = (int)Field(windows[0], "metricRevision")!;
                originalBody = (Rect)Field(windows[0], "bodyRect")!;
                Screen.SetResolution(2560, 1440, false); Prefs.UIScale = 5f / 3f;
                break;
            case "metrics-stable": revision = (int)Field(windows[0], "metricRevision")!; originalBody = (Rect)Field(windows[0], "bodyRect")!; break;
            case "font": drawCount = buttonCount = 0; fontTest = true; break;
            case "close":
                foreach (Window window in windows.ToArray()) window.Close(false);
                windows.Clear(); Ready(pending); break;
            case "exit":
                Require(Visible == 1 && windows.Count == 4, "Exit must begin with one live and three suspended welcomes");
                for (int i = 0; i < 4; i++)
                {
                    int index = i;
                    bool claimed = (bool)TypeOf(assemblies[i]).GetMethod("ClaimSave", All)!.Invoke(null, new object[] { seen[i], (Action)(() => { throw new InvalidOperationException("Unseen queued welcome was admitted during disposal: " + index); }) })!;
                    Require(!claimed && seen[i].Count == 0, "Full display must queue unseen claims");
                }
                exiting = true; GenScene.GoToMainMenu(); break;
            case "reload": GameDataSaveLoader.LoadGame("Autostart"); break;
            default: throw new InvalidOperationException("Unknown scenario: " + pending);
        }
    }
    private static void Observe()
    {
        if (pending == "exit")
        {
            if (Current.Game != null || !GenScene.InEntryScene) return;
            Record("world-exit-stops-all-queues-before-close-and-releases-previews", () =>
            {
                Require(closeCallbacks == 1, "Exactly one visible welcome must close during disposal");
                foreach (string assembly in assemblies)
                {
                    object queue = Queue(TypeOf(assembly));
                    Require((bool)Property(queue, "IsStopped")! && (int)Property(queue, "Count")! == 0, "Queue must stop and clear: " + assembly);
                }
                Require(windows.All(w => Field(w, "preview") == null && !Find.WindowStack.Windows.Contains(w)), "Every preview and window must be released");
                Require(seen.All(s => s.Count == 0), "Unseen queued dialogs must remain unseen");
            }); exiting = false; Ready(pending); return;
        }
        if (pending == "reload")
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || ReferenceEquals(firstGame, Current.Game)) return;
            Record("next-loaded-game-resets-queue-admission", () =>
            {
                QuietLog();
                foreach (string assembly in assemblies) Require(!(bool)Property(Queue(TypeOf(assembly)), "IsStopped")!, "New game must restart queue: " + assembly);
                windows.Clear();
                bool claim = (bool)TypeOf(assemblies[3]).GetMethod("ClaimSave", All)!.Invoke(null, new object[] { seen[3], (Action)(() => { }) })!;
                Require(claim && seen[3].Count == 1, "Unseen welcome must be claimable in next loaded game");
                Open(3); Require(Visible == 1 && Field(windows[0], "preview") != null, "Next game's welcome must open with image");
            }); Ready(pending); return;
        }
        if (Current.ProgramState != ProgramState.Playing || frames < 4) return;
        QuietLog();
        if (pending == "shrink" && Visible != 1) return;
        if (pending == "grow" && (Screen.width != originalWidth || Screen.height != originalHeight || Visible != windows.Count)) return;
        if (pending == "metrics-change")
        {
            if (Screen.width != 2560 || Screen.height != 1440 || UI.screenWidth != 1536 || UI.screenHeight != 864) return;
            Record(windows[0].GetType().Assembly.GetName().Name + "-physical-scale-change-refreshes-metrics", () =>
            {
                Require((int)Field(windows[0], "metricRevision")! > revision, "Metric revision must change despite equal logical dimensions");
                Require(((Rect)Field(windows[0], "bodyRect")!).height > 0, "Rebuilt body must retain valid measured geometry");
            });
        }
        if (pending == "metrics-stable")
        {
            if (frames < 25) return;
            Record(windows[0].GetType().Assembly.GetName().Name + "-unchanged-metrics-reuse-layout", () =>
            {
                Require((int)Field(windows[0], "metricRevision")! == revision, "Steady metric revision must remain stable");
                Require(((Rect)Field(windows[0], "bodyRect")!) == originalBody, "Steady geometry must remain stable");
            });
        }
        if (pending == "font")
        {
            if (drawCount < 6) return;
            fontTest = false;
            Record("close-button-font-independent-and-gui-state-restored", () => Require(buttonCount >= 6, "Close must draw Small despite incoming Medium font"));
        }
        Ready(pending);
    }
    private static void BeforeClose()
    {
        if (!exiting) return;
        closeCallbacks++;
        foreach (string assembly in assemblies)
            Require((bool)Property(Queue(TypeOf(assembly)), "IsStopped")!, "All assembly queues must stop before any close callback");
    }
    private sealed record GuiState(GameFont Font, TextAnchor Anchor, bool Wrap, Color Color);
    private static void BeforeDraw(out GuiState? __state)
    {
        __state = null;
        if (!fontTest) return;
        __state = new(Text.Font, Text.Anchor, Text.WordWrap, GUI.color);
        Text.Font = GameFont.Medium; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = false; GUI.color = Color.white;
    }
    private static void AfterDraw(GuiState? __state)
    {
        if (__state == null) return;
        try
        {
            Require(Text.Font == GameFont.Medium && Text.Anchor == TextAnchor.UpperLeft && !Text.WordWrap && GUI.color == Color.white,
                "Welcome drawing must restore incoming font, anchor, wrap and color");
            drawCount++;
        }
        finally { Text.Font = __state.Font; Text.Anchor = __state.Anchor; Text.WordWrap = __state.Wrap; GUI.color = __state.Color; }
    }
    private static void BeforeButton(string label)
    {
        if (!fontTest || label != "CloseButton".Translate().ToString()) return;
        Require(Text.Font == GameFont.Small, "Close button must explicitly select measured Small font"); buttonCount++;
    }
}
