using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.UI;

namespace WorkRolesMechanicalReview
{
    public static partial class Probe
    {
        internal static bool NativeReloadPending;
        private static int travelRobotId, travelStationId, travelRoleId;
        private static Game travelGame;
        private static int travelStage;
        private static Pawn travelRobot;
        private static Thing travelStation;
        private static Building_GravEngine travelEngine;

        // Run after the visual scenes: native flight replaces the map's ship,
        // and the quality scenario deliberately isolates its available workers.
        internal static void RunNativeChecks()
        {
            Find.WindowStack.WindowOfType<Dialog_PriorityGrid>()?.Close(false);
            RoleCommands.SetMechanicalRecommendationsEnabled(false);
            RoleCommands.SetMechanicalWorkersEnabled(false);
            Pawn factionRobot = workers[1];
            RoleCommands.PasteRoleSet(factionRobot, new List<RoleAssignment>());
            int before = UiVersion.Roster;
            factionRobot.SetFaction(null);
            factionRobot.SetFaction(Faction.OfPlayer);
            Check(UiVersion.Roster == before, "O4 both-OFF native robot faction round trip preserves human roster revision");
            RoleCommands.SetMechanicalWorkersEnabled(true);
            before = UiVersion.Roster;
            factionRobot.SetFaction(null);
            Check(UiVersion.Roster != before, "O4 enabled support observes native robot leaving player faction");
            factionRobot.SetFaction(Faction.OfPlayer);

            {
                Pawn doomed;
                if (ModsConfig.BiotechActive)
                {
                    doomed = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Cleansweeper"), Faction.OfPlayer);
                    GenSpawn.Spawn(doomed, workers[0].Position + IntVec3.North, map);
                }
                else
                {
                    doomed = (Pawn)AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"),
                        "CreateRobot", new[] { typeof(string), typeof(IntVec3), typeof(Map) })
                        .Invoke(null, new object[] { "AIRobot_Cleaner", workers[0].Position + IntVec3.North, map });
                }
                object roster = Field(view, "rosterState");
                PropertySetter(roster, "Search", "");
                Check(!RoleStore.Current.pawnSets.ContainsKey(doomed), "N2 dying native mechanical worker is unassigned");
                Check(((IEnumerable)Invoke(roster, "ListedPawns")).Cast<Pawn>().Contains(doomed), "N2 native unassigned mechanical worker is in cached roster before death");
                doomed.Kill(null);
                Check(doomed.Dead && !((IEnumerable)Invoke(roster, "ListedPawns")).Cast<Pawn>().Contains(doomed),
                    "N2 death removes unassigned mechanical worker from published roster while paused");
            }
            Find.MainTabsRoot.EscapeCurrentTab(false);

            Pawn cleaner = workers[0];
            Type stationType = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
            ThingDef stationDef = DefDatabase<ThingDef>.GetNamed("AIRobot_RechargeStation_Cleaner");
            Building_GravEngine engine = map.listerThings.AllThings.OfType<Building_GravEngine>().FirstOrDefault();
            if (engine == null) throw new InvalidOperationException("Native gravship in source save is required for N1");
            IntVec3 cell = engine.ValidSubstructure.First(c => GenAdj.OccupiedRect(c, Rot4.North, stationDef.size).Cells
                .All(x => engine.ValidSubstructure.Contains(x) && x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstPawn(map) == null));
            Thing station = ThingMaker.MakeThing(stationDef);
            station.SetFactionDirect(Faction.OfPlayer); GenSpawn.Spawn(station, cell, map);
            AccessTools.Field(cleaner.GetType(), "rechargeStation").SetValue(cleaner, station);
            AccessTools.Field(stationType, "robot").SetValue(station, cleaner);
            AccessTools.Field(stationType, "robotSpawnedOnce").SetValue(station, true);
            Role clean = RoleStore.Current.RoleByTemplate("WS_Cleaner");
            RoleCommands.PasteRoleSet(cleaner, new List<RoleAssignment> { new RoleAssignment { roleId = clean.id } });
            var areas = (Dictionary<Map, Area>)Field(cleaner.playerSettings, "allowedAreas");
            for (int mode = 0; mode < 2; mode++)
            {
                RoleCommands.SetMechanicalWorkersEnabled(mode == 1);
                if (!cleaner.Spawned) AccessTools.Method(stationType, "Button_SpawnBot").Invoke(station, null);
                Check(cleaner.Spawned, "O3 native station UI action respawns its contained robot");
                if (!map.areaManager.TryMakeNewAllowed(out Area_Allowed area)) throw new InvalidOperationException("Could not create allowed area");
                area[cell] = true; cleaner.playerSettings.AreaRestrictionInPawnCurrentMap = area;
                AccessTools.Method(stationType, "AddRobotToContainer").Invoke(station, new object[] { cleaner });
                Check(!cleaner.Spawned && !PawnsFinder.All_AliveOrDead.Contains(cleaner), "O3 native docked robot uses plain-list containment");
                area.Delete();
                Check(areas.ContainsKey(map) == (mode == 0), mode == 0
                    ? "O3 both-OFF area deletion retains pre-feature native docked restriction behavior"
                    : "O3 enabled support clears deleted docked restriction");
            }

            if (ModsConfig.BiotechActive) RunQualityModeChecks();
            if (cleaner.Spawned) AccessTools.Method(stationType, "AddRobotToContainer").Invoke(station, new object[] { cleaner });
            // No further simulation after takeoff: the save must capture the
            // actual airborne dictionary rather than a spawned/packed holder.
            RoleCommands.SetMechanicalWorkersEnabled(true);
            travelRobotId = cleaner.thingIDNumber; travelStationId = station.thingIDNumber; travelRoleId = clean.id;
            travelRobot = cleaner; travelStation = station; travelEngine = engine; travelStage = 1;
            travelGame = Current.Game; NativeReloadPending = true;
            Mark("RUNNING native takeoff");
        }

        internal static bool FinishNativeReloadCheck()
        {
            // Keep native takeoff, save and reload as separate main-thread
            // operations. The automation runtime can publish progress between
            // them; no simulation advances between flight and serialization.
            if (travelStage == 1)
            {
                var oldTile = map.Tile;
                if (!TileFinder.TryFindNewSiteTile(out var targetTile)) throw new InvalidOperationException("No gravship destination");
                Gravship airborne = GravshipUtility.GenerateGravship(travelEngine);
                GravshipUtility.TravelTo(airborne, oldTile, targetTile);
                Check(airborne.ContainsThing(travelStation) && travelStation.MapHeld == null && travelStation.ParentHolder == null,
                    "N1 fixture uses native airborne gravship placement table without holder");
                travelStage = 2; Mark("RUNNING native airborne save"); return false;
            }
            if (travelStage == 2)
            {
                GameDataSaveLoader.SaveGame("MechanicalReviewAirborne");
                XDocument xml = XDocument.Load(GenFilePaths.FilePathForSavedGame("MechanicalReviewAirborne"));
                XElement savedStore = xml.Descendants("li").First(e => (string)e.Attribute("Class") == "WorkRoles.RoleStore");
                Check(xml.Descendants("id").Any(e => e.Value == travelRobot.ThingID), "N1 native save serializes docked robot inside airborne station");
                Check(savedStore.Element("pawnSets").Element("keys").Elements("li").Any(e => e.Value == travelRobot.GetUniqueLoadID()),
                    "N1 native airborne save preserves robot role key");
                travelStage = 3; Mark("RUNNING native airborne reload"); return false;
            }
            if (travelStage == 3)
            {
                travelStage = 4;
                GameDataSaveLoader.LoadGame("MechanicalReviewAirborne");
                return false;
            }
            if (Current.Game == travelGame) return false;
            Gravship ship = Find.CurrentGravship;
            Check(ship != null, "N1 full reload restores airborne native gravship");
            Thing station = ship.Things.First(t => t.thingIDNumber == travelStationId);
            Pawn cleaner = ((IEnumerable)Field(station, "container")).Cast<Pawn>().Single(p => p.thingIDNumber == travelRobotId);
            Check(RoleStore.Current.pawnSets.TryGetValue(cleaner, out PawnRoleSet set)
                && set.assignments.Any(a => a.roleId == travelRoleId), "N1 full airborne save/reload retains docked Cleaner assignment");
            Check((bool)AccessTools.Method(Wr("MiscRobotSupport"), "HasPersistenceAnchor").Invoke(null, new object[] { cleaner }),
                "N1 reloaded station registry recognizes native airborne persistence");
            NativeReloadPending = false; travelGame = null; map = Find.CurrentMap;
            travelRobot = null; travelStation = null; travelEngine = null;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            return true;
        }
    }
}
