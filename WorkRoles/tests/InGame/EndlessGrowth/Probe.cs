using System;
using System.Collections.Generic;
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

namespace EndlessGrowthProbe;

// WorkRoles with Endless Growth loaded (2026-11-02): native skills pass 20.
//  W1 a colonist raised to Crafting 60 and Plants 100 is captured at those
//     levels and a full-colony recommendation plan still builds.
//  W2 the Colonists tab shows the three-digit levels in the skill columns
//     and the selected panel (capture wr-endless-colonists); the
//     Recommendations tab renders (capture wr-endless-recommendations).
public sealed class Probe : GameComponent
{
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private int phase, frames, nextFrame, failures;
    private string capture;
    private RoleStore store;
    private List<Pawn> pawns;
    private Pawn han;
    private MainTabWindow_WorkRoles window;
    private object view, stats;

    public Probe(Game game) { }

    private static Type ModType(string name) => typeof(RoleCommands).Assembly.GetType("WorkRoles." + name, true);
    private static object Get(object owner, string name) => AccessTools.Property(owner.GetType(), name)?.GetValue(owner)
        ?? AccessTools.Field(owner.GetType(), name)?.GetValue(owner);
    private static object Call(object owner, string name, params object[] args)
    {
        Type type = owner as Type ?? owner.GetType();
        return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length)
            .Invoke(owner is Type ? null : owner, args);
    }
    private static void Record(string text) { File.AppendAllText(Path.Combine(Root, "wr-endless.txt"), text + Environment.NewLine); Log.Message("[WREndlessGrowth] " + text); }
    private void Check(bool value, string text) { Record((value ? "PASS " : "FAIL ") + text); if (!value) failures++; }
    private void Capture(string name)
    {
        capture = name;
        File.WriteAllText(Path.Combine(Root, "wr-endless-stage.txt"), "CAPTURE " + name);
    }
    private bool Captured() => File.Exists(Path.Combine(Root, "wr-endless-captured-" + capture + ".txt"));

    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting || ++frames < 30) return;
        try
        {
            if (phase == 0) { Setup(); nextFrame = frames + 15; phase = 1; }
            else if (phase == 1 && frames >= nextFrame) { Verify(); Capture("wr-endless-colonists"); phase = 2; }
            else if (phase == 2 && Captured())
            {
                AccessTools.Field(typeof(MainTabWindow_WorkRoles), "curTab").SetValue(window, 2); // Recommendations
                nextFrame = frames + 15; phase = 3;
            }
            else if (phase == 3 && frames >= nextFrame) { Capture("wr-endless-recommendations"); phase = 4; }
            else if (phase == 4 && Captured())
            {
                string status = failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)";
                Record(status); File.WriteAllText(Path.Combine(Root, "wr-endless-stage.txt"), status); phase = 99;
            }
        }
        catch (Exception error)
        {
            Record("FAILED " + error); File.WriteAllText(Path.Combine(Root, "wr-endless-stage.txt"), "FAILED " + error.Message); phase = 99;
        }
    }

    private void Setup()
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
        foreach (Window popup in Find.WindowStack.Windows.ToArray()) popup.Close(false);
        RoleCommands.SetAutoOptimize(false);
        store = RoleStore.Current;
        pawns = (List<Pawn>)Call(ModType("AutoOptimizer"), "SimulationColonists", Find.CurrentMap);
        han = pawns.Single(p => p.LabelShort == "Han");
        SkillRecord crafting = han.skills.GetSkill(SkillDefOf.Crafting);
        SkillRecord plants = han.skills.GetSkill(SkillDefOf.Plants);
        crafting.Level = 60;
        plants.Level = 100;
        Check(crafting.Level == 60 && plants.Level == 100, "W1 Endless Growth lets native skills pass 20 (Crafting " + crafting.Level + ", Plants " + plants.Level + ")");
        WorkRolesMod.Settings.skillColumns = new List<string> { "Crafting", "Plants" };
        Find.Selector.ClearSelection(); Find.Selector.Select(han);
        var button = DefDatabase<MainButtonDef>.AllDefsListForReading.Single(d => d.TabWindow is MainTabWindow_WorkRoles);
        window = (MainTabWindow_WorkRoles)button.TabWindow;
        Find.MainTabsRoot.SetCurrentTab(button);
        if (!Find.WindowStack.Windows.Contains(window)) Call(ModType("SetupDefaults"), "Decide", false, false);
        if (!Find.WindowStack.Windows.Contains(window)) Find.WindowStack.Add(window);
        view = Get(window, "colonistsTab"); stats = Get(view, "statsState");
        object roster = Get(view, "rosterState");
        AccessTools.Property(roster.GetType(), "Search").SetValue(roster, "Han");
        ((ListSelection<Pawn>)Get(view, "selection")).Click(han);
    }

    private void Verify()
    {
        Call(view, "RefreshExternalSnapshotIfNeeded");
        Call(stats, "Snapshot", han);
        var colony = RecsAdapter.BuildColonyView(store, pawns);
        int index = pawns.IndexOf(han);
        Check(colony.Pawns[index].SkillLevels["Crafting"] == 60 && colony.Pawns[index].SkillLevels["Plants"] == 100,
            "W1 recommendation facts carry the modded levels");
        var plan = RecommendationPlan.Build(colony, store.recommendationTuning);
        Check(plan != null && plan.RoleCountAt(index) > 0, "W1 full-colony plan builds and recommends roles for Han (" + (plan == null ? 0 : plan.RoleCountAt(index)) + ")");
        SkillLine crafting = (SkillLine)Call(stats, "SkillLineSnapshot", han, SkillDefOf.Crafting);
        SkillLine plants = (SkillLine)Call(stats, "SkillLineSnapshot", han, SkillDefOf.Plants);
        Check(crafting.Level == 60 && plants.Level == 100, "W2 roster skill lines show " + crafting.ValueText + " and " + plants.ValueText);
    }
}
