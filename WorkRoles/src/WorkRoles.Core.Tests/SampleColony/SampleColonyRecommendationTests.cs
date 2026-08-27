using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

/// Recommendation regressions pinned to the sample colony's published outcome:
/// the ordered per-pawn role list and the removals implied by it.
public class SampleColonyRecommendationTests
{
    /// Theith Noin: a miner/artist with burning passions in both, minor
    /// shooting, and a stack of unskilled chores. The plan keeps his skilled
    /// roles and auto/rules roles, and drops the unskilled extras that the
    /// colony's Unskilled strategies hand to other pawns.
    [Test]
    public async Task NoinKeepsSkilledRolesAndShedsUnskilledExtras()
    {
        RecommendationScenario scenario = BuildScenario("Noin");

        await AssertSameRoles(scenario.CurrentAssignments,
            ["Core", "Basics", "Cleaner", "Hauler", "Farmer Away", "Butcher", "Brewer", "Miner", "Artist", "Grunt", "Hunter", "Gene Maker"], "Noin's current assignments");
        await AssertSameRoles(scenario.RecommendedAssignments, ["Core", "Basics", "Farmer Away", "Miner", "Artist", "Butcher", "Brewer", "Grunt", "Hunter"], "Noin's recommended assignments");
        await AssertSameRoles(scenario.RemovedAssignments, ["Cleaner", "Hauler", "Gene Maker"], "Noin's removed assignments");
    }

    [Test]
    public async Task NoinAssignmentsRemainInPublishedOrder()
    {
        RecommendationScenario scenario = BuildScenario("Noin");
        string actualCurrentOrder = string.Join(", ", scenario.CurrentAssignments);
        string actualRecommendedOrder = string.Join(", ", scenario.RecommendedAssignments);
        string actualRemovedOrder = string.Join(", ", scenario.RemovedAssignments);

        await Assert.That(actualCurrentOrder).IsEqualTo("Core, Basics, Cleaner, Hauler, Farmer Away, Butcher, Brewer, Miner, Artist, Grunt, Hunter, Gene Maker");
        await Assert.That(actualRecommendedOrder).IsEqualTo("Core, Basics, Farmer Away, Miner, Artist, Butcher, Brewer, Hunter, Grunt");
        await Assert.That(actualRemovedOrder).IsEqualTo("Cleaner, Hauler, Gene Maker");
    }

    /// Barbor (Barborbar "Barbor" Bico): the colony's medic-track pawn
    /// (Medicine 11, no passion), strong animals and shooting, currently also
    /// wearing Childcare, Warden and unskilled chores.
    [Test]
    public async Task BarborKeepsMedicTrackAndGainsPainterGruntResearcher()
    {
        RecommendationScenario scenario = BuildScenario("Barbor");

        await AssertSameRoles(scenario.CurrentAssignments,
            ["Core", "Medic", "Basics", "Handler", "Childcare", "Warden", "Fisher", "Hauler", "Cleaner", "Hunter", "Gene Maker"], "Barbor's current assignments");
        await AssertSameRoles(scenario.RecommendedAssignments, ["Core", "Medic", "Basics", "Handler", "Fisher", "Grunt", "Hunter", "Researcher"], "Barbor's recommended assignments");
    }

    [Test]
    public async Task BarborAssignmentsRemainInPublishedOrder()
    {
        RecommendationScenario scenario = BuildScenario("Barbor");
        string actualCurrentOrder = string.Join(", ", scenario.CurrentAssignments);
        string actualRecommendedOrder = string.Join(", ", scenario.RecommendedAssignments);

        await Assert.That(actualCurrentOrder).IsEqualTo("Core, Medic, Basics, Handler, Childcare, Warden, Fisher, Hauler, Cleaner, Hunter, Gene Maker");
        await Assert.That(actualRecommendedOrder).IsEqualTo("Core, Medic, Basics, Handler, Fisher, Grunt, Hunter, Researcher");
    }

    /// Takeo Mahoney: broad generalist (burning Construction, minor Plants,
    /// Animals and Intellectual, Medicine 10) with pinned Childcare and both
    /// away-rules roles; the plan trades Joint Maker for Miner and keeps the
    /// rest.
    [Test]
    public async Task TakeoTradesJointMakerForMinerAndKeepsHisSpread()
    {
        RecommendationScenario scenario = BuildScenario("Takeo");

        await AssertSameRoles(scenario.CurrentAssignments,
            ["Core", "Medic", "Basics", "Builder", "Farmer Away", "Herder", "Hunter", "Miner Away", "Farmer", "Joint Maker", "Fisher", "Grunt", "Researcher", "Childcare", "Gene Maker"],
            "Takeo's current assignments");
        await AssertSameRoles(scenario.RecommendedAssignments,
            ["Core", "Medic", "Basics", "Builder", "Farmer Away", "Hunter", "Herder", "Miner Away", "Farmer", "Miner", "Joint Maker", "Fisher", "Grunt", "Researcher", "Childcare"],
            "Takeo's recommended assignments");
    }

    [Test]
    public async Task TakeoAssignmentsRemainInPublishedOrder()
    {
        RecommendationScenario scenario = BuildScenario("Takeo");
        string actualCurrentOrder = string.Join(", ", scenario.CurrentAssignments);
        string actualRecommendedOrder = string.Join(", ", scenario.RecommendedAssignments);

        await Assert.That(actualCurrentOrder).IsEqualTo("Core, Medic, Basics, Builder, Farmer Away, Herder, Hunter, Miner Away, Farmer, Joint Maker, Fisher, Grunt, Researcher, Childcare, Gene Maker");
        await Assert.That(actualRecommendedOrder).IsEqualTo("Core, Medic, Basics, Farmer Away, Builder, Herder, Miner Away, Farmer, Hunter, Joint Maker, Miner, Fisher, Grunt, Researcher, Childcare");
    }

    /// Pheanox (Fey "Pheanox" Nickel): crafting/intellectual specialist via
    /// xenogerm aptitudes (Crafting 13, Intellectual 15), burning Animals.
    [Test]
    public async Task PheanoxKeepsCraftingTrackAndDropsPainterAndGeneMaker()
    {
        RecommendationScenario scenario = BuildScenario("Pheanox");

        await AssertSameRoles(scenario.CurrentAssignments,
            ["Core", "Basics", "Farmer Away", "Drug Maker", "Fabricator", "Tailor", "Smith", "Hunter", "Herder", "Plant Cutter", "Painter", "Crafter", "Fisher", "Grunt", "Researcher", "Gene Maker"],
            "Pheanox's current assignments");
        await AssertSameRoles(scenario.RecommendedAssignments,
            ["Core", "Basics", "Farmer Away", "Drug Maker", "Fabricator", "Hunter", "Herder", "Smith", "Tailor", "Crafter", "Plant Cutter", "Fisher", "Grunt", "Researcher"],
            "Pheanox's recommended assignments");
    }

    [Test]
    public async Task PheanoxAssignmentsRemainInPublishedOrder()
    {
        RecommendationScenario scenario = BuildScenario("Pheanox");
        string actualCurrentOrder = string.Join(", ", scenario.CurrentAssignments);
        string actualRecommendedOrder = string.Join(", ", scenario.RecommendedAssignments);

        await Assert.That(actualCurrentOrder).IsEqualTo("Core, Basics, Farmer Away, Drug Maker, Fabricator, Tailor, Smith, Hunter, Herder, Plant Cutter, Painter, Crafter, Fisher, Grunt, Researcher, Gene Maker");
        await Assert.That(actualRecommendedOrder).IsEqualTo("Core, Basics, Farmer Away, Drug Maker, Hunter, Fabricator, Smith, Tailor, Crafter, Herder, Plant Cutter, Fisher, Grunt, Researcher");
    }

    [Test]
    public async Task QuinnDropsGeneMakerAndKeepsOtherAssignments()
    {
        RecommendationScenario scenario = BuildScenario("Quinn");

        await AssertSameRoles(scenario.CurrentAssignments,
            ["Core", "Basics", "Farmer Away", "Fabricator", "Tailor", "Smith", "Childcare", "Warden", "Hunter", "Crafter", "Grunt", "Gene Maker"], "Quinn's current assignments");
        await AssertSameRoles(scenario.RecommendedAssignments,
            ["Core", "Basics", "Childcare", "Warden", "Farmer Away", "Fabricator", "Smith", "Tailor", "Crafter", "Hunter", "Grunt"], "Quinn's recommended assignments");
    }

    [Test]
    public async Task QuinnAssignmentsRemainInPublishedOrder()
    {
        RecommendationScenario scenario = BuildScenario("Quinn");
        string actualCurrentOrder = string.Join(", ", scenario.CurrentAssignments);
        string actualRecommendedOrder = string.Join(", ", scenario.RecommendedAssignments);

        await Assert.That(actualCurrentOrder).IsEqualTo("Core, Basics, Farmer Away, Fabricator, Tailor, Smith, Childcare, Warden, Hunter, Crafter, Grunt, Gene Maker");
        await Assert.That(actualRecommendedOrder).IsEqualTo("Core, Basics, Farmer Away, Childcare, Warden, Fabricator, Smith, Tailor, Hunter, Crafter, Grunt");
    }

    [Test]
    public async Task KoenTradesObsoleteChoresForGruntAndResearch()
    {
        RecommendationScenario scenario = BuildScenario("Koen");

        await AssertSameRoles(scenario.CurrentAssignments,
            ["Core", "Medic", "Basics", "Handler", "Crafter", "Fabricator", "Tailor", "Smith", "Hunter", "Farmer", "Miner", "Fisher", "Hauler", "Cleaner", "Gene Maker"],
            "Koen's current assignments");
        await AssertSameRoles(scenario.RecommendedAssignments,
            ["Core", "Basics", "Handler", "Fabricator", "Smith", "Tailor", "Farmer", "Crafter", "Hunter", "Fisher", "Grunt", "Researcher"], "Koen's recommended assignments");
    }

    [Test]
    public async Task KoenAssignmentsRemainInPublishedOrder()
    {
        RecommendationScenario scenario = BuildScenario("Koen");
        string actualCurrentOrder = string.Join(", ", scenario.CurrentAssignments);
        string actualRecommendedOrder = string.Join(", ", scenario.RecommendedAssignments);

        await Assert.That(actualCurrentOrder).IsEqualTo("Core, Medic, Basics, Handler, Crafter, Fabricator, Tailor, Smith, Hunter, Farmer, Miner, Fisher, Hauler, Cleaner, Gene Maker");
        await Assert.That(actualRecommendedOrder).IsEqualTo("Core, Basics, Handler, Fabricator, Smith, Tailor, Hunter, Crafter, Farmer, Fisher, Grunt, Researcher");
    }

    [Test]
    public async Task BlackwellReplacesHaulingAndCleaningWithGrunt()
    {
        RecommendationScenario scenario = BuildScenario("Blackwell");

        await AssertSameRoles(scenario.CurrentAssignments,
            ["Core", "Basics", "Farmer Away", "Cook", "Childcare", "Warden", "Smith Mech", "Hauler", "Hunter", "Farmer", "Crafter", "Cleaner"], "Blackwell's current assignments");
        await AssertSameRoles(scenario.RecommendedAssignments,
            ["Core", "Basics", "Farmer Away", "Childcare", "Warden", "Cook", "Smith Mech", "Hunter", "Farmer", "Tailor", "Crafter", "Grunt"], "Blackwell's recommended assignments");
    }

    [Test]
    public async Task BlackwellAssignmentsRemainInPublishedOrder()
    {
        RecommendationScenario scenario = BuildScenario("Blackwell");
        string actualCurrentOrder = string.Join(", ", scenario.CurrentAssignments);
        string actualRecommendedOrder = string.Join(", ", scenario.RecommendedAssignments);

        await Assert.That(actualCurrentOrder).IsEqualTo("Core, Basics, Farmer Away, Cook, Childcare, Warden, Smith Mech, Hauler, Hunter, Farmer, Crafter, Cleaner");
        await Assert.That(actualRecommendedOrder).IsEqualTo("Core, Basics, Farmer Away, Childcare, Warden, Cook, Smith Mech, Farmer, Tailor, Crafter, Grunt, Hunter");
    }

    [Test]
    public async Task MorgAddsHuntingAndBuildingToExistingAssignments()
    {
        RecommendationScenario scenario = BuildScenario("Morg");

        await AssertSameRoles(scenario.CurrentAssignments, ["Core", "Basics", "Miner", "Handyman", "Grunt"], "Morg's current assignments");
        await AssertSameRoles(scenario.RecommendedAssignments, ["Core", "Basics", "Hunter", "Miner", "Builder", "Grunt"], "Morg's recommended assignments");
    }

    [Test]
    public async Task MorgAssignmentsRemainInPublishedOrder()
    {
        RecommendationScenario scenario = BuildScenario("Morg");
        string actualCurrentOrder = string.Join(", ", scenario.CurrentAssignments);
        string actualRecommendedOrder = string.Join(", ", scenario.RecommendedAssignments);

        await Assert.That(actualCurrentOrder).IsEqualTo("Core, Basics, Miner, Handyman, Grunt");
        await Assert.That(actualRecommendedOrder).IsEqualTo("Core, Basics, Hunter, Miner, Builder, Grunt");
    }

    /// Full-colony pins: every remaining current-map colonist's stored
    /// assignments and published recommendation order, extracted 2026-08-16.
    /// Guards recommendation outcomes and ordering across the whole cohort,
    /// not just the individually narrated pawns above.
    [Test]
    [Arguments("Bashy", "Core, Basics, Warden, Farmer Away, Crafter, Hauler, Cleaner, Gene Maker", "Core, Basics, Warden, Farmer Away, Grunt")]
    [Arguments("Cockroach", "Core, Basics, Cook, Smelter, Fabricator, Tailor, Smith, Drug Maker, Crafter, Hauler, Cleaner, Researcher, Gene Maker, Hunter", "Core, Basics, Cook, Smith, Fabricator, Tailor, Crafter, Drug Maker, Grunt, Hunter, Researcher")]
    [Arguments("Flo", "Core, Medic, Basics, Handyman, Grower, Plant Cutter, Miner, Gene Maker", "Core, Medic, Basics, Handyman, Hunter, Grower, Plant Cutter, Grunt")]
    [Arguments("Gorilla", "Core, Fix Roof, Nurse, Basics, Farmer Away, Handyman, Butcher, Brewer, Tailor, Smith, Hunter, Crafter, Grunt, Gene Maker", "Core, Medic, Nurse, Basics, Farmer Away, Hunter, Handyman, Smith, Tailor, Crafter, Butcher, Brewer, Grunt")]
    [Arguments("Han", "Core, Basics, Farmer Away, Hauler, Butcher, Brewer, Tailor, Smith, Herder, Hunter, Crafter, Fisher, Grunt, Gene Maker", "Core, Basics, Farmer Away, Herder, Butcher, Brewer, Hunter, Smith, Tailor, Crafter, Fisher, Grunt")]
    [Arguments("Hasonove", "Core, Basics, Fabricator, Cook, Tailor, Smith, Drug Maker, Builder, Crafter, Hauler, Cleaner, Researcher, Gene Maker, Hunter", "Core, Basics, Tailor, Cook, Fabricator, Smith, Crafter, Drug Maker, Builder, Grunt, Hunter, Researcher")]
    [Arguments("Hazelnut", "Core, Basics, Farmer Away, Cook, Butcher, Brewer, Warden, Hunter, Hauler, Cleaner, Gene Maker", "Core, Basics, Farmer Away, Childcare, Warden, Butcher, Brewer, Hunter, Grunt")]
    [Arguments("Jet", "Core, Basics, Fix Roof, Builder, Drug Maker, Fabricator, Tailor, Smith, Crafter, Hauler, Cleaner, Researcher, Gene Maker, Hunter", "Core, Basics, Builder, Crafter, Drug Maker, Fabricator, Smith, Tailor, Grunt, Hunter, Researcher")]
    [Arguments("Macey", "Core, Basics, Fabricator, Tailor, Smith, Drug Maker, Crafter, Brewer, Hunter, Hauler, Cleaner, Researcher, Gene Maker", "Core, Basics, Drug Maker, Fabricator, Smith, Hunter, Tailor, Crafter, Butcher, Brewer, Grunt, Researcher")]
    [Arguments("Madcat", "Core, Doctor, Basics, Haul Now, Handler, Cook, Childcare, Warden, Grower, Plant Cutter, Miner, Fisher, Grunt, Hunter, Gene Maker", "Core, Medic, Basics, Haul Now, Childcare, Warden, Cook, Handler, Grower, Plant Cutter, Hunter, Fisher, Grunt, Researcher")]
    [Arguments("Niklas", "Core, Basics, Cook, Smelter, Farmer Away, Fabricator, Tailor, Smith, Handler, Crafter, Fisher, Hauler, Cleaner, Hunter, Gene Maker", "Core, Basics, Farmer Away, Handler, Cook, Fabricator, Smith, Tailor, Crafter, Fisher, Grunt, Hunter")]
    [Arguments("Noah", "Core, Basics, Grower, Plant Cutter, Haul Now, Hunter, Cleaner, Hauler, Gene Maker", "Core, Basics, Cleaner, Hauler, Farmer, Grunt, Hunter")]
    [Arguments("Pun", "Core, Doctor, Basics, Childcare, Warden, Farmer Away, Herder, Hunter, Fisher, Grunt", "Core, Doctor, Basics, Warden, Childcare, Farmer Away, Handler, Farmer, Fisher, Grunt, Hunter")]
    [Arguments("Selma", "Core, Medic, Basics, Farmer Away, Butcher, Brewer, Tailor, Smith, Hunter, Grower, Plant Cutter, Crafter, Grunt, Researcher, Gene Maker", "Core, Medic, Basics, Farmer Away, Hunter, Grower, Plant Cutter, Smith, Tailor, Crafter, Butcher, Brewer, Grunt, Researcher")]
    [Arguments("Severin", "Core, Doctor, Basics, Handler, Hunter, Farmer, Miner, Fisher, Grunt, Gene Maker", "Core, Doctor, Basics, Farmer, Handler, Miner, Fisher, Hunter, Grunt")]
    [Arguments("Tontrool", "Core, Basics, Cook, Builder, Handler, Miner, Painter, Fisher, Hauler, Cleaner, Hunter, Gene Maker", "Core, Basics, Cook, Builder, Handler, Fisher, Grunt, Hunter")]
    [Arguments("Twiggy", "Core, Basics, Farmer Away, Miner Away, Builder, Handler, Hunter, Miner, Fisher, Hauler, Cleaner, Gene Maker", "Core, Basics, Farmer Away, Miner Away, Handler, Miner, Builder, Fisher, Hunter, Grunt")]
    public async Task RemainingColonistsKeepTheirPublishedOrders(string pawnName, string expectedCurrentOrder, string expectedRecommendedOrder)
    {
        RecommendationScenario scenario = BuildScenario(pawnName);

        await Assert.That(string.Join(", ", scenario.CurrentAssignments)).IsEqualTo(expectedCurrentOrder);
        await Assert.That(string.Join(", ", scenario.RecommendedAssignments)).IsEqualTo(expectedRecommendedOrder);
    }

    /// Set equality with a readable diff on failure: the roles the expectation
    /// lists but the actual set lacks, and the roles it carries beyond it.
    private static async Task AssertSameRoles(string[] actual, string[] expected, string label)
    {
        HashSet<string> actualSet = [.. actual];
        string missing = string.Join(", ", expected.Where(role => !actualSet.Contains(role)));
        string extra = string.Join(", ", actualSet.Except(expected));
        await Assert.That(actualSet.SetEquals(expected)).IsTrue().Because($"{label} differ from the expected set: missing [{missing}], extra [{extra}]");
    }

    private static RecommendationScenario BuildScenario(string pawnName)
    {
        SamplePawn pawn = SampleColony.Pawn(pawnName);
        RecommendationPlan plan = RecommendationPlan.Build(SampleColony.BuildColonyView());
        int pawnIndex = Enumerable.Range(0, SampleColony.CurrentMapPawns.Count).Single(index => SampleColony.CurrentMapPawns[index] == pawn);
        int[] recommendedRoleIds = [.. Enumerable.Range(0, plan.RoleCountAt(pawnIndex)).Select(index => plan.RoleAt(pawnIndex, index))];
        HashSet<int> recommendedRoleMembership = [.. recommendedRoleIds];
        string[] currentAssignments = [.. pawn.Assignments.Select(assignment => SampleColony.RoleLabel(assignment.RoleId))];
        string[] recommendedAssignments = [.. recommendedRoleIds.Select(SampleColony.RoleLabel)];
        string[] removedAssignments = [.. pawn.Assignments.Where(assignment => !recommendedRoleMembership.Contains(assignment.RoleId)).Select(assignment => SampleColony.RoleLabel(assignment.RoleId))];
        return new RecommendationScenario(currentAssignments, recommendedAssignments, removedAssignments);
    }

    private sealed record RecommendationScenario(string[] CurrentAssignments, string[] RecommendedAssignments, string[] RemovedAssignments);
}
