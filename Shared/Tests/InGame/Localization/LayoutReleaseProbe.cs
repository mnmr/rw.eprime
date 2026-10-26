using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using RimWorld.Automation.Core;
using UnityEngine;
using Verse;

namespace LocalizationProbe;

// Opt-in native-font reproduction. Owned run only; no production callbacks.
public sealed class LayoutReleaseProbe : GameComponent
{
    private string profile = "";
    private bool initialized;
    private bool measured;
    private SampleWindow? window;
    private static string? injectedText;
    private static string phase = "initial";
    private static string activeProfile = "";
    private static bool inspectAutomation;
    private static readonly List<object> automationChecks = new();
    private Window? automationWindow;
    public LayoutReleaseProbe(Game game) { }

    public override void GameComponentUpdate()
    {
        if (initialized && profile.Length > 0 && automationWindow == null
            && File.Exists(Path.Combine(profile, "release-automation.request")))
        {
            activeProfile = profile;
            window?.Close(false);
            Type type = TypeOf("Implanner", "Implanner.UI.Dialog_Implanner");
            FieldInfo tab = type.GetField("curTab", All)!;
            tab.SetValue(null, Enum.Parse(tab.FieldType, "Automation"));
            TypeOf("Implanner", "Implanner.PlannerCommands").GetMethod("SetAutoProduction", All)!.Invoke(null, new object[] { true });
            automationWindow = (Window)Activator.CreateInstance(type)!;
            Find.WindowStack.Add(automationWindow);
            automationWindow.windowRect = new Rect(300, 40, 840, 760);
            var harmony = new Harmony("rimshared.tests.release-automation");
            harmony.Patch(type.GetMethod("DrawAutomation", All),
                prefix: new HarmonyMethod(typeof(LayoutReleaseProbe), nameof(BeforeAutomation)),
                postfix: new HarmonyMethod(typeof(LayoutReleaseProbe), nameof(AfterAutomation)));
            harmony.Patch(AccessTools.Method(typeof(Widgets), nameof(Widgets.CheckboxLabeled)),
                prefix: new HarmonyMethod(typeof(LayoutReleaseProbe), nameof(ObserveCheckbox)));
            harmony.Patch(SegmentMethod(), prefix: new HarmonyMethod(typeof(LayoutReleaseProbe), nameof(ObserveSegment)));
        }
        if (initialized || Current.ProgramState != ProgramState.Playing) return;
        initialized = true;
        string token = "";
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)) profile = arg.Substring(16).Trim('"');
            if (arg.StartsWith("-automationtoken=", StringComparison.Ordinal)) token = arg.Substring(17);
        }
        if (!RunIdentity.Matches(profile, token) || !File.Exists(Path.Combine(profile, "Config", "release-layout.enabled"))) profile = "";
        if (profile.Length == 0) return;
        window = new SampleWindow();
        Find.WindowStack.Add(window);
    }

    private static void BeforeAutomation() => inspectAutomation = Event.current.type == EventType.Repaint && automationChecks.Count == 0;
    private static void ObserveCheckbox(Rect rect, string label)
    {
        if (!inspectAutomation) return;
        float needed = Text.CalcHeight(label, rect.width - 24f);
        automationChecks.Add(new { label, width = rect.width - 24f, height = rect.height, needed, passed = rect.height >= needed });
    }
    private static MethodInfo SegmentMethod() => TypeOf("Implanner", "RimShared.UiLib.SegmentedControl").GetMethods(All)
        .Single(m => m.Name == "Tab" && m.GetParameters().Length == 4);
    private static void ObserveSegment(Rect rect, string label)
    {
        if (!inspectAutomation) return;
        float needed = Text.CalcHeight(label, rect.width);
        automationChecks.Add(new { label, width = rect.width, height = rect.height, needed, segment = true, passed = rect.height >= needed });
    }
    private static void AfterAutomation()
    {
        if (!inspectAutomation) return;
        inspectAutomation = false;
        File.WriteAllText(Path.Combine(activeProfile, "release-automation.json"), JsonSerializer.Serialize(automationChecks,
            new JsonSerializerOptions { WriteIndented = true }));
        var harmony = new Harmony("rimshared.tests.release-automation");
        harmony.Unpatch(TypeOf("Implanner", "Implanner.UI.Dialog_Implanner").GetMethod("DrawAutomation", All), HarmonyPatchType.All, harmony.Id);
        harmony.Unpatch(AccessTools.Method(typeof(Widgets), nameof(Widgets.CheckboxLabeled)), HarmonyPatchType.Prefix, harmony.Id);
        harmony.Unpatch(SegmentMethod(), HarmonyPatchType.Prefix, harmony.Id);
    }

    public override void GameComponentOnGUI()
    {
        if (profile.Length == 0 || window == null || measured || Event.current.type != EventType.Repaint) return;
        measured = true;
        GameFont previous = Text.Font;
        try
        {
            var measurements = new List<object>();
            foreach (Sample sample in window.Samples)
            {
                Text.Font = sample.Font;
                measurements.Add(new { sample.Key, sample.Text, sample.Width, sample.Height,
                    font = sample.Font.ToString(), lineHeight = Text.LineHeight,
                    actualWidth = Text.CalcSize(sample.Text).x,
                    actualHeight = Text.CalcHeight(sample.Text, sample.Width) });
            }
            object names = CheckNameFields();
            object styledElements = CheckStyledElements();
            object? controls = File.Exists(Path.Combine(profile, "Config", "release-layout-final.enabled"))
                ? CheckControls(window) : null;
            object? search = LanguageDatabase.activeLanguage.LegacyFolderName == "Turkish" ? ReleaseSearchChecks.Check() : null;
            object? metrics = controls != null ? ReleaseMetricChecks.Check() : null;
            File.WriteAllText(Path.Combine(profile, "release-layout.json"), JsonSerializer.Serialize(new {
                language = LanguageDatabase.activeLanguage.LegacyFolderName, measurements, names, styledElements, controls, search, metrics
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(profile, "release-layout-error.json"), JsonSerializer.Serialize(new { phase, error = error.ToString() }));
        }
        finally { Text.Font = previous; }
    }

    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Type TypeOf(string assembly, string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == assembly).GetType(name, true)!;
    private static object? Get(object value, string field) =>
        value.GetType().GetField(field, All)?.GetValue(value)
        ?? value.GetType().GetProperty(field, All)?.GetValue(value);
    private static void Set(object value, string field, object? data) => value.GetType().GetField(field, All)!.SetValue(value, data);

    private static object CheckControls(SampleWindow window)
    {
        var results = new List<object>();
        phase = "readouts fitted model";
        Type modelType = TypeOf("EPrimeReadouts.Core", "EPrimeReadouts.Core.RenderModel");
        object model = Activator.CreateInstance(modelType)!;
        Type cellType = TypeOf("EPrimeReadouts.Core", "EPrimeReadouts.Core.RenderCell");
        object cell = Activator.CreateInstance(cellType)!;
        Set(cell, "Kind", Enum.Parse(cellType.GetField("Kind")!.FieldType, "Label"));
        Set(cell, "Text", "EPR.NoMatches");
        Set(cell, "Rect", Activator.CreateInstance(cellType.GetField("Rect")!.FieldType, 0f, 0f, 68f, 22f));
        ((IList)Get(model, "Cells")!).Add(cell);
        object draw = TypeOf("EPrimeReadouts", "EPrimeReadouts.UI.DrawModel").GetMethod("Resolve", All)!.Invoke(null, new object?[] { model, null })!;
        string fitted = ((string[])Get(draw, "Labels")!)[0];
        using ((IDisposable)TypeOf("EPrimeReadouts", "RimShared.UiLib.TinyText").GetMethod("UseFont", All)!.Invoke(null, null)!)
            results.Add(new { check = "readouts-80px-panel-label", text = fitted, width = Text.CalcSize(fitted).x, available = 68f, passed = Text.CalcSize(fitted).x <= 68f });

        phase = "implanner toggle layout";
        TypeOf("Implanner", "Implanner.UI.PlannerLabels").GetMethod("Ensure", All)!.Invoke(null, null);
        Type toggleType = TypeOf("Implanner", "Implanner.UI.ToggleRowState");
        object toggles = Activator.CreateInstance(toggleType, true)!;
        MethodInfo current = toggleType.GetMethod("Current", All)!;
        object narrow = current.Invoke(toggles, new object[] { 334f })!;
        object same = current.Invoke(toggles, new object[] { 334f })!;
        object wide = current.Invoke(toggles, new object[] { 534f })!;
        object restored = current.Invoke(toggles, new object[] { 334f })!;
        PropertyInfo indexer = narrow.GetType().GetProperty("Item", All)!;
        object row = Enum.Parse(indexer.GetIndexParameters()[0].ParameterType, "Intermediaries");
        float height = (float)indexer.GetValue(narrow, new[] { row })!;
        Text.Font = GameFont.Small;
        float required = Text.CalcHeight("IMP_OptIntermediaries".Translate(), 334f);
        results.Add(new { check = "implanner-narrow-checkbox-wrap", height, required, passed = height >= required });
        results.Add(new { check = "implanner-checkbox-cache", passed = ReferenceEquals(narrow, same) && (float)indexer.GetValue(restored, new[] { row })! == height });
        window.Samples.Single(s => s.Key == "IMP_OptIntermediaries" && s.Width == 334).Height = height;

        phase = "implanner override identity";
        Type pickerType = TypeOf("Implanner", "Implanner.UI.PickerRow");
        object picker = Activator.CreateInstance(pickerType, true)!;
        string operand = DefDatabase<ThingDef>.GetNamed("BionicLeg").LabelCap.ToString();
        Set(picker, "OverridesTarget", operand);
        Set(picker, "OverridesLabel", "IMP_Overrides".Translate(operand).ToString());
        IList rows = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pickerType))!;
        rows.Add(picker);
        Array captions = (Array)TypeOf("Implanner", "Implanner.UI.PlansState").GetMethod("FitCaptions", All)!.Invoke(null, new object[] { rows, 140f })!;
        object caption = captions.GetValue(0)!;
        string captionText = (string)Get(caption, "Text")!;
        results.Add(new { check = "implanner-override-keeps-identity", operand, text = captionText,
            full = Get(caption, "Full"), width = Get(caption, "Width"),
            passed = captionText.Contains(operand.Substring(0, Math.Min(2, operand.Length))) && (float)Get(caption, "Width")! <= 80f });

        phase = "workroles holder summary";
        Type holderType = TypeOf("WorkRoles", "WorkRoles.UI.RoleHolderPresentation");
        IList holders = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(holderType))!;
        var overflow = new List<string> { "" };
        for (int i = 0; i < 100; i++)
        {
            holders.Add(Activator.CreateInstance(holderType, All, null, new object[] { "Alexandria " + i, i }, null));
            overflow.Add("WR_PlusOthers".Translate(i + 1).ToString());
        }
        Text.Font = GameFont.Small;
        Array holderLabels = (Array)TypeOf("WorkRoles", "WorkRoles.UI.RoleEditorState").GetMethod("BuildHolderLabels", All)!.Invoke(null, new object[] { holders, overflow, 200f })!;
        bool holdersFit = holderLabels.Length > 0;
        var holderEvidence = new List<object>();
        foreach (object label in holderLabels)
        {
            string text = (string)Get(label, "Text")!;
            float x = (float)Get(label, "X")!, w = (float)Get(label, "Width")!;
            float actual = Text.CalcSize(text).x;
            holdersFit &= x + w <= 200.1f && actual <= w + 0.1f;
            holderEvidence.Add(new { text, x, width = w, actual });
        }
        results.Add(new { check = "workroles-translated-holder-summary", labels = holderEvidence, passed = holdersFit });

        phase = "qualityjobs panel";
        Type panelType = TypeOf("QualityJobs", "QualityJobs.UI.BillQualityPanel");
        RecipeDef recipe = DefDatabase<RecipeDef>.AllDefsListForReading.First(r => r.unfinishedThingDef != null && r.products.Any(p => p.thingDef.HasComp(typeof(CompQuality))));
        Bill_ProductionWithUft bill = (Bill_ProductionWithUft)BillUtility.MakeNewBill(recipe);
        object panel = Activator.CreateInstance(panelType, All, null, new object[] { bill }, null)!;
        Set(panel, "_statusQueueLabel", "QJ_StatusQueueShared".Translate(100, 100, 100).ToString());
        Set(panel, "_statusFinishersLabel", "QJ_StatusFinishersNone".Translate().ToString());
        Set(panel, "_statusStalledLabel", "QJ_StatusStalled".Translate().ToString());
        Set(panel, "statusValid", true); Set(panel, "statusWaiting", 100); Set(panel, "statusEligibleCount", 0);
        object store = TypeOf("QualityJobs", "QualityJobs.QualityJobsStore").GetProperty("Active", All)!.GetValue(null)!;
        Set(panel, "statusRevision", store.GetType().GetField("BillStatusRevision", All)!.GetValue(store));
        MethodInfo ensure = panelType.GetMethod("EnsureStatusGeometry", All)!;
        ensure.Invoke(panel, new object[] { 270f });
        float stallHeight = (float)Get(panel, "stallHeight")!;
        float queueHeight = (float)Get(panel, "queueHeight")!;
        required = Text.CalcHeight("QJ_StatusStalled".Translate(), 270f);
        results.Add(new { check = "qualityjobs-stall-wrap", height = stallHeight, required, passed = stallHeight >= required });
        required = Text.CalcHeight("QJ_StatusQueueShared".Translate(100, 100, 100), 270f);
        results.Add(new { check = "qualityjobs-queue-wrap", height = queueHeight, required, passed = queueHeight >= required });
        window.Samples.Single(s => s.Key == "QJ_StatusStalled").Height = stallHeight;
        window.Samples.Single(s => s.Key == "QJ_StatusQueueShared").Height = queueHeight;
        window.Panel = panel;
        return results;
    }

    private static object CheckStyledElements()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "WorkRoles");
        Type hostType = assembly.GetTypes().Single(t => t.Name == "WorkRolesHelpHost");
        object host = hostType.GetField("Instance", flags)!.GetValue(null)!;
        Type measurerType = assembly.GetType("RimShared.UiLib.HelpTextMeasurer", true)!;
        object measurer = Activator.CreateInstance(measurerType, flags, null, new[] { host }, null)!;
        MethodInfo method = measurerType.GetMethod("WordWidth", flags)!;
        Type font = method.GetParameters()[1].ParameterType, style = method.GetParameters()[2].ParameterType;
        var results = new List<object>();
        Text.Font = GameFont.Small;
        foreach (string text in new[] { "か\u3099", "\ud840\udc00" })
        {
            float actual = (float)method.Invoke(measurer, new[] { (object)text, Enum.Parse(font, "Body"), Enum.Parse(style, "Bold") })!;
            float expected = Text.CalcSize("<b>" + text + "</b>").x * 1.02f;
            results.Add(new { text, actual, expected, passed = Math.Abs(actual - expected) < 0.01f });
        }
        return results;
    }

    private static object CheckNameFields()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var reports = new List<object>();
        var harmony = new Harmony("rimshared.tests.release-name-input");
        MethodInfo field = AccessTools.Method(typeof(Widgets), nameof(Widgets.TextField), new[] { typeof(Rect), typeof(string) });
        harmony.Patch(field, postfix: new HarmonyMethod(typeof(LayoutReleaseProbe), nameof(InjectText)));
        try
        {
            foreach (string assembly in new[] { "Implanner", "WorkRoles" })
            {
                bool shared = assembly == "Implanner";
                int max = shared ? 40 : 30;
                Type type = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == assembly)
                    .GetType(shared ? "RimShared.UiLib.NameDialog" : "WorkRoles.UI.Dialog_RenameRole", true)!;
                foreach (string cluster in new[] { "\ud840\udc00", "e\u0301" })
                {
                    string input = new string('a', max - 1) + cluster + "b";
                    Window target = (Window)Activator.CreateInstance(type, shared
                        ? new object?[] { "Input regression", "initial", (Action<string>)(_ => { }) }
                        : new object?[] { "Input regression", (Action<string>)(_ => { }), "initial" })!;
                    injectedText = input;
                    target.DoWindowContents(new Rect(0, 0, 324, 180));
                    injectedText = null;
                    string actual = (string)type.GetField("name", flags)!.GetValue(target)!;
                    string expected = new string('a', max - 1) + cluster;
                    reports.Add(new { assembly, inputUnits = input.Select(c => (int)c).ToArray(), actualUnits = actual.Select(c => (int)c).ToArray(), expectedUnits = expected.Select(c => (int)c).ToArray(), passed = actual == expected });
                }
            }
        }
        finally { injectedText = null; harmony.Unpatch(field, HarmonyPatchType.Postfix, harmony.Id); }
        return reports;
    }

    private static void InjectText(ref string __result)
    {
        if (injectedText != null) __result = injectedText;
    }

    private sealed class Sample
    {
        public string Key = "", Text = "";
        public float Width, Height;
        public GameFont Font;
    }

    private sealed class SampleWindow : Window
    {
        internal readonly List<Sample> Samples = new();
        internal object? Panel;
        public override Vector2 InitialSize => new(1480, 720);
        internal SampleWindow()
        {
            doCloseX = true; forcePause = true;
            Add("EPR.NoMatches", 128, 22, GameFont.Tiny);
            Add("QJ_StatusStalled", 270, 22);
            Add("QJ_StatusQueueShared", 270, 22, GameFont.Small, 100, 100, 100);
            Add("QJ_MigrationBody", 524, 92, GameFont.Small, 25);
            Add("QJ_MigrationEnableExisting", 494, 44);
            Add("IMP_OptIntermediaries", 534, 28);
            Add("IMP_OptIntermediaries", 334, 28);
            Add("WR_PlusOthers", 70, 22, GameFont.Small, 99);
        }
        private void Add(string key, float width, float height, GameFont font = GameFont.Small, params NamedArgument[] args)
        {
            Samples.Add(new Sample { Key = key, Text = key.Translate(args), Width = width, Height = height, Font = font });
        }
        public override void DoWindowContents(Rect inRect)
        {
            GameFont previous = Text.Font;
            try
            {
                float y = 8;
                foreach (Sample sample in Samples)
                {
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(0, y, 410, 24), sample.Key + " (" + sample.Width + " × " + sample.Height + ")");
                    Text.Font = sample.Font;
                    Rect box = new(420, y, sample.Width, sample.Height);
                    Widgets.DrawBoxSolid(box, new Color(0.15f, 0.22f, 0.22f));
                    Widgets.Label(box, sample.Text);
                    y += Math.Max(60, sample.Height + 15);
                }
                if (Panel != null)
                    Panel.GetType().GetMethod("DrawQualityPanel", All)!.Invoke(Panel, new object[] { new Rect(1160, 0, 270, 650) });
            }
            finally { Text.Font = previous; }
        }
    }
}
