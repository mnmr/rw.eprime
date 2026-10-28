using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EPrimeReadouts;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;
using HarmonyLib;
using LudeonTK;
using Verse;

namespace ReadoutsSetupProbe;

[StaticConstructorOnStartup]
public static class Probe
{
    private static int stage, frames;
    private static bool finished;
    private static readonly List<string> results = new();
    private static readonly Type Defaults = typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.SetupDefaults");
    private static readonly Type PlayerBridge = typeof(ReadoutStore).Assembly.GetType("EPrimeReadouts.PlayerSetupBridge");
    private static string original, beforeBatch;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static string DefaultPath => Path.Combine(GenFilePaths.ConfigFolderPath, "EPrimeReadouts.Default.xml");
    private static Window editor;
    private static string playerXml;
    private static bool desiredEnabled;
    private static int groupId, playerRevision;
    static Probe()
    {
        var harmony = new Harmony("eprime.readouts.setupprobe");
        harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"),
            postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
        if (File.ReadAllText(Path.Combine(Root, "setup-mode.txt")).Trim() == "newgame")
            harmony.Patch(AccessTools.Method(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(StartNewGame)));
    }

    private static bool StartNewGame()
    {
        // The same native setup/generation path used by Root_Play.Start's quick test.
        // This test-only hook selects a new colony without changing the copied source save.
        Root_Play.SetupForQuickTestPlay();
        Find.GameInitData.PrepForMapGen();
        Find.Scenario.PreMapGenerate();
        Current.Game.InitNewGame();
        return false;
    }

    private static object Call(Type type, string name, params object[] args) => AccessTools.Method(type, name).Invoke(null, args);
    private static void Check(bool condition, string name) => results.Add((condition ? "PASS " : "FAIL ") + name);
    private static bool Has(string name) => Find.WindowStack.Windows.Any(w => w.GetType().Name == name);
    private static Window Get(string name) => Find.WindowStack.Windows.First(w => w.GetType().Name == name);
    private static void CloseWindows()
    { for (int i = Find.WindowStack.Count - 1; i >= 0; i--) Find.WindowStack[i].Close(false); }

    private static void Finish()
    {
        finished = true;
        File.WriteAllLines(Path.Combine(Root, "setup-results.txt"), results);
        File.WriteAllText(Path.Combine(Root, "setup-stage.txt"), results.Any(r => r.StartsWith("FAIL")) ? "FAIL" : "PASS");
    }

    private static void Update()
    {
        if (finished || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
            || LongEventHandler.AnyEventNowOrWaiting || ++frames < 8) return;
        frames = 0;
        try
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            // Preserve all logged errors, but keep the save's unrelated debug log
            // from covering the UI being verified and captured in this owned run.
            AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
            EditWindow_Log.wantsToOpen = false;
            for (int i = Find.WindowStack.Count - 1; i >= 0; i--)
                if (Find.WindowStack[i] is EditWindow_Log) Find.WindowStack[i].Close(false);
            var store = ReadoutStore.Current;
            if (File.ReadAllText(Path.Combine(Root, "setup-mode.txt")).Trim() == "accept")
            {
                switch (stage++)
                {
                    case 0:
                        original = File.ReadAllText(DefaultPath);
                        CloseWindows(); Find.WindowStack.Add(new Dialog_ReadoutConfig()); break;
                    case 1:
                        if (!File.Exists(Path.Combine(Root, "setup-choose.txt")))
                        {
                            File.WriteAllText(Path.Combine(Root, "setup-await-choice.txt"), "ready");
                            stage--; return;
                        }
                        Check(Has("Dialog_SetupChoice") && !Has("Dialog_ReadoutConfig"), "acceptance starts from the conflict prompt");
                        Check(File.ReadAllText(DefaultPath) == original, "waiting for a decision does not write");
                        var choice = Get("Dialog_SetupChoice");
                        AccessTools.Field(choice.GetType(), "answered").SetValue(choice, true);
                        Call(Defaults, "Decide", true, false); choice.Close(false); break;
                    case 2:
                        Check(Has("Dialog_ReadoutConfig") && !Has("Dialog_SetupChoice"), "acceptance opens editor once");
                        SetupDocument.TryRead(File.ReadAllText(DefaultPath), out var accepted, out _);
                        Check(accepted.Groups.Select(g => g.Name).SequenceEqual(SetupDocument.Capture(store.Model).Groups.Select(g => g.Name)), "accepted default uses this colony's groups");
                        ReadoutCommands.SetThreshold("Steel", 8642, 4321);
                        Get("Dialog_ReadoutConfig").Close(false); break;
                    case 3:
                        SetupDocument.TryRead(File.ReadAllText(DefaultPath), out var batched, out _);
                        Check(batched.Thresholds["Steel"].Low == 8642, "accepted colony auto-updates on editor close");
                        CloseWindows(); Finish(); break;
                }
                return;
            }
            if (File.ReadAllText(Path.Combine(Root, "setup-mode.txt")).Trim() == "manual")
            {
                switch (stage++)
                {
                    case 0:
                        var previous = SetupDocument.Capture(store.Model);
                        previous.Groups[0].Tiers = new() { new() { "WoodLog", "Steel" } };
                        previous.Groups.Add(new ReadoutGroup { Name = "Absent from this colony", Tiers = new() { new() { "Steel" } } });
                        File.WriteAllText(DefaultPath, previous.ToXml());
                        original = SetupDocument.Capture(store.Model).ToXml();
                        CloseWindows(); Find.WindowStack.Add(new Dialog_ReadoutConfig());
                        break;
                    case 1:
                        Check(Has("Dialog_ReadoutConfig") && !Has("Dialog_SetupChoice"), "auto off opens editor without conflict prompt");
                        Call(Defaults, "QueueSave", false);
                        break;
                    case 2:
                        SetupDocument.TryRead(original, out var expected, out _);
                        SetupDocument.TryRead(File.ReadAllText(DefaultPath), out var actual, out _);
                        actual.Player = null;
                        Check(actual.Groups.Count == expected.Groups.Count, "manual save does not resurrect an old default group");
                        Check(actual.Groups[0].Tiers.SelectMany(t => t).SequenceEqual(expected.Groups[0].Tiers.SelectMany(t => t)), "manual save uses current colony tiers");
                        CloseWindows(); Finish(); break;
                }
                return;
            }
            if (File.ReadAllText(Path.Combine(Root, "setup-mode.txt")).Trim() == "newgame")
            {
                Check(store.Model.Groups.Count == 1 && store.Model.Groups[0].Name == "Verification metals", "new game receives personal groups");
                Check(store.Model.Thresholds.TryGetValue("Steel", out var threshold) && threshold.Low == 333, "new game receives thresholds");
                Check(store.Model.CountRules.TryGetValue("Steel", out var rule) && rule.StorageOnly == BasisOverride.ForceOff, "new game receives count overrides");
                Check(store.Model.Groups[0].DefaultEnabled, "new game receives enabled groups");
                Check(EPrimeReadoutsMod.Settings.panelWidth != 321f, "new game preserves current global preferences");
                Check(File.ReadAllText(DefaultPath) == File.ReadAllText(Path.Combine(Root, "setup-original.xml")), "new game initialization does not write the template");
                CloseWindows(); Finish(); return;
            }
            switch (stage++)
            {
                case 0:
                    original = File.ReadAllText(DefaultPath);
                    Check(original == File.ReadAllText(Path.Combine(Root, "setup-original.xml")), "load does not write template");
                    Check(!Has("Dialog_SetupChoice"), "load does not prompt");
                    CloseWindows();
                    Find.WindowStack.Add(new Dialog_ReadoutConfig());
                    break;
                case 1:
                    Check(Has("Dialog_SetupChoice") && !Has("Dialog_ReadoutConfig"), "first open asks before editor");
                    Check(File.ReadAllText(DefaultPath) == original, "conflict check is read only");
                    Window decision = Get("Dialog_SetupChoice");
                    AccessTools.Field(decision.GetType(), "answered").SetValue(decision, true);
                    Call(Defaults, "Decide", false, false); decision.Close(false);
                    break;
                case 2:
                    Check(Has("Dialog_ReadoutConfig"), "decline continues to editor");
                    Get("Dialog_ReadoutConfig").Close(false);
                    Find.WindowStack.Add(new Dialog_ReadoutConfig());
                    Check(!Has("Dialog_SetupChoice") && Has("Dialog_ReadoutConfig"), "reopen does not prompt again");
                    editor = Get("Dialog_ReadoutConfig");
                    groupId = store.Model.Groups[0].Id;
                    object view = AccessTools.Field(editor.GetType(), "groups").GetValue(editor);
                    AccessTools.Method(view.GetType(), "EnsureRows").Invoke(view, new object[] { store });
                    var rows = (Array)AccessTools.Field(view.GetType(), "rows").GetValue(view);
                    object row = rows.GetValue(0);
                    desiredEnabled = !(bool)AccessTools.Field(row.GetType(), "Enabled").GetValue(row);
                    string name = (string)AccessTools.Field(row.GetType(), "OriginalName").GetValue(row);
                    groupId = (int)AccessTools.Field(row.GetType(), "Id").GetValue(row);
                    ReadoutPanel.SearchText = "Steel";
                    EPrimeReadoutsMod.Settings.showSearchFilter = true;
                    var player = new PlayerSetup(); player.Options["showSearchFilter"] = "False"; player.EnabledGroups[name] = desiredEnabled;
                    playerXml = SetupDocument.Capture(store.Model, player).ToXml();
                    ReadoutCommands.ImportSelected(playerXml, (int)SetupSections.PlayerSettings);
                    break;
                case 3:
                    object groupView = AccessTools.Field(editor.GetType(), "groups").GetValue(editor);
                    AccessTools.Method(groupView.GetType(), "EnsureRows").Invoke(groupView, new object[] { store });
                    var updatedRows = (Array)AccessTools.Field(groupView.GetType(), "rows").GetValue(groupView);
                    object updated = updatedRows.GetValue(0);
                    Check((bool)AccessTools.Field(updated.GetType(), "Enabled").GetValue(updated) == desiredEnabled, "player-only import refreshes enabled checkboxes");
                    Check(ReadoutPanel.SearchText == "", "hiding imported search clears filter");
                    playerRevision = (int)AccessTools.Property(PlayerBridge, "Revision").GetValue(null);
                    ReadoutCommands.ImportSelected(playerXml, (int)SetupSections.PlayerSettings);
                    Check((int)AccessTools.Property(PlayerBridge, "Revision").GetValue(null) == playerRevision, "identical player import preserves revision");
                    Check(File.ReadAllText(DefaultPath) == original, "declined session cannot auto-update default");
                    Call(Defaults, "QueueSave", true);
                    break;
                case 4:
                    beforeBatch = File.ReadAllText(DefaultPath);
                    Check(beforeBatch.Contains("PlayerSettings") && beforeBatch.Contains("Thresholds") && beforeBatch.Contains("CountRules"), "default stores complete setup");
                    ReadoutCommands.SetThreshold("Steel", 9876, 1234);
                    Check(File.ReadAllText(DefaultPath) == beforeBatch, "edits do not immediately write template");
                    EPrimeReadoutsMod.Settings.tierDepths[store.DepthKey(groupId)] = 2;
                    EPrimeReadoutsMod.Persist();
                    break;
                case 5:
                    Check(File.ReadAllText(DefaultPath) == beforeBatch, "view state and steady updates do not write template");
                    editor.Close(false);
                    break;
                case 6:
                    SetupDocument.TryRead(File.ReadAllText(DefaultPath), out var saved, out _);
                    Check(saved.Thresholds["Steel"].Low == 9876, "editor close publishes batched edits");
                    var export = new Dialog_ExportReadouts();
                    string exported = (string)AccessTools.Field(typeof(Dialog_ExportReadouts), "xml").GetValue(export);
                    SetupDocument.TryRead(exported, out var transferred, out _);
                    Check(transferred.Player != null && transferred.Thresholds["Steel"].Critical == 1234, "export dialog includes player settings and thresholds");
                    Check(!exported.Contains("tierDepths"), "export excludes current tier depth");
                    File.WriteAllText(Path.Combine(Root, "setup-export.xml"), exported);
                    var import = new Dialog_ImportReadouts(); Find.WindowStack.Add(import);
                    AccessTools.Method(typeof(Dialog_ImportReadouts), "TryEnterPreview").Invoke(import, new object[] { exported });
                    Check(((SetupSections)AccessTools.Field(typeof(Dialog_ImportReadouts), "selected").GetValue(import) & SetupSections.PlayerSettings) == 0, "player settings are opt in");
                    break;
                case 7: Finish(); break;
            }
        }
        catch (Exception e) { results.Add("FAIL " + e); Finish(); }
    }
}
