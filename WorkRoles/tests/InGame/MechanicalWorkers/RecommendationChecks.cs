using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles;
using WorkRoles.UI;

namespace WorkRolesMechanicalVerification
{
    public static partial class Probe
    {
        private static int mechanicalCaptures;
        private static void CountMechanicalCapture() => mechanicalCaptures++;

        private static void VerifyRecommendationBoundaries(ColonistsTabView view)
        {
            Type stateType = AccessTools.TypeByName("WorkRoles.UI.ColonistRecommendationState");
            object state = Activator.CreateInstance(stateType, true);
            var plansMethod = AccessTools.GetDeclaredMethods(stateType).Single(m => m.Name == "Plans" && m.GetParameters().Length == 3);
            object stamp = Activator.CreateInstance(plansMethod.GetParameters()[1].ParameterType);
            object external = AccessTools.Field(typeof(ColonistsTabView), "externalSnapshotProvider").GetValue(view);
            Func<Pawn, IList> plansFor = pawn => (IList)plansMethod.Invoke(state, new[] { pawn, stamp, external });
            Func<IList, Pawn[]> planPawns = list => list.Cast<object>().Select(p =>
                (Pawn)AccessTools.Property(p.GetType(), "Pawn").GetValue(p)).ToArray();
            Type mechanical = AccessTools.TypeByName("WorkRoles.MechanicalWorkers");
            int workerCount = ((IList)AccessTools.Method(mechanical, "OnMap").Invoke(null, new object[] { map })).Count;
            var observer = new Harmony("eprime.workroles.mechanicalprobe.projection");
            var capture = AccessTools.Method(mechanical, "Capture");
            observer.Patch(capture, prefix: new HarmonyMethod(typeof(Probe), nameof(CountMechanicalCapture)));
            bool wasIntegrated = RoleStore.Current.mechanicalRecommendationsEnabled;
            try
            {
                RoleCommands.SetMechanicalRecommendationsEnabled(true);
                mechanicalCaptures = 0;
                IList first = plansFor(workers[2]);
                ReviewCheck(mechanicalCaptures == workerCount,
                    "human coverage and robot targets share one capture per worker (actual " + mechanicalCaptures + ", workers " + workerCount + ")");
                mechanicalCaptures = 0;
                ReviewCheck(ReferenceEquals(first, plansFor(workers[2])) && mechanicalCaptures == 0,
                    "unchanged recommendation reads reuse targets without recapturing robots");
                AccessTools.Method(stateType, "InvalidateExternalFacts").Invoke(state, null);
                ReviewCheck(ReferenceEquals(first, plansFor(workers[2])),
                    "equal refreshed recommendation inputs preserve target identity");
            }
            finally
            {
                observer.Unpatch(capture, HarmonyPatchType.All, observer.Id);
                RoleCommands.SetMechanicalRecommendationsEnabled(wasIntegrated);
            }

            // Real native maps and spawned mechs, both created before either
            // read: switching the selected pawn changes no model revision.
            Pawn[] anchors = new Pawn[2];
            for (int i = 0; i < anchors.Length; i++)
            {
                var parent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                parent.SetFaction(Faction.OfPlayer);
                Check(TileFinder.TryFindNewSiteTile(out var tile), "map-switch fixture finds an unused tile");
                parent.Tile = tile;
                Find.WorldObjects.Add(parent);
                var generator = new MapGeneratorDef { defName = "MechanicalReview", genSteps = new List<GenStepDef>() };
                Map emptyMap = MapGenerator.GenerateMap(new IntVec3(25, 1, 25), parent, generator,
                    extraInitBeforeContentGen: m => { foreach (IntVec3 cell in m.AllCells) m.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil); });
                Pawn worker = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Cleansweeper"), Faction.OfPlayer);
                GenSpawn.Spawn(worker, emptyMap.Center, emptyMap);
                anchors[i] = worker;
            }
            Current.Game.CurrentMap = map;
            state = Activator.CreateInstance(stateType, true);
            Pawn[] firstMap = planPawns(plansFor(anchors[0]));
            Pawn[] secondMap = planPawns(plansFor(anchors[1]));
            ReviewCheck(firstMap.SequenceEqual(new[] { anchors[0] }) && secondMap.SequenceEqual(new[] { anchors[1] }),
                "switching robot-only maps gives preview and Fix targets only for the selected map");
            AccessTools.Method(stateType, "Reset").Invoke(state, null);
        }
    }
}
