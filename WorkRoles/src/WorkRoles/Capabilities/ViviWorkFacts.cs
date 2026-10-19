using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Core.Recs;

namespace WorkRoles.Capabilities
{
    /// Optional adapter for the audited Vivi gathering contract. It reads the
    /// same final pawn stats as GatherWorker.PawnCanDoBill, never a bill or job.
    internal static class ViviWorkFacts
    {
        private sealed class Recipe
        {
            internal string Name = "";
            internal int Pair;
        }

        // Owner: definition session. Key: explicit definition/lifecycle reset.
        // Value: private, immutable-after-binding recipe and stat references.
        // Dependencies: audited Vivi module, exact giver/worker classes, fixed
        // bench recipe sets and supported stat definitions. Refresh: first
        // capture after Reset. Equality: retained until Reset. Teardown: Reset
        // via PawnWorkFacts.Clear (world, definition and language invalidation).
        private static bool initialized;
        private static WorkGiverDef? giver;
        private static Recipe[] recipes = Array.Empty<Recipe>();
        private static StatDef[] stats = Array.Empty<StatDef>();
        private static bool complete;

        internal static int Observe(Pawn pawn)
        {
            EnsureBound();
            int available = 0;
            for (int pair = 0; pair < stats.Length / 2; pair++)
                if (stats[pair * 2] != null && pawn.GetStatValue(stats[pair * 2]) > 0f
                    && pawn.GetStatValue(stats[pair * 2 + 1]) > 0f) available |= 1 << pair;
            return available;
        }

        internal static int Collect(Pawn pawn, List<WorkCapabilityFact> blocks)
        {
            int available = Observe(pawn);
            if (giver == null) return available;
            bool allBlocked = complete && recipes.Length > 0;
            for (int i = 0; i < recipes.Length; i++)
            {
                Recipe recipe = recipes[i];
                if ((available & (1 << recipe.Pair)) != 0) { allBlocked = false; continue; }
                blocks.Add(Block(recipe.Name));
            }
            if (allBlocked) blocks.Add(Block(null));
            return available;
        }

        private static WorkCapabilityFact Block(string? recipe) => new WorkCapabilityFact(
            giver!.defName, recipe, "gguake.race.vivi", "WR_WorkBlockedViviGathering");

        private static void EnsureBound()
        {
            if (initialized) return;
            initialized = true;
            Type? worker = AccessTools.TypeByName("VVRace.GatherWorker");
            if (worker == null) return;
            // gguake/RWMod-Vivi e9f438ff: distributed 1.6 and 1.7 assemblies.
            string module = worker.Module.ModuleVersionId.ToString();
            if (module != "aba230ff-ccb1-4848-afb3-527bd0dfb106"
                && module != "c2d409c4-383f-487e-a682-12eae0ade489")
            {
                Log.Warning("[WorkRoles] Vivi gathering capability rules are unknown for module " + module + "; recommendations retain existing behavior.");
                return;
            }
            var rule = AccessTools.Method(worker, "PawnCanDoBill");
            if (Harmony.GetPatchInfo(rule) != null) return;
            WorkGiverDef? def = DefDatabase<WorkGiverDef>.GetNamedSilentFail("VV_DoBillsGathering");
            if (def == null || def.giverClass != AccessTools.TypeByName("VVRace.WorkGiver_GatheringBill")
                || def.fixedBillGiverDefs == null) return;
            stats = new StatDef[4];
            BindPair(0, "VV_HoneyGatherSpeed", "VV_HoneyGatherYield");
            BindPair(1, "VV_PlantGatherSpeed", "VV_PlantGatherYield");
            Type? recipeType = AccessTools.TypeByName("VVRace.RecipeDef_Gathering");
            var workerField = AccessTools.Field(recipeType, "gatherWorker");
            var bindings = new List<Recipe>();
            var seen = new HashSet<RecipeDef>();
            complete = !def.billGiversAllAnimals && !def.billGiversAllAnimalsCorpses
                && !def.billGiversAllHumanlikes && !def.billGiversAllHumanlikesCorpses
                && !def.billGiversAllMechanoids && !def.billGiversAllMechanoidsCorpses;
            foreach (ThingDef bench in def.fixedBillGiverDefs)
                foreach (RecipeDef recipe in bench.AllRecipes)
                {
                    if ((recipe.requiredGiverWorkType != null && recipe.requiredGiverWorkType != def.workType) || !seen.Add(recipe)) continue;
                    object? instance = recipe.GetType() == recipeType ? workerField.GetValue(recipe) : null;
                    Type? type = instance?.GetType();
                    // Accept only the three audited workers; a third-party
                    // override or unfamiliar recipe must not imply denial.
                    if (type == null || type.Assembly != worker.Assembly
                        || (type.FullName != "VVRace.GatherWorker_Honey" && type.FullName != "VVRace.GatherWorker_Pollen"
                            && type.FullName != "VVRace.GatherWorker_Plant")
                        || AccessTools.Method(type, "PawnCanDoBill").DeclaringType != worker)
                    { complete = false; continue; }
                    int pair = recipe.workSpeedStat == stats[0] && recipe.efficiencyStat == stats[1] ? 0
                        : recipe.workSpeedStat == stats[2] && recipe.efficiencyStat == stats[3] ? 1 : -1;
                    if (pair < 0 || stats[pair * 2] == null) { complete = false; continue; }
                    bindings.Add(new Recipe { Name = recipe.defName, Pair = pair });
                }
            recipes = bindings.ToArray();
            giver = def;
        }

        private static void BindPair(int pair, string speedName, string yieldName)
        {
            StatDef? speed = DefDatabase<StatDef>.GetNamedSilentFail(speedName);
            StatDef? yield = DefDatabase<StatDef>.GetNamedSilentFail(yieldName);
            if (!Supported(speed) || !Supported(yield)) return;
            stats[pair * 2] = speed!; stats[pair * 2 + 1] = yield!;
        }

        private static bool Supported(StatDef? stat)
        {
            // No periodic observation is needed for stock Vivi: skill and
            // global-speed factors stay positive. Health, gear, genes/traits
            // and ideology already have explicit work-evidence notifications.
            // Custom dynamic stat parts/workers have no such contract.
            if (stat == null || stat.workerClass != typeof(StatWorker) || !stat.neverDisabled
                || !stat.parts.NullOrEmpty() || !stat.skillNeedOffsets.NullOrEmpty()
                || stat.postProcessCurve != null || !stat.postProcessStatFactors.NullOrEmpty()
                || stat.roundValue || stat.roundToFiveOver != float.MaxValue) return false;
            if (stat.statFactors != null)
                foreach (StatDef factor in stat.statFactors)
                    if (factor != StatDefOf.WorkSpeedGlobal || factor.minValue <= 0f) return false;
            if (stat.skillNeedFactors != null)
                foreach (SkillNeed need in stat.skillNeedFactors)
                {
                    if (need.GetType() == typeof(SkillNeed_BaseBonus))
                    {
                        float start = (float)AccessTools.Field(typeof(SkillNeed_BaseBonus), "baseValue").GetValue(need);
                        float bonus = (float)AccessTools.Field(typeof(SkillNeed_BaseBonus), "bonusPerLevel").GetValue(need);
                        if (start <= 0f || start + 20f * bonus <= 0f) return false;
                    }
                    else if (need.GetType() == typeof(SkillNeed_Direct))
                    {
                        foreach (float value in ((SkillNeed_Direct)need).valuesPerLevel) if (value <= 0f) return false;
                    }
                    else return false;
                }
            return true;
        }

        internal static void Reset()
        {
            initialized = false; giver = null; complete = false;
            recipes = Array.Empty<Recipe>(); stats = Array.Empty<StatDef>();
        }
    }
}
