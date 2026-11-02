using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace EndlessGrowthProbe;

// Quality Jobs with Endless Growth loaded (2026-11-02):
//  E1 Core follows the mod: SkillRules.EndlessGrowth is on.
//  E2 the odds replica matches the game's actual rolls: 20000 samples of
//     QualityUtility.GenerateQualityCreatedByPawn per skill (5, 20, 35, 60)
//     land within 2 percentage points of QualityOdds.Distribution in every
//     quality bucket.
//  E3 a managed bill accepts a finisher skill of 60 and its dialog shows it
//     (capture qj-endless-bill: slider past 20, odds for skill 60).
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.qualityjobs.endlessgrowth";
    static int phase, failures, captureSeq, waitFrames;
    static string pendingCapture;
    static DateTime captureHeldUntil, deadline;
    static Window dialog;
    static string Root => GenFilePaths.SaveDataFolderPath;

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[QJEndlessGrowth] " + message);
        File.AppendAllText(Path.Combine(Root, "qj-endless.txt"), message + Environment.NewLine);
    }

    static void Check(bool condition, string message)
    {
        Write((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }

    static void Update()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing
            || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            foreach (Window window in Find.WindowStack.Windows.ToArray())
                if (!(window is MainTabWindow) && window != dialog) window.Close(false);
            if (pendingCapture != null)
            {
                if (waitFrames-- > 0) return;
                if (waitFrames == -1)
                {
                    File.WriteAllText(Path.Combine(Root, "qj-endless-capture.txt"),
                        captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase > 0 && DateTime.UtcNow > deadline) throw new Exception("Timed out at phase " + phase);
            switch (phase)
            {
                case 0: Find.TickManager.Pause(); deadline = DateTime.UtcNow.AddSeconds(50); Rules(); Sample(); phase = 1; return;
                case 1: OpenBill(); Capture("qj-endless-bill"); phase = 2; return;
                case 2: Finish(); return;
            }
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            File.WriteAllText(Path.Combine(Root, "qj-endless-stage.txt"), "FAILED " + ex.Message);
            phase = 99;
        }
    }

    static void Capture(string name, int frames = 3)
    {
        captureSeq++;
        pendingCapture = name;
        waitFrames = frames;
    }

    static void Rules()
    {
        Check(SkillRules.EndlessGrowth, "E1 Core follows Endless Growth (max skill " + SkillRules.MaxSkill + ")");
    }

    static void Sample()
    {
        const int N = 20000;
        foreach (int skill in new[] { 5, 20, 35, 60 })
        {
            var counts = new int[7];
            for (int i = 0; i < N; i++)
                counts[(int)QualityUtility.GenerateQualityCreatedByPawn(skill, false)]++;
            double[] predicted = QualityOdds.Distribution(skill, false, 0);
            double worst = 0;
            var line = new System.Text.StringBuilder("skill " + skill + ":");
            for (int q = 0; q < 7; q++)
            {
                double observed = counts[q] / (double)N;
                worst = Math.Max(worst, Math.Abs(observed - predicted[q]));
                line.Append(' ').Append(((QualityCategory)q).ToString()).Append(' ')
                    .Append((observed * 100).ToString("0.0")).Append("% vs ").Append((predicted[q] * 100).ToString("0.0")).Append('%');
            }
            Write(line.ToString());
            Check(worst < 0.02, "E2 odds replica within 2 points of the game's rolls at skill " + skill
                + " (worst " + (worst * 100).ToString("0.00") + " points)");
        }
    }

    static void OpenBill()
    {
        Building_WorkTable bench = null;
        Bill_Production bill = null;
        foreach (Building_WorkTable table in Find.CurrentMap.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>())
        {
            for (int i = 0; i < table.BillStack.Count; i++)
                if (table.BillStack[i] is Bill_Production candidate && ManagedRecipes.IsManagedRecipe(candidate.recipe))
                { bench = table; bill = candidate; break; }
            if (bill != null) break;
        }
        if (bill == null) throw new Exception("fixture: no managed bill on any bench");
        string id = BillIds.IdOf(bill);
        Commands.SetBillManaged(id, true);
        Commands.SetBillAutoBest(id, false);
        Commands.SetBillMinSkill(id, 60);
        var store = QualityJobsStore.Active;
        int effective = store.billMinSkill.TryGetValue(id, out int value) ? value : -1;
        Check(effective == 60, "E3 managed bill " + bill.LabelCap + " accepts finisher skill 60 (stored " + effective + ")");
        dialog = new Dialog_BillConfig(bill, bench.Position);
        Find.WindowStack.Add(dialog);
    }

    static void Finish()
    {
        phase = 99;
        Write(failures == 0 ? "SUCCESS" : failures + " failure(s)");
        File.WriteAllText(Path.Combine(Root, "qj-endless-stage.txt"),
            failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)");
    }
}
