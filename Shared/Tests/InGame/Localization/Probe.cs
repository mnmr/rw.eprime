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

public sealed class ProbeDef : Def
{
    [TranslationCanChangeCount] public List<string> notes = new();
    public List<string> extras = new();
}

public sealed class Probe : GameComponent
{
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private bool completed;
    private readonly List<Result> results = new();
    private Window[]? welcomeWindows;
    private Rect[]? welcomeStarts;
    private string profileDirectory = "";
    private int welcomeStage;
    private float welcomeOriginalScale;
    private int welcomeOriginalWidth, welcomeOriginalHeight;
    private bool welcomeOriginalDevMode;
    public Probe(Game game) { }

    public override void GameComponentUpdate()
    {
        if (welcomeWindows != null) { ObserveWelcomeInput(); return; }
        if (completed || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
        completed = true;
        string profile = "", token = "";
        foreach (string argument in Environment.GetCommandLineArgs())
        {
            if (argument.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)) profile = argument.Substring(16).Trim('"');
            if (argument.StartsWith("-automationtoken=", StringComparison.Ordinal)) token = argument.Substring(17);
        }
        if (!RunIdentity.Matches(profile, token)) return;
        profileDirectory = profile;
        if (File.Exists(Path.Combine(profile, "Config", "locale-probe.enabled"))
            || File.Exists(Path.Combine(profile, "Config", "welcome-scenarios.enabled"))) return;
        Check("selected-game-language", () => Equal("German", LanguageDatabase.activeLanguage.LegacyFolderName));
        Check("normal-community-key-wins", () => Equal("Gemeinschaftsübersetzung", "LP_Override".Translate().ToString()));
        Check("missing-key-uses-bundle", () => Equal("Bündelergänzung", "LP_Fallback".Translate().ToString()));
        Check("placeholder-uses-bundle", () => Equal("Bündelplatzhalter", "LP_Placeholder".Translate().ToString()));
        Check("empty-community-text-preserved", () => Equal("", "LP_Empty".Translate().ToString()));
        Check("missing-bundle-keeps-english", () =>
        {
            bool dev = Prefs.DevMode;
            try { Prefs.DevMode = false; Equal("English-only fallback", "LP_EnglishOnly".Translate().ToString()); }
            finally { Prefs.DevMode = dev; }
        });
        Check("one-shared-loader-registration", SharedRegistration);
        Check("actual-def-injection-precedence", DefTranslations);
        Check("decorated-language-alias", Alias);
        Check("companion-help-precedence-and-cache", Help);
        Check("seeded-role-chip-and-custom-name", RoleChip);
        Check("generated-carrier-invariant-and-localized-display", CarrierRole);
        Check("seeded-group-and-custom-name", RoleGroup);
        Check("seeded-readout-and-custom-name", Readout);
        Check("signal-condition-display", SignalCondition);
        Check("reserved-author-persisted-marker", ReservedAuthor);
        if (File.Exists(Path.Combine(profile, "Config", "welcome-probe.enabled")))
            Check("welcome-four-assemblies-tiled", OpenWelcomes);
        PublishResults();
        Log.Message("[LocalizationProbe] COMPLETE " + results.Count(r => r.Passed) + "/" + results.Count);
    }

    private void PublishResults() => File.WriteAllText(Path.Combine(profileDirectory, "localization-report.json"),
        JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

    private void OpenWelcomes()
    {
        welcomeOriginalScale = Prefs.UIScale;
        welcomeOriginalWidth = Screen.width;
        welcomeOriginalHeight = Screen.height;
        welcomeOriginalDevMode = Prefs.DevMode;
        Prefs.DevMode = false;
        Find.WindowStack.TryRemove(typeof(EditWindow_Log), false);
        string[] assemblies = { "WorkRoles", "EPrimeReadouts", "QualityJobs", "Implanner" };
        string[] packages = { "mnmr.workroles", "mnmr.eprimereadouts", "eprime.qualityjobs", "eprime.implanner" };
        welcomeWindows = new Window[4];
        welcomeStarts = new Rect[4];
        for (int i = 0; i < assemblies.Length; i++)
        {
            ModContentPack mod = LoadedModManager.RunningModsListForReading.Single(m => m.PackageId == packages[i]);
            welcomeWindows[i] = (Window)Activator.CreateInstance(TypeOf(assemblies[i], "RimShared.UiLib.WelcomeDialog"),
                assemblies[i], "Willkommen. Diese ausführliche Einführung erklärt die wichtigen Einstellungen und die ersten Schritte. "
                    + "Die Inhalte bleiben lesbar, während alle vier Hinweise gleichzeitig sichtbar sind.",
                "Öffne die Einstellungen über die Schaltfläche in der Hauptleiste.", "Jetzt öffnen",
                Path.Combine(mod.RootDir, "About", "Preview.png"), new Rect(0, 0, 1280, 720), (Action)(() => { }))!;
            Find.WindowStack.Add(welcomeWindows[i]);
        }
        for (int i = 0; i < welcomeWindows.Length; i++) welcomeStarts[i] = welcomeWindows[i].windowRect;
        CheckWelcomeBounds();
        PublishWelcome("welcome-ready.json");
    }

    private void CheckWelcomeBounds()
    {
        Window[] windows = welcomeWindows!;
        for (int i = 0; i < windows.Length; i++)
        {
            if (!Find.WindowStack.Windows.Contains(windows[i])) continue;
            Rect a = windows[i].windowRect;
            Equal(true, a.x >= 0 && a.y >= 0 && a.xMax <= UI.screenWidth + 0.1f && a.yMax <= UI.screenHeight + 0.1f);
            for (int j = 0; j < i; j++)
                if (Find.WindowStack.Windows.Contains(windows[j])) Equal(false, a.Overlaps(windows[j].windowRect));
        }
    }

    private void ObserveWelcomeInput()
    {
        Window target = welcomeWindows![3];
        if (welcomeStage == 0 && File.Exists(Path.Combine(profileDirectory, "welcome-shrink.request")))
        {
            // 1.7 clamps UI scale to its design floor. Exercise actual display
            // capacity through the owned game window instead of a clamped value.
            Prefs.UIScale = 1f;
            Screen.SetResolution(640, 480, false);
            welcomeStage = 10;
            return;
        }
        if (welcomeStage == 10 && UI.screenWidth == Mathf.RoundToInt(Screen.width / Prefs.UIScale)
            && welcomeWindows.Count(w => Find.WindowStack.Windows.Contains(w)) == 1)
        {
            Check("welcome-shrink-closes-overflow-and-releases-resources", () =>
            {
                Equal(1, welcomeWindows.Count(w => Find.WindowStack.Windows.Contains(w)));
                foreach (Window window in welcomeWindows)
                {
                    bool shown = Find.WindowStack.Windows.Contains(window);
                    Equal(shown, Get(window, "preview") != null);
                    object pending = window.GetType().GetField("suspendedWindows", All)!.GetValue(null)!;
                    Equal(shown ? 0 : 1, Get(pending, "Count"));
                }
                CheckWelcomeBounds();
            });
            PublishWelcome("welcome-shrunk.json");
            PublishResults();
            welcomeStage = 11;
        }
        if (welcomeStage == 11 && File.Exists(Path.Combine(profileDirectory, "welcome-grow.request")))
        {
            Prefs.UIScale = welcomeOriginalScale;
            Screen.SetResolution(welcomeOriginalWidth, welcomeOriginalHeight, false);
            welcomeStage = 12;
            return;
        }
        if (welcomeStage == 12 && UI.screenWidth == Mathf.RoundToInt(Screen.width / Prefs.UIScale)
            && welcomeWindows.All(w => Find.WindowStack.Windows.Contains(w)))
        {
            Check("welcome-grow-restores-same-instances", () =>
            {
                foreach (Window window in welcomeWindows)
                {
                    Equal(true, Find.WindowStack.Windows.Contains(window));
                    Equal(true, Get(window, "preview") != null);
                    object pending = window.GetType().GetField("suspendedWindows", All)!.GetValue(null)!;
                    Equal(0, Get(pending, "Count"));
                }
                CheckWelcomeBounds();
            });
            for (int i = 0; i < welcomeWindows.Length; i++) welcomeStarts![i] = welcomeWindows[i].windowRect;
            PublishWelcome("welcome-grown.json");
            PublishResults();
            welcomeStage = 20;
        }
        if (welcomeStage == 20 && target.windowRect.position != welcomeStarts![3].position)
        {
            welcomeStage = 21;
            Check("welcome-drag-consumed", () => Equal(true, target.draggable));
            PublishWelcome("welcome-dragged.json");
            PublishResults();
        }
        if (welcomeStage == 21 && !Find.WindowStack.Windows.Contains(target))
        {
            welcomeStage = 22;
            Check("welcome-close-and-reflow", () =>
            {
                for (int i = 0; i < 3; i++) Equal(true, Find.WindowStack.Windows.Contains(welcomeWindows[i]));
                CheckWelcomeBounds();
            });
            PublishWelcome("welcome-closed.json");
            PublishResults();
            welcomeWindows = null;
            welcomeStarts = null;
            Prefs.DevMode = welcomeOriginalDevMode;
        }
    }

    private void PublishWelcome(string file)
    {
        Window target = welcomeWindows![3];
        Rect close = (Rect)Get(target, "closeRect")!;
        float margin = (float)AccessTools.Property(typeof(Window), "Margin").GetValue(target)!;
        float scale = Prefs.UIScale;
        File.WriteAllText(Path.Combine(profileDirectory, file), JsonSerializer.Serialize(new
        {
            headerX = (int)((target.windowRect.x + target.windowRect.width / 2) * scale),
            headerY = (int)((target.windowRect.y + 10) * scale),
            closeX = (int)((target.windowRect.x + margin + close.center.x) * scale),
            closeY = (int)((target.windowRect.y + margin + close.center.y) * scale),
            windows = welcomeWindows.Select(w => new { x = w.windowRect.x, y = w.windowRect.y,
                width = w.windowRect.width, height = w.windowRect.height,
                shown = Find.WindowStack.Windows.Contains(w), assembly = w.GetType().Assembly.GetName().Name })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Check(string name, Action action)
    {
        try { action(); results.Add(new Result(name, true, "")); }
        catch (Exception ex)
        {
            Exception error = ex is TargetInvocationException { InnerException: not null } invocation ? invocation.InnerException! : ex;
            results.Add(new Result(name, false, error.ToString()));
        }
    }

    private static void Equal(object? expected, object? actual)
    {
        if (!Equals(expected, actual)) throw new InvalidOperationException("Expected '" + expected + "', actual '" + actual + "'.");
    }

    private static Type TypeOf(string assembly, string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == assembly).GetType(name, true)!;
    private static object? Call(Type type, string name, object? target, params object?[] args) =>
        type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length
            && m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x))
            .Invoke(target, args);
    private static object? Get(object value, string name) => value.GetType().GetProperty(name, All)?.GetValue(value)
        ?? value.GetType().GetField(name, All)!.GetValue(value);
    private static void Set(object value, string name, object? field) => value.GetType().GetField(name, All)!.SetValue(value, field);

    private static void SharedRegistration()
    {
        const string id = "rimshared.bundled-translation-fallback";
        var method = AccessTools.Method(typeof(LoadedLanguage), nameof(LoadedLanguage.LoadData));
        foreach (string assembly in new[] { "WorkRoles", "EPrimeReadouts", "QualityJobs", "Implanner" })
            Call(TypeOf(assembly, "RimShared.GameLib.Localization.BundledTranslationFallback"), "Install", null);
        var patches = Harmony.GetPatchInfo(method);
        Equal(1, patches.Postfixes.Count(p => p.owner == id));
        Equal(1, patches.Prefixes.Count(p => p.owner == id));
        var before = LanguageDatabase.activeLanguage.keyedReplacements["LP_Fallback"];
        LanguageDatabase.activeLanguage.LoadData();
        Equal(true, ReferenceEquals(before, LanguageDatabase.activeLanguage.keyedReplacements["LP_Fallback"]));
    }

    private static void DefTranslations()
    {
        ProbeDef value = DefDatabase<ProbeDef>.GetNamed("LP_Item");
        Equal("Gemeinschaftsbezeichnung", value.label);
        Equal("Bündelbeschreibung", value.description);
        Equal("Gemeinschaftsnotiz", value.notes[0]);
        Equal("Bündel erster Eintrag", value.extras[0]);
        Equal("Gemeinschaft zweiter Eintrag", value.extras[1]);
    }

    private static void Alias()
    {
        var language = new LoadedLanguage("German (Deutsch)");
        language.LoadData();
        language.TryGetTextFromKey("LP_Fallback", out TaggedString fallback);
        Equal("Bündelergänzung", fallback.ToString());
        language.TryGetTextFromKey("LP_Override", out TaggedString community);
        Equal("Gemeinschaftsübersetzung", community.ToString());
    }

    private static void Help()
    {
        Type hostType = TypeOf("WorkRoles", "WorkRoles.UI.WorkRolesHelpHost");
        object host = hostType.GetField("Instance", All)!.GetValue(null)!;
        Type stateType = TypeOf("WorkRoles", "RimShared.UiLib.HelpContentState");
        object state = Activator.CreateInstance(stateType, host)!;
        try
        {
            Call(stateType, "ObserveLanguage", state);
            Array first = (Array)Call(stateType, "ChapterTopics", state, 0)!;
            Equal("Gemeinschaftshilfe", Get(first.GetValue(0)!, "ListTitle"));
            Equal(true, ReferenceEquals(first, Call(stateType, "ChapterTopics", state, 0)));
            Texture2D? image = (Texture2D?)Call(stateType, "LoadTexture", state, "bill-role-filter.png");
            Equal(true, image != null && image.width > 0);
            Call(stateType, "Release", state);
            Equal(false, ReferenceEquals(first, Call(stateType, "ChapterTopics", state, 0)));
        }
        finally { Call(stateType, "Release", state); }
    }

    private static void RoleChip()
    {
        Type defType = TypeOf("WorkRoles", "WorkRoles.RoleDef");
        Def definition = GenDefDatabase.GetDef(defType, "WS_Builder");
        Type identity = TypeOf("WorkRoles", "WorkRoles.SeededDefIdentity");
        string seed = (string)Call(identity, "RoleLabel", null, definition)!;
        Type roleType = TypeOf("WorkRoles", "WorkRoles.Role");
        object role = Activator.CreateInstance(roleType)!;
        Set(role, "id", -900); Set(role, "label", seed); Set(role, "templateDefName", definition.defName);
        Type chipType = TypeOf("WorkRoles", "WorkRoles.UI.RoleChipRenderData");
        object chip = Call(chipType, "From", null, role)!;
        Equal(definition.LabelCap.ToString(), Get(chip, "Label"));
        Equal(true, ReferenceEquals(Get(chip, "Label"), Get(Call(chipType, "From", null, role)!, "Label")));
        Equal(seed, Get(role, "label"));
        string oldLabel = definition.label;
        try
        {
            definition.label = "Neu geladener Baumeister";
            definition.ClearCachedData();
            Call(TypeOf("WorkRoles", "WorkRoles.DefinitionReloadCoordinator"), "DefinitionsRegenerated", null);
            Equal("Neu geladener Baumeister", Get(Call(chipType, "From", null, role)!, "Label"));
            Equal(seed, Get(role, "label"));
            definition.label = "Sprachwechsel Baumeister";
            definition.ClearCachedData();
            Type language = TypeOf("WorkRoles", "WorkRoles.LanguageChangeCoordinator");
            Call(language, "Request", null);
            Call(language, "Complete", null);
            Equal("Sprachwechsel Baumeister", Get(Call(chipType, "From", null, role)!, "Label"));
            Equal(seed, Get(role, "label"));
        }
        finally
        {
            definition.label = oldLabel;
            definition.ClearCachedData();
            Call(TypeOf("WorkRoles", "WorkRoles.DefinitionReloadCoordinator"), "DefinitionsRegenerated", null);
        }
        Set(role, "label", "Custom role α");
        chip = Call(chipType, "From", null, role)!;
        Equal("Custom role α", Get(chip, "Label"));
        Equal("Custom role α", Get(role, "label"));
    }

    private static void RoleGroup()
    {
        Def definition = GenDefDatabase.GetDef(TypeOf("WorkRoles", "WorkRoles.RoleGroupDef"), "WS_GroupEssentials");
        string seed = (string)Call(TypeOf("WorkRoles", "WorkRoles.SeededDefIdentity"), "GroupLabel", null, definition)!;
        object group = Activator.CreateInstance(TypeOf("WorkRoles", "WorkRoles.RoleGroup"))!;
        Set(group, "id", -901); Set(group, "label", seed);
        Set(group, "nameSourceDef", definition.defName);
        object? displayed = group.GetType().GetProperty("DisplayLabel", All)?.GetValue(group) ?? Get(group, "label");
        Equal(definition.LabelCap.ToString(), displayed);
        Equal(seed, Get(group, "label"));
        Set(group, "label", "Custom group α");
        Equal("Custom group α", Get(group, "DisplayLabel"));
        Equal("Custom group α", Get(group, "label"));
        object authored = Activator.CreateInstance(TypeOf("WorkRoles", "WorkRoles.RoleGroup"))!;
        Set(authored, "id", -902); Set(authored, "label", seed);
        Equal(seed, Get(authored, "DisplayLabel"));
    }

    private static void CarrierRole()
    {
        Type identity = TypeOf("WorkRoles", "WorkRoles.SeededDefIdentity");
        Type commands = TypeOf("WorkRoles", "WorkRoles.RoleCommands");
        object store = commands.GetProperty("Store", All)!.GetValue(null)!;
        IEnumerable roles = (IEnumerable)Get(store, "roles")!;
        var existing = new HashSet<string>(roles.Cast<object>().Select(r => (string)Get(r, "label")!));
        WorkTypeDef workType = DefDatabase<WorkTypeDef>.AllDefsListForReading.First(w => w.visible
            && !string.IsNullOrEmpty(w.labelShort)
            && (string)Call(identity, "WorkTypeRoleLabel", null, w)! != w.labelShort.CapitalizeFirst()
            && !existing.Contains((string)Call(identity, "WorkTypeRoleLabel", null, w)!));
        string seed = (string)Call(identity, "WorkTypeRoleLabel", null, workType)!;
        object role = Call(TypeOf("WorkRoles", "WorkRoles.Seeding"), "CreateCarrierRole", null, workType.defName)!;
        try
        {
            Equal(seed, Get(role, "label"));
            Type chipType = TypeOf("WorkRoles", "WorkRoles.UI.RoleChipRenderData");
            object first = Call(chipType, "From", null, role)!;
            Equal(workType.labelShort.CapitalizeFirst(), Get(first, "Label"));
            Equal(true, ReferenceEquals(Get(first, "Label"), Get(Call(chipType, "From", null, role)!, "Label")));
            Equal(seed, Get(role, "label"));
            Call(commands, "RenameRole", null, Get(role, "id"), "Custom carrier α");
            Equal("Custom carrier α", Get(Call(chipType, "From", null, role)!, "Label"));
            Equal("Custom carrier α", Get(role, "label"));
            Call(commands, "RenameRole", null, Get(role, "id"), seed);
            Equal(seed, Get(Call(chipType, "From", null, role)!, "Label"));
        }
        finally { Call(commands, "DeleteRole", null, Get(role, "id")); }
    }

    private static void Readout()
    {
        // Detached store: exercise the same published rows drawn by the real
        // editor without changing the loaded world's authoritative store.
        Type storeType = TypeOf("EPrimeReadouts", "EPrimeReadouts.ReadoutStore");
        object store = Activator.CreateInstance(storeType, Find.World)!;
        object model = Get(store, "Model")!;
        object seed = Call(model.GetType(), "CreateGroup", model, 1, "Meals")!;
        Set(seed, "SeedName", "Meals");
        object custom = Call(model.GetType(), "CreateGroup", model, 2, "My special meals α")!;
        Call(model.GetType(), "CreateGroup", model, 3, "Food");
        Type viewType = TypeOf("EPrimeReadouts", "EPrimeReadouts.UI.GroupListView");
        Call(TypeOf("EPrimeReadouts", "RimShared.UiLib.UiRevision"), "ObserveCurrentMetrics", null);
        object view = Activator.CreateInstance(viewType)!;
        Call(viewType, "EnsureRows", view, store);
        Array rows = (Array)Get(view, "rows")!;
        Equal("Gemeinschaftsmahlzeiten", Get(rows.GetValue(0)!, "Name"));
        Equal("My special meals α", Get(rows.GetValue(1)!, "Name"));
        Equal("Food", Get(rows.GetValue(2)!, "Name"));
        Equal("Meals", Get(seed, "Name"));
        Equal("My special meals α", Get(custom, "Name"));
        Call(viewType, "EnsureRows", view, store);
        Equal(true, ReferenceEquals(rows, Get(view, "rows")));
        Call(viewType, "Reset", view);
    }

    private static void ReservedAuthor()
    {
        RecipeDef recipe = DefDatabase<RecipeDef>.AllDefsListForReading.First(r => r.unfinishedThingDef != null);
        var uft = (UnfinishedThing)ThingMaker.MakeThing(recipe.unfinishedThingDef, GenStuff.DefaultStuffFor(recipe.unfinishedThingDef));
        Type author = TypeOf("QualityJobs", "QualityJobs.UftAuthor");
        Call(author, "Clear", null, uft);
        string marker = "reserved (Quality Jobs)";
        Equal(marker, AccessTools.Field(typeof(UnfinishedThing), "creatorName").GetValue(uft));
        Equal(true, uft.GetInspectString().Contains("Gemeinschaftsreserviert", StringComparison.Ordinal));
        Equal(true, uft.GetInspectString().Contains("Gemeinschaftsreserviert", StringComparison.Ordinal));
        Equal(marker, AccessTools.Field(typeof(UnfinishedThing), "creatorName").GetValue(uft));
        Pawn pawn = Find.CurrentMap.mapPawns.FreeColonists.First();
        Call(author, "Assign", null, uft, pawn);
        Equal(true, uft.GetInspectString().Contains(pawn.LabelShort, StringComparison.Ordinal));
        Equal(pawn.LabelShort, AccessTools.Field(typeof(UnfinishedThing), "creatorName").GetValue(uft));
    }

    private static void SignalCondition()
    {
        object condition = Activator.CreateInstance(TypeOf("WorkRoles.Core", "WorkRoles.Core.Signals.SignalCondition"),
            "work:passionate", "Mood from doing passionate work")!;
        Equal("Gemeinschaftsleidenschaft", Call(TypeOf("WorkRoles", "WorkRoles.Signals.SkillSignalPresentation"),
            "ConditionDisplay", null, condition));
        Equal("work:passionate", Get(condition, "Key"));
        Equal("Mood from doing passionate work", Get(condition, "Description"));
    }

    public sealed record Result(string Name, bool Passed, string Detail);
}
