using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace WorkRolesPawnFacts;

// This is a throwaway managed-run fixture, never a shipped mod. Native game
// mutations are deliberately exercised; no save is written. Reflection reaches
// the real window/cache boundary without introducing production test seams.
public sealed partial class Probe : GameComponent
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Probe instance;
    private int frames, phase;
    private string mode, capture;
    private RoleStore store;
    private MainTabWindow_WorkRoles window;
    private object view, roster;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static Assembly Mod => typeof(RecsAdapter).Assembly;
    private static Type ModType(string name) => Mod.GetType("WorkRoles." + name, true);
    private static object Get(object obj, string name) => AccessTools.Property(obj.GetType(), name)?.GetValue(obj) ?? AccessTools.Field(obj.GetType(), name)?.GetValue(obj);
    private static void Set(object obj, string name, object value) => AccessTools.Property(obj.GetType(), name).SetValue(obj, value);
    private static object Call(object owner, string name, params object[] args)
    {
        Type type = owner as Type ?? owner.GetType();
        return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(owner is Type ? null : owner, args);
    }
    private static void Record(string text)
    {
        File.AppendAllText(Path.Combine(Root, "pawn-facts-results.txt"), "tick=" + Find.TickManager.TicksGame + " " + text + Environment.NewLine);
        Log.Message("[PawnFactsProbe] " + text);
    }
    private static void Require(bool value, string text)
    {
        if (!value) throw new InvalidOperationException(text);
        Record("PASS " + text);
    }
    private void Capture(string name)
    {
        capture = name;
        File.WriteAllText(Path.Combine(Root, "pawn-facts-stage.txt"), "CAPTURE " + name);
    }
    private bool Captured() => capture != null && File.Exists(Path.Combine(Root, "pawn-facts-captured-" + capture + ".txt"));
    private void Finish(string text)
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        Record(text);
        File.WriteAllText(Path.Combine(Root, "pawn-facts-stage.txt"), text);
        phase = 99;
    }
    private static PawnView Facts(object snapshot) => (PawnView)Get(snapshot, "RecommendationFacts");
    private static int Revision(string registry, Pawn pawn) => (int)Call(ModType("ExternalPawnFacts").GetField(registry, All).GetValue(null), "RevisionOf", pawn);
    private static object SkillIdentity(object stats, Pawn pawn) => Get(Call(stats, "Snapshot", pawn), "Facts");
    private static object Snapshot(object stats, Pawn pawn) => Call(stats, "ExternalSnapshot", pawn);
    private static bool Has(RecommendationPlan plan, int pawn, int role)
    {
        for (int i = 0; i < plan.RoleCountAt(pawn); i++) if (plan.RoleAt(pawn, i) == role) return true;
        return false;
    }
    static Probe()
    {
        new Harmony("tests.workroles.pawnfacts").Patch(AccessTools.Method(typeof(Pawn_AgeTracker), "BirthdayBiological"),
            postfix: new HarmonyMethod(typeof(Probe), nameof(Birthday)));
    }
    public Probe(Game game) { instance = this; }
    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting || ++frames < 30) return;
        try
        {
            if (mode == null)
            {
                mode = File.ReadAllText(Path.Combine(Root, "pawn-facts-mode.txt")).Trim();
                Require(mode == "ui" || mode == "external" || mode == "birthday", "known fresh-run mode " + mode);
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                // Hide only the automatic log overlay. Recoverable native save
                // errors remain fully present in Player.log for review.
                AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                foreach (Window popup in Find.WindowStack.Windows.ToArray()) popup.Close(false);
                RoleCommands.SetAutoOptimize(false);
                store = RoleStore.Current;
            }
            if (mode == "external") { ExternalChecks(); Finish("SUCCESS external WR16 WR17"); }
            else if (mode == "ui") UiChecks();
            else BirthdayUpdate();
        }
        catch (Exception ex) { Finish("FAILED " + mode + " " + ex); }
    }
    private bool OpenColonists()
    {
        var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
        window = (MainTabWindow_WorkRoles)button.TabWindow;
        FieldInfo tab = AccessTools.Field(window.GetType(), "curTab");
        tab.SetValue(window, Enum.Parse(tab.FieldType, "Colonists"));
        if (!Find.WindowStack.Windows.Contains(window))
        {
            Find.MainTabsRoot.SetCurrentTab(button);
            // The player's legitimate first-use default chooser can intercept
            // opening. Explicitly decline in this isolated profile only.
            if (!Find.WindowStack.Windows.Contains(window))
                Call(ModType("SetupDefaults"), "Decide", false, false);
            return false;
        }
        view = Get(window, "colonistsTab"); roster = Get(view, "rosterState");
        return true;
    }
}
