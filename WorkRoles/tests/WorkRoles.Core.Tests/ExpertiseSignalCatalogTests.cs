using System.Globalization;
using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

public class ExpertiseSignalCatalogTests
{
    private static readonly string[] VseRows =
    {
        "Precision|Shooting|ShootingAccuracyPawn=1",
        "CloseQuarters|Shooting|AimingDelayFactor=-0.05",
        "Gunner|Shooting|RangedCooldownFactor=-0.025",
        "VSE_Sniping|Shooting|VEF_VerbRangeFactor=0.01",
        "Sharp|Melee|VSE_ArmorPenetrationFactor=0.05",
        "Blunt|Melee|MeleeCooldownFactor=-0.025",
        "VSE_Striking|Melee|MeleeHitChance=1",
        "Tamer|Animals|TameAnimalChance=0.05,VSE_AttackOnFailChanceFactor=-0.05",
        "Rancher|Animals|AnimalGatherYield=0.05,AnimalGatherSpeed=0.05",
        "Hunter|Animals|HuntingStealth=0.05",
        "Trainer|Animals|TrainAnimalChance=0.05",
        "QualityExpert|Artistic|VSE_ArtQuality=0.05",
        "QuantityExpert|Artistic|VSE_ArtSpeed=0.05",
        "Flooring|Construction|VSE_FloorSpeed=0.05",
        "Repairman|Construction|FixBrokenDownBuildingSuccessChance=0.05,VSE_RepairSpeed=0.05",
        "Architect|Construction|ConstructSuccessChance=0.05,VSE_ConstructQuality=0.05",
        "Foreman|Construction|ConstructionSpeed=0.05",
        "VSE_Smoothing|Construction|SmoothingSpeed=0.05",
        "Butcher|Cooking|ButcheryFleshSpeed=0.05,ButcheryFleshEfficiency=0.05",
        "DrugChef|Cooking|DrugCookingSpeed=0.05",
        "TopChef|Cooking|FoodPoisonChance=-0.05",
        "IndustrialChef|Cooking|CookSpeed=0.05",
        "MechanoidExpert|Crafting|ButcheryMechanoidSpeed=0.05,ButcheryMechanoidEfficiency=0.05",
        "Tailor|Crafting|VSE_TailoringSpeed=0.05",
        "Weaponsmith|Crafting|VSE_WeaponCreationSpeed=0.05",
        "IndustrialProcessExpert|Crafting|VSE_ComponentCraftingSpeed=0.05",
        "Surgeon|Medicine|MedicalSurgerySuccessChance=0.05",
        "BattlefieldMedic|Medicine|MedicalTendSpeed=0.05",
        "InfectiousDiseaseExpert|Medicine|MedicalTendQuality=0.05",
        "VSE_Operating|Medicine|MedicalOperationSpeed=0.05",
        "Driller|Mining|DeepDrillingSpeed=0.05",
        "OreExpert|Mining|MiningYield=0.05",
        "Tunneller|Mining|MiningSpeed=0.05",
        "Geologist|Mining|VSE_RockChunkChance=0.05",
        "Pharmacologist|Intellectual|DrugSynthesisSpeed=0.05",
        "Hacker|Intellectual|HackingSpeed=0.05",
        "Researcher|Intellectual|ResearchSpeed=0.05",
        "VSE_Writing|Intellectual|VBE_WritingSpeed=0.05,ReadingSpeed=0.05",
        "VSE_DarkStudy|Intellectual|EntityStudyRate=0.03,StudyEfficiency=0.05",
        "Forager|Plants|ForagedNutritionPerDay=0.05",
        "Treespeaker|Plants|PruningSpeed=0.05",
        "HarvesterCareful|Plants|PlantHarvestYield=0.05",
        "GreenThumb|Plants|PlantWorkSpeed=0.05",
        "Warden|Social|ArrestSuccessChance=0.05,SuppressionPower=0.05,VSE_RecruitRate=0.05",
        "Negotiator|Social|TradePriceImprovement=0.05,VSE_PeaceTalksChance=0.05",
        "Proselytizer|Social|ConversionPower=0.05",
        "VSE_Lovin|Social|VEF_MTBLovinFactor=-0.04,SocialImpact=0.05",
    };

    private static readonly string[] AlphaRows =
    {
        "AS_Blasting|Shooting|VEF_RangeAttackDamageFactor=0.01",
        "AS_Mortaring|Shooting|AS_MortarDamageFactor=0.01,MortarMissRadiusFactor=-0.05",
        "AS_Pummeling|Melee|AS_FireStarter=0.02",
        "AS_Cleaving|Melee|MeleeDamageFactor=0.025",
        "AS_Evading|Melee|MeleeDodgeChance=2",
        "AS_Enduring|Melee|IncomingDamageFactor=-0.01",
        "AS_CraftingQuality|Crafting|VSE_CraftingQuality=0.05",
        "AS_CraftingYield|Crafting|AS_CraftingYield=0.025",
        "AS_Smelting|Crafting|SmeltingSpeed=0.05",
        "AS_Panning|Mining|AS_ExtraGoldYield=0.01",
        "AS_Salvaging|Mining|AS_ExtraComponentsYield=0.01",
        "AS_Trafficking|Social|DrugHarvestYield=0.025,DrugSellPriceImprovement=0.025",
        "AS_Influencing|Social|CertaintyLossFactor=-0.05,SocialIdeoSpreadFrequencyFactor=0.05",
        "AS_Psycasting|Intellectual|PsychicEntropyMax=10,PsychicEntropyRecoveryRate=0.05",
        "AS_Quelling|Intellectual|ActivitySuppressionRate=0.05",
        "AS_Piloting|Intellectual|PilotingAbility=0.05",
        "AS_VacuumResilience|Melee|VacuumResistance=0.05",
        "AS_RangedDodging|Melee|VEF_RangedDodgeChance=0.01",
        "AS_Mindfulness|Social|VEF_NegativeThoughtDurationFactor=-0.025",
    };

    [Test]
    public async Task MembershipSkillsAndStatOffsetsAreExact()
    {
        AssertRows("vanillaexpanded.skills", VseRows);
        AssertRows("sarg.alphaskills", AlphaRows);

        await Assert.That(ExpertiseSignalDefinitions.All.Count).IsEqualTo(66);
        await Assert.That(ExpertiseSignalDefinitions.All.All(x =>
            x.Type == SignalType.Active
            && !x.IsTransient
            && x.Effects.All(e => e.ScaleKind == SignalScaleKind.ExpertiseLevel))).IsTrue();
    }

    [Test]
    public async Task SourceAttributionAndDependenciesAreStaticPolicy()
    {
        var precision = One("Precision");
        var blasting = One("AS_Blasting");
        await Assert.That(precision.FallbackUi.SourceDisplayName).IsEqualTo("Vanilla Skills Expanded");
        await Assert.That(blasting.FallbackUi.SourceDisplayName).IsEqualTo("Alpha Skills");
        await Assert.That(blasting.Source.RequiredPackageIds).Contains("vanillaexpanded.skills");

        await Assert.That(One("Hacker").Source.RequiredPackageIds)
            .Contains("Ludeon.RimWorld.Ideology");
        await Assert.That(One("AS_Psycasting").Source.RequiredPackageIds)
            .Contains("Ludeon.RimWorld.Royalty");
        await Assert.That(One("AS_Piloting").Source.RequiredPackageIds)
            .Contains("Ludeon.RimWorld.Odyssey");

        var suppression = One("Warden").Effects.Single(x => x.TargetDefName == "SuppressionPower");
        await Assert.That(suppression.Conditions.Any(x =>
            x.Key == "package:Ludeon.RimWorld.Ideology")).IsTrue();
    }

    [Test]
    public async Task ObviousStatsReceiveSpecificEffectKindsWithoutRuntimeInference()
    {
        await Assert.That(Effect("Precision", "ShootingAccuracyPawn").Kind)
            .IsEqualTo(SignalEffectKind.Accuracy);
        await Assert.That(Effect("Foreman", "ConstructionSpeed").Kind)
            .IsEqualTo(SignalEffectKind.WorkSpeed);
        await Assert.That(Effect("QualityExpert", "VSE_ArtQuality").Kind)
            .IsEqualTo(SignalEffectKind.Quality);
        await Assert.That(Effect("AS_CraftingYield", "AS_CraftingYield").Kind)
            .IsEqualTo(SignalEffectKind.Yield);
        await Assert.That(Effect("AS_Cleaving", "MeleeDamageFactor").Kind)
            .IsEqualTo(SignalEffectKind.Damage);
        await Assert.That(Effect("AS_Evading", "MeleeDodgeChance").Kind)
            .IsEqualTo(SignalEffectKind.Dodge);
    }

    [Test]
    public async Task LevelAndSettingScaleResolveWithoutChangingPerLevelMagnitude()
    {
        var definition = One("Foreman");
        var signal = SignalFactory.Instantiate(
            definition,
            runtimeSkillDefName: "Construction",
            currentScale: 20f,
            scaleMultiplier: 1.5f);
        var effect = signal.Effects.Single();

        await Assert.That(effect.Magnitude).IsEqualTo(0.05f);
        await Assert.That(effect.CurrentScale).IsEqualTo(20f);
        await Assert.That(effect.ScaleMultiplier).IsEqualTo(1.5f);
        await Assert.That(Math.Abs(effect.ResolvedMagnitude.Value - 1.5f) < 0.0001f).IsTrue();
    }

    [Test]
    public async Task VseReflectionContractUsesTheVerifiedOnePointSixApiShape()
    {
        await Assert.That(VseSignalApi.ExpertiseTrackersType).IsEqualTo("VSE.ExpertiseTrackers");
        await Assert.That(VseSignalApi.ExpertiseTrackerType).IsEqualTo("VSE.ExpertiseTracker");
        await Assert.That(VseSignalApi.ExpertiseRecordType).IsEqualTo("VSE.ExpertiseRecord");
        await Assert.That(VseSignalApi.ExpertiseDefType).IsEqualTo("VSE.Expertise.ExpertiseDef");
        await Assert.That(VseSignalApi.PassionManagerType).IsEqualTo("VSE.Passions.PassionManager");
        await Assert.That(VseSignalApi.PassionDefType).IsEqualTo("VSE.Passions.PassionDef");
        await Assert.That(VseSignalApi.AllExpertiseMember).IsEqualTo("AllExpertise");
        await Assert.That(VseSignalApi.ExpertiseLevelMember).IsEqualTo("Level");
        await Assert.That(VseSignalApi.StatMultiplierMember).IsEqualTo("StatMultiplier");
    }

    private static void AssertRows(string packageId, IEnumerable<string> expectedRows)
    {
        var actual = ExpertiseSignalDefinitions.All
            .Where(x => x.Source.PackageId == packageId)
            .Select(Row)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var expected = expectedRows.Select(NormalizeRow)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new Exception("Expertise map mismatch:\nExpected:\n" + string.Join("\n", expected)
                + "\nActual:\n" + string.Join("\n", actual));
    }

    private static string Row(SignalDefinition definition)
    {
        var effects = definition.Effects
            .OrderBy(x => x.TargetDefName, StringComparer.Ordinal)
            .Select(x => x.TargetDefName + "=" + x.Magnitude.Value.ToString("0.###", CultureInfo.InvariantCulture));
        return definition.Source.DefName + "|" + definition.SkillDefName + "|" + string.Join(",", effects);
    }

    private static string NormalizeRow(string row)
    {
        var parts = row.Split('|');
        var effects = parts[2].Split(',').OrderBy(x => x, StringComparer.Ordinal);
        return parts[0] + "|" + parts[1] + "|" + string.Join(",", effects);
    }

    private static SignalDefinition One(string defName) =>
        ExpertiseSignalDefinitions.All.Single(x => x.Source.DefName == defName);

    private static SignalEffect Effect(string defName, string statDefName) =>
        One(defName).Effects.Single(x => x.TargetDefName == statDefName);
}
