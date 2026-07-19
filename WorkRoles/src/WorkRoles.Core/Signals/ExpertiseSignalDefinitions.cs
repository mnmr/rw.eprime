using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public static class ExpertiseSignalDefinitions
    {
        private const string Vse = "vanillaexpanded.skills";
        private const string Alpha = "sarg.alphaskills";
        // Star glyph both mods draw for expertise; resolved from the loaded mod at runtime.
        private const string ExpertiseIcon = "UI/Passion_Expertise";

        public static readonly IReadOnlyList<SignalDefinition> All = Build();

        private static IReadOnlyList<SignalDefinition> Build()
        {
            var result = new List<SignalDefinition>
            {
                V("Precision", "Sharpshooting", "Shooting", S("ShootingAccuracyPawn", 1f)),
                V("CloseQuarters", "Aiming", "Shooting", S("AimingDelayFactor", -0.05f)),
                V("Gunner", "Reloading", "Shooting", S("RangedCooldownFactor", -0.025f)),
                V("VSE_Sniping", "Sniping", "Shooting", S("VEF_VerbRangeFactor", 0.01f)),
                V("Sharp", "Piercing", "Melee", S("VSE_ArmorPenetrationFactor", 0.05f)),
                V("Blunt", "Dueling", "Melee", S("MeleeCooldownFactor", -0.025f)),
                V("VSE_Striking", "Striking", "Melee", S("MeleeHitChance", 1f)),
                V("Tamer", "Taming", "Animals",
                    S("TameAnimalChance", 0.05f), S("VSE_AttackOnFailChanceFactor", -0.05f)),
                V("Rancher", "Ranching", "Animals",
                    S("AnimalGatherYield", 0.05f), S("AnimalGatherSpeed", 0.05f)),
                V("Hunter", "Hunting", "Animals", S("HuntingStealth", 0.05f)),
                V("Trainer", "Handling", "Animals", S("TrainAnimalChance", 0.05f)),
                V("QualityExpert", "Art Quality", "Artistic", S("VSE_ArtQuality", 0.05f)),
                V("QuantityExpert", "Art Quantity", "Artistic", S("VSE_ArtSpeed", 0.05f)),
                V("Flooring", "Flooring", "Construction", S("VSE_FloorSpeed", 0.05f)),
                V("Repairman", "Repairing", "Construction",
                    S("FixBrokenDownBuildingSuccessChance", 0.05f), S("VSE_RepairSpeed", 0.05f)),
                V("Architect", "Architecture", "Construction",
                    S("ConstructSuccessChance", 0.05f), S("VSE_ConstructQuality", 0.05f)),
                V("Foreman", "Building", "Construction", S("ConstructionSpeed", 0.05f)),
                V("VSE_Smoothing", "Smoothing", "Construction", S("SmoothingSpeed", 0.05f)),
                V("Butcher", "Butchering", "Cooking",
                    S("ButcheryFleshSpeed", 0.05f), S("ButcheryFleshEfficiency", 0.05f)),
                V("DrugChef", "Drug Cooking", "Cooking", S("DrugCookingSpeed", 0.05f)),
                V("TopChef", "Food Hygiene", "Cooking", S("FoodPoisonChance", -0.05f)),
                V("IndustrialChef", "Feeding", "Cooking", S("CookSpeed", 0.05f)),
                V("MechanoidExpert", "Disassembler", "Crafting",
                    S("ButcheryMechanoidSpeed", 0.05f), S("ButcheryMechanoidEfficiency", 0.05f)),
                V("Tailor", "Tailoring", "Crafting", S("VSE_TailoringSpeed", 0.05f)),
                V("Weaponsmith", "Weaponsmithing", "Crafting", S("VSE_WeaponCreationSpeed", 0.05f)),
                V("IndustrialProcessExpert", "Fabricating", "Crafting", S("VSE_ComponentCraftingSpeed", 0.05f)),
                V("Surgeon", "Surgery", "Medicine", S("MedicalSurgerySuccessChance", 0.05f)),
                V("BattlefieldMedic", "First Aid", "Medicine", S("MedicalTendSpeed", 0.05f)),
                V("InfectiousDiseaseExpert", "Nursing", "Medicine", S("MedicalTendQuality", 0.05f)),
                V("VSE_Operating", "Operating", "Medicine", S("MedicalOperationSpeed", 0.05f)),
                V("Driller", "Drilling", "Mining", S("DeepDrillingSpeed", 0.05f)),
                V("OreExpert", "Prospecting", "Mining", S("MiningYield", 0.05f)),
                V("Tunneller", "Tunnelling", "Mining", S("MiningSpeed", 0.05f)),
                V("Geologist", "Geology", "Mining", S("VSE_RockChunkChance", 0.05f)),
                V("Pharmacologist", "Synthesizing", "Intellectual", S("DrugSynthesisSpeed", 0.05f)),
                Vd("Hacker", "Hacking", "Intellectual", "Ludeon.RimWorld.Ideology", S("HackingSpeed", 0.05f)),
                V("Researcher", "Researching", "Intellectual", S("ResearchSpeed", 0.05f)),
                Vd("VSE_Writing", "Writing", "Intellectual", "VanillaExpanded.VBooksE",
                    S("VBE_WritingSpeed", 0.05f), S("ReadingSpeed", 0.05f)),
                Vd("VSE_DarkStudy", "Dark study", "Intellectual", "Ludeon.RimWorld.Anomaly",
                    S("EntityStudyRate", 0.03f), S("StudyEfficiency", 0.05f)),
                V("Forager", "Foraging", "Plants", S("ForagedNutritionPerDay", 0.05f)),
                Vd("Treespeaker", "Pruning", "Plants", "Ludeon.RimWorld.Ideology", S("PruningSpeed", 0.05f)),
                V("HarvesterCareful", "Harvesting", "Plants", S("PlantHarvestYield", 0.05f)),
                V("GreenThumb", "Sowing", "Plants", S("PlantWorkSpeed", 0.05f)),
                V("Warden", "Wardening", "Social",
                    S("ArrestSuccessChance", 0.05f),
                    S("SuppressionPower", 0.05f, "package:Ludeon.RimWorld.Ideology"),
                    S("VSE_RecruitRate", 0.05f)),
                V("Negotiator", "Negotiating", "Social",
                    S("TradePriceImprovement", 0.05f), S("VSE_PeaceTalksChance", 0.05f)),
                Vd("Proselytizer", "Proselytizing", "Social", "Ludeon.RimWorld.Ideology", S("ConversionPower", 0.05f)),
                Vd("VSE_Lovin", "Lovin'", "Social", "vanillaracesexpanded.highmate",
                    S("VEF_MTBLovinFactor", -0.04f), S("SocialImpact", 0.05f)),

                A("AS_Blasting", "Blasting", "Shooting", S("VEF_RangeAttackDamageFactor", 0.01f)),
                A("AS_Mortaring", "Mortaring", "Shooting",
                    S("MortarMissRadiusFactor", -0.05f), S("AS_MortarDamageFactor", 0.01f)),
                A("AS_Pummeling", "Firestarter", "Melee", S("AS_FireStarter", 0.02f)),
                A("AS_Cleaving", "Cleaving", "Melee", S("MeleeDamageFactor", 0.025f)),
                A("AS_Evading", "Evading", "Melee", S("MeleeDodgeChance", 2f)),
                A("AS_Enduring", "Enduring", "Melee", S("IncomingDamageFactor", -0.01f)),
                A("AS_CraftingQuality", "Crafting quality", "Crafting", S("VSE_CraftingQuality", 0.05f)),
                A("AS_CraftingYield", "Crafting yield", "Crafting", S("AS_CraftingYield", 0.025f)),
                A("AS_Smelting", "Smelting", "Crafting", S("SmeltingSpeed", 0.05f)),
                A("AS_Panning", "Panning", "Mining", S("AS_ExtraGoldYield", 0.01f)),
                A("AS_Salvaging", "Salvaging", "Mining", S("AS_ExtraComponentsYield", 0.01f)),
                A("AS_Trafficking", "Trafficking", "Social",
                    S("DrugHarvestYield", 0.025f), S("DrugSellPriceImprovement", 0.025f)),
                Ad("AS_Influencing", "Influencing", "Social", "Ludeon.RimWorld.Ideology",
                    S("SocialIdeoSpreadFrequencyFactor", 0.05f), S("CertaintyLossFactor", -0.05f)),
                Ad("AS_Psycasting", "Psycasting", "Intellectual", "Ludeon.RimWorld.Royalty",
                    S("PsychicEntropyMax", 10f), S("PsychicEntropyRecoveryRate", 0.05f)),
                Ad("AS_Quelling", "Quelling", "Intellectual", "Ludeon.RimWorld.Anomaly",
                    S("ActivitySuppressionRate", 0.05f)),
                Ad("AS_Piloting", "Piloting", "Intellectual", "Ludeon.RimWorld.Odyssey",
                    S("PilotingAbility", 0.05f)),
                Ad("AS_VacuumResilience", "Vacuum Resilience", "Melee", "Ludeon.RimWorld.Odyssey",
                    S("VacuumResistance", 0.05f)),
                A("AS_RangedDodging", "Ranged Dodging", "Melee", S("VEF_RangedDodgeChance", 0.01f)),
                A("AS_Mindfulness", "Mindfulness", "Social", S("VEF_NegativeThoughtDurationFactor", -0.025f)),
            };
            return new ReadOnlyCollection<SignalDefinition>(result);
        }

        private static SignalDefinition V(string defName, string label, string skill, params SignalEffect[] effects) =>
            Definition(defName, label, skill, Vse, "Vanilla Skills Expanded", null, effects);

        private static SignalDefinition Vd(
            string defName, string label, string skill, string dependency, params SignalEffect[] effects) =>
            Definition(defName, label, skill, Vse, "Vanilla Skills Expanded", new[] { dependency }, effects);

        private static SignalDefinition A(string defName, string label, string skill, params SignalEffect[] effects) =>
            Definition(defName, label, skill, Alpha, "Alpha Skills", new[] { Vse }, effects);

        private static SignalDefinition Ad(
            string defName, string label, string skill, string dependency, params SignalEffect[] effects) =>
            Definition(defName, label, skill, Alpha, "Alpha Skills", new[] { Vse, dependency }, effects);

        private static SignalDefinition Definition(
            string defName,
            string label,
            string skill,
            string packageId,
            string sourceDisplayName,
            IEnumerable<string> dependencies,
            IEnumerable<SignalEffect> effects) =>
            new SignalDefinition(
                SignalType.Active,
                new SignalSource(SignalSourceKind.Expertise, defName, packageId,
                    requiredPackageIds: dependencies),
                degree: null,
                skillDefName: skill,
                derivesSkillFromSource: false,
                effects: effects,
                fallbackUi: new SignalUi(label, null, ExpertiseIcon, null, null, sourceDisplayName));

        private static SignalEffect S(string statDefName, float value, string condition = null) =>
            new SignalEffect(
                KindOf(statDefName),
                SignalOperation.Add,
                value,
                SignalValueUnit.StatValue,
                statDefName,
                condition == null ? null : new[] { new SignalCondition(condition, condition) },
                SignalScaleKind.ExpertiseLevel);

        // Explicit source-controlled assignments; runtime code never classifies stat names.
        private static SignalEffectKind KindOf(string statDefName)
        {
            switch (statDefName)
            {
                case "ShootingAccuracyPawn":
                case "MortarMissRadiusFactor":
                    return SignalEffectKind.Accuracy;
                case "AimingDelayFactor":
                    return SignalEffectKind.AimingDelay;
                case "AnimalGatherSpeed":
                case "VSE_ArtSpeed":
                case "VSE_FloorSpeed":
                case "VSE_RepairSpeed":
                case "ConstructionSpeed":
                case "SmoothingSpeed":
                case "ButcheryFleshSpeed":
                case "DrugCookingSpeed":
                case "CookSpeed":
                case "ButcheryMechanoidSpeed":
                case "VSE_TailoringSpeed":
                case "VSE_WeaponCreationSpeed":
                case "VSE_ComponentCraftingSpeed":
                case "MedicalTendSpeed":
                case "MedicalOperationSpeed":
                case "DeepDrillingSpeed":
                case "MiningSpeed":
                case "DrugSynthesisSpeed":
                case "HackingSpeed":
                case "ResearchSpeed":
                case "VBE_WritingSpeed":
                case "ReadingSpeed":
                case "PruningSpeed":
                case "PlantWorkSpeed":
                case "SmeltingSpeed":
                    return SignalEffectKind.WorkSpeed;
                case "VSE_ArtQuality":
                case "VSE_ConstructQuality":
                case "MedicalTendQuality":
                case "VSE_CraftingQuality":
                    return SignalEffectKind.Quality;
                case "AnimalGatherYield":
                case "ButcheryFleshEfficiency":
                case "ButcheryMechanoidEfficiency":
                case "MiningYield":
                case "PlantHarvestYield":
                case "AS_CraftingYield":
                case "AS_ExtraGoldYield":
                case "AS_ExtraComponentsYield":
                case "DrugHarvestYield":
                    return SignalEffectKind.Yield;
                case "VEF_RangeAttackDamageFactor":
                case "AS_MortarDamageFactor":
                case "AS_FireStarter":
                case "MeleeDamageFactor":
                case "IncomingDamageFactor":
                    return SignalEffectKind.Damage;
                case "MeleeHitChance":
                    return SignalEffectKind.HitChance;
                case "MeleeDodgeChance":
                case "VEF_RangedDodgeChance":
                    return SignalEffectKind.Dodge;
                default:
                    return SignalEffectKind.StatModifier;
            }
        }
    }
}
