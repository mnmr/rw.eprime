using System.Collections.Generic;
using EPrimeReadouts.Core;

namespace EPrimeReadouts
{
    /// Curated first-run groups covering all vanilla + official-DLC counted
    /// resources. Slot tokens may be plain defNames or "@CategoryDefName" pool
    /// tokens (pools expand dynamically at display time). DefNames missing from
    /// the current mod list are skipped at seed time; pool tokens are skipped
    /// when their category contains zero counted defs. Groups that end up empty
    /// are not created. Deterministic, so MP clients seed identically.
    /// All seeded groups have DefaultEnabled = true (the ReadoutGroup default),
    /// so the readout mirrors vanilla out of the box. Future pre-made "extra"
    /// groups that should be opt-in can pass DefaultEnabled = false after creation.
    public static class DefaultGroups
    {
        // Curated in-game (extracted from a live save 2026-07-21); '~' entries
        // are hidden while their count is zero.
        private static readonly (string name, string[][] tiers)[] Seeds =
        {
            ("Food", new[]
            {
                new[] { "MealSimple", "MealFine", "MealLavish", "~BabyFood" },
                new[] { "~MealNutrientPaste", "MealSurvivalPack", "Kibble" },
                new[] { "~Hay", "~Pemmican", "~HemogenPack", "~Chocolate" },
            }),
            ("Raw", new[]
            {
                new[] { "@MeatRaw", "@PlantFoodRaw", "~@EggsUnfertilized", "~Milk" },
                new[] { "~InsectJelly", "~@EggsFertilized" },
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
                new[] { "Cloth", "@Leathers", "@Wools" },
                new[] { "~DevilstrandCloth", "~Hyperweave", "~Synthread" },
            }),
            ("Materials", new[]
            {
                new[] { "Steel", "WoodLog", "ComponentIndustrial" },
                new[] { "Plasteel", "ComponentSpacer", "Uranium", "Chemfuel" },
                new[] { "@StoneBlocks", "Bioferrite", "Obsidian" },
            }),
            ("Wealth", new[]
            {
                new[] { "Silver", "Gold", "~Jade" },
            }),
        };

        public static void Seed(ReadoutStore store)
        {
            foreach (var (name, tiers) in Seeds)
            {
                var layout = new List<List<string>>();
                foreach (var tier in tiers)
                {
                    var kept = new List<string>();
                    foreach (var token in tier)
                    {
                        bool valid = SlotToken.IsPool(token)
                            ? GameResourceCatalog.Instance.CountedDefsIn(SlotToken.MemberName(token)).Count > 0
                            : GameResourceCatalog.Instance.Exists(SlotToken.MemberName(token));
                        if (valid) kept.Add(token);
                    }
                    if (kept.Count > 0) layout.Add(kept);
                }
                if (layout.Count == 0) continue;
                var group = store.Model.CreateGroup(store.TakeGroupId(), name);
                store.Model.SetTiers(group.Id, layout);
            }
        }
    }
}
