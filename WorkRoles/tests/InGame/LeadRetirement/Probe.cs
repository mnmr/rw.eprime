using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace LeadRetirement;

// Test-only native consumer: production adapter, planner, Apply handler and
// actual Recommendations tab. No changes to production or the player's save.
public sealed class Probe : GameComponent
{
    private static Probe active;
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly RecommendationTuningOption[] Hunter = {
        RecommendationTuningOption.HunterFirstTierMaximum, RecommendationTuningOption.HunterSecondTierMaximum,
        RecommendationTuningOption.HunterThirdTierMaximum, RecommendationTuningOption.HunterFourthTierMaximum };
    private int frames, phase, expectedSections, hunterIndex;
    private RoleStore store;
    private List<Pawn> pawns;
    private Dictionary<Pawn, List<RoleAssignment>> original;
    private MainTabWindow_WorkRoles window;
    private object colonists, recommendationsView, tuningState;
    private Vector2 headerPoint, resetPoint;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static object Get(object owner, string name) => AccessTools.Property(owner.GetType(), name)?.GetValue(owner)
        ?? AccessTools.Field(owner.GetType(), name)?.GetValue(owner);
    private static object Call(object owner, string name, params object[] args)
    {
        Type type = owner as Type ?? owner.GetType();
        return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length)
            .Invoke(owner is Type ? null : owner, args);
    }
    private static Type TypeOf(string name) => typeof(RoleCommands).Assembly.GetType(name, true);
    private static void Record(string message) { File.AppendAllText(Path.Combine(Root, "lead-results.txt"), message + Environment.NewLine); Log.Message("[LeadRetirement] " + message); }
    private static void Report(string message) => File.AppendAllText(Path.Combine(Root, "lead-outcomes.txt"), message + Environment.NewLine);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Record("PASS " + message); }
    private static List<RoleAssignment> Copy(IEnumerable<RoleAssignment> source) => source.Select(a => new RoleAssignment { roleId = a.roleId, state = a.state, pinned = a.pinned }).ToList();
    private static string Sequence(IEnumerable<RoleAssignment> source) => string.Join(",", source.Select(a => a.roleId + ":" + (int)a.state + ":" + (a.pinned ? "pin" : "free")));
    private List<RoleAssignment> Assignments(Pawn pawn) => store.pawnSets.TryGetValue(pawn, out var set) ? set.assignments : new List<RoleAssignment>();
    private void Stage(string stage, Vector2 point)
    {
        File.WriteAllText(Path.Combine(Root, "lead-point.txt"), Mathf.RoundToInt(point.x) + "," + Mathf.RoundToInt(point.y));
        File.WriteAllText(Path.Combine(Root, "lead-stage.txt"), stage);
    }
    public Probe(Game game) { active = this; }
    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
            || LongEventHandler.AnyEventNowOrWaiting || ++frames < 30) return;
        try
        {
            if (phase == 0)
            {
                expectedSections = File.ReadAllText(Path.Combine(Root, "lead-mode.txt")).Trim() == "baseline" ? 5 : 4;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                foreach (Window popup in Find.WindowStack.Windows.ToArray()) popup.Close(false);
                RoleCommands.SetAutoOptimize(false);
                store = RoleStore.Current;
                pawns = (List<Pawn>)Call(TypeOf("WorkRoles.AutoOptimizer"), "SimulationColonists", Find.CurrentMap);
                Check(pawns.Count == 27, "complete 27-pawn native Fisso cohort retained");
                original = pawns.ToDictionary(p => p, p => Copy(Assignments(p)));
                Find.Selector.ClearSelection();
                var button = DefDatabase<MainButtonDef>.AllDefsListForReading.Single(d => d.TabWindow is MainTabWindow_WorkRoles);
                window = (MainTabWindow_WorkRoles)button.TabWindow;
                Find.MainTabsRoot.SetCurrentTab(button);
                if (!Find.WindowStack.Windows.Contains(window)) Call(TypeOf("WorkRoles.SetupDefaults"), "Decide", false, false);
                colonists = Get(window, "colonistsTab");
                ((ListSelection<Pawn>)Get(colonists, "selection")).Clear();
                recommendationsView = Get(window, "recommendationsTab");
                tuningState = Get(recommendationsView, "state");
                new Harmony("tests.workroles.leadretirement").Patch(AccessTools.Method(recommendationsView.GetType(), "Draw"),
                    postfix: new HarmonyMethod(typeof(Probe), nameof(Drawn)));
                phase = 1;
            }
            else if (phase == 1 && frames > 45)
            {
                RunCase("saved");
                RestoreAssignments();
                RoleCommands.SetRecommendationTuningOption((int)RecommendationTuningOption.LeadMinimumConnectedTargets, 20);
                RoleCommands.SetRecommendationTuningOption((int)RecommendationTuningOption.LeadMinimumSignal, 5);
                RoundTripLegacyValues();
                RunCase("legacy-nondefault");
                RestoreAssignments();
                foreach (var option in Hunter) RoleCommands.SetRecommendationTuningOption((int)option, 0);
                var tabField = AccessTools.Field(window.GetType(), "curTab");
                tabField.SetValue(window, Enum.Parse(tabField.FieldType, "Recommendations"));
                ((HashSet<int>)Get(recommendationsView, "expandedSections")).Clear();
                phase = 2;
            }
            else if (phase == 4 && Hunter.All(o => store.recommendationTuning.Get(o) == RecommendationsTuningOptions.Default.Get(o)))
            {
                Check(Hunter.Select(o => store.recommendationTuning.Get(o)).SequenceEqual(new[] { 4, 8, 12, 16 }), "remaining Hunter Reset restores 4,8,12,16 through actual UI click");
                int revision = store.RecommendationTuningRevision;
                RoleCommands.ResetRecommendationTuningSection("WR_RecTuneHunterSection");
                Check(store.RecommendationTuningRevision == revision, "repeated remaining-section Reset is a no-op");
                Check(store.recommendationTuning.Get(RecommendationTuningOption.LeadMinimumConnectedTargets) == 20
                    && store.recommendationTuning.Get(RecommendationTuningOption.LeadMinimumSignal) == 5, "remaining-section Reset preserves hidden legacy values");
                phase = 5; Stage("collapse", headerPoint);
            }
        }
        catch (Exception error) { Fail(error); }
    }
    private void RestoreAssignments() { foreach (var pawn in pawns) RoleCommands.PasteRoleSet(pawn, Copy(original[pawn])); }
    private void RoundTripLegacyValues()
    {
        string assignments = string.Join("|", pawns.Select(p => p.thingIDNumber + ":" + Sequence(Assignments(p))));
        string xml = RoleIO.BuildXml(store);
        File.WriteAllText(Path.Combine(Root, "legacy-options-export.xml"), xml);
        RoleCommands.SetRecommendationTuningOption((int)RecommendationTuningOption.LeadMinimumConnectedTargets, 1);
        RoleCommands.SetRecommendationTuningOption((int)RecommendationTuningOption.LeadMinimumSignal, 0);
        RoleCommands.ApplyImport(new ImportSelection { xml = xml, gameSettings = true });
        Check(store.recommendationTuning.Get(RecommendationTuningOption.LeadMinimumConnectedTargets) == 20
            && store.recommendationTuning.Get(RecommendationTuningOption.LeadMinimumSignal) == 5,
            "production export/import preserves nondefault legacy lead option keys and values");
        Check(assignments == string.Join("|", pawns.Select(p => p.thingIDNumber + ":" + Sequence(Assignments(p)))),
            "settings-only roundtrip preserves all native assignments, states and pins");
    }
    private void RunCase(string name)
    {
        Report("CASE " + name);
        foreach (var pawn in pawns) Report("INPUT " + pawn.thingIDNumber + " " + pawn.LabelShort + " " + Sequence(Assignments(pawn)));
        ColonyView colony = RecsAdapter.BuildColonyView(store, pawns);
        RecommendationPlan direct = RecommendationPlan.Build(colony, store.recommendationTuning);
        Report("COHORT " + direct.PawnCount);
        for (int p = 0; p < direct.PawnCount; p++)
        {
            Report("RECOMMEND " + pawns[p].thingIDNumber + " " + string.Join(",", Enumerable.Range(0, direct.RoleCountAt(p)).Select(i => direct.RoleAt(p, i))));
            Report("PATHS " + pawns[p].thingIDNumber + " " + string.Join(",", Enumerable.Range(0, direct.PathCountAt(p)).Select(i => direct.PathAt(p, i) + ":" + direct.PathActivatedAt(p, i))));
        }
        Call(colonists, "RefreshExternalSnapshotIfNeeded");
        Call(colonists, "BuildFixEntries", null, false);
        object state = Get(colonists, "recommendationState");
        object plans = Get(state, "plans"), snapshot = Get(state, "planSnapshot");
        var targets = ((IEnumerable)plans).Cast<object>().ToDictionary(p => (Pawn)Get(p, "Pawn"), p => Copy((List<RoleAssignment>)Get(p, "Target")));
        Check(targets.Count == pawns.Count && pawns.All(targets.ContainsKey), name + " actual Fix My Colony covers the complete native cohort");
        foreach (var pawn in pawns) Report("TARGET " + pawn.thingIDNumber + " " + Sequence(targets[pawn]));
        int revision = store.RecommendationTuningRevision;
        RoleCommands.SetRecommendationTuningOption((int)RecommendationTuningOption.LeadMinimumSignal,
            store.recommendationTuning.Get(RecommendationTuningOption.LeadMinimumSignal));
        Call(colonists, "BuildFixEntries", null, false);
        Check(ReferenceEquals(plans, Get(state, "plans")) && ReferenceEquals(snapshot, Get(state, "planSnapshot"))
            && revision == store.RecommendationTuningRevision, name + " unchanged reads/no-op edit preserve plan and snapshot identity");
        Call(colonists, "ApplyFix", null, null, false);
        foreach (var pawn in pawns)
        {
            Check(Sequence(Assignments(pawn)) == Sequence(targets[pawn]), name + " actual Apply matches published target for " + pawn.LabelShort);
            Report("APPLIED " + pawn.thingIDNumber + " " + Sequence(Assignments(pawn)));
        }
    }
    private static void Drawn(object __instance, Rect rect)
    {
        if (active == null || active.recommendationsView != __instance || Event.current.type != EventType.Repaint) return;
        try { active.InspectDraw(rect); } catch (Exception error) { active.Fail(error); }
    }
    private void InspectDraw(Rect rect)
    {
        if (phase != 2 && phase != 3 && phase != 5) return;
        object tuning = Get(tuningState, "Tuning");
        if (tuning == null) return;
        int count = (int)Get(tuning, "Count");
        var expanded = (HashSet<int>)Get(recommendationsView, "expandedSections");
        if (phase == 2)
        {
            Check(count == expectedSections, "actual Recommendations tab publishes expected " + expectedSections + " sections");
            var keys = Enumerable.Range(0, count).Select(i => (string)Get(Call(tuning, "SectionAt", i), "Key")).ToArray();
            Check(keys.Contains("WR_RecTuneLeadSection") == (expectedSections == 5), "lead controls visible only in baseline");
            Record("SECTIONS " + string.Join(",", keys));
            hunterIndex = Array.IndexOf(keys, "WR_RecTuneHunterSection");
            Check(hunterIndex >= 0, "remaining Hunter section remains available");
            // Same native geometry inputs as the rendered section stack, used
            // only to annotate screenshots and choose actual input coordinates.
            float panelW = rect.width - 40f, width = (panelW - 24f) / 2f;
            float gx = 16f + width + 24f;
            float columnsTop = 12f + (float)Get(tuningState, "ExclusionsHeaderHeight") + 6f
                + (float)Get(tuningState, "ExclusionsHelpHeight") + 6f
                + (float)Get(Get(tuningState, "Exclusions"), "LayoutHeight") + 16f + 16f;
            float startY = columnsTop + 26f + (float)Get(tuning, "GlobalHelpHeight") + 8f;
            Vector2 origin = Verse.UI.GUIToScreenPoint(new Vector2(rect.x + gx, rect.y + startY)) * Prefs.UIScale;
            headerPoint = origin + new Vector2(width / 2f, hunterIndex * 32f + 14f) * Prefs.UIScale;
            resetPoint = origin + new Vector2(width - 35f, hunterIndex * 32f + 44f) * Prefs.UIScale;
            File.WriteAllText(Path.Combine(Root, "global-sections-bounds.txt"),
                Mathf.RoundToInt(origin.x) + "," + Mathf.RoundToInt(origin.y) + "," + Mathf.RoundToInt(width * Prefs.UIScale) + "," + Mathf.RoundToInt((count * 32f - 4f) * Prefs.UIScale));
            phase = 3; Stage("collapsed", headerPoint);
        }
        else if (phase == 3 && expanded.Contains(hunterIndex))
        {
            Check(Hunter.All(o => store.recommendationTuning.Get(o) == 0), "remaining controls display authored nondefault thresholds before reset");
            phase = 4; Stage("reset", resetPoint);
        }
        else if (phase == 5 && !expanded.Contains(hunterIndex))
        {
            float width = (float)Get(tuningState, "tuningWidth");
            Call(tuningState, "EnsureTuning", store, width);
            object stable = Get(tuningState, "Tuning");
            Call(tuningState, "EnsureTuning", store, width);
            Check(ReferenceEquals(stable, Get(tuningState, "Tuning")), "unchanged tuning UI reuses published snapshot");
            phase = 99; Stage("SUCCESS", Vector2.zero); Record("SUCCESS native lead retirement comparison");
        }
    }
    private void Fail(Exception error) { phase = 99; Record("FAILED " + error); Stage("FAILED " + error.Message, Vector2.zero); }
}
