using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WelcomeConflictsProbe;

[StaticConstructorOnStartup]
public static class Probe
{
    private static int settle;
    private static string command;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static readonly string[][] Hosts = {
        new[] { "WorkRoles", "WorkRoles.WorkRolesMod", "WorkRoles.WorkRolesGameComponent", "Enhanced Work Tab", "7" },
        new[] { "EPrimeReadouts", "EPrimeReadouts.EPrimeReadoutsMod", "EPrimeReadouts.ReadoutGameComponent", "Readouts+", "3" },
        new[] { "Implanner", "Implanner.ImplannerMod", "Implanner.ImplannerGameComponent", "Strata", "4" },
        new[] { "QualityJobs", "QualityJobs.QualityJobsMod", "QualityJobs.QualityJobsStore", "QualityBuilder", "3" }
    };
    static Probe() => new Harmony("rimshared.welcomeconflicts.probe").Patch(
        AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    private static void Write(string name, string value) => File.WriteAllText(Path.Combine(Root, "conflicts-" + name + ".txt"), value);
    private static void Update()
    {
        if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting || ++settle < 20) return;
        string path = Path.Combine(Root, "conflicts-command.txt");
        if (!File.Exists(path)) { Write("stage", "ready"); return; }
        string next = File.ReadAllText(path).Trim();
        if (command == next) return;
        command = next;
        try
        {
            Prefs.DevMode = false;
            string expectedLocale = File.ReadAllText(Path.Combine(Root, "expected-language.txt")).Trim();
            if (LanguageDatabase.activeLanguage.LegacyFolderName != expectedLocale)
                throw new InvalidOperationException("Expected locale " + expectedLocale + ", got " + LanguageDatabase.activeLanguage.folderName);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            // Namespace test packages during startup so installed real mods
            // are never substituted. Expose their tested identity only here.
            foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
                if (pack.PackageIdPlayerFacing.StartsWith("fixture."))
                    AccessTools.Field(typeof(ModContentPack), "packageIdPlayerFacingInt").SetValue(pack, pack.PackageIdPlayerFacing.Substring(8));
            for (int i = Find.WindowStack.Count - 1; i >= 0; i--) Find.WindowStack[i].Close(false);
            string[] host = Hosts[int.Parse(next)];
            var assembly = AccessTools.TypeByName(host[1]).Assembly;
            Type modType = assembly.GetType(host[1]);
            object settings = AccessTools.Field(modType, "Settings")?.GetValue(null)
                ?? AccessTools.Property(modType, "Settings").GetValue(null);
            var shown = (IList)AccessTools.Field(settings.GetType(), "welcomeShownSaves").GetValue(settings);
            string save = Find.World.info.persistentRandomValue.ToString();
            shown.Clear(); shown.Add(save); // Existing saves must receive newly discovered warnings.
            var seenField = AccessTools.Field(settings.GetType(), "compatibilityWarningsSeen");
            if (seenField != null) ((IList)seenField.GetValue(settings)).Clear();
            var queue = AccessTools.Method(assembly.GetType(host[2]), "QueueWelcome");
            queue.Invoke(null, null);
            Window welcome = null;
            for (int i = 0; i < Find.WindowStack.Count; i++)
                if (Find.WindowStack.Windows[i].GetType().Assembly == assembly && Find.WindowStack.Windows[i].GetType().Name == "WelcomeDialog")
                    welcome = Find.WindowStack.Windows[i];
            if (welcome == null) Write(host[0], "FAIL: existing save did not receive a new conflict warning");
            else
            {
                string text = (string)AccessTools.Field(welcome.GetType(), "conflictText").GetValue(welcome);
                object notice = AccessTools.Property(welcome.GetType(), "ConflictingMods").GetValue(welcome);
                int count = notice == null ? 0 : (int)AccessTools.Property(notice.GetType(), "Count").GetValue(notice);
                string heading = "RS_CompatibilityHeading".Translate().ToString();
                bool valid = text.Contains(host[3]) && heading != "RS_CompatibilityHeading" && text.Contains(heading) && count == int.Parse(host[4]);
                string expectedHeading = Path.Combine(Root, "expected-shared-heading.txt");
                if (File.Exists(expectedHeading)) valid &= heading == File.ReadAllText(expectedHeading).Trim();
                Write(host[0] + "-text", text);
                welcome.Close(false);
                queue.Invoke(null, null);
                valid &= Find.WindowStack.Count == 0;
                // Inactive packages must not leave a warning on an ordinary
                // first welcome. Restore markers after observing the window.
                foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
                    if (pack.PackageId.StartsWith("fixture."))
                        AccessTools.Field(typeof(ModContentPack), "packageIdPlayerFacingInt").SetValue(pack, pack.PackageId);
                shown.Clear();
                queue.Invoke(null, null);
                Window ordinary = Find.WindowStack.Windows[Find.WindowStack.Count - 1];
                valid &= string.IsNullOrEmpty((string)AccessTools.Field(ordinary.GetType(), "conflictText").GetValue(ordinary))
                    && (float)AccessTools.Field(ordinary.GetType(), "conflictHeight").GetValue(ordinary) == 0f;
                ordinary.Close(false);
                foreach (ModContentPack pack in LoadedModManager.RunningModsListForReading)
                    if (pack.PackageId.StartsWith("fixture."))
                        AccessTools.Field(typeof(ModContentPack), "packageIdPlayerFacingInt").SetValue(pack, pack.PackageId.Substring(8));
                // Render a fresh warning again for capture after checking acknowledgement.
                ((IList)seenField.GetValue(settings)).Clear();
                queue.Invoke(null, null);
                Write(host[0], valid ? "PASS" : "FAIL: catalog, shared heading, or acknowledgement");
            }
            Write("stage", next);
        }
        catch (Exception error) { Write("stage", "FAILED: " + error); }
    }
}
