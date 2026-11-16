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
using WorkRoles.UI;

namespace EducationTraitsProbe;

// WorkRoles with Progression: Education loaded (2026-11-16).
//  E1 the mod is active and its proficiency traits exist.
//  E2 a colonist carrying three proficiency traits shows only the other
//     traits in the selected panel's Traits box (capture wr-education-panel).
//  T1 a skill tooltip whose signal table is wider than the tooltip limit
//     (a long trait name and a long stat name) wraps those cells inside the
//     tooltip instead of running past its right edge (capture
//     wr-education-tooltip).
public sealed class Probe : GameComponent
{
    private const string LongTrait = "nimble disciple of the seventh mountain school of evasive footwork";
    private const string LongStat = "chance to spring a concealed trap while walking across it unaware";
    private static readonly string[] Proficiencies =
        { "PE_FirearmProficiency", "PE_AnimalRidingProficiency", "PE_BasicSpeechProficiency" };
    private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static bool inStats;
    private static string meleeLabel;
    private static Vector2 meleePoint;
    private int phase, frames, nextFrame, failures;
    private string capture;
    private Pawn han;
    private MainTabWindow_WorkRoles window;
    private object view;

    public Probe(Game game) { }

    private static Type ModType(string name) => typeof(RoleCommands).Assembly.GetType(name, true);
    private static object Get(object owner, string name) => AccessTools.Property(owner.GetType(), name)?.GetValue(owner)
        ?? AccessTools.Field(owner.GetType(), name)?.GetValue(owner);
    private static object Call(object owner, string name, params object[] args)
    {
        Type type = owner as Type ?? owner.GetType();
        return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length)
            .Invoke(owner is Type ? null : owner, args);
    }
    private static void Record(string text) { File.AppendAllText(Path.Combine(Root, "wr-education.txt"), text + Environment.NewLine); Log.Message("[WREducation] " + text); }
    private void Check(bool value, string text) { Record((value ? "PASS " : "FAIL ") + text); if (!value) failures++; }
    private void Capture(string name)
    {
        capture = name;
        File.WriteAllText(Path.Combine(Root, "wr-education-stage.txt"), "CAPTURE " + name);
    }
    private bool Captured() => File.Exists(Path.Combine(Root, "wr-education-captured-" + capture + ".txt"));

    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting || ++frames < 30) return;
        try
        {
            if (phase == 0) { Setup(); nextFrame = frames + 15; phase = 1; }
            else if (phase == 1 && frames >= nextFrame) { VerifyTraits(); Capture("wr-education-panel"); phase = 2; }
            else if (phase == 2 && Captured())
            {
                if (meleePoint == default) throw new InvalidOperationException("Melee skill cell was not drawn");
                File.WriteAllText(Path.Combine(Root, "wr-education-hover.txt"),
                    (int)(meleePoint.x * Prefs.UIScale) + "," + (int)(meleePoint.y * Prefs.UIScale));
                Capture("wr-education-tooltip"); phase = 3;
            }
            else if (phase == 3 && Captured())
            {
                VerifyTooltip();
                string status = failures == 0 ? "SUCCESS" : "FAILED " + failures + " check(s)";
                Record(status); File.WriteAllText(Path.Combine(Root, "wr-education-stage.txt"), status); phase = 99;
            }
        }
        catch (Exception error)
        {
            Record("FAILED " + error); File.WriteAllText(Path.Combine(Root, "wr-education-stage.txt"), "FAILED " + error.Message); phase = 99;
        }
    }

    private void Setup()
    {
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
        RoleCommands.SetAutoOptimize(false);

        Check(ModLister.GetActiveModWithIdentifier("ferny.ProgressionEducation", true) != null
            && Proficiencies.All(name => DefDatabase<TraitDef>.GetNamedSilentFail(name) != null),
            "E1 Progression: Education is active and defines its proficiency traits");

        // Long names as a wordy mod or language would supply them.
        TraitDef nimble = DefDatabase<TraitDef>.GetNamed("Nimble");
        nimble.degreeDatas[0].label = LongTrait;
        AccessTools.Field(typeof(TraitDegreeData), "cachedLabelCap").SetValue(nimble.degreeDatas[0], null);
        StatDef trap = DefDatabase<StatDef>.GetNamed("PawnTrapSpringChance");
        trap.label = LongStat;
        AccessTools.Field(typeof(Def), "cachedLabelCap").SetValue(trap, default(TaggedString));

        han = PawnsFinder.AllMaps_FreeColonists.Single(p => p.LabelShort == "Han");
        if (!han.story.traits.HasTrait(nimble)) han.story.traits.GainTrait(new Trait(nimble));
        foreach (string name in Proficiencies)
        {
            TraitDef def = DefDatabase<TraitDef>.GetNamed(name);
            if (!han.story.traits.HasTrait(def)) han.story.traits.GainTrait(new Trait(def));
        }

        meleeLabel = SkillDefOf.Melee.skillLabel.CapitalizeFirst();
        var harmony = new Harmony("tests.workroles.educationtraits");
        harmony.Patch(AccessTools.Method(typeof(ColonistsTabView), "DrawStatsPanel"),
            prefix: new HarmonyMethod(typeof(Probe), nameof(StatsBegin)),
            finalizer: new HarmonyMethod(typeof(Probe), nameof(StatsEnd)));
        harmony.Patch(AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) }),
            postfix: new HarmonyMethod(typeof(Probe), nameof(LabelDrawn)));
        // Vanilla Expanded Framework asks about each new faction after load,
        // again whenever the dialog is closed unanswered; it would cover the
        // panel, so other mods' dialogs are not admitted during this run.
        harmony.Patch(AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add)),
            prefix: new HarmonyMethod(typeof(Probe), nameof(AdmitWindow)));
        foreach (Window popup in Find.WindowStack.Windows.ToArray()) popup.Close(false);

        Find.Selector.ClearSelection(); Find.Selector.Select(han);
        var button = DefDatabase<MainButtonDef>.AllDefsListForReading.Single(d => d.TabWindow is MainTabWindow_WorkRoles);
        window = (MainTabWindow_WorkRoles)button.TabWindow;
        Find.MainTabsRoot.SetCurrentTab(button);
        if (!Find.WindowStack.Windows.Contains(window)) Call(ModType("WorkRoles.SetupDefaults"), "Decide", false, false);
        if (!Find.WindowStack.Windows.Contains(window)) Find.WindowStack.Add(window);
        view = Get(window, "colonistsTab");
        object roster = Get(view, "rosterState");
        AccessTools.Property(roster.GetType(), "Search").SetValue(roster, "Han");
        ((ListSelection<Pawn>)Get(view, "selection")).Click(han);
    }

    private static bool AdmitWindow(Window window)
    {
        Assembly owner = window.GetType().Assembly;
        return window.layer != WindowLayer.Dialog || owner == typeof(Window).Assembly
            || owner == typeof(RoleCommands).Assembly;
    }
    private static void StatsBegin() => inStats = true;
    private static Exception StatsEnd(Exception __exception) { inStats = false; return __exception; }
    private static void LabelDrawn(Rect rect, string label)
    {
        if (inStats && Event.current.type == EventType.Repaint && label == meleeLabel)
            meleePoint = Verse.UI.GUIToScreenPoint(new Vector2(rect.x + 20f, rect.center.y));
    }

    private void VerifyTraits()
    {
        List<Trait> carried = han.story.traits.allTraits.Where(t => !t.Suppressed).ToList();
        // The mod grants proficiencies of its own on load (Han: fluent speech).
        Func<Trait, bool> fromEducation = t => string.Equals(t.def.modContentPack?.PackageId,
            "ferny.progressioneducation", StringComparison.OrdinalIgnoreCase);
        List<string> education = carried.Where(fromEducation).Select(t => t.LabelCap).ToList();
        List<string> others = carried.Where(t => !fromEducation(t)).Select(t => t.LabelCap).ToList();
        object traits = Call(ModType("WorkRoles.UI.ColonistSelectedPanelState"), "BuildTraits", han);
        int count = (int)Get(traits, "Count");
        var shown = new List<string>();
        for (int i = 0; i < count; i++) shown.Add((string)Get(Call(traits, "RowAt", i), "Label"));
        Check(education.Count >= 3 && Proficiencies.All(name => carried.Any(t => t.def.defName == name)),
            "E2 Han carries the proficiency traits (" + string.Join(", ", education) + ")");
        Check(shown.SequenceEqual(others), "E2 the Traits box lists only the other traits (" + string.Join(", ", shown) + "; carried: " + string.Join(", ", others) + ")");
    }

    private void VerifyTooltip()
    {
        object prepared = AccessTools.Field(ModType("RimShared.UiLib.StructuredTipPresenter"), "frozenGeometry").GetValue(null);
        object geometry = AccessTools.Field(prepared.GetType(), "geometry").GetValue(prepared);
        if (geometry == null) { Check(false, "T1 the skill tooltip opened"); return; }
        Vector2 size = (Vector2)Get(geometry, "Size");
        float padding = (float)Get(geometry, "Padding");
        float contentW = size.x - 2f * (4f + padding);
        float lineH = Text.LineHeightOf(GameFont.Small);
        float furthest = 0f; int wrappedLong = 0, texts = 0;
        foreach (object cmd in (IEnumerable)Get(geometry, "Cmds"))
        {
            string text = (string)AccessTools.Field(cmd.GetType(), "Text").GetValue(cmd);
            if (text == null) continue;
            Rect rect = (Rect)AccessTools.Field(cmd.GetType(), "Rect").GetValue(cmd);
            bool noWrap = (bool)AccessTools.Field(cmd.GetType(), "NoWrap").GetValue(cmd);
            texts++;
            furthest = Mathf.Max(furthest, rect.xMax);
            bool longCell = text.IndexOf("seventh mountain", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("concealed trap", StringComparison.OrdinalIgnoreCase) >= 0;
            if (longCell && !noWrap && rect.height > lineH * 1.5f) wrappedLong++;
        }
        Check(texts > 0 && size.x <= 800f + 2f * (4f + padding) + 1f, "T1 the tooltip stays within its width limit (" + size.x + " wide)");
        Check(furthest <= contentW + 0.5f, "T1 no text extends past the tooltip's content (" + furthest + " of " + contentW + ")");
        Check(wrappedLong >= 2, "T1 the long trait and stat cells wrap onto more lines (" + wrappedLong + ")");
    }
}
