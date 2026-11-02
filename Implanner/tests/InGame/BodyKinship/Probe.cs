using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace BodyKinshipProbe;

// Body kinship (owner, 2026-11-01): a race whose implant slots are the
// human ones (the probe's Implanner_HumanCopy) folds under the human body;
// a race with every human slot and more (Milira: wings) joins plans for the
// human body and is still offered as a body of its own; an android is a
// body of its own. Checked on the catalog, the acceptance rule, the synced
// AssignPlan command, the evaluation of an accepted colonist, and the body
// lists the picker and the new-plan dialog show.
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.bodykinship";
    static int phase, failures, captureSeq, waitFrames;
    static string pendingCapture;
    static DateTime captureHeldUntil;
    static Window dialog;
    static string Root => GenFilePaths.SaveDataFolderPath;
    static readonly Type Catalogs = AccessTools.TypeByName("Implanner.Catalogs");
    static readonly Type PlanBodies = AccessTools.TypeByName("Implanner.PlanBodies");

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[BodyKinship] " + message);
        File.AppendAllText(Path.Combine(Root, "body-kinship.txt"), message + Environment.NewLine);
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
                    File.WriteAllText(Path.Combine(Root, "body-kinship-capture.txt"),
                        captureSeq + " " + pendingCapture);
                    captureHeldUntil = DateTime.UtcNow.AddSeconds(2.5);
                }
                if (DateTime.UtcNow < captureHeldUntil) return;
                pendingCapture = null;
            }
            if (phase == 0) { Run(); phase = 1; return; }
            if (phase == 1) { Finish(); return; }
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            failures++;
            Finish();
        }
    }

    static void Capture(string name)
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        captureSeq++;
        pendingCapture = name;
        waitFrames = 3;
    }

    static void Finish()
    {
        phase = 99;
        dialog?.Close(false);
        Find.TickManager.Pause();
        string status = failures == 0 ? "SUCCESS" : "FAILED " + failures;
        Write(status);
        File.WriteAllText(Path.Combine(Root, "body-kinship-stage.txt"), status);
    }

    // ---------------------------------------------------------- helpers

    static List<BodyDef> Plannable() => (List<BodyDef>)AccessTools.Method(Catalogs, "PlannableBodies").Invoke(null, null);
    static List<BodyDef> Offered() => (List<BodyDef>)AccessTools.Method(Catalogs, "OfferedBodies").Invoke(null, null);
    static BodyKinship Kinship(BodyDef body) =>
        (BodyKinship)AccessTools.Method(Catalogs, "KinshipOf").Invoke(null, new object[] { Plannable().IndexOf(body) });
    static bool Accepts(string planBody, Pawn pawn) =>
        (bool)AccessTools.Method(PlanBodies, "Accepts").Invoke(null, new object[] { planBody, pawn });
    static string Names(IEnumerable<BodyDef> bodies) => string.Join(", ", bodies.Select(b => b.defName));

    static Pawn Generate(string kindName)
    {
        PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName);
        if (kind == null) throw new Exception("Fixture: no pawn kind " + kindName);
        var request = new PawnGenerationRequest(kind, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
            forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: false,
            colonistRelationChanceFactor: 0f, fixedBiologicalAge: 30f, fixedChronologicalAge: 30f);
        return PawnGenerator.GeneratePawn(request);
    }

    static Pawn SpawnColonist(string kindName, Map map, IntVec3 near)
    {
        Pawn pawn = Generate(kindName);
        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(near, map, 6), map);
        foreach (Need need in pawn.needs.AllNeeds) need.CurLevelPercentage = 1;
        return pawn;
    }

    // -------------------------------------------------------------- run

    static void Run()
    {
        Find.TickManager.Pause();
        Map map = Find.CurrentMap;
        List<BodyDef> bodies = Plannable();
        List<BodyDef> offered = Offered();
        foreach (BodyDef body in bodies)
            Write("body " + body.defName + ": " + Kinship(body) + (offered.Contains(body) ? " offered" : " folded"));

        BodyDef copy = DefDatabase<BodyDef>.GetNamedSilentFail("Implanner_HumanCopy");
        BodyDef milira = DefDatabase<BodyDef>.GetNamedSilentFail("Milira_Body");
        BodyDef android = DefDatabase<BodyDef>.GetNamedSilentFail("Android4Tier");
        Check(copy != null && milira != null && android != null && bodies.Contains(copy)
            && bodies.Contains(milira) && bodies.Contains(android), "fixture: copy, Milira and android bodies are plannable");
        if (failures > 0) return;

        Check(Kinship(BodyDefOf.Human) == BodyKinship.Human && Kinship(copy) == BodyKinship.HumanEquivalent
            && Kinship(milira) == BodyKinship.HumanSuperset && Kinship(android) == BodyKinship.Distinct,
            "K1 kinship: human copy equivalent, Milira superset, android distinct");
        Check(!offered.Contains(copy) && offered.Contains(BodyDefOf.Human) && offered.Contains(milira)
            && offered.Contains(android), "K2 offered bodies fold the human copy and keep Milira and the android (" + Names(offered) + ")");

        // Pawns of each body, spawned as colonists so the synced command
        // sees them among the planable colonists.
        IntVec3 near = map.mapPawns.FreeColonistsSpawned.First().Position;
        Pawn human = SpawnColonist("Colonist", map, near);
        Pawn copyPawn = SpawnColonist("Implanner_HumanCopyColonist", map, near);
        PawnKindDef miliraKind = DefDatabase<PawnKindDef>.AllDefsListForReading
            .First(k => k.race?.race != null && k.race.race.Humanlike && k.race.race.body == milira);
        Pawn miliraPawn = SpawnColonist(miliraKind.defName, map, near);
        Pawn androidPawn = SpawnColonist("AndroidT5Colonist", map, near);
        Write("pawns: " + human.def.defName + ", " + copyPawn.def.defName + ", " + miliraPawn.def.defName
            + " (" + miliraKind.defName + "), " + androidPawn.def.defName);

        Check(Accepts("Human", human) && Accepts("Human", copyPawn) && Accepts("Human", miliraPawn)
            && !Accepts("Human", androidPawn), "A1 a plan for the human body takes humans, the copy and Milira, not the android");
        Check(Accepts("Milira_Body", miliraPawn) && !Accepts("Milira_Body", human) && !Accepts("Milira_Body", copyPawn),
            "A2 a plan for Milira takes Milira only");
        Check(Accepts("Implanner_HumanCopy", human) && Accepts("Implanner_HumanCopy", miliraPawn),
            "A3 a plan pinned to the folded copy takes the human family");
        Check(Accepts("Android4Tier", androidPawn) && !Accepts("Android4Tier", human) && Accepts("", androidPawn),
            "A4 an android plan takes androids; a plan for any body takes everyone");

        // The synced command and the evaluation of an accepted colonist.
        ImplannerStore store = ImplannerStore.Current;
        PlannerModel model = store.Model;
        Plan humanPlan = model.CreatePlan("Human family", store.TakePlanId, 0, "Human");
        Plan miliraPlan = model.CreatePlan("Wings", store.TakePlanId, 0, "Milira_Body");
        Plan anyPlan = model.CreatePlan("Everyone", store.TakePlanId, 0, "");
        model.SetImplantSlot(humanPlan.Id, "BionicArm", new PartAddress("Shoulder", 0), true);
        store.Bump(PlannerChange.All);
        PlannerCommands.AssignPlan(copyPawn.thingIDNumber, humanPlan.Id);
        PlannerCommands.AssignPlan(miliraPawn.thingIDNumber, humanPlan.Id);
        PlannerCommands.AssignPlan(androidPawn.thingIDNumber, humanPlan.Id);
        PlannerCommands.AssignPlan(human.thingIDNumber, miliraPlan.Id);
        int Assigned(Pawn pawn) => model.Assignments.TryGetValue(pawn.thingIDNumber, out int id) ? id : 0;
        Check(Assigned(copyPawn) == humanPlan.Id && Assigned(miliraPawn) == humanPlan.Id,
            "C1 AssignPlan enlists the copy and Milira in the human-body plan");
        Check(Assigned(androidPawn) == 0 && Assigned(human) == 0,
            "C2 AssignPlan refuses the android for the human plan and the human for the Milira plan");
        Type projection = typeof(ImplannerStore).Assembly.GetType("Implanner.PawnProjection");
        var evaluation = (PlanEvaluation)AccessTools.Method(projection, "Evaluate",
            new[] { typeof(PlannerModel), typeof(Pawn), typeof(IReadOnlyList<ImplantGoal>), typeof(bool), typeof(int), typeof(string) })
            .Invoke(null, new object[] { model, miliraPawn, model.EffectiveImplants(humanPlan), false, humanPlan.MinQuality, humanPlan.Body });
        Check(evaluation.TotalUnits == 1, "E1 the Milira colonist's human-plan bionic arm counts as a target (" + evaluation.TotalUnits + ")");

        // The picker's body filter for an unpinned plan lists the offered
        // bodies only; the new-plan dialog uses the same list.
        OpenDialog("Plans");
        object plans = Traverse.Create(dialog).Field("view").Field("plans").GetValue();
        Traverse.Create(plans).Field("SelectedPlanId").SetValue(anyPlan.Id);
        object snapshot = AccessTools.Method(plans.GetType(), "Current").Invoke(plans, new object[] { store });
        var labels = (string[])Traverse.Create(snapshot).Field("BodyLabels").GetValue();
        var positions = (int[])Traverse.Create(snapshot).Field("BodyPositions").GetValue();
        Write("body filter: " + string.Join(" | ", labels) + "; positions " + string.Join(",", positions));
        Check(labels.Length == offered.Count + 1 && !labels.Contains(copy.LabelCap.ToString())
            && labels.Contains(milira.LabelCap.ToString()) && positions[1] == 0,
            "U1 the body filter lists the offered bodies and folds the human copy");
        Capture("plans-body-filter");
    }

    static void OpenDialog(string tab)
    {
        Type type = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.Dialog_Implanner");
        Type view = typeof(ImplannerStore).Assembly.GetType("Implanner.UI.ImplannerView");
        Type tabs = view.GetNestedType("Tab", AccessTools.all);
        AccessTools.Field(view, "curTab").SetValue(null, Enum.Parse(tabs, tab));
        dialog = (Window)Activator.CreateInstance(type);
        Find.WindowStack.Add(dialog);
    }
}
