using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Core.Recs;

namespace WorkRoles.Capabilities
{
    /// Optional, version-audited HAR rules. Calls the framework's pure rule
    /// functions, never a scanner, bill, job search or a guessed race mapping.
    internal static class HumanoidAlienWorkFacts
    {
        private delegate void GiverRule(WorkGiver giver, Pawn pawn, ref bool allowed);
        private sealed class Binding
        {
            internal WorkGiverDef Def = null!;
            internal bool CheckGiver;
            internal RecipeDef[] Recipes = Array.Empty<RecipeDef>();
            internal bool CompleteRecipes;
        }

        // Owner: definition session. Key: loaded HAR module + installed defs and
        // actual Harmony patch targets. Values: fixed delegates and immutable
        // giver/recipe mappings. Refresh: first capture after definition reset.
        // Equality: reuse until reset; no per-pawn/reflection cache. Teardown:
        // PawnWorkFacts.Clear on definition reload and world release.
        private static bool initialized;
        private static GiverRule? giverRule;
        private static Func<RecipeDef, ThingDef, bool>? recipeRule;
        private static Binding[] bindings = Array.Empty<Binding>();
        private const string Package = "erdelf.HumanoidAlienRaces";

        internal static void Collect(Pawn pawn, List<WorkCapabilityFact> facts)
        {
            Initialize();
            for (int i = 0; i < bindings.Length; i++)
            {
                Binding binding = bindings[i];
                bool allowed = true;
                if (binding.CheckGiver) giverRule!(binding.Def.Worker, pawn, ref allowed);
                if (!allowed)
                    facts.Add(new WorkCapabilityFact(binding.Def.defName, null, Package, "WR_WorkBlockedRace"));
                int blocked = 0;
                for (int r = 0; r < binding.Recipes.Length; r++)
                    if (!recipeRule!(binding.Recipes[r], pawn.def))
                    {
                        blocked++;
                        facts.Add(new WorkCapabilityFact(binding.Def.defName, binding.Recipes[r].defName, Package, "WR_WorkBlockedRecipe"));
                    }
                if (binding.CompleteRecipes && blocked > 0 && blocked == binding.Recipes.Length)
                    facts.Add(new WorkCapabilityFact(binding.Def.defName, null, Package, "WR_WorkBlockedRecipe"));
            }
        }

        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Type? patches = AccessTools.TypeByName("AlienRace.HarmonyPatches");
            if (patches == null) return;
            // Audited installed 1.6 and 1.7 binaries. Unknown builds are not
            // mechanically certified by a matching method name/signature.
            string module = patches.Module.ModuleVersionId.ToString();
            if (module != "71176dfd-d54d-4f06-985e-6706cfd7af4e"
                && module != "d73c75f3-a94e-45ae-ac61-1266bbd0f6a7")
            {
                Log.Warning("[WorkRoles] HAR work capability rules are unknown for module " + module + "; recommendations retain existing behavior.");
                return;
            }
            Type? restrictions = AccessTools.TypeByName("AlienRace.RaceRestrictionSettings");
            MethodInfo giver = AccessTools.Method(patches, "GenericHasJobOnThingPostfix");
            MethodInfo job = AccessTools.Method(patches, "GenericJobOnThingPostfix");
            MethodInfo recipe = AccessTools.Method(restrictions, "CanDoRecipe", new[] { typeof(RecipeDef), typeof(ThingDef) });
            giverRule = (GiverRule)Delegate.CreateDelegate(typeof(GiverRule), giver);
            recipeRule = (Func<RecipeDef, ThingDef, bool>)Delegate.CreateDelegate(typeof(Func<RecipeDef, ThingDef, bool>), recipe);
            bool recipePatched = HasPostfix(AccessTools.Method(typeof(Bill), nameof(Bill.PawnAllowedToStartAnew)),
                AccessTools.Method(patches, "PawnAllowedToStartAnewPostfix"));
            var result = new List<Binding>();
            foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (def.giverClass == null) continue;
                bool check = HasPostfix(AccessTools.Method(def.giverClass, "HasJobOnThing"), giver)
                    || HasPostfix(AccessTools.Method(def.giverClass, "JobOnThing"), job);
                var recipes = new List<RecipeDef>();
                bool standardBill = def.giverClass == typeof(WorkGiver_DoBill);
                if (recipePatched && typeof(WorkGiver_DoBill).IsAssignableFrom(def.giverClass) && def.fixedBillGiverDefs != null)
                    foreach (ThingDef bench in def.fixedBillGiverDefs)
                        foreach (RecipeDef content in bench.AllRecipes)
                            if ((content.requiredGiverWorkType == null || content.requiredGiverWorkType == def.workType) && !recipes.Contains(content))
                                recipes.Add(content);
                if (check || recipes.Count > 0)
                    result.Add(new Binding { Def = def, CheckGiver = check, Recipes = recipes.ToArray(),
                        CompleteRecipes = standardBill && !def.billGiversAllAnimals && !def.billGiversAllAnimalsCorpses
                            && !def.billGiversAllHumanlikes && !def.billGiversAllHumanlikesCorpses
                            && !def.billGiversAllMechanoids && !def.billGiversAllMechanoidsCorpses });
            }
            bindings = result.ToArray();
        }

        private static bool HasPostfix(MethodInfo? target, MethodInfo patch)
        {
            if (target == null) return false;
            HarmonyLib.Patches? info = Harmony.GetPatchInfo(target);
            if (info == null) return false;
            foreach (Patch item in info.Postfixes) if (item.PatchMethod == patch) return true;
            return false;
        }

        internal static void Reset()
        {
            initialized = false;
            giverRule = null;
            recipeRule = null;
            bindings = Array.Empty<Binding>();
        }
    }
}
