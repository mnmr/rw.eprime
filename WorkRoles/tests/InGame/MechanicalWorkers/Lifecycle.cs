using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace WorkRolesMechanicalVerification
{
    public static partial class Probe
    {
        private static int phase, phaseTicks;
        private static Thing[] stations;
        private static int[] savedIds;
        private static int cleanId;
        private static readonly ScopeOption all = new ScopeOption { Kind = ScopeKind.All };
        private static string PhaseMode => File.Exists(Path.Combine(Root, "mechanical-phase.txt"))
            ? File.ReadAllText(Path.Combine(Root, "mechanical-phase.txt")).Trim() : "all";
        private static void ResumeFixture()
        {
            phase = PhaseMode == "reload-off" ? 3 : 4;
            bool enabled = RoleStore.Current.mechanicalWorkersEnabled;
            RoleCommands.SetMechanicalWorkersEnabled(true);
            savedIds = ColonistsViewProfile.Colonists().PawnsIn(all)
                .Where(p => p.Name?.ToStringFull.StartsWith("WR test ") == true)
                .OrderBy(p => p.Name.ToStringFull).Select(p => p.thingIDNumber).ToArray();
            Check(savedIds.Length == 3, "checkpoint contains all three native workers");
            RoleCommands.SetMechanicalWorkersEnabled(enabled);
            cleanId = RoleStore.Current.RoleByTemplate("WS_Cleaner").id;
            map = null;
            ContinueLifecycle();
        }
        private static void BeginLifecycle()
        {
            phase = 1; phaseTicks = 0;
            Type stationType = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
            stations = new Thing[3];
            for (int i = 0; i < workers.Length; i++)
            {
                Pawn pawn = workers[i];
                pawn.Name = new NameSingle("WR test " + i);
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = null;
                ThingDef def = i == 2 ? DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.thingClass == typeof(Building_MechCharger)).OrderBy(d => d.size.Area).First()
                    : DefDatabase<ThingDef>.AllDefsListForReading.First(d => d.thingClass == stationType
                        && (i == 0 ? d.defName == "AIRobot_RechargeStation_Cleaner"
                            : d.defName.Contains("Omni") && d.defName.EndsWith("_V")));
                Thing station = ThingMaker.MakeThing(def); station.SetFactionDirect(Faction.OfPlayer);
                IntVec3 cell = GenRadial.RadialCellsAround(pawn.Position, 8f, false).First(c => c.DistanceTo(pawn.Position) >= 3f
                    && GenAdj.OccupiedRect(c, Rot4.North, def.size).Cells.All(x => x.InBounds(map) && x.Standable(map)
                        && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null)
                    && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly));
                stations[i] = GenSpawn.Spawn(station, cell, map);
                station.TryGetComp<CompPowerTrader>().PowerOn = true;
                if (i < 2)
                {
                    AccessTools.Field(pawn.GetType(), "rechargeStation").SetValue(pawn, station);
                    AccessTools.Field(stationType, "robot").SetValue(station, pawn);
                    AccessTools.Field(stationType, "robotSpawnedOnce").SetValue(station, true);
                    pawn.needs.rest.CurLevel = 0.2f;
                }
                else pawn.needs.energy.CurLevel = 1f;
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                if (i < 2)
                {
                    if (station.TryGetComp<CompForbiddable>() != null) station.SetForbidden(false);
                    AccessTools.Method(stationType, "Notify_CallBotForRecharge").Invoke(station, null);
                    Log.Message("[MechanicalProbe] recharge facts " + pawn + " alive=" + !pawn.Dead + " spawned=" + pawn.Spawned
                        + " station=" + station.def.defName + " pos=" + station.Position + " pawnPos=" + pawn.Position
                        + " reachable=" + pawn.CanReserveAndReach(station, PathEndMode.OnCell, Danger.Deadly) + " job=" + pawn.CurJob
                        + " canReach=" + pawn.CanReach(station, PathEndMode.OnCell, Danger.Deadly) + " reserve=" + pawn.CanReserve(station)
                        + " standable=" + station.Position.Standable(map) + " forbidden=" + station.IsForbidden(pawn));
                    Check(pawn.CurJob?.def.defName == "AIRobot_GoRecharge", pawn.def.defName + " native recharge command starts a charging job");
                }
            }
            cleanId = RoleStore.Current.RoleByTemplate("WS_Cleaner").id;
            Mark("RUNNING native recharge");
        }

        private static void ContinueLifecycle()
        {
            if (phase == 5) { VerifyReviewBoundaries(); return; }
            if (phase == 6) { FinishReviewBoundaries(); return; }
            if (phase == 3 || phase == 4)
            {
                if (map == Find.CurrentMap) return;
                map = Find.CurrentMap;
                RoleStore store = RoleStore.Current;
                Check(store.mechanicalWorkersEnabled == (phase == 4) && store.mechanicalRecommendationsEnabled,
                    "full reload preserves both per-save mechanical options");
                if (phase == 3) RoleCommands.SetMechanicalWorkersEnabled(true);
                var roster = ColonistsViewProfile.Colonists().PawnsIn(all);
                workers = savedIds.Select(id => roster.Single(p => p.thingIDNumber == id)).ToArray();
                Check(workers.All(p => store.IsManaged(p) && store.pawnSets[p].assignments.Any(a => a.roleId == cleanId)),
                    "full reload preserves spawned mech and docked robot roles, including while support is off");
                Check(workers.Take(2).All(p => !p.Spawned), "docked robots remain visible in the Colonists roster");
                var outsideHours = new Role { activeHours = 1 << ((GenLocalDate.HourInteger(map) + 1) % 24) };
                Check(workers.Take(2).All(p => RoleRules.FailReason(outsideHours, p) == RuleFailReason.OutsideHours),
                    "loaded docked robots evaluate time rules using their station map");
                if (phase == 3)
                {
                    phase = 4;
                    GameDataSaveLoader.SaveGame("MechanicalSupportOn");
                    if (PhaseMode == "reload-off") { done = true; Mark("EXPORTED"); return; }
                    GameDataSaveLoader.LoadGame("MechanicalSupportOn");
                    return;
                }
                VerifyLiveCoverage();
                foreach (Window popup in Find.WindowStack.Windows.ToArray()) if (!(popup is MainTabWindow)) popup.Close(false);
                Find.Selector.ClearSelection(); Find.Selector.Select(workers[2]);
                MainButtonDef button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                Find.MainTabsRoot.SetCurrentTab(button);
                object view = AccessTools.Field(typeof(MainTabWindow_WorkRoles), "colonistsTab").GetValue(button.TabWindow);
                object state = AccessTools.Field(view.GetType(), "rosterState").GetValue(view);
                AccessTools.Property(state.GetType(), "Search").SetValue(state, "WR test");
                phase = 5; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                return;
            }
            for (int t = 0; t < 50; t++)
            {
                // The isolated fixture supplies electricity; job choice,
                // pathing, energy gain and docking all run native code.
                foreach (Thing station in stations) station.TryGetComp<CompPowerTrader>().PowerOn = true;
                Find.TickManager.DoSingleTick(); phaseTicks++;
                if (phaseTicks > 18000) throw new InvalidOperationException("Lifecycle timeout phase=" + phase + " "
                    + string.Join(";", workers.Select(p => p + " " + p.CurJob + " rest=" + p.needs.rest?.CurLevel + " energy=" + p.needs.energy?.CurLevel)));
                if (phase == 1 && workers.Take(2).All(p => p.needs.rest.CurLevel > 0.23f) && workers[2].needs.energy.CurLevel > 3f)
                {
                    Check(true, "native charging increases energy for both Misc robots and the vanilla mech despite assigned roles");
                    for (int i = 0; i < 2; i++) AccessTools.Method(stations[i].GetType(), "Notify_CallBotForShutdown").Invoke(stations[i], null);
                    phase = 2; phaseTicks = 0; Mark("RUNNING native docking");
                }
                if (phase == 2 && workers.Take(2).All(p => !p.Spawned))
                {
                    Check(workers.All(p => ColonistsViewProfile.Colonists().PawnsIn(all).Contains(p)), "native shutdown retains robot rows");
                    savedIds = workers.Select(p => p.thingIDNumber).ToArray();
                    RoleCommands.SetMechanicalRecommendationsEnabled(true);
                    RoleCommands.SetMechanicalWorkersEnabled(false);
                    phase = 3; Mark("RUNNING full save/load");
                    GameDataSaveLoader.SaveGame("MechanicalSupportOff");
                    if (PhaseMode == "native") { done = true; Mark("EXPORTED"); return; }
                    GameDataSaveLoader.LoadGame("MechanicalSupportOff");
                    return;
                }
            }
        }

        private static void VerifyLiveCoverage()
        {
            RoleStore store = RoleStore.Current;
            // Isolate the adapter's full-duty replacement boundary from the
            // save's automatic Grunt role, which already covers cleaning.
            var originalPolicies = store.roles.Select(r => (r.id, r.autoAssign, r.excludedFromRecommendations)).ToArray();
            foreach (var policy in originalPolicies)
            {
                RoleCommands.SetRoleAutoAssign(policy.id, false);
                RoleCommands.SetRoleRecommendationExcluded(policy.id, true);
            }
            RoleCommands.CreateRole("WR service verification");
            Role role = store.roles.Single(r => r.label == "WR service verification");
            RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkType, "Cleaning"));
            RoleCommands.SetRoleColonyMinimum(role.id, 1);
            var humans = map.mapPawns.FreeColonistsSpawned.Where(p => !p.Downed && !p.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning)
                && !p.DevelopmentalStage.Baby()).Take(1).ToList();
            Check(humans.Count == 1, "live coverage fixture has a capable human fallback");
            Func<bool> assigned = () =>
            {
                RecommendationPlan plan = RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, humans), store.recommendationTuning);
                return Enumerable.Range(0, plan.RoleCountAt(0)).Any(i => plan.RoleAt(0, i) == role.id);
            };
            RoleCommands.SetMechanicalRecommendationsEnabled(false);
            Check(assigned(), "integration OFF retains human service assignment");
            RoleCommands.SetMechanicalRecommendationsEnabled(true);
            Check(!assigned(), "integration ON replaces the last human when an assigned Work-mode mech covers the complete duty");
            var group = workers[2].GetMechControlGroup();
            group.SetWorkMode(MechWorkModeDefOf.Escort);
            Check(assigned(), "native mode change immediately restores human service recommendation");
            group.SetWorkMode(MechWorkModeDefOf.Work);
            Check(!assigned(), "returning to native Work mode restores mechanical coverage");
            RoleCommands.DeleteRole(role.id);
            foreach (var policy in originalPolicies)
            {
                RoleCommands.SetRoleAutoAssign(policy.id, policy.autoAssign);
                RoleCommands.SetRoleRecommendationExcluded(policy.id, policy.excludedFromRecommendations);
            }
        }
    }
}
