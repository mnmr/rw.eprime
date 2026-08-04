using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecommendationBandScenarioTests
{
    [Test]
    public async Task ConnectedTrainingRolesUseBandMinimumBeforeOtherTieBreakers()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        RecommendationBandFixture.Scenario scenario =
            RecommendationBandFixture.Build(15, 23);
        RecommendationPlan plan = RecommendationPlan.Build(scenario.Colony);
        string markedPaths = string.Join(", ", Enumerable
            .Range(0, plan.PathCountAt(0))
            .Select(index => scenario.PathNames[plan.PathAt(0, index)]
                + (plan.PathActivatedAt(0, index) ? "*" : string.Empty)));

        await Assert.That(markedPaths)
            .IsEqualTo("Tailor*, Smith*, Fabricator*, Drug Maker, Farmer, Builder");
        await Assert.That(RoleNames(plan, 0, scenario.RoleNames))
            .IsEqualTo("Core, Basics, Builder, Drug Maker, Fabricator, Smith, Tailor, Farmer, Crafter, Grunt, Researcher, Anomalist");
    }

    [Test]
    public Task Band01_Size01_Seed101()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(1, 101,
            "Drug Maker, Fabricator, Cook, Farmer, Socialist, Builder, Smith, Tailor, Doctor",
            "Core, Doctor, Basics, Caretaker, Warden, Builder, Drug Maker, Fabricator, Smith, Tailor, Cook, Farmer, Miner, Crafter, Grunt, Researcher, Anomalist");
    }

    [Test]
    public Task Band02_Size04_Seed102()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(4, 102,
            """
            Builder, Tailor, Smith, Drug Maker, Farmer, Socialist, Artist, Handler, Doctor
            Doctor, Socialist, Handler, Farmer, Fabricator, Cook, Artist, Smith, Tailor
            Cook, Farmer, Artist, Builder
            Cook, Drug Maker, Fabricator, Socialist, Handler, Builder, Smith, Tailor, Doctor
            """,
            """
            Core, Doctor, Basics, Caretaker, Warden, Builder, Handler, Farmer, Smith, Tailor, Crafter, Artist, Fisher, Grunt, Researcher
            Core, Medic, Basics, Caretaker, Warden, Smith, Tailor, Cook, Jailor, Herder, Grower, Miner, Plant Cutter, Crafter, Artist, Fisher, Grunt
            Core, Basics, Warden, Builder, Cook, Farmer, Miner, Artist, Grunt, Researcher
            Core, Doctor, Basics, Caretaker, Builder, Handler, Drug Maker, Fabricator, Smith, Tailor, Butcher, Brewer, Miner, Crafter, Fisher, Grunt, Researcher, Anomalist
            """);
    }

    [Test]
    public Task Band03_Size07_Seed103()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(7, 103,
            """
            Drug Maker, Socialist, Builder, Doctor
            Handler, Farmer, Socialist, Artist, Doctor
            Builder, Cook, Tailor, Smith, Artist
            Doctor, Farmer, Drug Maker, Fabricator, Handler, Smith, Tailor
            Socialist, Fabricator, Farmer, Handler, Builder, Smith, Tailor, Doctor
            Doctor, Tailor, Smith, Cook, Farmer, Artist, Handler, Builder
            Handler, Builder, Fabricator, Cook, Artist, Smith, Tailor
            """,
            """
            Core, Doctor, Basics, Caretaker, Drug Maker, Builder, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Warden, Herder, Farmer, Miner, Artist, Grunt
            Core, Basics, Cook, Handyman, Miner, Tailor, Crafter, Artist, Grunt, Researcher
            Core, Medic, Basics, Warden, Handler, Drug Maker, Fabricator, Smith, Tailor, Grower, Plant Cutter, Fisher, Grunt, Researcher, Crafter, Anomalist
            Core, Doctor, Basics, Caretaker, Warden, Builder, Fabricator, Handler, Smith, Tailor, Farmer, Crafter, Fisher, Grunt
            Core, Nurse, Basics, Handler, Builder, Cook, Farmer, Tailor, Crafter, Artist, Fisher, Grunt, Researcher
            Core, Basics, Fabricator, Builder, Smith, Tailor, Cook, Herder, Miner, Crafter, Artist, Fisher, Grunt
            """);
    }

    [Test]
    public Task Band04_Size10_Seed104()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(10, 104,
            """
            Doctor, Drug Maker, Fabricator, Farmer, Socialist, Smith, Tailor
            Builder, Artist, Drug Maker, Farmer
            Farmer, Tailor, Smith, Fabricator, Cook, Socialist, Handler, Doctor
            Fabricator, Farmer, Handler, Builder, Smith, Tailor
            Handler, Tailor, Smith, Fabricator, Cook, Artist, Builder
            Handler, Socialist, Doctor
            Doctor, Builder, Cook, Socialist
            Drug Maker, Socialist, Doctor
            Handler, Builder
            Fabricator, Cook, Artist, Smith, Tailor
            """,
            """
            Core, Medic, Basics, Caretaker, Drug Maker, Fabricator, Smith, Tailor, Farmer, Grunt, Researcher, Crafter, Anomalist
            Core, Basics, Drug Maker, Builder, Farmer, Artist, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Handler, Cook, Smith, Tailor, Farmer, Grower, Crafter, Fisher, Grunt
            Core, Basics, Warden, Handler, Fabricator, Builder, Smith, Tailor, Farmer, Crafter, Fisher, Grunt
            Core, Basics, Builder, Handler, Cook, Smith, Tailor, Crafter, Artist, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Herder, Miner, Fisher, Grunt, Researcher
            Core, Medic, Basics, Caretaker, Builder, Cook, Miner, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Drug Maker, Miner, Grunt, Researcher, Crafter, Anomalist
            Core, Basics, Warden, Handler, Builder, Fisher, Grunt, Researcher
            Core, Basics, Fabricator, Smith, Tailor, Cook, Miner, Crafter, Artist, Grunt
            """);
    }

    [Test]
    public Task Band05_Size13_Seed105()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(13, 105,
            """
            Doctor, Builder, Cook, Farmer, Handler
            Builder, Drug Maker, Socialist, Artist, Doctor
            Farmer, Drug Maker, Cook, Handler
            Drug Maker, Fabricator, Farmer, Socialist, Smith, Tailor, Doctor
            Drug Maker, Socialist, Doctor
            Handler, Tailor, Smith, Fabricator, Artist
            Tailor, Smith, Fabricator, Cook, Farmer, Artist
            Cook, Handler, Builder
            Drug Maker, Fabricator, Cook, Handler, Smith, Tailor
            Doctor, Handler, Drug Maker, Socialist, Artist
            Fabricator, Builder, Smith, Tailor
            Farmer, Socialist, Artist, Builder, Doctor
            Drug Maker, Fabricator, Builder, Smith, Tailor
            """,
            """
            Core, Medic, Basics, Handler, Cook, Handyman, Farmer, Miner, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Drug Maker, Handyman, Artist, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Drug Maker, Handler, Cook, Farmer, Grower, Fisher, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Drug Maker, Fabricator, Smith, Tailor, Farmer, Miner, Grunt, Researcher, Crafter, Anomalist
            Core, Doctor, Basics, Caretaker, Drug Maker, Miner, Grunt, Researcher, Anomalist
            Core, Basics, Fabricator, Smith, Tailor, Herder, Miner, Crafter, Artist, Fisher, Grunt
            Core, Basics, Warden, Fabricator, Smith, Tailor, Cook, Farmer, Crafter, Artist, Grunt
            Core, Basics, Warden, Handler, Builder, Cook, Fisher, Grunt
            Core, Basics, Drug Maker, Handler, Cook, Fabricator, Smith, Tailor, Fisher, Grunt, Researcher, Crafter, Anomalist
            Core, Medic, Basics, Caretaker, Drug Maker, Handler, Artist, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Builder, Fabricator, Smith, Tailor, Miner, Crafter, Grunt
            Core, Doctor, Basics, Caretaker, Builder, Farmer, Artist, Grunt
            Core, Basics, Drug Maker, Builder, Fabricator, Smith, Tailor, Grunt, Researcher, Crafter, Anomalist
            """);
    }

    [Test]
    public Task Band06_Size16_Seed106()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(16, 106,
            """
            Drug Maker, Fabricator, Builder, Smith, Tailor
            Cook, Socialist, Handler, Doctor
            Farmer, Builder
            Farmer, Builder
            Handler, Builder, Cook, Farmer
            Fabricator, Cook, Handler, Smith, Tailor
            Drug Maker, Farmer
            Drug Maker, Fabricator, Farmer, Artist, Smith, Tailor
            Socialist, Farmer, Artist, Doctor
            Fabricator, Socialist, Smith, Tailor, Doctor
            Doctor, Builder
            Drug Maker, Socialist, Artist, Handler, Doctor
            Fabricator, Farmer, Smith, Tailor
            Doctor, Handler, Fabricator, Cook, Artist, Smith, Tailor
            Fabricator, Artist, Handler, Builder, Smith, Tailor
            Drug Maker, Farmer
            """,
            """
            Core, Basics, Builder, Drug Maker, Fabricator, Smith, Tailor, Miner, Crafter, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Handler, Cook, Fisher, Grunt
            Core, Basics, Warden, Builder, Farmer, Grunt
            Core, Basics, Warden, Builder, Farmer, Grunt
            Core, Basics, Warden, Handler, Builder, Butcher, Brewer, Farmer, Miner, Fisher, Grunt
            Core, Basics, Warden, Handler, Cook, Fabricator, Smith, Tailor, Crafter, Fisher, Grunt
            Core, Basics, Warden, Drug Maker, Farmer, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Drug Maker, Fabricator, Smith, Tailor, Farmer, Artist, Grunt, Researcher, Crafter, Anomalist
            Core, Doctor, Basics, Caretaker, Warden, Farmer, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Fabricator, Smith, Tailor, Crafter, Grunt
            Core, Medic, Basics, Builder, Miner, Grunt
            Core, Doctor, Basics, Caretaker, Drug Maker, Handler, Artist, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Fabricator, Smith, Tailor, Farmer, Miner, Crafter, Grunt
            Core, Medic, Basics, Fabricator, Handler, Smith, Tailor, Cook, Crafter, Artist, Grunt
            Core, Basics, Builder, Handler, Fabricator, Smith, Tailor, Crafter, Artist, Fisher, Grunt
            Core, Basics, Drug Maker, Farmer, Miner, Grunt, Researcher, Anomalist
            """);
    }

    [Test]
    public Task Band07_Size19_Seed107()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(19, 107,
            """
            Handler, Socialist, Artist, Doctor
            Handler, Drug Maker, Cook, Farmer, Artist
            Drug Maker, Socialist, Doctor
            Drug Maker, Fabricator, Farmer, Smith, Tailor
            Drug Maker, Fabricator, Artist, Smith, Tailor
            Drug Maker, Farmer, Builder
            Doctor, Tailor, Smith, Fabricator, Artist, Handler
            Handler, Drug Maker, Fabricator, Cook, Socialist, Builder, Smith, Tailor, Doctor
            Drug Maker, Artist, Handler
            Cook, Farmer, Builder
            Tailor, Smith, Fabricator, Artist, Builder
            Doctor, Drug Maker, Cook, Handler
            Farmer, Artist
            Builder, Cook, Socialist, Doctor
            Builder, Drug Maker, Fabricator, Farmer, Smith, Tailor
            Doctor, Tailor, Smith, Fabricator, Drug Maker, Artist
            Fabricator, Farmer, Handler, Smith, Tailor
            Cook, Artist, Builder
            Socialist, Builder, Drug Maker, Handler, Doctor
            """,
            """
            Core, Doctor, Basics, Caretaker, Warden, Handler, Artist, Fisher, Grunt
            Core, Basics, Warden, Drug Maker, Handler, Cook, Farmer, Artist, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Warden, Drug Maker, Grunt, Researcher, Anomalist
            Core, Basics, Drug Maker, Fabricator, Smith, Tailor, Farmer, Crafter, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Drug Maker, Fabricator, Smith, Tailor, Artist, Grunt, Researcher, Crafter, Anomalist
            Core, Basics, Warden, Builder, Drug Maker, Farmer, Grunt, Researcher, Anomalist
            Core, Medic, Basics, Warden, Handler, Fabricator, Smith, Tailor, Crafter, Artist, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Drug Maker, Fabricator, Smith, Tailor, Builder, Handler, Cook, Crafter, Grunt, Researcher, Anomalist
            Core, Basics, Drug Maker, Handler, Artist, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Builder, Cook, Farmer, Grunt
            Core, Basics, Warden, Builder, Fabricator, Smith, Tailor, Miner, Crafter, Artist, Grunt
            Core, Medic, Basics, Drug Maker, Handler, Cook, Miner, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Farmer, Miner, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Builder, Cook, Grunt
            Core, Basics, Drug Maker, Fabricator, Builder, Smith, Tailor, Farmer, Miner, Crafter, Grunt, Researcher, Anomalist
            Core, Medic, Basics, Drug Maker, Fabricator, Smith, Tailor, Artist, Grunt, Researcher, Crafter, Anomalist
            Core, Basics, Fabricator, Handler, Smith, Tailor, Farmer, Crafter, Fisher, Grunt
            Core, Basics, Builder, Cook, Miner, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Handler, Drug Maker, Builder, Miner, Fisher, Grunt, Researcher, Anomalist
            """);
    }

    [Test]
    public Task Band08_Size22_Seed108()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(22, 108,
            """
            Fabricator, Cook, Artist, Smith, Tailor
            Builder, Cook, Artist
            Builder, Drug Maker, Cook, Handler
            Socialist, Doctor
            Fabricator, Cook, Smith, Tailor
            Farmer, Tailor, Smith, Fabricator, Socialist, Artist, Doctor
            Farmer, Handler, Builder
            Handler, Cook, Socialist, Artist, Doctor
            Tailor, Smith, Fabricator, Cook, Artist, Builder
            Drug Maker, Farmer, Builder
            Builder, Farmer, Socialist, Artist, Doctor
            Drug Maker, Cook, Socialist, Handler, Doctor
            Handler, Drug Maker, Cook, Socialist, Doctor
            Cook, Socialist, Handler, Doctor
            Tailor, Smith, Fabricator, Cook, Socialist, Doctor
            Handler, Fabricator, Cook, Artist, Smith, Tailor
            Fabricator, Cook, Artist, Smith, Tailor
            Farmer, Drug Maker
            Drug Maker, Handler, Builder
            Cook, Farmer
            Socialist, Builder, Doctor
            Fabricator, Cook, Farmer, Artist, Smith, Tailor
            """,
            """
            Core, Basics, Warden, Fabricator, Smith, Tailor, Cook, Crafter, Artist, Grunt
            Core, Basics, Warden, Builder, Cook, Artist, Grunt
            Core, Basics, Drug Maker, Handler, Builder, Cook, Fisher, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Miner, Grunt
            Core, Basics, Warden, Fabricator, Smith, Tailor, Cook, Miner, Crafter, Grunt
            Core, Doctor, Basics, Caretaker, Fabricator, Smith, Tailor, Grower, Miner, Plant Cutter, Crafter, Artist, Grunt
            Core, Basics, Warden, Builder, Handler, Farmer, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Handler, Cook, Artist, Grunt
            Core, Basics, Builder, Smith, Tailor, Cook, Miner, Crafter, Artist, Grunt
            Core, Basics, Builder, Drug Maker, Farmer, Miner, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Builder, Farmer, Miner, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Drug Maker, Handler, Cook, Fisher, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Drug Maker, Handler, Cook, Fisher, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Handler, Cook, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Smith, Tailor, Cook, Miner, Crafter, Grunt
            Core, Basics, Warden, Handler, Cook, Fabricator, Smith, Tailor, Crafter, Artist, Fisher, Grunt
            Core, Basics, Warden, Fabricator, Smith, Tailor, Cook, Crafter, Artist, Grunt
            Core, Basics, Warden, Drug Maker, Grower, Plant Cutter, Grunt, Researcher, Anomalist
            Core, Basics, Drug Maker, Builder, Handler, Miner, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Cook, Farmer, Miner, Grunt
            Core, Doctor, Basics, Caretaker, Builder, Miner, Grunt
            Core, Basics, Fabricator, Smith, Tailor, Cook, Farmer, Crafter, Artist, Grunt
            """);
    }

    [Test]
    public Task Band09_Size25_Seed109()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(25, 109,
            """
            Drug Maker, Builder
            Cook, Artist, Builder
            Drug Maker, Artist
            Builder, Handler
            Drug Maker, Farmer, Builder
            Doctor, Fabricator, Cook, Socialist, Artist, Smith, Tailor
            Drug Maker, Fabricator, Farmer, Socialist, Smith, Tailor, Doctor
            Drug Maker, Farmer, Handler
            Drug Maker, Farmer, Socialist, Builder, Doctor
            Fabricator, Farmer, Socialist, Artist, Smith, Tailor, Doctor
            Drug Maker, Fabricator, Handler, Smith, Tailor
            Tailor, Smith, Fabricator, Drug Maker, Cook, Handler
            Doctor, Cook, Socialist, Handler
            Cook, Socialist, Builder, Doctor
            Drug Maker, Cook, Artist
            Fabricator, Cook, Handler, Builder, Smith, Tailor
            Tailor, Smith, Fabricator, Cook, Farmer, Socialist, Doctor
            Artist, Handler
            Drug Maker, Cook, Handler
            Tailor, Smith, Fabricator, Handler
            Doctor, Farmer, Socialist, Handler
            Builder, Fabricator, Socialist, Handler, Doctor
            Fabricator, Farmer, Handler
            Builder, Drug Maker, Handler
            Drug Maker, Farmer
            """,
            """
            Core, Basics, Builder, Drug Maker, Miner, Grunt, Researcher, Anomalist
            Core, Basics, Builder, Cook, Miner, Artist, Grunt
            Core, Basics, Drug Maker, Artist, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Handler, Handyman, Fisher, Grunt
            Core, Basics, Drug Maker, Builder, Farmer, Grunt, Researcher, Anomalist
            Core, Medic, Basics, Caretaker, Fabricator, Smith, Tailor, Cook, Crafter, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Drug Maker, Fabricator, Smith, Tailor, Farmer, Crafter, Grunt, Researcher, Anomalist
            Core, Basics, Drug Maker, Handler, Farmer, Fisher, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Builder, Drug Maker, Farmer, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Fabricator, Smith, Tailor, Farmer, Crafter, Artist, Grunt
            Core, Basics, Warden, Handler, Drug Maker, Fabricator, Smith, Tailor, Miner, Fisher, Grunt, Researcher, Crafter, Anomalist
            Core, Basics, Handler, Cook, Drug Maker, Fabricator, Smith, Tailor, Fisher, Grunt, Researcher, Crafter, Anomalist
            Core, Medic, Basics, Caretaker, Warden, Handler, Cook, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Builder, Cook, Grunt
            Core, Basics, Warden, Drug Maker, Cook, Artist, Grunt, Researcher, Anomalist
            Core, Basics, Builder, Handler, Cook, Fabricator, Smith, Tailor, Crafter, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Fabricator, Smith, Tailor, Cook, Farmer, Crafter, Grunt
            Core, Basics, Handler, Miner, Artist, Fisher, Grunt
            Core, Basics, Handler, Cook, Drug Maker, Miner, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Handler, Fabricator, Smith, Tailor, Miner, Crafter, Fisher, Grunt
            Core, Medic, Basics, Caretaker, Handler, Farmer, Miner, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Builder, Handler, Fabricator, Smith, Crafter, Fisher, Grunt
            Core, Basics, Warden, Handler, Fabricator, Farmer, Smith, Crafter, Fisher, Grunt
            Core, Basics, Handler, Drug Maker, Handyman, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Drug Maker, Farmer, Grunt, Researcher, Anomalist
            """);
    }

    [Test]
    public Task Band10_Size28_Seed110()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(28, 110,
            """
            Cook, Farmer, Artist
            Farmer, Socialist, Artist, Builder, Doctor
            Drug Maker, Farmer, Socialist, Builder, Doctor
            Handler, Fabricator, Artist, Smith, Tailor
            Drug Maker, Fabricator, Artist, Builder, Smith, Tailor
            Socialist, Artist, Handler, Builder, Doctor
            Doctor, Socialist, Builder
            Drug Maker, Fabricator, Cook, Artist, Smith, Tailor
            Cook
            Doctor, Cook, Socialist
            Socialist, Doctor
            Drug Maker, Cook, Farmer
            Cook, Builder
            Fabricator, Cook, Smith, Tailor
            Handler, Farmer, Artist
            Cook, Builder
            Farmer, Socialist, Artist, Doctor
            Drug Maker, Fabricator, Handler, Smith, Tailor
            Farmer, Drug Maker, Handler
            Fabricator, Cook, Socialist, Smith, Tailor, Doctor
            Handler
            Artist, Handler
            Handler, Fabricator, Farmer, Smith, Tailor
            Doctor, Fabricator, Cook, Socialist, Builder, Smith, Tailor
            Fabricator, Builder, Smith, Tailor
            Fabricator, Smith, Tailor
            Fabricator, Builder, Smith, Tailor
            Drug Maker, Cook, Artist
            """,
            """
            Core, Basics, Cook, Farmer, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Builder, Grower, Plant Cutter, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Builder, Drug Maker, Farmer, Grunt, Researcher, Anomalist
            Core, Basics, Fabricator, Handler, Smith, Tailor, Miner, Crafter, Artist, Fisher, Grunt
            Core, Basics, Builder, Drug Maker, Fabricator, Smith, Tailor, Crafter, Artist, Grunt, Researcher
            Core, Doctor, Basics, Caretaker, Handler, Builder, Artist, Fisher, Grunt
            Core, Medic, Basics, Caretaker, Builder, Miner, Grunt
            Core, Basics, Drug Maker, Fabricator, Smith, Tailor, Cook, Crafter, Artist, Grunt, Researcher, Anomalist
            Core, Basics, Cook, Miner, Grunt
            Core, Medic, Basics, Caretaker, Warden, Cook, Miner, Grunt
            Core, Doctor, Basics, Caretaker, Miner, Grunt
            Core, Basics, Drug Maker, Cook, Farmer, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Builder, Cook, Grunt
            Core, Basics, Fabricator, Smith, Tailor, Cook, Miner, Crafter, Grunt
            Core, Basics, Herder, Farmer, Miner, Artist, Fisher, Grunt
            Core, Basics, Builder, Cook, Miner, Grunt
            Core, Doctor, Basics, Caretaker, Farmer, Miner, Artist, Grunt
            Core, Basics, Handler, Fabricator, Smith, Tailor, Crafter, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Handler, Drug Maker, Farmer, Grower, Fisher, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Fabricator, Smith, Tailor, Cook, Crafter, Grunt
            Core, Basics, Warden, Handler, Miner, Fisher, Grunt
            Core, Basics, Warden, Handler, Artist, Fisher, Grunt
            Core, Basics, Handler, Fabricator, Smith, Tailor, Farmer, Crafter, Fisher, Grunt
            Core, Medic, Basics, Caretaker, Builder, Fabricator, Smith, Tailor, Cook, Crafter, Grunt
            Core, Basics, Warden, Builder, Fabricator, Smith, Tailor, Crafter, Grunt
            Core, Basics, Fabricator, Smith, Tailor, Miner, Crafter, Grunt
            Core, Basics, Warden, Fabricator, Builder, Smith, Tailor, Crafter, Grunt
            Core, Basics, Cook, Artist, Grunt, Researcher, Crafter, Anomalist
            """);
    }

    [Test]
    public Task Band11_Size31_Seed111()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(31, 111,
            """
            Fabricator, Socialist, Smith, Tailor, Doctor
            Drug Maker, Socialist, Artist, Doctor
            Fabricator, Artist, Smith, Tailor
            Farmer, Artist
            Drug Maker, Cook
            Drug Maker, Artist, Handler
            Farmer, Artist, Builder
            Handler, Fabricator, Cook, Artist, Smith, Tailor
            Drug Maker, Socialist, Builder, Doctor
            Doctor, Drug Maker, Socialist, Artist
            Doctor, Socialist, Builder
            Drug Maker, Socialist, Builder, Doctor
            Fabricator, Farmer, Artist, Smith, Tailor
            Drug Maker, Cook, Handler
            Fabricator, Farmer, Builder, Smith, Tailor
            Doctor, Socialist, Builder
            Drug Maker, Farmer, Socialist, Builder, Doctor
            Farmer, Cook
            Handler, Drug Maker, Fabricator, Builder, Smith, Tailor
            Fabricator, Cook, Artist, Smith, Tailor
            Handler, Fabricator, Farmer, Builder, Smith, Tailor
            Drug Maker, Cook
            Socialist, Handler, Doctor
            Cook, Farmer, Builder
            Doctor, Socialist, Handler, Builder
            Artist, Builder
            Drug Maker, Farmer, Handler
            Fabricator, Cook, Artist, Smith, Tailor
            Socialist, Builder, Doctor
            Drug Maker, Fabricator, Smith, Tailor
            Fabricator, Artist, Smith, Tailor
            """,
            """
            Core, Doctor, Basics, Caretaker, Fabricator, Smith, Tailor, Miner, Crafter, Grunt
            Core, Doctor, Basics, Caretaker, Drug Maker, Artist, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Fabricator, Smith, Tailor, Crafter, Artist, Grunt
            Core, Basics, Warden, Farmer, Artist, Grunt
            Core, Basics, Warden, Drug Maker, Cook, Miner, Grunt, Researcher, Anomalist
            Core, Basics, Drug Maker, Handler, Miner, Artist, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Builder, Grower, Plant Cutter, Artist, Grunt
            Core, Basics, Handler, Cook, Fabricator, Smith, Tailor, Crafter, Artist, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Builder, Drug Maker, Grunt, Researcher, Anomalist
            Core, Medic, Basics, Caretaker, Drug Maker, Artist, Grunt, Researcher, Anomalist
            Core, Medic, Basics, Caretaker, Builder, Miner, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Builder, Drug Maker, Grunt, Researcher, Anomalist
            Core, Basics, Fabricator, Smith, Tailor, Farmer, Crafter, Artist, Grunt
            Core, Basics, Handler, Cook, Drug Maker, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Fabricator, Builder, Smith, Tailor, Farmer, Crafter, Grunt
            Core, Medic, Basics, Caretaker, Builder, Miner, Grunt
            Core, Doctor, Basics, Caretaker, Drug Maker, Builder, Farmer, Grunt, Researcher, Anomalist
            Core, Basics, Cook, Grower, Plant Cutter, Grunt
            Core, Basics, Drug Maker, Fabricator, Smith, Tailor, Builder, Herder, Crafter, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Fabricator, Smith, Tailor, Cook, Crafter, Artist, Grunt
            Core, Basics, Fabricator, Builder, Smith, Tailor, Handler, Farmer, Miner, Crafter, Fisher, Grunt
            Core, Basics, Drug Maker, Cook, Miner, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Handler, Miner, Fisher, Grunt
            Core, Basics, Warden, Builder, Cook, Farmer, Grunt
            Core, Medic, Basics, Caretaker, Warden, Handler, Builder, Fisher, Grunt
            Core, Basics, Warden, Builder, Miner, Artist, Fisher, Grunt
            Core, Basics, Drug Maker, Handler, Farmer, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Fabricator, Smith, Tailor, Cook, Miner, Crafter, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Builder, Grunt
            Core, Basics, Drug Maker, Fabricator, Smith, Tailor, Crafter, Grunt, Researcher, Anomalist
            Core, Basics, Fabricator, Smith, Tailor, Miner, Crafter, Artist, Grunt
            """);
    }

    [Test]
    public Task Band12_Size34_Seed112()
    {
        /*
         * Default order: Core > Doctor > Basics > Caretaker > Warden > Handler > Builder > Cook > Farmer > Miner > Tailor > Smith > Crafter > Artist > Fisher > Grunt > Researcher > Anomalist.
         * Paths/anchors: Drug Maker after Warden | Fabricator after Warden | Cook after Handler | Farmer before Miner | Socialist after Basics | Artist unanchored | Handler after Warden | Builder after Warden | Smith after Fabricator | Tailor before Smith | Doctor after Core.
         * Scales: Doctor=Doctoring | Caretaker=Caretaking | Warden=Wardening | Handler=Handling | Cook=Cooking | Fisher=Fishing | Builder=Building | Farmer=Farming | Miner=Mining | Tailor=Tailoring | Smith=Smithing | Fabricator=Fabrication | Crafter=Crafting | Researcher=Research | Anomalist=Dark Study | Artist=Artistry | Drug Maker=Drug Fabrication.
         */
        return AssertBand(34, 112,
            """
            Drug Maker, Farmer, Socialist, Builder, Doctor
            Socialist, Artist, Doctor
            Fabricator, Drug Maker, Smith, Tailor
            Socialist, Artist, Doctor
            Artist, Handler
            Fabricator, Drug Maker, Cook, Farmer, Handler
            Drug Maker, Fabricator, Farmer, Artist, Smith, Tailor
            Farmer, Socialist, Builder, Doctor
            Farmer, Socialist, Doctor
            Drug Maker, Builder
            Drug Maker, Handler, Builder
            Drug Maker, Handler
            Fabricator, Artist, Handler, Smith, Tailor
            Drug Maker, Cook, Farmer, Builder
            Socialist, Doctor
            Fabricator, Artist, Builder, Smith, Tailor
            Socialist, Builder, Doctor
            Fabricator, Artist, Builder, Smith, Tailor
            Drug Maker, Farmer, Socialist, Doctor
            Drug Maker, Cook, Farmer, Handler
            Cook
            Cook, Artist, Builder
            Socialist, Builder, Doctor
            Farmer, Socialist, Artist, Handler, Doctor
            Farmer, Socialist, Doctor
            Fabricator, Cook, Artist, Smith, Tailor
            Cook, Socialist, Builder, Doctor
            Drug Maker, Socialist, Artist, Handler, Doctor
            Fabricator, Drug Maker, Artist, Builder, Smith, Tailor
            Fabricator, Handler, Smith, Tailor
            Farmer, Socialist, Handler, Doctor
            Cook, Farmer, Builder
            Fabricator, Builder, Smith, Tailor
            Drug Maker, Handler
            """,
            """
            Core, Doctor, Basics, Caretaker, Builder, Drug Maker, Farmer, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Warden, Artist, Grunt
            Core, Basics, Warden, Drug Maker, Smith, Tailor, Crafter, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Artist, Grunt
            Core, Basics, Handler, Artist, Fisher, Grunt
            Core, Basics, Handler, Cook, Drug Maker, Fabricator, Farmer, Smith, Fisher, Grunt, Researcher, Crafter, Anomalist
            Core, Basics, Drug Maker, Fabricator, Smith, Tailor, Farmer, Crafter, Artist, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Builder, Farmer, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Farmer, Grunt
            Core, Basics, Builder, Drug Maker, Miner, Grunt, Researcher, Anomalist
            Core, Basics, Handler, Builder, Drug Maker, Miner, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Drug Maker, Handler, Miner, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Handler, Fabricator, Smith, Tailor, Miner, Crafter, Artist, Fisher, Grunt
            Core, Basics, Builder, Drug Maker, Cook, Farmer, Grunt, Researcher, Anomalist
            Core, Doctor, Basics, Caretaker, Warden, Miner, Grunt
            Core, Basics, Builder, Fabricator, Smith, Tailor, Crafter, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Builder, Grunt
            Core, Basics, Builder, Smith, Tailor, Crafter, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Drug Maker, Farmer, Grunt, Researcher, Anomalist
            Core, Basics, Handler, Cook, Drug Maker, Farmer, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Warden, Cook, Miner, Grunt
            Core, Basics, Builder, Cook, Miner, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Builder, Miner, Grunt
            Core, Doctor, Basics, Caretaker, Handler, Farmer, Artist, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Warden, Farmer, Grunt
            Core, Basics, Fabricator, Smith, Tailor, Cook, Crafter, Artist, Grunt
            Core, Doctor, Basics, Caretaker, Builder, Cook, Grunt
            Core, Doctor, Basics, Caretaker, Drug Maker, Handler, Artist, Fisher, Grunt, Researcher, Anomalist
            Core, Basics, Drug Maker, Builder, Smith, Tailor, Artist, Grunt, Researcher, Crafter, Anomalist
            Core, Basics, Fabricator, Handler, Smith, Tailor, Crafter, Fisher, Grunt
            Core, Doctor, Basics, Caretaker, Handler, Farmer, Miner, Fisher, Grunt
            Core, Basics, Builder, Cook, Farmer, Grunt
            Core, Basics, Builder, Fabricator, Smith, Tailor, Miner, Crafter, Grunt
            Core, Basics, Warden, Handler, Drug Maker, Fisher, Grunt, Researcher, Anomalist
            """);
    }

    private static async Task AssertBand(
        int size,
        int seed,
        string expectedPathLines,
        string expectedRoleLines)
    {
        RecommendationBandFixture.Scenario scenario =
            RecommendationBandFixture.Build(size, seed);
        RecommendationPlan plan = RecommendationPlan.Build(scenario.Colony);
        string[] expectedPaths = Lines(expectedPathLines);
        string[] expectedRoles = Lines(expectedRoleLines);
        await Assert.That(expectedPaths.Length).IsEqualTo(size);
        await Assert.That(expectedRoles.Length).IsEqualTo(size);
        for (int pawnIndex = 0; pawnIndex < size; pawnIndex++)
        {
            await Assert.That(PathNames(plan, pawnIndex, scenario.PathNames))
                .IsEqualTo(expectedPaths[pawnIndex]);
            await Assert.That(RoleNames(plan, pawnIndex, scenario.RoleNames))
                .IsEqualTo(expectedRoles[pawnIndex]);
        }
    }

    private static string[] Lines(string value)
        => value.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).ToArray();

    private static string PathNames(
        RecommendationPlan plan,
        int pawnIndex,
        IReadOnlyDictionary<int, string> names)
        => plan.PathCountAt(pawnIndex) == 0
            ? "-"
            : string.Join(", ", Enumerable.Range(0, plan.PathCountAt(pawnIndex))
                .Select(index => names[plan.PathAt(pawnIndex, index)]));

    private static string RoleNames(
        RecommendationPlan plan,
        int pawnIndex,
        IReadOnlyDictionary<int, string> names)
        => string.Join(", ", Enumerable.Range(0, plan.RoleCountAt(pawnIndex))
            .Select(index => names[plan.RoleAt(pawnIndex, index)]));
}
