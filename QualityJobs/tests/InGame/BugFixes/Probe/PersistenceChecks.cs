using System;
using System.IO;
using System.Xml;
using HarmonyLib;
using QualityJobs;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobsBugReview
{
    public static class PersistenceChecks
    {
        public static void Run(Map map, QualityJobsStore store)
        {
            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            RecipeDef recipe;
            ThingDef benchDef = FindMinifiableBench(out recipe);
            IntVec3 location = FindEmptyLocation(map, benchDef);
            var bench = (Building_WorkTable)ThingMaker.MakeThing(benchDef,
                benchDef.MadeFromStuff ? GenStuff.DefaultStuffFor(benchDef) : null);
            bench.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bench, location, map, Rot4.North);
            var bill = (Bill_ProductionWithUft)recipe.MakeNewBill();
            bench.BillStack.AddBill(bill);
            string id = bill.GetUniqueLoadID();
            int savedLoadId = (int)AccessTools.Field(typeof(Bill), "loadID").GetValue(bill);

            Commands.SetBillManaged(id, !store.manageNewBillsDefault);
            Commands.SetBillMinSkill(id, store.minSkillDefault == 20 ? 19 : 20);
            Commands.SetBillAutoBest(id, !store.autoBestDefault);
            Commands.SetBillRequireInspired(id, !store.requireInspiredDefault);
            Commands.SetBillRequireSpecialist(id, !store.requireSpecialistDefault);
            Commands.SetBillTargetQuality(id, store.targetQualityDefault == 6 ? 5 : 6);
            BillConfig before = store.ConfigFor(bill);
            int qualityBefore = store.TargetQualityFor(bill);
            string configured = Describe(store, bill);

            // Control: the same first-install lifecycle scan quarantines the
            // bill while its workbench is spawned. These stores are detached,
            // so this does not alter the loaded fixture's migration choices.
            QualityJobsStore spawnedMigration = NewMigrationStore();
            Quarantine(spawnedMigration);
            bool spawnedQuarantined = !spawnedMigration.ConfigFor(bill).Managed
                && spawnedMigration.pendingExistingBillMigrationIds.Contains(id);

            // This is the native uninstall transition used by JobDriver_Uninstall:
            // it despawns the original worktable and puts it inside a crate.
            MinifiedThing crate = bench.MakeMinified();
            GenSpawn.Spawn(crate, location, map);
            if (bench.Spawned || bench.Destroyed || bill.DeletedOrDereferenced
                || !bench.BillStack.Bills.Contains(bill))
                throw new InvalidOperationException("Native minification did not preserve the live bill.");

            QualityJobsStore minifiedMigration = NewMigrationStore();
            Quarantine(minifiedMigration);
            bool minifiedMissed = minifiedMigration.ConfigFor(bill).Managed
                && !minifiedMigration.pendingExistingBillMigrationIds.Contains(id);
            Probe.Record("QJ009", spawnedQuarantined && !minifiedMigration.ConfigFor(bill).Managed
                && minifiedMigration.pendingExistingBillMigrationIds.Contains(id),
                "First-install quarantine control: spawned=" + spawnedQuarantined
                + "; same live minified bill missed=" + minifiedMissed
                + "; bench=" + benchDef.defName + "; recipe=" + recipe.defName);

            if (!before.Equals(store.ConfigFor(bill))
                || qualityBefore != store.TargetQualityFor(bill))
                throw new InvalidOperationException("Config already changed before the save boundary.");

            const string saveName = "QualityJobsBugReview-MinifiedBill";
            GameDataSaveLoader.SaveGame(saveName);
            string savePath = GenFilePaths.FilePathForSavedGame(saveName);
            if (!File.Exists(savePath))
                throw new InvalidOperationException("Review save was not created.");
            var save = new XmlDocument();
            save.Load(savePath);
            XmlNode savedBill = save.SelectSingleNode("//billStack/bills/li[loadID='"
                + savedLoadId + "' and recipe='" + recipe.defName + "']");
            bool billSavedInsideCrate = false;
            for (XmlNode ancestor = savedBill; ancestor != null; ancestor = ancestor.ParentNode)
                if (ancestor.Name == "innerContainer") billSavedInsideCrate = true;
            if (!billSavedInsideCrate)
                throw new InvalidOperationException("Save does not contain the original bill inside the native crate.");

            string afterSave = Describe(store, bill);
            bool changedAtSave = !before.Equals(store.ConfigFor(bill))
                || qualityBefore != store.TargetQualityFor(bill);
            bool overridesRemoved = !store.billManaged.ContainsKey(id)
                && !store.billMinSkill.ContainsKey(id)
                && !store.billAutoBest.ContainsKey(id)
                && !store.billRequireInspired.ContainsKey(id)
                && !store.billRequireSpecialist.ContainsKey(id)
                && !store.billTargetQuality.ContainsKey(id);

            // Reinstall the same held building to prove the lost values are
            // observable on its original bill without requiring a reload.
            crate.GetDirectlyHeldThings().Remove(bench);
            crate.Destroy();
            GenSpawn.Spawn(bench, location, map, Rot4.North);
            bool originalBillSurvives = bench.BillStack.Bills.Contains(bill)
                && bill.GetUniqueLoadID() == id && !bill.DeletedOrDereferenced;
            bool changedAfterReinstall = !before.Equals(store.ConfigFor(bill))
                || qualityBefore != store.TargetQualityFor(bill);
            Probe.Record("QJ008", billSavedInsideCrate && originalBillSurvives
                && !changedAtSave && !overridesRemoved && !changedAfterReinstall,
                "Native " + benchDef.defName + "/" + recipe.defName
                + "; before=" + configured + "; after save=" + afterSave
                + "; after reinstall=" + Describe(store, bill)
                + "; original bill saved inside crate=" + billSavedInsideCrate
                + "; original bill survives reinstall=" + originalBillSurvives
                + "; all overrides removed=" + overridesRemoved
                + "; save=" + savePath);
            CheckCarriedRelease(map, bench, recipe);
        }

        private static void CheckCarriedRelease(Map map, Building_WorkTable bench, RecipeDef recipe)
        {
            Pawn replacement = Dispatcher.SelectFinisher(map, recipe, default, relaxed: true);
            if (replacement == null)
                throw new InvalidOperationException("No capable replacement crafter for disable-release check.");
            Pawn carrier = null;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (pawn.IsColonist && !pawn.Dead && !pawn.Downed
                    && pawn.carryTracker != null && pawn.carryTracker.CarriedThing == null)
                {
                    carrier = pawn;
                    break;
                }
            if (carrier == null)
                throw new InvalidOperationException("No free colonist carrier for disable-release check.");

            // The temporary deceased original never enters the map or its jobs.
            // SetDead is the game's own health-state transition; the release rule
            // needs the original creator to have died after the item was paused.
            Pawn original = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            original.health.SetDead();
            var bill = (Bill_ProductionWithUft)recipe.MakeNewBill();
            bench.BillStack.AddBill(bill);
            var uft = (UnfinishedThing)ThingMaker.MakeThing(recipe.unfinishedThingDef,
                recipe.unfinishedThingDef.MadeFromStuff
                    ? GenStuff.DefaultStuffFor(recipe.ProducedThingDef) : null);
            uft.Creator = original;
            uft.BoundBill = bill;
            uft.workLeft = 0.01f;
            uft.BoundBill = null;
            UftAuthor.Clear(uft);
            GenPlace.TryPlaceThing(uft, carrier.Position, map, ThingPlaceMode.Near);
            var entry = new WorkItemEntry
            {
                uft = uft,
                originalCreator = original,
                sourceBill = bill,
                state = WorkItemState.Paused
            };
            var isolatedStore = new QualityJobsStore(Current.Game);
            var release = AccessTools.Method(typeof(Dispatcher), "ReleaseToVanilla");
            release.Invoke(null, new object[] { isolatedStore, entry });
            bool groundRestored = uft.Creator != null && !uft.Creator.Dead;

            UftAuthor.Clear(uft);
            bool carried = carrier.carryTracker.TryStartCarry(uft, 1, reserve: false) == 1;
            if (!carried || uft.Map != null || uft.MapHeld != map)
                throw new InvalidOperationException("Native carrying did not establish Map=null / MapHeld=map.");
            release.Invoke(null, new object[] { isolatedStore, entry });
            bool carriedUnrestored = uft.Creator == null;
            carrier.carryTracker.TryDropCarriedThing(carrier.Position, ThingPlaceMode.Near, out Thing dropped);
            bool stillAuthorless = uft.Spawned && uft.Creator == null;
            Probe.Record("QJ010", groundRestored && !carriedUnrestored && !stillAuthorless
                && uft.Spawned && uft.Creator == replacement,
                "Disable/unmanage ReleaseToVanilla with deceased original author: ground control assigns living crafter="
                + groundRestored + "; same item inside native carrier remains authorless=" + carriedUnrestored
                + "; after drop still authorless=" + stillAuthorless
                + "; available replacement=" + replacement.LabelShort
                + "; recipe=" + recipe.defName);
            uft.Destroy();
            bench.BillStack.Delete(bill);
            original.Destroy();
        }

        private static QualityJobsStore NewMigrationStore()
        {
            return new QualityJobsStore(Current.Game)
            {
                manageNewBillsDefault = true,
                minSkillDefault = 15,
                targetQualityDefault = 0
            };
        }

        private static void Quarantine(QualityJobsStore store)
        {
            AccessTools.Method(typeof(QualityJobsStore), "QuarantineExistingBills")
                .Invoke(store, null);
        }

        private static string Describe(QualityJobsStore store, Bill bill)
        {
            BillConfig config = store.ConfigFor(bill);
            return "managed:" + config.Managed + ",skill:" + config.Condition.MinSkill
                + ",auto:" + config.AutoBest + ",inspired:" + config.Condition.RequireInspired
                + ",specialist:" + config.Condition.RequireSpecialist
                + ",quality:" + store.TargetQualityFor(bill);
        }

        private static ThingDef FindMinifiableBench(out RecipeDef recipe)
        {
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.Minifiable || def.thingClass == null
                    || !typeof(Building_WorkTable).IsAssignableFrom(def.thingClass)) continue;
                foreach (RecipeDef candidate in def.AllRecipes)
                    if (ManagedRecipes.IsManagedRecipe(candidate))
                    {
                        recipe = candidate;
                        return def;
                    }
            }
            throw new InvalidOperationException("No minifiable workbench with a Quality Jobs recipe.");
        }

        private static IntVec3 FindEmptyLocation(Map map, ThingDef def)
        {
            // Start near the map edge, keep every existing item/structure intact.
            for (int z = 8; z < map.Size.z - 8; z++)
                for (int x = 8; x < map.Size.x - 8; x++)
                {
                    IntVec3 position = new IntVec3(x, 0, z);
                    bool free = true;
                    foreach (IntVec3 cell in GenAdj.OccupiedRect(position, Rot4.North, def.size))
                        if (!cell.InBounds(map) || !cell.Standable(map)
                            || cell.GetThingList(map).Count != 0)
                        {
                            free = false;
                            break;
                        }
                    if (free) return position;
                }
            throw new InvalidOperationException("No empty workbench footprint found in the isolated map.");
        }
    }
}
