using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Focused model tests for the spec builder: capability deduplication,
/// weighted participation, primary selection, and training coverage
/// exclusion have no stable final recommendation output of their own.
public class RoleWorkSpecBuilderTests
{
    [Test]
    public async Task CapabilitiesDeduplicateSeedsAndPreserveOrderAndLiterals()
    {
        RoleWorkSpec spec = RoleWorkSpecBuilder.Build(
            1,
            orderedGivers: [],
            seedWorkTypes: ["Crafting", "Hunting", "Crafting"],
            literalWorkTypes: ["Hunting"],
            workTypeOf: _ => null!,
            naturalPriorities: new Dictionary<string, int> { ["Crafting"] = 7, ["Hunting"] = 3 },
            giverFacts: new Dictionary<string, WorkRoles.Core.JobProfileGiverFacts>(),
            index: null!,
            assignmentSkillGates: null!);

        await Assert.That(spec.CapabilityWorkTypes).IsEquivalentTo(["Crafting", "Hunting"]);
        await Assert.That(spec.MaxNaturalPriority).IsEqualTo(7);
        await Assert.That(spec.HasLiteralWorkType("Hunting")).IsTrue();
        await Assert.That(spec.HasLiteralWorkType("Crafting")).IsFalse();
        await Assert.That(spec.HasHuntingCapability).IsTrue();
        await Assert.That(spec.IsSkilled).IsFalse();
        await Assert.That(spec.CapabilityRequirement).IsEqualTo(RoleWorkCapabilityRequirement.Any);
    }

    [Test]
    public async Task MinorityUsedSkillStaysInFactsButDoesNotParticipate()
    {
        // Two Cooking givers (one trains, weight 4) beside one one-off Melee
        // giver: Melee's share is 1/6, below half, so it is display-only.
        RoleWorkSpec spec = RoleWorkSpecBuilder.Build(1, [
            RecsTestBed.Capability("Cooking", 0,
                RecsTestBed.Giver("CookMeals", used: ["Cooking"], trained: ["Cooking"]),
                RecsTestBed.Giver("CookOther", used: ["Cooking"]),
                RecsTestBed.Giver("FinishOff", used: ["Melee"])),
        ], null!);

        RoleSkillFact cooking = spec.Skills[0];
        RoleSkillFact melee = spec.Skills[1];
        await Assert.That(cooking.SkillDefName).IsEqualTo("Cooking");
        await Assert.That(cooking.Primary).IsTrue();
        await Assert.That(cooking.Participates).IsTrue();
        await Assert.That(melee.SkillDefName).IsEqualTo("Melee");
        await Assert.That(melee.Participates).IsFalse();
        await Assert.That(spec.PrimarySkillDefName).IsEqualTo("Cooking");
        await Assert.That(spec.IsSkilled).IsTrue();
        await Assert.That(spec.CapabilityRequirement).IsEqualTo(RoleWorkCapabilityRequirement.All);
    }

    [Test]
    public async Task EvenlySplitUsedSkillsKeepThePrimaryParticipating()
    {
        // Three equal single-giver skills all fall below half share; the
        // primary-by-importance skill still participates so the role keeps a
        // decisive skill and its skilled classification.
        RoleWorkSpec spec = RoleWorkSpecBuilder.Build(1, [
            RecsTestBed.Capability("Crafting", 0,
                RecsTestBed.Giver("A", used: ["Alpha"]),
                RecsTestBed.Giver("B", used: ["Beta"]),
                RecsTestBed.Giver("C", used: ["Gamma"])),
        ], null!);

        await Assert.That(spec.IsSkilled).IsTrue();
        await Assert.That(spec.PrimarySkillDefName).IsEqualTo("Alpha");
        await Assert.That(spec.Skills[0].Participates).IsTrue();
        await Assert.That(spec.Skills[1].Participates).IsFalse();
        await Assert.That(spec.Skills[2].Participates).IsFalse();
    }

    [Test]
    public async Task GateOnlySkillIsNeverUsedTrainedOrPrimary()
    {
        RoleWorkSpec spec = RoleWorkSpecBuilder.Build(1, [
            RecsTestBed.Capability("Crafting", 0,
                RecsTestBed.Giver("MakeDrugs", used: ["Intellectual"], trained: ["Intellectual"], gates: ("Crafting", 4))),
        ], null!);

        RoleSkillFact? crafting = null;
        foreach (RoleSkillFact fact in spec.Skills)
            if (fact.SkillDefName == "Crafting") crafting = fact;
        await Assert.That(spec.PrimarySkillDefName).IsEqualTo("Intellectual");
        await Assert.That(crafting!.UsedGivers).IsEqualTo(0);
        await Assert.That(crafting.TrainedGivers).IsEqualTo(0);
        await Assert.That(crafting.GatedContents).IsEqualTo(1);
    }

    [Test]
    public async Task CuratedGiverEffectsFlowIntoTheSpecUsedSkills()
    {
        // A code-defined giver's curated effect kinds (adapter-wired stats)
        // surface on the spec's used-skill facts; uncurated skills stay
        // Unspecified so no effect claim is invented.
        var builder = new WorkRoles.Core.JobProfileIndexBuilder();
        builder.AddWorkType(1, "Doctor", [new WorkRoles.Core.JobProfileSkillSource(1, "Medicine")], ["TendWork"]);
        builder.AddGiver("TendWork", 1, [new WorkRoles.Core.JobProfileSkillSource(1, "Medicine")],
            hasCuratedXp: true, curatedXpSkillDefNames: ["Medicine"],
            hasCuratedUsedSkills: true, curatedUsedSkillDefNames: ["Medicine"],
            curatedSkillEffects: [new WorkRoles.Core.JobProfileSkillEffect("Medicine", RoleWorkEffect.Speed | RoleWorkEffect.Quality)]);
        WorkRoles.Core.JobProfileIndex index = builder.Build();

        RoleWorkSpec spec = RoleWorkSpecBuilder.Build(
            1, ["TendWork"], ["Doctor"], [], _ => "Doctor",
            new Dictionary<string, int>(), index.Givers, index, null!);

        await Assert.That(spec.Skills[0].SkillDefName).IsEqualTo("Medicine");
        await Assert.That(spec.Skills[0].Effects).IsEqualTo(RoleWorkEffect.Speed | RoleWorkEffect.Quality);
        RoleWorkGiverSpec giver = spec.Capabilities[0].Givers[0];
        await Assert.That(giver.UsedSkills[0].Effects).IsEqualTo(RoleWorkEffect.Speed | RoleWorkEffect.Quality);
    }

    [Test]
    public async Task CompositeMergePreservesEachMemberParticipatingSkill()
    {
        RoleView farmer = RecsTestBed.Skilled(1, "Growing", "Plants", "Grow");
        RoleView handler = RecsTestBed.Skilled(2, "Handling", "Animals", "Handle");

        RoleWorkSpec merged = RoleWorkSpecBuilder.Merge(3, [farmer.WorkSpec, handler.WorkSpec], null!);

        await Assert.That(merged.CapabilityWorkTypes).IsEquivalentTo(["Growing", "Handling"]);
        await Assert.That(merged.Skills.Count).IsEqualTo(2);
        await Assert.That(merged.Skills[0].Participates).IsTrue();
        await Assert.That(merged.Skills[1].Participates).IsTrue();
        await Assert.That(merged.IsSkilled).IsTrue();
    }

    [Test]
    public async Task SubsetTargetCoverageIsExcludedFromTheTrainingRoleProfile()
    {
        RoleView general = RecsTestBed.Role(1, "Crafting", "MakeDrugs", "Smith", "Tailor");
        RoleView drugMaker = RecsTestBed.Role(2, "Crafting", "MakeDrugs");
        RoleView unrelated = RecsTestBed.Role(3, "Crafting", "CookMeals");
        PathView applicable = RecsTestBed.Path(10, (general.Id, 0, 15), (drugMaker.Id, 15, 21));
        PathView notASubset = RecsTestBed.Path(11, (general.Id, 0, 15), (unrelated.Id, 15, 21));

        IReadOnlyDictionary<int, HashSet<string>> excluded = TrainingCoverageExclusion.ExcludedCoverageByRole([general, drugMaker, unrelated], [applicable, notASubset]);

        await Assert.That(excluded.Keys).IsEquivalentTo([1]);
        await Assert.That(excluded[1]).IsEquivalentTo(["MakeDrugs"]);
    }
}
