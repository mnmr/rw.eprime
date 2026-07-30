using System.Collections.Generic;
using System.IO;
using EPrimeReadouts.Core;
using Verse;

namespace EPrimeReadouts
{
    /// Default pools + groups seeded into new saves (and via Restore Defaults).
    /// The authoritative definitions ship as Seed/Readouts.xml in the mod
    /// folder — the same format Import/Export uses — curated in-game and
    /// exported. The programmatic tables below are only the fallback when the
    /// file is missing or unparsable. Both paths are deterministic functions
    /// of the shipped data + installed defs, so MP clients seed identically.
    public static class DefaultGroups
    {
        public static void Seed(ReadoutStore store)
        {
            if (TryReadSeedFile(out _, out var pools, out var groups))
            {
                store.Model.ApplyImport(pools, groups,
                    store.TakePoolId, store.TakeGroupId);
                return;
            }
            SeedFallback(store.Model, store.TakePoolId, store.TakeGroupId);
        }

        /// <summary>
        /// Reads or constructs the complete deterministic restore payload on the
        /// initiating client. The synced command receives this string and never
        /// performs local filesystem or installed-def discovery.
        /// </summary>
        public static string GetRestorePayload()
        {
            if (TryReadSeedFile(out string xml, out _, out _)) return xml;

            var model = new ReadoutModel();
            int nextPoolId = 1;
            int nextGroupId = 1;
            SeedFallback(model, () => nextPoolId++, () => nextGroupId++);
            return ReadoutsXml.Export(model.Pools, model.InDisplayOrder());
        }

        private static bool TryReadSeedFile(out string xml,
            out List<ResourcePool> pools, out List<ReadoutGroup> groups)
        {
            xml = null;
            pools = null;
            groups = null;
            try
            {
                string root = EPrimeReadoutsMod.ContentPack?.RootDir;
                if (string.IsNullOrEmpty(root)) return false;
                string path = Path.Combine(root, "Seed", "Readouts.xml");
                if (!File.Exists(path)) return false;
                xml = File.ReadAllText(path);
                if (!ReadoutsXml.TryImport(xml, out pools, out groups, out string error))
                {
                    Log.Warning("[EPrimeReadouts] Seed/Readouts.xml invalid (" + error
                        + "); falling back to built-in defaults.");
                    return false;
                }
                return true;
            }
            catch (System.Exception e)
            {
                Log.Warning("[EPrimeReadouts] Failed reading Seed/Readouts.xml ("
                    + e.Message + "); falling back to built-in defaults.");
                return false;
            }
        }

        // Fixed pool seed order → deterministic ids
        private static readonly (string name, string catRef)[] PoolSeeds =
        {
            ("Meats",           "@MeatRaw"),
            ("Plant food",      "@PlantFoodRaw"),
            ("Eggs",            "@EggsUnfertilized"),
            ("Fertilized eggs", "@EggsFertilized"),
            ("Leathers",        "@Leathers"),
            ("Wools",           "@Wools"),
            ("Stone blocks",    "@StoneBlocks"),
        };

        // Group seeds use pool name keys instead of @Category tokens.
        // '~' prefix on pool name key = hide-when-zero on the resulting #id token.
        private static readonly (string name, string[][] tiers)[] GroupSeeds =
        {
            ("Food", new[]
            {
                new[] { "MealSimple", "MealFine", "MealLavish", "~BabyFood" },
                new[] { "~MealNutrientPaste", "MealSurvivalPack", "Kibble" },
                new[] { "~Hay", "~Pemmican", "~HemogenPack", "~Chocolate" },
            }),
            ("Raw", new[]
            {
                new[] { "pool:Meats", "pool:Plant food", "~pool:Eggs", "~Milk" },
                new[] { "~InsectJelly", "~pool:Fertilized eggs" },
            }),
            ("Medicine", new[]
            {
                new[] { "MedicineHerbal", "MedicineIndustrial", "MedicineUltratech" },
                new[] { "Neutroamine", "Penoxycyline" },
            }),
            ("Drugs", new[]
            {
                new[] { "Beer", "SmokeleafJoint", "PsychiteTea" },
                new[] { "Yayo", "Flake", "WakeUp", "GoJuice" },
                new[] { "Luciferium", "Ambrosia" },
            }),
            ("Textiles", new[]
            {
                new[] { "Cloth", "pool:Leathers", "pool:Wools" },
                new[] { "~DevilstrandCloth", "~Hyperweave", "~Synthread" },
            }),
            ("Materials", new[]
            {
                new[] { "Steel", "WoodLog", "ComponentIndustrial" },
                new[] { "Plasteel", "ComponentSpacer", "Uranium", "Chemfuel" },
                new[] { "pool:Stone blocks", "Bioferrite", "Obsidian" },
            }),
            ("Wealth", new[]
            {
                new[] { "Silver", "Gold", "~Jade" },
            }),
        };

        private static void SeedFallback(ReadoutModel model,
            System.Func<int> takePoolId, System.Func<int> takeGroupId)
        {
            // 1. Seed pools first, recording name→id map
            var poolIdByName = new Dictionary<string, int>();
            foreach (var (name, catRef) in PoolSeeds)
            {
                string catDefName = catRef.Substring(1); // strip '@'
                // Only create pool when category resolves AND has ≥1 counted def
                var members = GameResourceCatalog.Instance.CountedDefsIn(catDefName);
                if (members.Count == 0) continue;
                int poolId = takePoolId();
                var pool = model.CreatePool(poolId, name);
                pool.Members.Add(catRef);
                poolIdByName[name] = poolId;
            }

            // 2. Seed groups, resolving pool: references → #id tokens
            foreach (var (name, tiers) in GroupSeeds)
            {
                var layout = new List<List<string>>();
                foreach (var tier in tiers)
                {
                    var kept = new List<string>();
                    foreach (var token in tier)
                    {
                        string resolved = ResolveGroupToken(token, poolIdByName);
                        if (resolved == null) continue;
                        kept.Add(resolved);
                    }
                    if (kept.Count > 0) layout.Add(kept);
                }
                if (layout.Count == 0) continue;
                var group = model.CreateGroup(takeGroupId(), name);
                model.SetTiers(group.Id, layout);
            }
        }

        /// Resolves a group-seed token string:
        ///   "pool:Name"  → "#id" (or "~#id" if prefixed with "~pool:")
        ///   "~pool:Name" → "~#id"
        ///   plain defName (or "~defName") → validated via catalog, returns as-is or null
        private static string ResolveGroupToken(string token, Dictionary<string, int> poolIdByName)
        {
            bool hide = token.StartsWith("~");
            string core = hide ? token.Substring(1) : token;

            if (core.StartsWith("pool:"))
            {
                string poolName = core.Substring(5); // strip "pool:"
                if (!poolIdByName.TryGetValue(poolName, out int poolId)) return null;
                string poolToken = SlotToken.PoolToken(poolId);
                return hide ? ("~" + poolToken) : poolToken;
            }

            // Plain defName: validate via catalog
            bool valid = GameResourceCatalog.Instance.Exists(core);
            return valid ? token : null;
        }
    }
}
