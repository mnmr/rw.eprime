using System;
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

namespace TrainingPolicyRegression;

public sealed class Probe : GameComponent
{
    private static Probe active;
    private int frames, phase, beforeRevision;
    private RoleStore store;
    private List<Pawn> pawns;
    private Role medic, target;
    private object view, lastSnapshot;
    private Window confirmation;
    private Vector2 blockerPoint;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Type TypeOf(string name) => typeof(RoleCommands).Assembly.GetType(name, true);
    private static object Get(object owner, string name) => AccessTools.Property(owner.GetType(), name)?.GetValue(owner)
        ?? AccessTools.Field(owner.GetType(), name)?.GetValue(owner);
    private static object Call(object owner, string name, params object[] args)
    {
        Type type = owner as Type ?? owner.GetType();
        return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length)
            .Invoke(owner is Type ? null : owner, args);
    }
    private static void Record(string value)
    {
        File.AppendAllText(Path.Combine(Root, "training-policy-results.txt"), value + Environment.NewLine);
        Log.Message("[TrainingPolicy] " + value);
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); Record("PASS " + message); }
    private void Stage(string name, Vector2 point)
    {
        File.WriteAllText(Path.Combine(Root, "training-policy-point.txt"), Mathf.RoundToInt(point.x) + "," + Mathf.RoundToInt(point.y));
        File.WriteAllText(Path.Combine(Root, "training-policy-stage.txt"), name);
    }
    private static Window Dialog() => Find.WindowStack.Windows.SingleOrDefault(w => w.GetType().Name == "CompactConfirmDialog");
    private void DialogStage(string name, bool accept)
    {
        Rect r = confirmation.windowRect;
        Stage(name, new Vector2(accept ? r.xMax - 78f : r.x + 78f, r.yMax - 33f) * Prefs.UIScale);
    }
    public Probe(Game game) { }
    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
            || LongEventHandler.AnyEventNowOrWaiting || ++frames < 30) return;
        try
        {
            if (phase == 0)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                foreach (Window w in Find.WindowStack.Windows.ToArray()) w.Close(false);
                RoleCommands.SetAutoOptimize(false);
                store = RoleStore.Current;
                pawns = (List<Pawn>)Call(TypeOf("WorkRoles.AutoOptimizer"), "SimulationColonists", Find.CurrentMap);
                Check(pawns.Count == 27, "full native Fisso cohort retained");
                Tuning(); Training();
                new Harmony("tests.workroles.trainingpolicy").Patch(AccessTools.Method(TypeOf("WorkRoles.UI.RolesTabView"), "DrawEditorChecks"),
                    postfix: new HarmonyMethod(typeof(Probe), nameof(Drawn)));
                active = this;
                var button = DefDatabase<MainButtonDef>.AllDefsListForReading.Single(d => d.TabWindow is MainTabWindow_WorkRoles);
                Find.MainTabsRoot.SetCurrentTab(button);
                if (!Find.WindowStack.Windows.Contains(button.TabWindow))
                    Call(TypeOf("WorkRoles.SetupDefaults"), "Decide", false, false);
                var window = (MainTabWindow_WorkRoles)button.TabWindow;
                AccessTools.Field(typeof(MainTabWindow_WorkRoles), "curTab").SetValue(window,
                    Enum.Parse(typeof(MainTabWindow_WorkRoles).GetNestedType("Tab", BindingFlags.NonPublic), "Roles"));
                view = Get(window, "rolesTab");
                Call(view, "SelectRole", medic.id);
                phase = 1;
            }
            else if (phase == 1)
            {
                var button = DefDatabase<MainButtonDef>.AllDefsListForReading.Single(d => d.TabWindow is MainTabWindow_WorkRoles);
                if (!Find.WindowStack.Windows.Contains(button.TabWindow)) Find.MainTabsRoot.SetCurrentTab(button);
                AccessTools.Field(typeof(MainTabWindow_WorkRoles), "curTab").SetValue(button.TabWindow,
                    Enum.Parse(typeof(MainTabWindow_WorkRoles).GetNestedType("Tab", BindingFlags.NonPublic), "Roles"));
                Call(view, "SelectRole", medic.id);
            }
            else if (phase == 2 && Dialog() != null)
            {
                confirmation = Dialog();
                Check(!medic.blocker && UiVersion.Current == beforeRevision, "trainer confirmation precedes mutation");
                Check(((string)Get(confirmation, "body")).Contains((string)Get(medic, "DisplayLabel")), "confirmation names the selected trainer");
                phase = 3; DialogStage("cancel-warning", false);
            }
            else if (phase == 3 && Dialog() == null)
            {
                Check(!medic.blocker && UiVersion.Current == beforeRevision, "Cancel preserves role and revisions");
                phase = 4; Stage("trainer-confirm", blockerPoint);
            }
            else if (phase == 4 && Dialog() != null)
            {
                confirmation = Dialog();
                Check(!medic.blocker && UiVersion.Current == beforeRevision, "second request still waits for confirmation");
                phase = 5; DialogStage("confirm-warning", true);
            }
            else if (phase == 5 && Dialog() == null)
            {
                Check(medic.blocker && UiVersion.Current > beforeRevision, "Confirm applies blocker");
                int revision = UiVersion.Current;
                confirmation.OnAcceptKeyPressed();
                Check(UiVersion.Current == revision, "repeated confirm is a no-op");
                phase = 6;
            }
            else if (phase == 7 && !medic.blocker)
            {
                Check(Dialog() == null, "turning off blocker needs no confirmation");
                const string label = "Training target — Médecin spécialiste / Krankenpflege und langfristige medizinische Ausbildung";
                RoleCommands.CreateRole(label);
                target = store.roles.Single(r => r.label == label);
                RoleCommands.AddEntry(target.id, new JobEntry(JobEntryKind.WorkType, "Doctor"));
                RoleCommands.SetRoleTraining(target.id, new List<int> { medic.id, target.id }, new List<int> { 0, 15 }, new List<int> { 15, 21 });
                Call(view, "SelectRole", target.id);
                beforeRevision = UiVersion.Current;
                phase = 8;
            }
            else if (phase == 9 && Dialog() != null)
            {
                confirmation = Dialog();
                Check(!target.blocker && UiVersion.Current == beforeRevision, "authored target warns before mutation too");
                Check(((string)Get(confirmation, "body")).Contains((string)Get(target, "DisplayLabel")), "long native target label preserved in warning");
                phase = 10; DialogStage("target-warning", false);
            }
            else if (phase == 10 && Dialog() == null)
            {
                Check(!target.blocker && UiVersion.Current == beforeRevision, "target cancellation is unchanged");
                RoleCommands.SetRoleTraining(target.id, new List<int>(), new List<int>(), new List<int>());
                phase = 11;
            }
            else if (phase == 12 && target.blocker)
            {
                Check(Dialog() == null, "unreferenced role turns on without confirmation");
                phase = 13;
            }
            else if (phase == 14 && !target.blocker)
            {
                Check(Dialog() == null, "unreferenced role turns off without confirmation");
                Check(Find.TickManager.CurTimeSpeed == TimeSpeed.Paused, "all policy changes visible while paused");
                phase = 99; Stage("SUCCESS", Vector2.zero); Record("SUCCESS WR25 and WR27 native command, plan, checkbox and confirmation regressions");
            }
        }
        catch (Exception e) { Fail(e); }
    }
    private void Fail(Exception e) { phase = 99; Record("FAILED " + e); Stage("FAILED " + e.Message, Vector2.zero); }
    private static void Drawn(object __instance, Rect rect, object model, float rowH)
    {
        if (active == null || active.phase == 99 || Event.current.type != EventType.Repaint) return;
        try { active.InspectDraw(__instance, rect, model, rowH); } catch (Exception e) { active.Fail(e); }
    }
    private void InspectDraw(object instance, Rect rect, object model, float rowH)
    {
        object header = Get(model, "Header");
        int id = (int)Get(header, "RoleId");
        blockerPoint = Verse.UI.GUIToScreenPoint(new Vector2(rect.xMax - 12f, rect.y + rowH * 1.5f)) * Prefs.UIScale;
        if (phase == 1 && id == medic.id)
        {
            Check(Get(header, "BlockerTrainingConfirmation") != null, "snapshot detects trainer membership");
            // Two unchanged published reads retain exact snapshot identity.
            if (lastSnapshot == null) { lastSnapshot = model; return; }
            Check(ReferenceEquals(lastSnapshot, model), "unchanged editor snapshot is reused");
            beforeRevision = UiVersion.Current; phase = 2; Stage("trainer-cancel", blockerPoint);
        }
        else if (phase == 6 && id == medic.id && (bool)Get(header, "Blocker"))
        { phase = 7; Stage("trainer-off", blockerPoint); }
        else if (phase == 8 && id == target.id)
        {
            Check(Get(header, "BlockerTrainingConfirmation") != null, "snapshot detects target membership");
            phase = 9; Stage("target-cancel", blockerPoint);
        }
        else if (phase == 11 && id == target.id)
        {
            Check(Get(header, "BlockerTrainingConfirmation") == null, "removing path clears warning immediately while paused");
            phase = 12; Stage("unused-on", blockerPoint);
        }
        else if (phase == 13 && id == target.id && (bool)Get(header, "Blocker"))
        { phase = 14; Stage("unused-off", blockerPoint); }
    }
    private void Tuning()
    {
        var baseline = store.recommendationTuning;
        foreach (string section in new[] { "WR_RecTuneHunterSection", "WR_RecTuneOptionalSection" })
        {
            var options = section == "WR_RecTuneHunterSection"
                ? new[] { RecommendationTuningOption.HunterFirstTierMaximum, RecommendationTuningOption.HunterSecondTierMaximum, RecommendationTuningOption.HunterThirdTierMaximum, RecommendationTuningOption.HunterFourthTierMaximum }
                : new[] { RecommendationTuningOption.OptionalTargetStrongLevel, RecommendationTuningOption.OptionalTargetGreatLevel };
            foreach (var option in options) RoleCommands.SetRecommendationTuningOption((int)option, 0);
            int revision = store.RecommendationTuningRevision;
            RoleCommands.ResetRecommendationTuningSection(section);
            Check(options.All(o => store.recommendationTuning.Get(o) == RecommendationsTuningOptions.Default.Get(o)), "WR25 exact defaults restored " + section);
            Check(store.RecommendationTuningRevision == revision + 1, "WR25 reset publishes once");
            var snapshot = store.recommendationTuning;
            RoleCommands.ResetRecommendationTuningSection(section);
            Check(store.RecommendationTuningRevision == revision + 1 && ReferenceEquals(snapshot, store.recommendationTuning), "WR25 repeated reset preserves identity and revision");
            foreach (var descriptor in RecommendationsTuningOptions.Descriptors)
                Check(descriptor.SectionLabelKey == section || snapshot.Get(descriptor.Option) == baseline.Get(descriptor.Option), "unrelated tuning retained: " + descriptor.StableKey);
        }
    }
    private static bool Has(RecommendationPlan plan, int pawn, int role) => Enumerable.Range(0, plan.RoleCountAt(pawn)).Any(i => plan.RoleAt(pawn, i) == role);
    private void Training()
    {
        medic = store.RoleByTemplate("WS_Medic");
        Role doctor = store.RoleByTemplate("WS_Doctor");
        Pawn barbor = pawns.Single(p => p.LabelShort == "Barbor");
        int b = pawns.IndexOf(barbor);
        RoleCommands.RemoveRoleFromPawn(barbor, medic.id);
        var valid = RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, pawns), store.recommendationTuning);
        Check(Has(valid, b, medic.id), "native positive control: Barbor learns through Medic");
        string authored = string.Join(",", doctor.trainingRoleIds) + ":" + string.Join(",", doctor.trainingMins) + ":" + string.Join(",", doctor.trainingMaxes);
        RoleCommands.SetRoleBlocker(medic.id, true);
        var blockedColony = RecsAdapter.BuildColonyView(store, pawns);
        var rejected = RecommendationPlan.Build(blockedColony, store.recommendationTuning);
        Check(!Has(rejected, b, medic.id), "WR27 planner excludes newly blocking Medic");
        var fix = ColonyFixPlanner.Build(blockedColony, rejected)[b];
        RoleCommands.PasteRoleSet(barbor, fix.RoleIds.Select(id => new RoleAssignment { roleId = id }).ToList());
        Check(!store.pawnSets[barbor].assignments.Any(a => a.roleId == medic.id), "WR27 applied colony fix does not assign the training blocker");
        RoleCommands.SetRoleBlocker(medic.id, false);
        var restored = RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, pawns), store.recommendationTuning);
        Check(Has(restored, b, medic.id), "native positive control restores training once unblocked");
        RoleCommands.SetRoleActiveHours(medic.id, 1);
        var conditional = RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, pawns), store.recommendationTuning);
        Check(!Has(conditional, b, medic.id), "WR27 planner excludes conditional trainer");
        RoleCommands.ClearRoleRules(medic.id);
        Check(authored == string.Join(",", doctor.trainingRoleIds) + ":" + string.Join(",", doctor.trainingMins) + ":" + string.Join(",", doctor.trainingMaxes), "authored path bands retained across policy changes");
    }
}
