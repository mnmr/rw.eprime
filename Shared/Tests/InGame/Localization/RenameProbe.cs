using System;
using System.Collections;
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

// A real rename dialog on an independently owned run. No test hook is installed
// unless both the run token and this scenario's marker are present.
public sealed class RenameProbe : GameComponent
{
    private const string LocalizedName = "Gemeinschaftlicher Spezialist fuer langfristige Bauvorhaben";
    private const string EditedName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234";
    private const string HarmonyId = "rimshared.tests.localization.rename";
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Window? paintedDialog;
    private static int repaints;
    private readonly List<Observation> results = new();
    private string profile = "";
    private int stage;
    private object? role;
    private Def? definition;
    private string originalName = "", originalDefinitionLabel = "";
    private Type? commands;
    private MethodInfo? drawMethod;
    private bool originalDevMode, originalLogAutoOpen, changedDebugUi;

    public RenameProbe(Game game) { }

    public override void GameComponentUpdate()
    {
        if (stage < 0 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null) return;
        try
        {
            if (stage == 0)
            {
                string token = "";
                foreach (string argument in Environment.GetCommandLineArgs())
                {
                    if (argument.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)) profile = argument.Substring(16).Trim('"');
                    if (argument.StartsWith("-automationtoken=", StringComparison.Ordinal)) token = argument.Substring(17);
                }
                if (!RunIdentity.Matches(profile, token) || !File.Exists(Path.Combine(profile, "Config", "rename-probe.enabled")))
                { stage = -1; return; }
                // The ordinary localization checks briefly replace definition
                // labels; wait until those checks have released their fixtures.
                if (!File.Exists(Path.Combine(profile, "localization-report.json"))) return;
                Begin();
                stage = 1;
            }
            else if ((stage == 1 || stage == 3) && repaints >= 2)
            {
                PublishReady(stage == 1 ? "rename-untouched-ready.json" : "rename-edited-ready.json");
                stage++;
            }
            else if (stage == 2 && !Find.WindowStack.IsOpen(paintedDialog!))
            {
                Observe("untouched-localized-prefill-preserves-invariant-name", originalName, (string)Get(role!, "label")!);
                Rename(originalName);
                OpenDialog();
                stage = 3;
            }
            else if (stage == 4 && !Find.WindowStack.IsOpen(paintedDialog!))
            {
                Observe("actual-edit-retains-thirty-character-cap", EditedName, (string)Get(role!, "label")!);
                Cleanup();
                stage = -1;
                PublishReport();
            }
        }
        catch (Exception error)
        {
            results.Add(new Observation("rename-scenario-completed", false, "", error.ToString()));
            stage = -1;
            Cleanup();
            PublishReport();
        }
    }

    private void Begin()
    {
        originalDevMode = Prefs.DevMode;
        originalLogAutoOpen = (bool)AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").GetValue(null)!;
        changedDebugUi = true;
        Prefs.DevMode = false;
        AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
        EditWindow_Log.wantsToOpen = false;
        Find.WindowStack.TryRemove(typeof(EditWindow_Log), false);
        commands = TypeOf("WorkRoles.RoleCommands");
        object store = commands.GetProperty("Store", All)!.GetValue(null)!;
        Type defType = TypeOf("WorkRoles.RoleDef");
        Type identity = TypeOf("WorkRoles.SeededDefIdentity");
        foreach (object candidate in (IEnumerable)Get(store, "roles")!)
        {
            string? template = (string?)Get(candidate, "templateDefName");
            if (string.IsNullOrEmpty(template)) continue;
            Def? candidateDef = GenDefDatabase.GetDef(defType, template, false);
            if (candidateDef == null) continue;
            string seed = (string)identity.GetMethods(All).Single(m => m.Name == "RoleLabel").Invoke(null, new[] { candidateDef })!;
            if ((string)Get(candidate, "label")! != seed) continue;
            role = candidate;
            definition = candidateDef;
            originalName = seed;
            originalDefinitionLabel = definition.label;
            break;
        }
        if (role == null || definition == null) throw new InvalidOperationException("The source save has no unchanged seeded role for the rename scenario.");
        definition.label = LocalizedName;
        RefreshDefinitions();
        if ((string)Get(role, "DisplayLabel")! != LocalizedName) throw new InvalidOperationException("Localized display fixture did not resolve.");
        drawMethod = AccessTools.Method(TypeOf("WorkRoles.UI.Dialog_RenameRole"), nameof(Window.DoWindowContents));
        new Harmony(HarmonyId).Patch(drawMethod, postfix: new HarmonyMethod(typeof(RenameProbe), nameof(AfterDraw)));
        OpenDialog();
    }

    private void OpenDialog()
    {
        repaints = 0;
        paintedDialog = (Window)Activator.CreateInstance(TypeOf("WorkRoles.UI.Dialog_RenameRole"), role)!;
        Find.WindowStack.Add(paintedDialog);
    }

    private static void AfterDraw(Window __instance)
    {
        if (ReferenceEquals(__instance, paintedDialog) && Event.current.type == EventType.Repaint) repaints++;
    }

    private void PublishReady(string name)
    {
        Window dialog = paintedDialog!;
        float margin = (float)AccessTools.Property(typeof(Window), "Margin").GetValue(dialog)!;
        float scale = Prefs.UIScale;
        Rect rect = dialog.windowRect;
        File.WriteAllText(Path.Combine(profile, name), JsonSerializer.Serialize(new
        {
            invariantName = originalName,
            localizedName = LocalizedName,
            textAfterRepaint = Get(dialog, "name"),
            repaints,
            fieldX = Mathf.RoundToInt(rect.center.x * scale),
            fieldY = Mathf.RoundToInt((rect.y + margin + 49f) * scale),
            okX = Mathf.RoundToInt(rect.center.x * scale),
            okY = Mathf.RoundToInt((rect.yMax - margin - 20f) * scale)
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Observe(string name, string expected, string actual) =>
        results.Add(new Observation(name, expected == actual, expected, actual));

    private void Rename(string name)
    {
        if (role != null) AccessTools.Method(commands, "RenameRole").Invoke(null, new[] { Get(role, "id"), name });
    }

    private void RefreshDefinitions()
    {
        definition!.ClearCachedData();
        AccessTools.Method(TypeOf("WorkRoles.DefinitionReloadCoordinator"), "DefinitionsRegenerated").Invoke(null, null);
    }

    private void Cleanup()
    {
        if (paintedDialog != null && Find.WindowStack.IsOpen(paintedDialog)) paintedDialog.Close(false);
        paintedDialog = null;
        if (drawMethod != null) new Harmony(HarmonyId).Unpatch(drawMethod, HarmonyPatchType.All, HarmonyId);
        if (role != null) Rename(originalName);
        if (definition != null) { definition.label = originalDefinitionLabel; RefreshDefinitions(); }
        if (changedDebugUi)
        {
            Prefs.DevMode = originalDevMode;
            AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, originalLogAutoOpen);
            changedDebugUi = false;
        }
    }

    private void PublishReport() => File.WriteAllText(Path.Combine(profile, "rename-report.json"),
        JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

    private static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "WorkRoles").GetType(name, true)!;
    private static object? Get(object target, string name) => target.GetType().GetProperty(name, All)?.GetValue(target)
        ?? target.GetType().GetField(name, All)!.GetValue(target);
    public sealed record Observation(string Name, bool Passed, string Expected, string Actual);
}
