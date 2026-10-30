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
    public static class ClipboardChecks
    {
        public static void Run(Map map, QualityJobsStore store)
        {
            Bill previousClipboard = BillUtility.Clipboard;
            Building_WorkTable bench = null;
            try
            {
                ThingDef benchDef = DefDatabase<ThingDef>.GetNamed("TableSculpting");
                RecipeDef recipe = DefDatabase<RecipeDef>.GetNamed("Make_SculptureSmall");
                Require(ManagedRecipes.IsManagedRecipe(recipe), "Sculpture fixture is not managed");
                bench = (Building_WorkTable)ThingMaker.MakeThing(benchDef,
                    benchDef.MadeFromStuff ? GenStuff.DefaultStuffFor(benchDef) : null);
                bench.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(bench, Probe.EmptySpot(benchDef, 38), map, Rot4.North);
                var source = NewBill(recipe, bench);
                Expected configured = Configure(source, store);
                Expected defaults = Expected.Defaults(store);

                Check("QJ004-OVERRIDES", () =>
                {
                    BillUtility.Clipboard = source.Clone();
                    Bill_ProductionWithUft pasted = Paste(bench);
                    Require(configured.Matches(store, pasted), "Clipboard lost one of the six overrides");
                    return "Native copy/paste retained " + Describe(store, pasted)
                        + "; Ideology=" + ModsConfig.IdeologyActive;
                });

                Check("QJ004-DETACHED", () =>
                {
                    // An unstaged clipboard is normal after copying while QJ is
                    // disabled. Neither native clone has a BillStack; both must
                    // remain safe, then use destination-store defaults on paste.
                    Bill detached = recipe.MakeNewBill();
                    BillUtility.Clipboard = detached.Clone();
                    Bill_ProductionWithUft pasted = Paste(bench);
                    Require(defaults.Matches(store, pasted), "Unstaged paste did not use current defaults");
                    return "Unstaged native detached double clone pasted with " + Describe(store, pasted);
                });

                Check("QJ004-DELETED-SOURCE", () =>
                {
                    Bill_ProductionWithUft disposableSource = NewBill(recipe, bench);
                    Expected expected = Configure(disposableSource, store);
                    BillUtility.Clipboard = disposableSource.Clone();
                    bench.BillStack.Delete(disposableSource);
                    Bill_ProductionWithUft pasted = Paste(bench);
                    Require(expected.Matches(store, pasted), "Deleting the source lost the copied settings");
                    return "Native source deletion after copy preserved detached snapshot: " + Describe(store, pasted);
                });

                Check("QJ004-OWNER-RESET", () =>
                {
                    BillUtility.Clipboard = source.Clone();
                    // Exercise the exact teardown used by Game.Dispose without
                    // replacing the active store or disturbing other fixtures.
                    ResetTransfers();
                    Bill_ProductionWithUft pasted = Paste(bench);
                    Require(defaults.Matches(store, pasted), "Expired clipboard settings leaked into a new owner");
                    return "After transfer lifecycle reset, native paste uses current defaults: " + Describe(store, pasted);
                });

                Check("QJ004-SCRIBE", () =>
                {
                    string detachedPath = Path.Combine(GenFilePaths.SaveDataFolderPath,
                        "QualityJobsClipboard-Detached.xml");
                    Bill_ProductionWithUft detached = (Bill_ProductionWithUft)source.Clone();
                    SaveBill(detachedPath, detached);
                    ResetTransfers();
                    Bill_ProductionWithUft loaded = LoadBill(detachedPath);
                    Require(loaded != null, "Detached Scribe bill was not loaded");
                    loaded.InitializeAfterClone();
                    bench.BillStack.AddBill(loaded);
                    Require(configured.Matches(store, loaded), "Detached Scribe roundtrip lost the six overrides");

                    string attachedPath = Path.Combine(GenFilePaths.SaveDataFolderPath,
                        "QualityJobsClipboard-Attached.xml");
                    SaveBill(attachedPath, loaded);
                    var document = new XmlDocument();
                    document.Load(attachedPath);
                    Require(document.SelectSingleNode("/QualityJobsClipboard/bill/qualityJobs_clonedConfig") == null,
                        "Consumed clipboard marker was written into an ordinary attached-bill save");
                    return "Detached Scribe roundtrip retained " + Describe(store, loaded)
                        + "; attached save omits consumed transfer metadata";
                });
            }
            finally
            {
                if (Scribe.mode != LoadSaveMode.Inactive) Scribe.ForceStop();
                BillUtility.Clipboard = previousClipboard;
                if (bench != null && !bench.Destroyed)
                {
                    for (int i = bench.BillStack.Count - 1; i >= 0; i--)
                        bench.BillStack.Delete(bench.BillStack[i]);
                    bench.Destroy();
                }
            }
        }

        private static Bill_ProductionWithUft NewBill(RecipeDef recipe, Building_WorkTable bench)
        {
            var bill = (Bill_ProductionWithUft)recipe.MakeNewBill();
            bench.BillStack.AddBill(bill);
            return bill;
        }

        private static Bill_ProductionWithUft Paste(Building_WorkTable bench)
        {
            var pasted = (Bill_ProductionWithUft)BillUtility.Clipboard.Clone();
            pasted.InitializeAfterClone();
            bench.BillStack.AddBill(pasted);
            return pasted;
        }

        private static Expected Configure(Bill bill, QualityJobsStore store)
        {
            var expected = new Expected
            {
                Managed = !store.manageNewBillsDefault,
                AutoBest = !store.autoBestDefault,
                Inspired = !store.requireInspiredDefault,
                Specialist = ModsConfig.IdeologyActive && !store.requireSpecialistDefault,
                MinSkill = store.minSkillDefault == 20 ? 19 : 20,
                Quality = store.targetQualityDefault == 6 ? 5 : 6
            };
            string id = bill.GetUniqueLoadID();
            Commands.SetBillManaged(id, expected.Managed);
            Commands.SetBillAutoBest(id, expected.AutoBest);
            Commands.SetBillRequireInspired(id, expected.Inspired);
            Commands.SetBillRequireSpecialist(id, expected.Specialist);
            Commands.SetBillMinSkill(id, expected.MinSkill);
            Commands.SetBillTargetQuality(id, expected.Quality);
            Require(expected.Matches(store, bill), "Could not configure source fixture");
            return expected;
        }

        private struct Expected
        {
            internal bool Managed, AutoBest, Inspired, Specialist;
            internal int MinSkill, Quality;

            internal static Expected Defaults(QualityJobsStore store) => new Expected
            {
                Managed = store.manageNewBillsDefault,
                AutoBest = store.autoBestDefault,
                Inspired = store.requireInspiredDefault,
                Specialist = ModsConfig.IdeologyActive && store.requireSpecialistDefault,
                MinSkill = store.minSkillDefault,
                Quality = store.targetQualityDefault
            };

            internal bool Matches(QualityJobsStore store, Bill bill)
            {
                BillConfig actual = store.ConfigFor(bill);
                return actual.Managed == Managed && actual.AutoBest == AutoBest
                    && actual.Condition.RequireInspired == Inspired
                    && actual.Condition.RequireSpecialist == Specialist
                    && actual.Condition.MinSkill == MinSkill
                    && store.TargetQualityFor(bill) == Quality;
            }
        }

        private static void ResetTransfers()
        {
            Type transfer = typeof(QualityJobsStore).Assembly.GetType("QualityJobs.BillConfigTransfer", true);
            AccessTools.Method(transfer, "Reset").Invoke(null, null);
        }

        private static void SaveBill(string path, Bill_ProductionWithUft bill)
        {
            Require(Scribe.mode == LoadSaveMode.Inactive, "Scribe already active before clipboard save");
            try
            {
                Scribe.saver.InitSaving(path, "QualityJobsClipboard");
                Scribe_Deep.Look(ref bill, "bill");
                Scribe.saver.FinalizeSaving();
            }
            finally
            {
                if (Scribe.mode != LoadSaveMode.Inactive) Scribe.ForceStop();
            }
        }

        private static Bill_ProductionWithUft LoadBill(string path)
        {
            Require(Scribe.mode == LoadSaveMode.Inactive, "Scribe already active before clipboard load");
            Bill_ProductionWithUft bill = null;
            try
            {
                Scribe.loader.InitLoading(path);
                Scribe_Deep.Look(ref bill, "bill");
                Scribe.loader.FinalizeLoading();
                return bill;
            }
            finally
            {
                if (Scribe.mode != LoadSaveMode.Inactive) Scribe.ForceStop();
            }
        }

        private static void Check(string id, Func<string> check)
        {
            try { Probe.Record(id, true, check()); }
            catch (Exception ex) { Probe.Record(id, false, ex.ToString()); }
        }

        private static string Describe(QualityJobsStore store, Bill bill)
        {
            BillConfig config = store.ConfigFor(bill);
            return "managed=" + config.Managed + ", auto=" + config.AutoBest
                + ", inspired=" + config.Condition.RequireInspired
                + ", specialist=" + config.Condition.RequireSpecialist
                + ", skill=" + config.Condition.MinSkill
                + ", quality=" + store.TargetQualityFor(bill);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
