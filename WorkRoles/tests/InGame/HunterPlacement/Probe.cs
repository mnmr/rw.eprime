using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

// Disposable main-thread observer for the Hunter placement picker. Clicks
// come through Shared automation; the probe publishes segment centers,
// opens the section, and asserts the authoritative plan per mode.
namespace WorkRolesHunterPlacementVerification
{
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static readonly Harmony Patches = new Harmony("eprime.workroles.hunterplacementprobe");
        private static MainTabWindow_WorkRoles window;
        private static RoleStore store;
        private static List<Pawn> pawns;
        private static Role hunter;
        private static int stage, settle;
        private static string before;
        private static Pawn topShooter;
        private static int poorHunterIndex, armedHolders;
        private static readonly Dictionary<string, string> Points = new Dictionary<string, string>();
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static Probe()
        {
            Patches.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            Patches.Patch(AccessTools.Method(typeof(RecommendationsTabView), "DrawTuningEnumSegments"), prefix: new HarmonyMethod(typeof(Probe), nameof(Segments)));
        }

        private static object Read(object value, string name)
        {
            Type type = value as Type ?? value.GetType();
            object instance = value is Type ? null : value;
            FieldInfo field = AccessTools.Field(type, name);
            return field != null ? field.GetValue(instance) : AccessTools.Property(type, name).GetValue(instance, null);
        }
        private static object Call(object value, string name, params object[] args)
        {
            Type type = value as Type ?? value.GetType();
            return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length)
                .Invoke(value is Type ? null : value, args);
        }
        private static void Assert(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Log.Message("[HunterPlacementProbe] PASS " + message);
        }
        private static void Mark(string value) => File.WriteAllText(Path.Combine(Root, "hunter-stage.txt"), value);
        private static void Point(string name, Vector2 local)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 point = GUIUtility.GUIToScreenPoint(local);
            // Both runtimes scale the local coordinates but report the current
            // GUI group's origin in logical units. Include every nested group.
            point += GUIUtility.GUIToScreenPoint(Vector2.zero) * (Prefs.UIScale - 1f);
            string value = Math.Round(point.x).ToString(CultureInfo.InvariantCulture) + "," + Math.Round(point.y).ToString(CultureInfo.InvariantCulture);
            if (Points.TryGetValue(name, out string previous) && previous == value) return;
            Points[name] = value;
            File.WriteAllText(Path.Combine(Root, "hunter-" + name + ".txt"), value);
        }
        /// Mirrors the view's segment geometry for the Hunter placement row.
        private static void Segments(Rect area, object row)
        {
            var descriptor = (RecommendationTuningDescriptor)Read(row, "Descriptor");
            if (descriptor.Option != RecommendationTuningOption.HunterPlacement) return;
            var options = (IReadOnlyList<string>)Read(row, "EnumOptions");
            int count = options.Count;
            const float gap = 2f;
            float cellW = (area.width - (count - 1) * gap) / count;
            for (int i = 0; i < count; i++)
                Point("seg" + i, new Vector2(area.x + i * (cellW + gap) + cellW / 2f, area.center.y));
        }
        private static void SelectTab(string name)
        {
            FieldInfo field = AccessTools.Field(window.GetType(), "curTab");
            field.SetValue(window, Enum.Parse(field.FieldType, name));
        }
        private static RecommendationsTuningOptions Tuning => store.recommendationTuning ?? RecommendationsTuningOptions.Default;
        private static HunterPlacementMode Mode => (HunterPlacementMode)Tuning.Get(RecommendationTuningOption.HunterPlacement);
        private static RecommendationPlan Plan() => RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, pawns), Tuning);
        private static string Signature(RecommendationPlan plan)
            => string.Join(";", Enumerable.Range(0, plan.PawnCount).Select(p =>
                string.Join(",", Enumerable.Range(0, plan.RoleCountAt(p)).Select(r => plan.RoleAt(p, r)))));
        private static int HunterIndex(RecommendationPlan plan, Pawn pawn)
        {
            int p = pawns.IndexOf(pawn);
            for (int r = 0; r < plan.RoleCountAt(p); r++)
                if (plan.RoleAt(p, r) == hunter.id) return r;
            return -1;
        }
        private static bool HoldsPinnedHunter(Pawn pawn)
            => store.pawnSets.TryGetValue(pawn, out PawnRoleSet set)
                && set.assignments.Any(a => a.roleId == hunter.id && a.pinned);
        private static void Update()
        {
            if (stage == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (settle++ < 6) return;
                if (stage == 0)
                {
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                    foreach (Window popup in Find.WindowStack.Windows.ToArray()) if (!(popup is MainTabWindow)) popup.Close(false);
                    store = RoleStore.Current;
                    Assert(store != null, "real save loaded");
                    pawns = Find.CurrentMap.mapPawns.FreeColonistsSpawned.ToList();
                    hunter = store.roles.Single(r => r.templateDefName == "WS_Hunter");
                    Assert(Mode == HunterPlacementMode.PoorShotsFirst, "saved tuning uses the default Hunter placement");
                    RecommendationPlan poor = Plan();
                    before = Signature(poor);
                    topShooter = pawns.Where(p => !p.Downed && p.equipment?.Primary != null && p.equipment.Primary.def.IsRangedWeapon
                            && HunterIndex(poor, p) >= 0 && !HoldsPinnedHunter(p))
                        .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Shooting).Level).First();
                    poorHunterIndex = HunterIndex(poor, topShooter);
                    armedHolders = pawns.Count(p => HunterIndex(poor, p) >= 0);
                    Log.Message("[HunterPlacementProbe] top armed shooter " + topShooter.LabelShort + " shooting "
                        + topShooter.skills.GetSkill(SkillDefOf.Shooting).Level + " Hunter at index " + poorHunterIndex);
                    var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                    window = (MainTabWindow_WorkRoles)button.TabWindow;
                    Find.MainTabsRoot.SetCurrentTab(button);
                    SelectTab("Recommendations");
                    stage = 10; settle = 0;
                }
                else if (stage == 10)
                {
                    // PreOpen resets the view; expand only the Hunter section
                    // afterwards so the picker is on screen.
                    var expanded = (HashSet<int>)Read(Read(window, "recommendationsTab"), "expandedSections");
                    expanded.Clear();
                    expanded.Add(3);
                    stage = 1; settle = 0; Mark("poor");
                }
                else if (stage == 1 && Mode == HunterPlacementMode.BestShotsFirst)
                {
                    RecommendationPlan best = Plan();
                    int index = HunterIndex(best, topShooter);
                    Assert(index >= 0 && index < poorHunterIndex,
                        "best shots first moves the top armed shooter's Hunter earlier (" + poorHunterIndex + " -> " + index + ")");
                    // Champion scoring weighs signals, aversion and repeat
                    // championships, so the champion need not be the highest
                    // raw level; there is at most one and everyone else is a
                    // special (backup) pick.
                    int champions = 0;
                    for (int p = 0; p < pawns.Count; p++)
                    {
                        if (HunterIndex(best, pawns[p]) < 0
                            || !best.TryGetExplanation(p, hunter.id, out RoleRecommendationExplanation explanation)) continue;
                        Log.Message("[HunterPlacementProbe] " + pawns[p].LabelShort + " shooting "
                            + pawns[p].skills.GetSkill(SkillDefOf.Shooting).Level + " Hunter at " + HunterIndex(best, pawns[p])
                            + " stage " + explanation.SelectionStage + " reason " + explanation.SpecialPickReason);
                        if (explanation.SelectionStage == RecommendationSelectionStage.Required) champions++;
                        else Assert(explanation.SpecialPickReason == SpecialPickReason.Hunter || explanation.SelectionStage == RecommendationSelectionStage.Special,
                            pawns[p].LabelShort + " holds Hunter as an armed backup");
                    }
                    Assert(champions <= 1, "best shots first names at most one Hunter champion (" + champions + ")");
                    stage = 2; settle = 0; Mark("best");
                }
                else if (stage == 2 && Mode == HunterPlacementMode.NormalRole)
                {
                    RecommendationPlan normal = Plan();
                    Assert(hunter.colonyMin == 0 && hunter.coverage == 0, "seeded Hunter carries no demand");
                    // A demand-less skilled role is still surplus-eligible for
                    // strong signals, like Butcher or Brewer: holders come from
                    // the scored pass, never from the armed-everyone handout.
                    int holders = 0;
                    for (int p = 0; p < pawns.Count; p++)
                    {
                        if (HunterIndex(normal, pawns[p]) < 0 || HoldsPinnedHunter(pawns[p])) continue;
                        holders++;
                        Assert(normal.TryGetExplanation(p, hunter.id, out RoleRecommendationExplanation explanation)
                            && explanation.SpecialPickReason == SpecialPickReason.None
                            && explanation.SelectionStage != RecommendationSelectionStage.Special
                            && explanation.SelectionStage != RecommendationSelectionStage.None,
                            pawns[p].LabelShort + " holds Hunter through the scored pass (" + explanation.SelectionStage + ")");
                        Assert(pawns[p].equipment?.Primary != null && pawns[p].equipment.Primary.def.IsRangedWeapon,
                            pawns[p].LabelShort + " is armed");
                    }
                    Assert(holders > 0 && holders < armedHolders,
                        "normal role: Hunter is a scored surplus pick for strong shooters, not every armed colonist (" + holders + " of " + armedHolders + ")");
                    stage = 3; settle = 0; Mark("normal");
                }
                else if (stage == 3 && Mode == HunterPlacementMode.PoorShotsFirst)
                {
                    Assert(Signature(Plan()) == before, "returning to poor shots first restores the original recommendations exactly");
                    stage = 99; Mark("SUCCESS");
                    Log.Message("[HunterPlacementProbe] SUCCESS");
                    Patches.UnpatchAll(Patches.Id);
                }
            }
            catch (Exception error)
            {
                stage = 99;
                Log.Error("[HunterPlacementProbe] FAILED " + error);
                Mark("FAILED " + error);
            }
        }
    }
}
