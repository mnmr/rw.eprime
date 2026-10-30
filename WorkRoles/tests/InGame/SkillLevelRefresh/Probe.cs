using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace SkillLevelRefresh;

// Test-only native integration and timing fixture; copied Fisso save only.
public sealed class Probe : GameComponent
{
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Probe active;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private int phase, frames, nextFrame, failures;
    private bool baseline, gainFailed, lossFailed, tracking;
    private string capture;
    private RoleStore store;
    private List<Pawn> pawns;
    private Pawn han, unrelated;
    private SkillRecord skill;
    private Dictionary<Pawn, List<RoleAssignment>> original;
    private MainTabWindow_WorkRoles window;
    private object view, stats, roster, recs;
    private Dialog_PriorityGrid grid;
    private int handler, herder, hanIndex, animalsColumn;
    private int[] pawnCaptures, capabilityCaptures, capabilityRefreshes;
    private static Type ModType(string name) => typeof(RoleCommands).Assembly.GetType("WorkRoles." + name, true);
    private static object Get(object owner, string name) => AccessTools.Property(owner.GetType(), name)?.GetValue(owner)
        ?? AccessTools.Field(owner.GetType(), name)?.GetValue(owner);
    private static object Call(object owner, string name, params object[] args)
    {
        Type type = owner as Type ?? owner.GetType();
        return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length)
            .Invoke(owner is Type ? null : owner, args);
    }
    private static void Record(string text) { File.AppendAllText(Path.Combine(Root, "skill-results.txt"), text + Environment.NewLine); Log.Message("[SkillLevelRefresh] " + text); }
    private static void Must(bool value, string text) { if (!value) throw new InvalidOperationException("SETUP/CONTROL " + text); Record("PASS " + text); }
    private bool Verify(bool value, string text)
    {
        Record((value ? "PASS " : "FAIL WR28 ") + text);
        if (!value) failures++;
        return value;
    }
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static List<RoleAssignment> Copy(IEnumerable<RoleAssignment> items) => items.Select(a => new RoleAssignment { roleId = a.roleId, state = a.state, pinned = a.pinned }).ToList();
    private List<RoleAssignment> Assignments(Pawn pawn) => store.pawnSets.TryGetValue(pawn, out var set) ? set.assignments : new List<RoleAssignment>();
    private void RestoreAssignments() { foreach (Pawn pawn in pawns) RoleCommands.PasteRoleSet(pawn, Copy(original[pawn])); }
    private int Revision(string owner, string field, Pawn pawn) => (int)Call(ModType(owner).GetField(field, All).GetValue(null), "RevisionOf", pawn);
    private int FactRevision() => Revision("ExternalPawnFacts", "FactRevisions", han);
    private int GridRevision() => Revision("PriorityGridFacts", "Revisions", han);
    private object External(Pawn pawn) => Call(stats, "ExternalSnapshot", pawn);
    private object Skills(Pawn pawn) => Get(Call(stats, "Snapshot", pawn), "Facts");
    private int CachedLevel(Pawn pawn) => ((PawnView)Get(External(pawn), "RecommendationFacts")).SkillLevels["Animals"];
    private object PlanSnapshot() { Call(view, "BuildFixEntries", null, false); return Get(recs, "planSnapshot"); }
    private void Refresh() { Call(view, "RefreshExternalSnapshotIfNeeded"); PlanSnapshot(); Call(stats, "Snapshot", han); }
    private static bool Has(RecommendationPlan plan, int index, int role)
    { for (int i = 0; i < plan.RoleCountAt(index); i++) if (plan.RoleAt(index, i) == role) return true; return false; }
    private bool UiHas(int role)
    {
        PlanSnapshot();
        var cohort = (List<Pawn>)Get(recs, "planPawns");
        Must(cohort.Count == 27, "warm UI keeps the complete native recommendation cohort");
        return Has((RecommendationPlan)Get(recs, "recommendations"), cohort.IndexOf(han), role);
    }
    private float GridAlpha()
    {
        Call(grid, "EnsureGridSnapshot");
        return (float)Get(Call(Get(grid, "gridSnapshot"), "CellAt", hanIndex, animalsColumn), "BlendAlpha");
    }
    public Probe(Game game) { active = this; }
    private static void PawnCaptured(Pawn pawn) { if (active?.tracking == true) active.Count(active.pawnCaptures, pawn); }
    private static void CapabilityCaptured(Pawn pawn) { if (active?.tracking == true) active.Count(active.capabilityCaptures, pawn); }
    private static void CapabilityRefreshed(Pawn pawn) { if (active?.tracking == true) active.Count(active.capabilityRefreshes, pawn); }
    private void Count(int[] counts, Pawn pawn) { int index = pawns.IndexOf(pawn); if (index >= 0) counts[index]++; }
    private void BeginCounts() { Array.Clear(pawnCaptures, 0, pawnCaptures.Length); Array.Clear(capabilityCaptures, 0, capabilityCaptures.Length); Array.Clear(capabilityRefreshes, 0, capabilityRefreshes.Length); tracking = true; }
    private void EndCounts() => tracking = false;
    private void Capture(string name)
    {
        capture = name;
        File.WriteAllText(Path.Combine(Root, "skill-stage.txt"), "CAPTURE " + name);
    }
    private bool Captured() => File.Exists(Path.Combine(Root, "skill-captured-" + capture + ".txt"));
    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting || ++frames < 30) return;
        try
        {
            if (phase == 0) Setup();
            else if (phase == 1 && frames >= nextFrame)
            {
                Must(skill.Level == 7 && CachedLevel(han) == 7 && UiHas(herder) && !UiHas(handler), "native Han Animals 7 initially displays Herder");
                OrdinaryXp();
                gainFailed = !Transition("gain-learn", () => skill.Learn(skill.XpRequiredForLevelUp - skill.xpSinceLastLevel + 1f, true, true), 8, true);
                Capture("gain"); phase = 2;
            }
            else if (phase == 2 && Captured())
            {
                RestoreAssignments();
                window.Close(false);
                OpenWindow(); nextFrame = frames + 12; phase = 3;
            }
            else if (phase == 3 && frames >= nextFrame)
            {
                // Independently warm level 8 even on the old build, so loss
                // cannot accidentally pass merely because gain stayed stale.
                Record("LOSS_FIXTURE native=" + skill.Level + " cached=" + CachedLevel(han) + " Handler=" + UiHas(handler));
                Must(skill.Level == 8 && CachedLevel(han) == 8 && UiHas(handler), "independent loss fixture warms native Animals 8 and Handler");
                lossFailed = !Transition("loss-learn", () => skill.Learn(-skill.xpSinceLastLevel - 1000f, true, true), 7, true);
                Capture("loss"); phase = 4;
            }
            else if (phase == 4 && Captured())
            {
                RestoreAssignments();
                window.Close(false);
                OpenWindow(); nextFrame = frames + 12; phase = 5;
            }
            else if (phase == 5 && frames >= nextFrame)
            {
                SetterAndEnsureControls();
                Must(Find.TickManager.Paused, "all whole-level edits become observable while paused");
                grid.PostClose(); grid = null;
                if (baseline) Must(gainFailed && lossFailed, "baseline reproduces both native gain and native loss defects");
                else Must(failures == 0, "all requested whole-level behavior passes");
                string status = baseline ? "RED EXPECTED gain/loss failures=" + failures : "SUCCESS WR28 native refresh and ordinary-XP controls";
                Record(status); File.WriteAllText(Path.Combine(Root, "skill-stage.txt"), status); phase = 99;
            }
        }
        catch (Exception error)
        {
            EndCounts(); grid?.PostClose(); grid = null;
            Record("FAILED " + error); File.WriteAllText(Path.Combine(Root, "skill-stage.txt"), "FAILED " + error.Message); phase = 99;
        }
    }
    private void Setup()
    {
        baseline = File.ReadAllText(Path.Combine(Root, "skill-mode.txt")).Trim() == "baseline";
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
        foreach (Window popup in Find.WindowStack.Windows.ToArray()) popup.Close(false);
        RoleCommands.SetAutoOptimize(false);
        store = RoleStore.Current;
        pawns = (List<Pawn>)Call(ModType("AutoOptimizer"), "SimulationColonists", Find.CurrentMap);
        Must(pawns.Count == 27, "complete 27-pawn Fisso colony retained without changing roles, training or demand");
        han = pawns.Single(p => p.LabelShort == "Han"); unrelated = pawns.Single(p => p.LabelShort == "Hysott"); hanIndex = pawns.IndexOf(han);
        skill = han.skills.GetSkill(SkillDefOf.Animals);
        Must(skill.Level == 7 && !skill.TotallyDisabled, "native source Han Animals 7 is usable");
        original = pawns.ToDictionary(p => p, p => Copy(Assignments(p)));
        File.WriteAllText(Path.Combine(Root, "authored-input.xml"), RoleIO.BuildXml(store));
        pawnCaptures = new int[pawns.Count]; capabilityCaptures = new int[pawns.Count]; capabilityRefreshes = new int[pawns.Count];
        var harmony = new Harmony("tests.workroles.skilllevelrefresh");
        harmony.Patch(AccessTools.Method(typeof(RecsAdapter), "CapturePawnSnapshot"), prefix: new HarmonyMethod(typeof(Probe), nameof(PawnCaptured)));
        harmony.Patch(AccessTools.Method(ModType("Capabilities.PawnWorkFacts"), "Capture"), prefix: new HarmonyMethod(typeof(Probe), nameof(CapabilityCaptured)));
        harmony.Patch(AccessTools.Method(ModType("Capabilities.PawnWorkFacts"), "Refresh"), prefix: new HarmonyMethod(typeof(Probe), nameof(CapabilityRefreshed)));
        handler = store.RoleByTemplate("WS_Handler").id; herder = store.RoleByTemplate("WS_Herder").id;
        WorkRolesMod.Settings.skillColumns = new List<string> { "Animals" };
        OpenWindow();
        grid = new Dialog_PriorityGrid(pawns);
        var workTypes = (List<WorkTypeDef>)Get(grid, "workTypes");
        animalsColumn = workTypes.FindIndex(w => w.relevantSkills?.Contains(SkillDefOf.Animals) == true);
        Must(animalsColumn >= 0, "native priority grid contains Animals work column");
        GridAlpha();
        nextFrame = frames + 15; phase = 1;
    }
    private void OpenWindow()
    {
        Find.Selector.ClearSelection(); Find.Selector.Select(han);
        var button = DefDatabase<MainButtonDef>.AllDefsListForReading.Single(d => d.TabWindow is MainTabWindow_WorkRoles);
        window = (MainTabWindow_WorkRoles)button.TabWindow;
        Find.MainTabsRoot.SetCurrentTab(button);
        if (!Find.WindowStack.Windows.Contains(window)) Call(ModType("SetupDefaults"), "Decide", false, false);
        if (!Find.WindowStack.Windows.Contains(window)) Find.WindowStack.Add(window);
        view = Get(window, "colonistsTab"); stats = Get(view, "statsState"); roster = Get(view, "rosterState"); recs = Get(view, "recommendationState");
        AccessTools.Property(roster.GetType(), "Search").SetValue(roster, "Han");
        ((ListSelection<Pawn>)Get(view, "selection")).Click(han);
        Refresh();
    }
    private bool Transition(string label, Action change, int expectedLevel, bool apply)
    {
        object beforeExternal = External(han), beforeSkills = Skills(han), otherExternal = External(unrelated), otherSkills = Skills(unrelated);
        object beforePlan = PlanSnapshot();
        int factBefore = FactRevision(), gridBefore = GridRevision(); float alphaBefore = GridAlpha();
        BeginCounts();
        long eventBytes = GC.GetAllocatedBytesForCurrentThread(), eventStart = Stopwatch.GetTimestamp();
        change();
        long eventElapsed = Stopwatch.GetTimestamp() - eventStart; eventBytes = GC.GetAllocatedBytesForCurrentThread() - eventBytes;
        EndCounts();
        Must(skill.Level == expectedLevel, label + " native transition reached whole level " + expectedLevel);
        Must(pawnCaptures.Sum() == 0 && capabilityCaptures.Sum() == 0 && capabilityRefreshes.Sum() == 0, label + " event performs no eager pawn or capability capture/refresh");
        Must(GridRevision() != gridBefore && GridAlpha() != alphaBefore, label + " existing priority-grid notification refreshes actual skill shading");
        BeginCounts();
        long refreshBytes = GC.GetAllocatedBytesForCurrentThread(), refreshStart = Stopwatch.GetTimestamp();
        Refresh();
        long refreshElapsed = Stopwatch.GetTimestamp() - refreshStart; refreshBytes = GC.GetAllocatedBytesForCurrentThread() - refreshBytes;
        EndCounts();
        bool accurate = Verify(CachedLevel(han) == expectedLevel, label + " cached external Animals level follows native whole level");
        Verify(FactRevision() != factBefore && !ReferenceEquals(beforeExternal, External(han)) && !ReferenceEquals(beforeSkills, Skills(han)), label + " factual revision and skill capture refresh");
        Verify(pawnCaptures[hanIndex] == 1 && pawnCaptures.Sum() == 1 && capabilityCaptures[hanIndex] == 1 && capabilityCaptures.Sum() == 1
            && capabilityRefreshes.Sum() == 0, label + " UI lazily recaptures only affected pawn and its capabilities once");
        Must(ReferenceEquals(otherExternal, External(unrelated)) && ReferenceEquals(otherSkills, Skills(unrelated)), label + " unrelated pawn preserves external and skill captures");
        SkillLine row = (SkillLine)Call(stats, "SkillLineSnapshot", han, SkillDefOf.Animals);
        object selected = Call(Call(stats, "Snapshot", han), "ForSkill", SkillDefOf.Animals);
        SkillLine selectedLine = (SkillLine)Get(selected, "Line");
        accurate &= Verify(row.Level == expectedLevel && selectedLine.Level == expectedLevel && row.ValueText == selectedLine.ValueText,
            label + " roster skill column and selected stats share refreshed displayed value (" + row.ValueText + ")");
        int expectedRole = expectedLevel >= 8 ? handler : herder, oldRole = expectedLevel >= 8 ? herder : handler;
        accurate &= Verify(UiHas(expectedRole) && !UiHas(oldRole), label + " actual warm UI recommends " + store.RoleById(expectedRole).label);
        Verify(!ReferenceEquals(beforePlan, PlanSnapshot()), label + " changed recommendation snapshot is published");
        var fresh = RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, pawns), store.recommendationTuning);
        Must(Has(fresh, hanIndex, expectedRole) && !Has(fresh, hanIndex, oldRole), label + " fresh full-colony native plan confirms expected training band");
        if (apply)
        {
            Call(view, "ApplyFix", han, null, false);
            accurate &= Verify(Assignments(han).Any(a => a.roleId == expectedRole) && !Assignments(han).Any(a => a.roleId == oldRole),
                label + " actual Make It So applies the current training role");
        }
        File.AppendAllText(Path.Combine(Root, "skill-transitions.csv"), label + "," + Number(eventElapsed * 1000000d / Stopwatch.Frequency) + "," + eventBytes
            + "," + Number(refreshElapsed * 1000000d / Stopwatch.Frequency) + "," + refreshBytes + "," + pawnCaptures.Sum() + "," + capabilityCaptures.Sum() + Environment.NewLine);
        return accurate;
    }
    private void SetterAndEnsureControls()
    {
        Must(skill.Level == 7 && CachedLevel(han) == 7, "setter fixture independently warmed level 7");
        StableControl("native same-Level setter", () => skill.Level = skill.levelInt);
        StableControl("native same-level EnsureMin", () => skill.EnsureMinLevelWithMargin(skill.levelInt));
        Transition("coalesced-setters", () => { skill.Level = 8; skill.Learn(.125f, true, true); skill.Level = 9; skill.Level = 8; skill.Learn(.125f, true, true); }, 8, false);
        // Independent minimum path: native EnsureMin writes levelInt directly.
        Transition("ensure-minimum", () => skill.EnsureMinLevelWithMargin(9), 9, false);
        StableControl("native EnsureMin no-op at ensured level", () => skill.EnsureMinLevelWithMargin(9));
        // The setter leaves XP untouched; this is a real same-level XP-only
        // EnsureMin adjustment, which must remain outside whole-level refresh.
        skill.Level = 10; Refresh();
        Must(skill.xpSinceLastLevel < skill.XpRequiredForLevelUp / 2f, "native EnsureMin XP-only control has room for margin");
        StableControl("native EnsureMin same-level XP margin", () => skill.EnsureMinLevelWithMargin(10));
    }
    private void StableControl(string label, Action action)
    {
        object external = External(han), skills = Skills(han), plan = PlanSnapshot();
        int fact = FactRevision(), gridRevision = GridRevision(), broad = Revision("ExternalPawnFacts", "Revisions", han);
        BeginCounts(); action(); Refresh(); EndCounts();
        Must(FactRevision() == fact && GridRevision() == gridRevision && Revision("ExternalPawnFacts", "Revisions", han) == broad
            && ReferenceEquals(external, External(han)) && ReferenceEquals(skills, Skills(han)) && ReferenceEquals(plan, PlanSnapshot())
            && pawnCaptures.Sum() == 0 && capabilityCaptures.Sum() == 0 && capabilityRefreshes.Sum() == 0, label + " preserves revisions and all warm captures");
    }
    private void OrdinaryXp()
    {
        Must(skill.XpRequiredForLevelUp - skill.xpSinceLastLevel > 32f, "source pawn has same-level positive XP headroom");
        StableControl("ordinary positive XP batch", () => { for (int i = 0; i < 100; i++) skill.Learn(.125f, true, true); });
        // Balanced exactly representable amounts keep real XP changing while
        // preventing level crossings or fixture drift across measured batches.
        for (int i = 0; i < 2000; i++) { skill.Learn(.125f, true, true); skill.Learn(-.125f, true, true); }
        const int samples = 32, pairs = 5000;
        var times = new long[samples]; var bytes = new long[samples];
        object external = External(han), skills = Skills(han), plan = PlanSnapshot();
        int fact = FactRevision(), gridRevision = GridRevision(); float xp = skill.xpSinceLastLevel;
        BeginCounts();
        for (int sample = 0; sample < samples; sample++)
        {
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread(), beforeTime = Stopwatch.GetTimestamp();
            for (int i = 0; i < pairs; i++) { skill.Learn(.125f, true, true); skill.Learn(-.125f, true, true); }
            times[sample] = Stopwatch.GetTimestamp() - beforeTime;
            bytes[sample] = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
        }
        Refresh(); EndCounts();
        Must(skill.Level == 7 && skill.xpSinceLastLevel == xp && FactRevision() == fact && GridRevision() == gridRevision,
            "measured native ordinary-XP batches retain level, XP endpoint and revisions");
        Must(ReferenceEquals(external, External(han)) && ReferenceEquals(skills, Skills(han)) && ReferenceEquals(plan, PlanSnapshot())
            && pawnCaptures.Sum() == 0 && capabilityCaptures.Sum() == 0 && capabilityRefreshes.Sum() == 0,
            "measured ordinary-XP batches allocate no recapture work and preserve all snapshot identities");
        File.WriteAllLines(Path.Combine(Root, "skill-xp-samples.csv"), new[] { "sample,calls,nanosecondsPerCall,allocatedBytes" }.Concat(
            Enumerable.Range(0, samples).Select(i => i + "," + (pairs * 2) + "," + Number(times[i] * 1000000000d / Stopwatch.Frequency / (pairs * 2)) + "," + bytes[i])));
        var ordered = times.OrderBy(v => v).ToArray();
        double medianNs = (ordered[15] + ordered[16]) / 2d * 1000000000d / Stopwatch.Frequency / (pairs * 2);
        string result = "{\"samples\":32,\"callsPerSample\":10000,\"medianNanosecondsPerCall\":" + Number(medianNs)
            + ",\"maxBatchAllocatedBytes\":" + bytes.Max() + ",\"totalAllocatedBytes\":" + bytes.Sum() + ",\"captures\":" + pawnCaptures.Sum() + "}";
        File.WriteAllText(Path.Combine(Root, "skill-xp-metrics.json"), result); Record("ORDINARY_XP " + result);
    }
}
