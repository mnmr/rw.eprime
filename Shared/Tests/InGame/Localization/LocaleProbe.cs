using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Automation.Core;
using UnityEngine;
using Verse;

namespace LocalizationProbe;

// Test-only controller. All filesystem polling and reflective inspection stays
// in this diagnostic assembly and activates only inside its owned managed run.
public sealed class LocaleProbe : GameComponent
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly List<object> results = new();
    private static string profile = "";
    private static int lastRequest;
    private static int reloadRequest;
    private static string reloadLanguage = "";
    private static object? oldStore;
    private static string? oldHelpLabel;
    private bool initialized;
    private Window? window;
    private object? help;
    private int responseRequest;
    private int settle;
    private string responseAction = "";
    private string responseError = "";
    private static bool layoutChecks;
    public LocaleProbe(Game game) { }

    public override void GameComponentUpdate()
    {
        if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
        if (!initialized)
        {
            initialized = true;
            string token = "";
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)) profile = arg.Substring(16).Trim('"');
                if (arg.StartsWith("-automationtoken=", StringComparison.Ordinal)) token = arg.Substring(17);
            }
            if (!RunIdentity.Matches(profile, token) || !File.Exists(Path.Combine(profile, "Config", "locale-probe.enabled"))) { profile = ""; return; }
            Prefs.DevMode = false;
            layoutChecks = File.Exists(Path.Combine(profile, "Config", "locale-layout-checks.enabled"));
            AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
            EditWindow_Log.wantsToOpen = false;
            Find.WindowStack.TryRemove(typeof(EditWindow_Log), false);
            Check("active-locale", () => Require(LanguageDatabase.activeLanguage.LegacyFolderName ==
                (reloadRequest != 0 ? reloadLanguage : File.ReadAllText(Path.Combine(profile, "Config", "locale-probe.enabled")).Trim()), "Wrong native language"));
            Check("translated-labels", () =>
            {
                foreach (string key in new[] { "WR_HelpTab", "EPR.Help", "IMP_HelpChapterBasics", "QJ_SettingsBillDefaults" })
                {
                    Require(LanguageDatabase.activeLanguage.keyedReplacements.ContainsKey(key), "Missing active keyed translation: " + key);
                    Require(!string.IsNullOrEmpty(key.Translate().ToString()), "Empty label: " + key);
                }
            });
            if (reloadRequest != 0)
            {
                Check("actual-save-reload-new-store", () => Require(!ReferenceEquals(oldStore, Store("WorkRoles", "WorkRoles.RoleStore")), "Store was not replaced"));
                Check("custom-names-persist-and-display", CheckNames);
                Check("native-language-change-presentation", () => Require(oldHelpLabel != "WR_HelpTab".Translate().ToString(), "Language label remained stale"));
                responseRequest = reloadRequest; reloadRequest = 0; responseAction = "save-reload-language"; settle = 3;
            }
            Publish("locale-ready.json", new { language = LanguageDatabase.activeLanguage.LegacyFolderName, folders = LanguageDatabase.AllLoadedLanguages.Select(l => new { legacy = l.LegacyFolderName, folder = l.folderName }) });
        }
        if (profile.Length == 0) return;
        if (responseRequest != 0)
        {
            if (--settle > 0) return;
            PublishResponse();
            return;
        }
        string path = Path.Combine(profile, "locale-request-" + (lastRequest + 1) + ".json");
        if (!File.Exists(path)) return;
        using JsonDocument request = JsonDocument.Parse(File.ReadAllText(path));
        int id = request.RootElement.GetProperty("id").GetInt32();
        if (id <= lastRequest) return;
        lastRequest = id;
        string action = request.RootElement.GetProperty("action").GetString()!;
        responseRequest = id; responseAction = action; responseError = ""; settle = 5;
        try
        {
            if (action == "inspect") { }
            else if (action == "help-cache") CheckHelpCache();
            else if (action == "help-next")
            {
                Require(help != null, "No help window");
                int before = (int)Get(help!, "activeChapter")!;
                Call(help!.GetType(), "SelectChapter", help, before + 1);
            }
            else if (action == "save-reload-language")
            {
                CreateNames(); CheckNames();
                oldStore = Store("WorkRoles", "WorkRoles.RoleStore");
                oldHelpLabel = "WR_HelpTab".Translate().ToString();
                GameDataSaveLoader.SaveGame("LocalizationAuthoredNames");
                string save = GenFilePaths.FilePathForSavedGame("LocalizationAuthoredNames");
                Require(File.Exists(save) && new FileInfo(save).Length > 10000, "Save was not written");
                reloadLanguage = "ChineseTraditional";
                LoadedLanguage next = LanguageDatabase.AllLoadedLanguages.Single(l => l.LegacyFolderName == reloadLanguage);
                reloadRequest = id; responseRequest = 0;
                GenScene.GoToMainMenu();
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    LanguageDatabase.SelectLanguage(next);
                    LongEventHandler.ExecuteWhenFinished(() => GameDataSaveLoader.LoadGame("LocalizationAuthoredNames"));
                });
            }
            else Open(action);
        }
        catch (Exception ex) { responseError = ex.ToString(); }
    }

    private void Open(string action)
    {
        if (window != null)
        {
            window.Close(false);
            if (layoutChecks && help != null)
                Check(window.GetType().Name + "-help-teardown", () => Require(Get(help, "topicRowRects") == null, "Help row geometry retained after close"));
        }
        Find.MainTabsRoot.SetCurrentTab(null);
        window = null; help = null;
        if (action.StartsWith("workroles", StringComparison.Ordinal))
        {
            Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("Work"));
            window = Find.WindowStack.Windows.Single(w => w.GetType().FullName == "WorkRoles.UI.MainTabWindow_WorkRoles");
            SetTab(window, action.EndsWith("help", StringComparison.Ordinal) ? "Help" : "Roles");
            help = Get(window, "helpTab");
        }
        else if (action.StartsWith("readouts", StringComparison.Ordinal))
        {
            window = (Window)Activator.CreateInstance(TypeOf("EPrimeReadouts", "EPrimeReadouts.UI.Dialog_ReadoutConfig"))!;
            SetTab(window, action.EndsWith("help", StringComparison.Ordinal) ? "Help" : "Overview");
            Find.WindowStack.Add(window); help = Get(window, "help");
        }
        else if (action.StartsWith("implanner", StringComparison.Ordinal))
        {
            window = (Window)Activator.CreateInstance(TypeOf("Implanner", "Implanner.UI.Dialog_Implanner"))!;
            SetTab(window, action.EndsWith("help", StringComparison.Ordinal) ? "Help" : "Automation");
            Find.WindowStack.Add(window); help = Get(window, "help");
        }
        else if (action == "qualityjobs")
        {
            var mod = (Mod)TypeOf("QualityJobs", "QualityJobs.QualityJobsMod").GetField("Instance", All)!.GetValue(null)!;
            window = new Dialog_ModSettings(mod); Find.WindowStack.Add(window);
        }
        else throw new InvalidOperationException("Unknown action: " + action);
        if (action.EndsWith("help", StringComparison.Ordinal) && action.StartsWith("workroles", StringComparison.Ordinal)) Call(help!.GetType(), "SelectChapter", help, 1);
    }

    private void PublishResponse()
    {
        var details = new Dictionary<string, object?>();
        if (window != null)
        {
            Rect r = window.windowRect;
            details["window"] = new { x = r.x, y = r.y, width = r.width, height = r.height };
            details["windowType"] = window.GetType().FullName;
            details["margin"] = (float)AccessTools.Property(typeof(Window), "Margin").GetValue(window)!;
            details["scale"] = Prefs.UIScale;
            details["tab"] = window.GetType().GetField("curTab", All)?.GetValue(window)?.ToString();
            if (layoutChecks && (responseAction.EndsWith("main", StringComparison.Ordinal) || responseAction == "qualityjobs"))
            {
                try { CheckControlGeometry(); }
                catch (Exception ex) { responseError = ex.ToString(); }
            }
            if (help != null && details["tab"]?.ToString() == "Help")
            {
                try
                {
                    int chapter = (int)Get(help, "activeChapter")!;
                    object content = Get(help, "content")!;
                    Array topics = (Array)Call(content.GetType(), "ChapterTopics", content, chapter)!;
                    Require(topics.Length > 0, "Empty help chapter");
                    details["chapter"] = chapter;
                    details["topics"] = topics.Cast<object>().Select(t => Get(t, "ListTitle")).ToArray();
                    details["topicSlugs"] = topics.Cast<object>().Select(t => Get(Get(t, "Entry")!, "Slug")).ToArray();
                    if (layoutChecks)
                    {
                        Rect[] rows = (Rect[])Get(help, "topicRowRects")!;
                        details["topicRows"] = rows.Select(row => new { x = row.x, y = row.y, width = row.width, height = row.height }).ToArray();
                        GameFont oldFont = Text.Font; bool oldWrap = Text.WordWrap;
                        try
                        {
                            Text.Font = GameFont.Small; Text.WordWrap = true;
                            for (int i = 0; i < rows.Length; i++)
                                Require(Text.CalcHeight((string)Get(topics.GetValue(i)!, "ListTitle")!, rows[i].width - 12f) <= rows[i].height - 8f + .01f,
                                    "Wrapped topic title clips at row " + i);
                        }
                        finally { Text.Font = oldFont; Text.WordWrap = oldWrap; }
                    }
                    details["selectedSlugs"] = Get(help, "selectedSlugs");
                    details["chapterLabels"] = Get(help, "chapterLabels");
                    details["contentScroll"] = Get(help, "contentScroll")?.ToString();
                    // Actual cached draw models, built and rendered by the live
                    // window, must fit their declared flow width. Styled runs
                    // intentionally carry a two-pixel final glyph overhang.
                    Array slots = (Array)Get(content, "modelSlots")!;
                    var measured = new List<object>();
                    foreach (object slot in slots)
                    {
                        object? model = Get(slot, "Model"); if (model == null) continue;
                        float width = (float)Get(slot, "Width")!;
                        Rect[] rects = (Rect[])Get(model, "Rects")!;
                        byte[] kinds = (byte[])Get(model, "Kinds")!;
                        float right = 0f;
                        for (int i = 0; i < rects.Length; i++) if (kinds[i] <= 2) right = Math.Max(right, rects[i].xMax);
                        Require(right <= width + 3f, "Help text exceeds flow width: " + right + "/" + width);
                        measured.Add(new { width, right, height = Get(model, "Height"), runs = rects.Length });
                    }
                    details["renderedModels"] = measured;
                }
                catch (Exception ex) { responseError = ex.ToString(); }
            }
        }
        results.Add(new { action = responseAction, passed = responseError.Length == 0, detail = responseError, language = LanguageDatabase.activeLanguage.LegacyFolderName, details });
        Publish("locale-report.json", results);
        var response = new { id = responseRequest, action = responseAction, passed = responseError.Length == 0, error = responseError, details };
        Publish("locale-response.json", response);
        Publish("locale-response-" + responseRequest + ".json", response);
        responseRequest = 0;
    }

    private void CheckHelpCache()
    {
        Require(layoutChecks && help != null, "Layout test requires a live help view");
        object view = help!;
        object entryState = MetricState();
        object topics = Get(view, "topicLayoutSource")!;
        object before = Get(view, "topicRowRects")!;
        float width = (float)Get(view, "topicLayoutWidth")!, height = (float)Get(view, "topicLayoutHeight")!;
        object content = Get(view, "content")!;
        object? generation = Get(content, "Generation");
        Call(view.GetType(), "EnsureTopicLayout", view, topics, width, height);
        Require(ReferenceEquals(before, Get(view, "topicRowRects")), "Unchanged help layout rebuilt");
        object measurementsBeforeNarrow = SmallMeasurements((Array)topics, width - 32f);
        Call(view.GetType(), "EnsureTopicLayout", view, topics, width - 20f, height);
        object narrower = Get(view, "topicRowRects")!;
        Require(!ReferenceEquals(before, narrower), "Changed help width retained obsolete geometry");
        Call(view.GetType(), "EnsureTopicLayout", view, topics, width - 20f, height);
        Require(ReferenceEquals(narrower, Get(view, "topicRowRects")), "Repeated changed-width read rebuilt");
        Require(Equals(generation, Get(content, "Generation")), "Width invalidated help content");
        Type metrics = view.GetType().Assembly.GetType("RimShared.UiLib.UiRevision", true)!;
        int oldMetric = (int)Get(view, "topicLayoutMetrics")!;
        bool oldTiny = Prefs.DisableTinyText;
        object metricBefore = MetricState();
        object measurementsBeforeMetric = SmallMeasurements((Array)topics, width - 32f);
        try
        {
            Prefs.DisableTinyText = !oldTiny;
            Call(metrics, "ObserveCurrentMetrics", null);
            Call(view.GetType(), "EnsureTopicLayout", view, topics, width - 20f, height);
            Require((int)Get(view, "topicLayoutMetrics")! != oldMetric, "Metric preference not observed");
            Rect[] metricRows = (Rect[])Get(view, "topicRowRects")!;
            Publish("locale-metric-diagnostic.json", new {
                language = LanguageDatabase.activeLanguage.LegacyFolderName,
                window = window!.GetType().Name, oldTiny, entryState, metricBefore,
                measurementsBeforeNarrow, measurementsBeforeMetric, measurementsAfterMetric = SmallMeasurements((Array)topics, width - 32f),
                metricAfter = MetricState(),
                titles = ((Array)topics).Cast<object>().Select(t => Get(t, "ListTitle")).ToArray(),
                before = ((Rect[])narrower).Select(r => new { r.x, r.y, r.width, r.height }).ToArray(),
                after = metricRows.Select(r => new { r.x, r.y, r.width, r.height }).ToArray(),
                sameIdentity = ReferenceEquals(narrower, metricRows)
            });
            // Small-font help rows are equal under a Tiny preference change,
            // so this unrelated metric rebuild must preserve row identity.
            Require(ReferenceEquals(narrower, Get(view, "topicRowRects")), "Equal metric rebuild lost row identity");
            Call(view.GetType(), "EnsureTopicLayout", view, topics, width - 20f, height);
            Require(ReferenceEquals(narrower, Get(view, "topicRowRects")), "Stable metric read rebuilt");
            Require(Equals(generation, Get(content, "Generation")), "Metric preference invalidated help content");
        }
        finally { Prefs.DisableTinyText = oldTiny; Call(metrics, "ObserveCurrentMetrics", null); Call(view.GetType(), "EnsureTopicLayout", view, topics, width, height); }
    }

    private static object MetricState() => new {
        font = Text.Font.ToString(), style = Text.CurFontStyle.fontStyle.ToString(),
        fontSize = Text.CurFontStyle.fontSize, fontAsset = Text.CurFontStyle.font?.name,
        lineHeight = Text.LineHeight, wrap = Text.WordWrap,
        smallStyle = Text.fontStyles[(int)GameFont.Small].fontStyle.ToString(),
        smallSize = Text.fontStyles[(int)GameFont.Small].fontSize,
        smallFontAsset = Text.fontStyles[(int)GameFont.Small].font?.name
    };

    private static object SmallMeasurements(Array topics, float width)
    {
        GUIStyle style = Text.fontStyles[(int)GameFont.Small];
        bool oldWrap = style.wordWrap;
        try
        {
            style.wordWrap = true;
            return topics.Cast<object>().Select(t => {
                var text = new GUIContent((string)Get(t, "ListTitle")!);
                return new { first = style.CalcHeight(text, width), second = style.CalcHeight(text, width) };
            }).ToArray();
        }
        finally { style.wordWrap = oldWrap; }
    }

    private void CheckControlGeometry()
    {
        GameFont old = Text.Font;
        try
        {
            Text.Font = GameFont.Small;
            string type = window!.GetType().Name;
            if (type == "MainTabWindow_WorkRoles")
            {
                Require(Text.CalcSize((string)Get(window, "restoreDefaultsLabel")!).x + 16f <= (float)Get(window, "actionWidth")!, "Restore button too narrow");
                Require(Text.CalcSize((string)Get(window, "importLabel")!).x + 16f <= (float)Get(window, "ioWidth")!, "Import button too narrow");
                Require(Text.CalcSize((string)Get(window, "exportLabel")!).x + 16f <= (float)Get(window, "ioWidth")!, "Export button too narrow");
                Require(Text.LineHeight <= (float)Get(window, "actionHeight")!, "Action button line clipped");
                object chrome = Get(Get(window, "rolesTab")!, "chromeSnapshot")!;
                foreach (string label in new[] { "TreeNested", "TreeFlat" })
                    Require(Text.CalcSize((string)Get(chrome, label)!).x + 12f <= (float)Get(chrome, "ToggleWidth")!, "Display mode button too narrow");
            }
            else if (type == "Dialog_ReadoutConfig")
            {
                Require(Text.CalcSize("EPR.RestoreDefaults".Translate().ToString()).x + 16f <= (float)Get(window, "restoreWidth")!, "Readouts restore button too narrow");
                object groups = Get(window, "groups")!;
                Require(Text.CalcSize("EPR.Add".Translate().ToString()).x + 12f <= (float)Get(groups, "addWidth")!, "Readouts Add button too narrow");
                Require(Text.LineHeight <= (float)Get(window, "headerButtonHeight")!, "Readouts header button line clipped");
                Require(Text.LineHeight <= (float)Get(groups, "addHeight")!, "Readouts Add line clipped");
            }
            else if (type == "Dialog_ModSettings")
            {
                Type labels = TypeOf("QualityJobs", "QualityJobs.QualityJobsMod+SettingsLabels");
                float width = (float)labels.GetField("ButtonWidth", All)!.GetValue(null)!;
                foreach (string name in new[] { "DisableButton", "EnableButton" })
                    Require(Text.CalcSize((string)labels.GetField(name, All)!.GetValue(null)!).x + 16f <= width, "Quality Jobs button too narrow");
                Require(Text.LineHeight <= (float)labels.GetField("ButtonHeight", All)!.GetValue(null)!, "Quality Jobs button line clipped");
            }
        }
        finally { Text.Font = old; }
    }

    private static void CreateNames()
    {
        Call(TypeOf("WorkRoles", "WorkRoles.RoleCommands"), "CreateRole", null, "Custom role Æ 中文 α");
        object role = Items(Get(Store("WorkRoles", "WorkRoles.RoleStore"), "roles")!).Single(r => (string)Get(r, "label")! == "Custom role Æ 中文 α");
        Call(TypeOf("WorkRoles", "WorkRoles.RoleCommands"), "SetRoleGroup", null, Get(role, "id"), "Custom group Ø 中文 β", false);
        Call(TypeOf("EPrimeReadouts", "EPrimeReadouts.ReadoutCommands"), "CreateGroup", null, "Custom readout Å 中文 γ");
        Call(TypeOf("EPrimeReadouts", "EPrimeReadouts.ReadoutCommands"), "CreatePool", null, "Custom pool Ü 中文 δ");
        Call(TypeOf("Implanner", "Implanner.PlannerCommands"), "CreatePlan", null, "Custom plan É 中文 ε", 0);
        if (layoutChecks)
        {
            object read = Get(Store("EPrimeReadouts", "EPrimeReadouts.ReadoutStore"), "Model")!;
            Type commands = TypeOf("EPrimeReadouts", "EPrimeReadouts.ReadoutCommands");
            object? existing = Items(Get(read, "Groups")!).FirstOrDefault(r => (string)Get(r, "Name")! == "Food");
            if (existing != null) Call(commands, "RenameGroup", null, Get(existing, "Id"), "Previous Food (authored)");
            Call(commands, "CreateGroup", null, "Food");
            existing = Items(Get(read, "Pools")!).FirstOrDefault(r => (string)Get(r, "Name")! == "Meals");
            if (existing != null) Call(commands, "RenamePool", null, Get(existing, "Id"), "Previous Meals (authored)");
            Call(commands, "CreatePool", null, "Meals");
        }
    }

    private static void CheckNames()
    {
        object work = Store("WorkRoles", "WorkRoles.RoleStore");
        object role = Items(Get(work, "roles")!).Single(r => (string)Get(r, "label")! == "Custom role Æ 中文 α");
        Require((string)Get(role, "DisplayLabel")! == "Custom role Æ 中文 α", "Role display changed");
        object group = Items(Get(work, "groups")!).Single(r => (string)Get(r, "label")! == "Custom group Ø 中文 β");
        Require((string)Get(group, "DisplayLabel")! == "Custom group Ø 中文 β", "Group display changed");
        object read = Get(Store("EPrimeReadouts", "EPrimeReadouts.ReadoutStore"), "Model")!;
        Require(Items(Get(read, "Groups")!).Any(r => (string)Get(r, "Name")! == "Custom readout Å 中文 γ"), "Readout name missing");
        Require(Items(Get(read, "Pools")!).Any(r => (string)Get(r, "Name")! == "Custom pool Ü 中文 δ"), "Pool name missing");
        if (layoutChecks)
        {
            object food = Items(Get(read, "Groups")!).Single(r => (string)Get(r, "Name")! == "Food");
            object meals = Items(Get(read, "Pools")!).Single(r => (string)Get(r, "Name")! == "Meals");
            Type names = TypeOf("EPrimeReadouts", "EPrimeReadouts.SeededReadoutNames");
            Require((string)Call(names, "Name", null, food)! == "Food", "Custom exact-seed group translated");
            Require((string)Call(names, "Name", null, meals)! == "Meals", "Custom exact-seed pool translated");
            Require(Get(food, "SeedName") == null && Get(meals, "SeedName") == null, "Custom names acquired seed provenance");
        }
        object planner = Get(Store("Implanner", "Implanner.ImplannerStore"), "Model")!;
        Require(Items(Get(planner, "Plans")!).Any(r => (string)Get(r, "Name")! == "Custom plan É 中文 ε"), "Plan name missing");
    }

    private static IEnumerable<object> Items(object value) => ((IEnumerable)value).Cast<object>();
    private static object Store(string assembly, string type) => TypeOf(assembly, type).GetProperty("Current", All)!.GetValue(null)!;
    private static Type TypeOf(string assembly, string type) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == assembly).GetType(type, true)!;
    private static object? Get(object value, string name) => value.GetType().GetProperty(name, All)?.GetValue(value) ?? value.GetType().GetField(name, All)?.GetValue(value);
    private static object? Call(Type type, string name, object? target, params object?[] args) => type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x)).Invoke(target, args);
    private static void SetTab(Window window, string name) { FieldInfo f = window.GetType().GetField("curTab", All)!; f.SetValue(window, Enum.Parse(f.FieldType, name)); }
    private static void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }
    private static void Check(string action, Action assertion)
    {
        try { assertion(); results.Add(new { action, passed = true, detail = "" }); }
        catch (Exception ex) { results.Add(new { action, passed = false, detail = ex.ToString() }); }
        Publish("locale-report.json", results);
    }
    private static void Publish(string name, object data)
    {
        string destination = Path.Combine(profile, name);
        string temporary = destination + ".pending";
        File.WriteAllText(temporary, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, destination, true);
    }
}
