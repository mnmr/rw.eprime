using System;
using System.Linq;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Patches;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace CompletedBuildingsVerification
{
    public static partial class Probe
    {
        private static Building_Bed batchBelow, batchSatisfied;
        private static int completionBeforeCancellation;
        private static int reloadTargetId, reloadBuilderId, reloadOwnerId;
        private static bool verifiedDialogCaches;

        private static void VerifyDialogCaches(Dialog_ConstructionPlanConfig __instance)
        {
            try { VerifyDialogCachesCore(__instance); }
            catch (Exception error)
            {
                stage = 99; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Mark("FAILED " + error); Log.Error("[CompletedBuildingsProbe] FAILED " + error);
            }
        }

        private static void VerifyDialogCachesCore(Dialog_ConstructionPlanConfig __instance)
        {
            if (verifiedDialogCaches || stage != 2 || Event.current.type != EventType.Repaint) return;
            verifiedDialogCaches = true;
            // Cache identity and refresh isolation cannot be established from a
            // screenshot. Exercise the real window in its actual GUI pass.
            Type type = typeof(Dialog_ConstructionPlanConfig);
            var ensure = AccessTools.Method(type, "EnsureLayout");
            var ensureBest = AccessTools.Method(type, "EnsureAutoBest");
            var labels = AccessTools.Field(type, "qualityLabels");
            var facts = AccessTools.Field(type, "autoBestFactsRevision");
            object initialLabels = labels.GetValue(__instance);
            Vector2 initialSize = __instance.InitialSize;
            bool oldPreference = Prefs.DisableTinyText;
            GameFont oldFont = Text.Font;
            try
            {
                ensureBest.Invoke(__instance, null);
                int factsRevision = (int)facts.GetValue(__instance);
                ensure.Invoke(__instance, null);
                Assert(ReferenceEquals(initialLabels, labels.GetValue(__instance)) && __instance.InitialSize == initialSize,
                    "unchanged window reuses measured geometry and translated labels");
                Prefs.DisableTinyText = !oldPreference;
                ensure.Invoke(__instance, null);
                Assert(ReferenceEquals(initialLabels, labels.GetValue(__instance))
                    && (int)facts.GetValue(__instance) == factsRevision,
                    "UI metric changes retain translations and pawn eligibility caches");
                Text.Font = GameFont.Small;
                float requiredHeight = Mathf.Ceil(Text.CalcHeight((string)AccessTools.Field(type, "rebuildNote")
                    .GetValue(__instance), __instance.InitialSize.x - 36f));
                Assert((float)AccessTools.Field(type, "rebuildNoteHeight").GetValue(__instance) >= requiredHeight,
                    "draft explanation geometry contains the measured wrapped text");
            }
            finally
            {
                Prefs.DisableTinyText = oldPreference;
                ensure.Invoke(__instance, null);
                Text.Font = oldFont;
            }
        }

        private static void BeginReload()
        {
            reloadTargetId = active.target.thingIDNumber;
            reloadBuilderId = builder.thingIDNumber; reloadOwnerId = owner.thingIDNumber;
            GameDataSaveLoader.SaveGame("CompletedBuildingsVerification");
            stage = 9;
            GameDataSaveLoader.LoadGame("CompletedBuildingsVerification");
        }

        private static void ResumeAfterReload()
        {
            Assert(Snapshot(batchSatisfied) == null, "game disposal releases the previous store's presentation cache");
            map = Find.CurrentMap; store = QualityJobsStore.Active;
            active = store.FindPlanById(reloadTargetId);
            builder = map.mapPawns.AllPawns.First(p => p.thingIDNumber == reloadBuilderId);
            owner = map.mapPawns.AllPawns.First(p => p.thingIDNumber == reloadOwnerId);
            Assert(active != null && active.restoreOnly && active.rebuildSettings != null,
                "full game save and reload retain cancelled replacement settings");
            Assert(active.target.Map == map && Snapshot(map.listerBuildings.allBuildingsColonist
                .First(b => b.thingIDNumber == batchSatisfied.thingIDNumber)) != null,
                "reloaded completed-building presentation belongs to the new map");
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            stage = 7;
        }

        private static void SelectBatch()
        {
            var others = map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>()
                .Where(b => b != bed && b.def == bed.def && store.FindPlan(b) == null)
                .Where(b => (bool)AccessTools.Method(typeof(QualityJobsStore).Assembly
                    .GetType("QualityJobs.CompletedBuilding"), "CanStart").Invoke(null, new object[] { b }))
                .Take(2).ToArray();
            Assert(others.Length == 2, "fixture has two additional eligible beds for batch activation");
            batchBelow = others[0]; batchSatisfied = others[1];
            batchBelow.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, null);
            batchSatisfied.TryGetComp<CompQuality>().SetQuality(QualityCategory.Good, null);
            Find.Selector.ClearSelection();
            Find.Selector.Select(bed); Find.Selector.Select(batchBelow); Find.Selector.Select(batchSatisfied);
            var ids = ConstructionGizmos.CollectSelectedIds(bed.thingIDNumber);
            Assert(ids.Count == 3 && ids[0] == bed.thingIDNumber,
                "completed-building selection captures all eligible buildings once");
        }

        private static void VerifyBatchActivation()
        {
            Assert(store.FindPlan(batchBelow) != null && Deconstruction(batchBelow) != null,
                "one Start rebuilding click also activates the second below-target building");
            Assert(store.FindPlan(batchSatisfied) == null && Deconstruction(batchSatisfied) == null,
                "the same batch leaves buildings already at target untouched");
            Commands.RemovePlan(batchBelow.thingIDNumber);
            Find.Selector.ClearSelection(); Find.Selector.Select(bed);
        }

        private static void VerifySettingsAndCacheLifecycle()
        {
            // Color activation is an independent compatibility invariant: the
            // fixture's vanilla beds use Building.PaintColorDef, not CompColorable.
            // Exercise the real component API on detached beds without modifying defs.
            var source = (Building_Bed)ThingMaker.MakeThing(bed.def, bed.Stuff);
            var target = (Building_Bed)ThingMaker.MakeThing(bed.def, bed.Stuff);
            var sourceColor = new CompColorable { parent = source };
            var targetColor = new CompColorable { parent = target };
            source.AllComps.Add(sourceColor); target.AllComps.Add(targetColor);
            sourceColor.SetColor(Color.red); sourceColor.SetColor(Color.white);
            sourceColor.DesiredColor = Color.blue;
            var capture = AccessTools.Method(typeof(BuildingRebuildSettings), "Capture");
            var apply = AccessTools.Method(typeof(BuildingRebuildSettings), "Apply");
            var settings = capture.Invoke(null, new object[] { source });
            apply.Invoke(settings, new object[] { target, true });
            Assert(targetColor.Active && targetColor.Color == Color.white && targetColor.DesiredColor == Color.blue,
                "active white paint and desired color survive an inactive replacement component");
            sourceColor.Disable();
            settings = capture.Invoke(null, new object[] { source });
            targetColor.SetColor(Color.red);
            apply.Invoke(settings, new object[] { target, true });
            Assert(!targetColor.Active && targetColor.DesiredColor == null,
                "an unpainted original clears a replacement component's generated color");

            object snapshot = Snapshot(bed);
            Assert(ReferenceEquals(snapshot, Snapshot(bed)), "unchanged completed-building reads retain identity");
            bed.SetFaction(null);
            Assert(Snapshot(bed) == null && !bed.GetGizmos().OfType<Command_QualityJob>().Any(),
                "losing player ownership removes completed-building presentation immediately");
            bed.SetFaction(Faction.OfPlayer);
            Assert(Snapshot(bed) != null, "claiming a completed building republishes its presentation");
            bed.CompAssignableToPawn.TryAssignPawn(owner);
            // Teardown must remove map references even if that map still has
            // live buildings; this ownership invariant has no player-visible UI.
            AccessTools.Method(typeof(QualityJobsStore), "ReleaseCompletedBuildings").Invoke(store, new object[] { map });
            Assert(Snapshot(bed) == null, "map cache teardown releases completed-building entries");
            AccessTools.Method(typeof(QualityJobsStore), "RebuildCompletedBuildings").Invoke(store, null);
            snapshot = Snapshot(bed);
            AccessTools.Method(typeof(QualityJobsStore), "RebuildCompletedBuildings").Invoke(store, null);
            Assert(ReferenceEquals(snapshot, Snapshot(bed)), "equal lifecycle rebuild preserves completed-building identity");
        }

        private static void SetupFailedRollCancellation()
        {
            bed.ForOwnerType = BedOwnerType.Colonist; bed.Medical = false;
            bed.CompAssignableToPawn.TryAssignPawn(owner);
            bed.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal, null);
            Commands.StartRebuild(bed.thingIDNumber, 0, false, false, (int)QualityCategory.Legendary, false);
            active = store.FindPlan(bed); originalId = bed.thingIDNumber;
            completionBeforeCancellation = completions;
        }

        private static void VerifyFailedRollCancellation()
        {
            var replacement = Replacement();
            Assert(replacement != null && replacement.TryGetComp<CompQuality>().Quality < QualityCategory.Legendary
                && Deconstruction(replacement) != null, "an actual below-target roll schedules another rebuild");
            Assert(owner.ownership.OwnedBed == null, "original ownership stays reserved until the quality loop ends");
            Commands.RemovePlan(replacement.thingIDNumber);
            Assert(store.FindPlan(replacement) == null && Deconstruction(replacement) == null
                && owner.ownership.OwnedBed == replacement,
                "cancelling after an unsuccessful roll keeps the building and restores its owner");
            stage = 99; Mark("SUCCESS"); Log.Message("[CompletedBuildingsProbe] SUCCESS");
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Find.Selector.ClearSelection(); Find.Selector.Select(replacement);
        }
    }
}
