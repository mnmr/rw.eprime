using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecommendationPlanScenarioTests
{
    [Test]
    public async Task ScaleRequiredTotalIncludesTrainingWaiverAssignments()
    {
        RoleView target = CraftingRole(100, "TargetWork");
        target.HolderMode = RoleHolderMode.Custom;
        target.Scale = new HolderScale();
        target.Scale.RequiredTotals[0] = 3;
        target.Scale.TrainingWaivers[0] = 1;

        RoleView trainee = CraftingRole(101, "TraineeWork");
        trainee.HolderMode = RoleHolderMode.Custom;

        PathView path = RecsTestBed.Path(
            200, (trainee.Id, 0, 10), (target.Id, 10, 21));
        path.AnchorRoleId = target.Id;

        PawnView firstDirect = CraftingPawn(
            16,
            ("TargetWork", SignalBucket.Neutral),
            ("TraineeWork", SignalBucket.Neutral));
        PawnView secondDirect = CraftingPawn(
            15,
            ("TargetWork", SignalBucket.Neutral),
            ("TraineeWork", SignalBucket.Neutral));
        PawnView trainingWaiver = CraftingPawn(
            5,
            ("TargetWork", SignalBucket.Neutral),
            ("TraineeWork", SignalBucket.Neutral));

        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { target, trainee },
            firstDirect,
            secondDirect,
            trainingWaiver);
        colony.Paths.Add(path);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        var roleNames = new Dictionary<int, string>
        {
            [target.Id] = "Target",
            [trainee.Id] = "Trainee",
        };

        await Assert.That(NamesOfRoles(plan, 0, roleNames)).IsEqualTo("Target");
        await Assert.That(NamesOfRoles(plan, 1, roleNames)).IsEqualTo("Target");
        await Assert.That(NamesOfRoles(plan, 2, roleNames)).IsEqualTo("Trainee");
    }

    [Test]
    public async Task Slice01_AllocatesConfiguredWantAndStrongSurplusWithoutAwfulPawns()
    {
        /*
         * Default role recommendation order: Doctor > Cook > Hauler.
         * Training paths: none.
         * Role scales: Doctor custom direct 1 + training 1; Cook custom direct 2;
         * Hauler custom direct 1 (legacy/unskilled and therefore out of scope).
         * Cook's earlier minimum-pick bonus places it before Doctor on the two
         * pawns selected for both roles.
         */
        RoleView doctor = RecsTestBed.Role(1, "Doctor");
        doctor.HolderMode = RoleHolderMode.Custom;
        doctor.RequiredTotal = 2;
        doctor.TrainingWaivers = 1;

        RoleView cook = RecsTestBed.Role(2, "Cooking");
        cook.HolderMode = RoleHolderMode.Custom;
        cook.RequiredTotal = 2;

        RoleView hauler = RecsTestBed.Unskilled(3, "Hauling");
        hauler.HolderMode = RoleHolderMode.Custom;
        hauler.RequiredTotal = 1;

        PawnView forcedDoctor = PawnWith(
            medicine: (14, SignalBucket.Neutral),
            cooking: (2, SignalBucket.Poor));
        PawnView strongLead = PawnWith(
            medicine: (8, SignalBucket.Strong),
            cooking: (10, SignalBucket.Strong));
        PawnView strongDoctorAndForcedCook = PawnWith(
            medicine: (4, SignalBucket.Strong),
            cooking: (8, SignalBucket.Neutral));
        PawnView awfulDespiteSkill = PawnWith(
            medicine: (20, SignalBucket.Awful),
            cooking: (20, SignalBucket.Awful));

        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { doctor, cook, hauler },
            forcedDoctor,
            strongLead,
            strongDoctorAndForcedCook,
            awfulDespiteSkill);
        var roleNames = new Dictionary<int, string>
        {
            [doctor.Id] = "Doctor",
            [cook.Id] = "Cook",
            [hauler.Id] = "Hauler",
        };
        var pathNames = new Dictionary<int, string>();

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        string[] expectedPaths = { "", "", "", "" };
        string[] expectedRoles =
        {
            "Doctor",
            "Cook, Doctor",
            "Cook, Doctor",
            "",
        };
        for (int pawnIndex = 0; pawnIndex < colony.Pawns.Count; pawnIndex++)
        {
            await Assert.That(NamesOfPaths(plan, pawnIndex, pathNames))
                .IsEqualTo(expectedPaths[pawnIndex]);
            await Assert.That(NamesOfRoles(plan, pawnIndex, roleNames))
                .IsEqualTo(expectedRoles[pawnIndex]);
        }
    }

    [Test]
    public async Task Slice02_SubstitutesEachWaiverThroughItsUniqueTargetPath()
    {
        /*
         * Default role recommendation order: Fabricator > Drug Maker > Smith > Tailor.
         * Training paths: Fabricator = Tailor[0,8), Smith[8,15), Fabricator[15,21),
         * anchored before Fabricator; Drug Maker = Tailor[0,8), Smith[8,15),
         * Drug Maker[15,21), anchored before Drug Maker; Smith = Tailor[0,8),
         * Smith[8,21), anchored before Fabricator.
         * Role scales: Fabricator custom direct 1 + training 1; Drug Maker custom
         * training 1; Smith custom training 1; Tailor custom direct 0.
         */
        RoleView fabricator = CraftingRole(10, "Fabrication");
        fabricator.HolderMode = RoleHolderMode.Custom;
        fabricator.RequiredTotal = 2;
        fabricator.TrainingWaivers = 1;
        RoleView drugMaker = CraftingRole(11, "DrugMaking");
        drugMaker.HolderMode = RoleHolderMode.Custom;
        drugMaker.RequiredTotal = 1;
        drugMaker.TrainingWaivers = 1;
        RoleView smith = CraftingRole(12, "Smithing");
        smith.HolderMode = RoleHolderMode.Custom;
        smith.RequiredTotal = 1;
        smith.TrainingWaivers = 1;
        RoleView tailor = CraftingRole(13, "Tailoring");
        tailor.HolderMode = RoleHolderMode.Custom;

        PathView fabricatorPath = RecsTestBed.Path(
            20, (tailor.Id, 0, 8), (smith.Id, 8, 15), (fabricator.Id, 15, 21));
        fabricatorPath.AnchorRoleId = fabricator.Id;
        PathView drugMakerPath = RecsTestBed.Path(
            21, (tailor.Id, 0, 8), (smith.Id, 8, 15), (drugMaker.Id, 15, 21));
        drugMakerPath.AnchorRoleId = drugMaker.Id;
        PathView smithPath = RecsTestBed.Path(
            22, (tailor.Id, 0, 8), (smith.Id, 8, 21));
        smithPath.AnchorRoleId = fabricator.Id;

        PawnView directUnderBand = CraftingPawn(
            14, ("Fabrication", SignalBucket.Neutral),
            ("DrugMaking", SignalBucket.Awful),
            ("Smithing", SignalBucket.Neutral),
            ("Tailoring", SignalBucket.Neutral));
        PawnView sharedTrainee = CraftingPawn(
            10, ("Fabrication", SignalBucket.Neutral),
            ("DrugMaking", SignalBucket.Neutral),
            ("Smithing", SignalBucket.Neutral),
            ("Tailoring", SignalBucket.Neutral));
        PawnView smithTrainee = CraftingPawn(
            4, ("Fabrication", SignalBucket.Awful),
            ("DrugMaking", SignalBucket.Awful),
            ("Smithing", SignalBucket.Strong),
            ("Tailoring", SignalBucket.Neutral));
        smithTrainee.SignalBuckets["Crafting"] = SignalBucket.Strong;

        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { fabricator, drugMaker, smith, tailor },
            directUnderBand,
            sharedTrainee,
            smithTrainee);
        colony.Paths.AddRange(new[] { fabricatorPath, drugMakerPath, smithPath });
        var roleNames = new Dictionary<int, string>
        {
            [fabricator.Id] = "Fabricator",
            [drugMaker.Id] = "Drug Maker",
            [smith.Id] = "Smith",
            [tailor.Id] = "Tailor",
        };
        var pathNames = new Dictionary<int, string>
        {
            [fabricatorPath.Id] = "Fabricator",
            [drugMakerPath.Id] = "Drug Maker",
            [smithPath.Id] = "Smith",
        };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        string[] expectedPaths = { "Fabricator", "Fabricator, Drug Maker", "Smith" };
        string[] expectedRoles = { "Fabricator", "Smith", "Tailor" };
        for (int pawnIndex = 0; pawnIndex < colony.Pawns.Count; pawnIndex++)
        {
            await Assert.That(NamesOfPaths(plan, pawnIndex, pathNames))
                .IsEqualTo(expectedPaths[pawnIndex]);
            await Assert.That(NamesOfRoles(plan, pawnIndex, roleNames))
                .IsEqualTo(expectedRoles[pawnIndex]);
        }
    }

    [Test]
    public async Task Slice02_IgnoresMalformedAlternativeAfterAValidPath()
    {
        /*
         * Default role recommendation order: Specialist > Trainee.
         * Training paths: Valid = Trainee[0,10), Specialist[10,21), anchored
         * before Specialist; Malformed has a Specialist member but no matching
         * band ranges and is therefore unavailable.
         * Role scales: Specialist custom direct 0 + training 1; Trainee custom
         * direct 0.
         */
        RoleView specialist = CraftingRole(24, "SpecialistWork");
        specialist.HolderMode = RoleHolderMode.Custom;
        specialist.RequiredTotal = 1;
        specialist.TrainingWaivers = 1;
        RoleView trainee = CraftingRole(25, "TraineeWork");
        trainee.HolderMode = RoleHolderMode.Custom;
        PathView valid = RecsTestBed.Path(
            26, (trainee.Id, 0, 10), (specialist.Id, 10, 21));
        valid.AnchorRoleId = specialist.Id;
        var malformed = new PathView { Id = 27 };
        malformed.RoleIds.Add(specialist.Id);
        PawnView pawn = CraftingPawn(
            5,
            ("SpecialistWork", SignalBucket.Neutral),
            ("TraineeWork", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { specialist, trainee }, pawn);
        colony.Paths.Add(valid);
        colony.Paths.Add(malformed);
        var roleNames = new Dictionary<int, string>
        {
            [specialist.Id] = "Specialist",
            [trainee.Id] = "Trainee",
        };
        var pathNames = new Dictionary<int, string> { [valid.Id] = "Valid" };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        await Assert.That(NamesOfPaths(plan, 0, pathNames)).IsEqualTo("Valid");
        await Assert.That(NamesOfRoles(plan, 0, roleNames)).IsEqualTo("Trainee");
    }

    [Test]
    public async Task Slice03_RemovesOnlyRolesWhoseWorkIsActuallyCovered()
    {
        /*
         * Default role recommendation order: Doctor > Rescuer > Cook > Butcher >
         * Brewer > Camp Cook > Chef.
         * Training paths: Chef = Butcher[0,15), Brewer[0,15), Chef[15,21),
         * anchored before Chef; its overlapping trainer bands are intentional.
         * Role scales: Doctor direct 1; Rescuer direct 2; Cook direct 1;
         * Butcher direct 2; Brewer direct 1; Camp Cook direct 1; Chef training 1.
         */
        RoleView doctor = SkilledRole(
            30, "Doctoring", "Medicine", "Rescue", "Treat");
        doctor.HolderMode = RoleHolderMode.Custom;
        doctor.RequiredTotal = 1;
        RoleView rescuer = SkilledRole(
            31, "Rescuing", "Medicine", "Rescue");
        rescuer.HolderMode = RoleHolderMode.Custom;
        rescuer.RequiredTotal = 2;
        RoleView cook = SkilledRole(
            32, "CookingAll", "Cooking", "Cook", "Butcher", "Brew");
        cook.HolderMode = RoleHolderMode.Custom;
        cook.RequiredTotal = 1;
        RoleView butcher = SkilledRole(
            33, "Butchering", "Cooking", "Butcher");
        butcher.HolderMode = RoleHolderMode.Custom;
        butcher.RequiredTotal = 2;
        RoleView brewer = SkilledRole(
            34, "Brewing", "Cooking", "Brew");
        brewer.HolderMode = RoleHolderMode.Custom;
        brewer.RequiredTotal = 1;
        RoleView campCook = SkilledRole(
            35, "CampCooking", "Cooking", "Butcher", "CampMeal");
        campCook.HolderMode = RoleHolderMode.Custom;
        campCook.RequiredTotal = 1;
        RoleView chef = SkilledRole(
            36, "Chefing", "Cooking", "Chef");
        chef.HolderMode = RoleHolderMode.Custom;
        chef.RequiredTotal = 1;
        chef.TrainingWaivers = 1;

        PathView chefPath = RecsTestBed.Path(
            40, (butcher.Id, 0, 15), (brewer.Id, 0, 15), (chef.Id, 15, 21));
        chefPath.AnchorRoleId = chef.Id;

        PawnView lead = RolePawn(
            cooking: 15,
            medicine: 15,
            ("Doctoring", SignalBucket.Neutral),
            ("Rescuing", SignalBucket.Neutral),
            ("CookingAll", SignalBucket.Neutral),
            ("Butchering", SignalBucket.Neutral),
            ("Brewing", SignalBucket.Neutral),
            ("CampCooking", SignalBucket.Awful),
            ("Chefing", SignalBucket.Awful));
        PawnView fallback = RolePawn(
            cooking: 12,
            medicine: 10,
            ("Doctoring", SignalBucket.Awful),
            ("Rescuing", SignalBucket.Neutral),
            ("CookingAll", SignalBucket.Awful),
            ("Butchering", SignalBucket.Neutral),
            ("Brewing", SignalBucket.Neutral),
            ("CampCooking", SignalBucket.Awful),
            ("Chefing", SignalBucket.Awful));
        PawnView trainee = RolePawn(
            cooking: 10,
            medicine: null,
            ("Doctoring", SignalBucket.Awful),
            ("Rescuing", SignalBucket.Awful),
            ("CookingAll", SignalBucket.Awful),
            ("Butchering", SignalBucket.Neutral),
            ("Brewing", SignalBucket.Neutral),
            ("CampCooking", SignalBucket.Neutral),
            ("Chefing", SignalBucket.Neutral));

        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView>
            {
                doctor, rescuer, cook, butcher, brewer, campCook, chef,
            },
            lead,
            fallback,
            trainee);
        colony.Paths.Add(chefPath);
        var roleNames = new Dictionary<int, string>
        {
            [doctor.Id] = "Doctor",
            [rescuer.Id] = "Rescuer",
            [cook.Id] = "Cook",
            [butcher.Id] = "Butcher",
            [brewer.Id] = "Brewer",
            [campCook.Id] = "Camp Cook",
            [chef.Id] = "Chef",
        };
        var pathNames = new Dictionary<int, string> { [chefPath.Id] = "Chef" };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        string[] expectedPaths = { "", "", "Chef" };
        string[] expectedRoles =
        {
            "Doctor, Cook",
            "Rescuer",
            // Repeat-champion spreading hands Brewer's championship to the
            // trainee, so its first-pick bonus leads the trainee's order.
            "Brewer, Camp Cook, Butcher",
        };
        for (int pawnIndex = 0; pawnIndex < colony.Pawns.Count; pawnIndex++)
        {
            await Assert.That(NamesOfPaths(plan, pawnIndex, pathNames))
                .IsEqualTo(expectedPaths[pawnIndex]);
            await Assert.That(NamesOfRoles(plan, pawnIndex, roleNames))
                .IsEqualTo(expectedRoles[pawnIndex]);
        }
    }

    [Test]
    public async Task Slice04_OrdersTargetsThroughAssignedAndVirtualAnchors()
    {
        /*
         * Default role recommendation order: Warden > Artist > Tailor > Smith >
         * Researcher > Fabricator > Drug Maker > Crafter.
         * Training paths: Fabricator = Tailor[0,21), Smith[0,21),
         * Fabricator[15,21), anchored after Warden; Drug Maker = Tailor[0,21),
         * Smith[0,21), Drug Maker[15,21), anchored after Warden; Crafter =
         * Tailor[0,21), Smith[0,21), Crafter[15,21), anchored after Drug Maker;
         * Smith = Tailor[0,8), Smith[8,21), anchored before Fabricator.
         * Role scales: Warden and Artist direct 1; Tailor interest-only; Smith,
         * Fabricator, Drug Maker, and Crafter training 1; Researcher direct 1.
         * Final per-pawn scores retain Artist immediately after Warden, while
         * the anchored targets form the following training block.
         */
        RoleView warden = SkilledRole(50, "Wardening", "Social");
        warden.HolderMode = RoleHolderMode.Custom;
        warden.RequiredTotal = 1;
        RoleView artist = SkilledRole(51, "ArtMaking", "Artistic");
        artist.HolderMode = RoleHolderMode.Custom;
        artist.RequiredTotal = 1;
        RoleView tailor = CraftingRole(52, "Tailoring");
        tailor.HolderMode = RoleHolderMode.Custom;
        RoleView smith = CraftingRole(53, "Smithing");
        smith.HolderMode = RoleHolderMode.Custom;
        smith.RequiredTotal = 1;
        smith.TrainingWaivers = 1;
        RoleView researcher = SkilledRole(54, "Researching", "Intellectual");
        researcher.HolderMode = RoleHolderMode.Custom;
        researcher.RequiredTotal = 1;
        RoleView fabricator = CraftingRole(55, "Fabrication");
        fabricator.HolderMode = RoleHolderMode.Custom;
        fabricator.RequiredTotal = 1;
        fabricator.TrainingWaivers = 1;
        RoleView drugMaker = CraftingRole(56, "DrugMaking");
        drugMaker.HolderMode = RoleHolderMode.Custom;
        drugMaker.RequiredTotal = 1;
        drugMaker.TrainingWaivers = 1;
        RoleView crafter = CraftingRole(57, "CraftingWork");
        crafter.HolderMode = RoleHolderMode.Custom;
        crafter.RequiredTotal = 1;
        crafter.TrainingWaivers = 1;

        PathView fabricatorPath = RecsTestBed.Path(
            60, (tailor.Id, 0, 21), (smith.Id, 0, 21),
            (fabricator.Id, 15, 21));
        fabricatorPath.AnchorRoleId = warden.Id;
        fabricatorPath.AnchorBefore = false;
        PathView drugMakerPath = RecsTestBed.Path(
            61, (tailor.Id, 0, 21), (smith.Id, 0, 21),
            (drugMaker.Id, 15, 21));
        drugMakerPath.AnchorRoleId = warden.Id;
        drugMakerPath.AnchorBefore = false;
        PathView crafterPath = RecsTestBed.Path(
            63, (tailor.Id, 0, 21), (smith.Id, 0, 21),
            (crafter.Id, 15, 21));
        crafterPath.AnchorRoleId = drugMaker.Id;
        crafterPath.AnchorBefore = false;
        PathView smithPath = RecsTestBed.Path(
            62, (tailor.Id, 0, 8), (smith.Id, 8, 21));
        smithPath.AnchorRoleId = fabricator.Id;
        smithPath.AnchorBefore = true;

        PawnView lateGameCrafter = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Crafting"] = (16, SignalBucket.Neutral),
                ["Social"] = (10, SignalBucket.Neutral),
                ["Artistic"] = (10, SignalBucket.Neutral),
                ["Intellectual"] = (4, SignalBucket.Neutral),
            },
            ("Wardening", SignalBucket.Neutral),
            ("ArtMaking", SignalBucket.Neutral),
            ("Tailoring", SignalBucket.Neutral),
            ("Smithing", SignalBucket.Neutral),
            ("Researching", SignalBucket.Awful),
            ("Fabrication", SignalBucket.Neutral),
            ("DrugMaking", SignalBucket.Neutral),
            ("CraftingWork", SignalBucket.Neutral));
        PawnView earlyCrafter = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Crafting"] = (4, SignalBucket.Strong),
                ["Intellectual"] = (10, SignalBucket.Neutral),
            },
            ("Wardening", SignalBucket.Awful),
            ("ArtMaking", SignalBucket.Awful),
            ("Tailoring", SignalBucket.Neutral),
            ("Smithing", SignalBucket.Neutral),
            ("Researching", SignalBucket.Neutral),
            ("Fabrication", SignalBucket.Awful),
            ("DrugMaking", SignalBucket.Awful),
            ("CraftingWork", SignalBucket.Awful));

        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView>
            {
                warden, artist, tailor, smith, researcher, fabricator, drugMaker,
                crafter,
            },
            lateGameCrafter,
            earlyCrafter);
        colony.Paths.AddRange(new[]
        {
            fabricatorPath, drugMakerPath, crafterPath, smithPath,
        });
        var roleNames = new Dictionary<int, string>
        {
            [warden.Id] = "Warden",
            [artist.Id] = "Artist",
            [tailor.Id] = "Tailor",
            [smith.Id] = "Smith",
            [researcher.Id] = "Researcher",
            [fabricator.Id] = "Fabricator",
            [drugMaker.Id] = "Drug Maker",
            [crafter.Id] = "Crafter",
        };
        var pathNames = new Dictionary<int, string>
        {
            [fabricatorPath.Id] = "Fabricator",
            [drugMakerPath.Id] = "Drug Maker",
            [crafterPath.Id] = "Crafter",
            [smithPath.Id] = "Smith",
        };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        string[] expectedPaths = { "Fabricator, Drug Maker, Crafter", "Smith" };
        string[] expectedRoles =
        {
            "Warden, Artist, Fabricator, Drug Maker, Crafter, Smith, Tailor",
            "Researcher, Tailor",
        };
        for (int pawnIndex = 0; pawnIndex < colony.Pawns.Count; pawnIndex++)
        {
            await Assert.That(NamesOfPaths(plan, pawnIndex, pathNames))
                .IsEqualTo(expectedPaths[pawnIndex]);
            await Assert.That(NamesOfRoles(plan, pawnIndex, roleNames))
                .IsEqualTo(expectedRoles[pawnIndex]);
        }
    }

    [Test]
    public async Task Slice04_DirectSpecialistSurvivesAndPrecedesItsCoveringTrainer()
    {
        /*
         * Default role recommendation order: Crafter > Fabricator.
         * Training paths: Fabricator = Crafter[0,21), Fabricator[8,21),
         * anchored before Crafter.
         * Role scales: Crafter direct 1; Fabricator direct 1.
         */
        RoleView crafter = SkilledRole(
            70, "CraftingWork", "Crafting", "Fabricate", "Stonecut");
        crafter.HolderMode = RoleHolderMode.Custom;
        crafter.RequiredTotal = 1;
        RoleView fabricator = SkilledRole(
            71, "Fabrication", "Crafting", "Fabricate");
        fabricator.HolderMode = RoleHolderMode.Custom;
        fabricator.RequiredTotal = 1;
        PathView path = RecsTestBed.Path(
            72, (crafter.Id, 0, 21), (fabricator.Id, 8, 21));
        path.AnchorRoleId = crafter.Id;

        PawnView pawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Crafting"] = (12, SignalBucket.Neutral),
            },
            ("CraftingWork", SignalBucket.Neutral),
            ("Fabrication", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { crafter, fabricator }, pawn);
        colony.Paths.Add(path);
        var roleNames = new Dictionary<int, string>
        {
            [crafter.Id] = "Crafter",
            [fabricator.Id] = "Fabricator",
        };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        await Assert.That(NamesOfPaths(
                plan, 0, new Dictionary<int, string> { [path.Id] = "Fabricator" }))
            .IsEqualTo("Fabricator");
        await Assert.That(NamesOfRoles(plan, 0, roleNames))
            .IsEqualTo("Fabricator, Crafter");
    }

    [Test]
    public async Task Slice06_OrdersConnectedTargetsWithoutLeadDiversification()
    {
        /*
         * Default role recommendation order: Tailor > Smith > Fabricator > Crafter.
         * Training paths: Tailor = Crafter[0,21), Tailor[2,21), anchored before
         * Crafter; Smith = Crafter[0,21), Smith[4,21), anchored before Crafter;
         * Fabricator = Crafter[0,21), Smith[4,21), Fabricator[8,21), anchored
         * before Crafter.
         * Role scales: all four roles are interest-only; Strong signal supplies
         * their surplus memberships.
         * With lead diversification disabled, ordinary target minima and
         * recommendation scores order every qualified pawn consistently. The
         * weak fourth pawn remains below Fabricator's target minimum.
         */
        RoleView tailor = CraftingRole(80, "Tailoring");
        tailor.HolderMode = RoleHolderMode.Custom;
        RoleView smith = CraftingRole(81, "Smithing");
        smith.HolderMode = RoleHolderMode.Custom;
        RoleView fabricator = CraftingRole(82, "Fabrication");
        fabricator.HolderMode = RoleHolderMode.Custom;
        RoleView crafter = CraftingRole(83, "CraftingWork");
        crafter.HolderMode = RoleHolderMode.Custom;
        PathView tailorPath = RecsTestBed.Path(
            90, (crafter.Id, 0, 21), (tailor.Id, 2, 21));
        tailorPath.AnchorRoleId = crafter.Id;
        PathView smithPath = RecsTestBed.Path(
            91, (crafter.Id, 0, 21), (smith.Id, 4, 21));
        smithPath.AnchorRoleId = crafter.Id;
        PathView fabricatorPath = RecsTestBed.Path(
            92, (crafter.Id, 0, 21), (smith.Id, 4, 21),
            (fabricator.Id, 8, 21));
        fabricatorPath.AnchorRoleId = crafter.Id;

        PawnView best = QualifiedCrafter(15);
        PawnView second = QualifiedCrafter(14);
        PawnView third = QualifiedCrafter(13);
        PawnView weak = QualifiedCrafter(4);
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { tailor, smith, fabricator, crafter },
            best,
            second,
            third,
            weak);
        colony.Paths.AddRange(new[] { tailorPath, smithPath, fabricatorPath });
        var roleNames = new Dictionary<int, string>
        {
            [tailor.Id] = "Tailor",
            [smith.Id] = "Smith",
            [fabricator.Id] = "Fabricator",
            [crafter.Id] = "Crafter",
        };
        var pathNames = new Dictionary<int, string>
        {
            [tailorPath.Id] = "Tailor",
            [smithPath.Id] = "Smith",
            [fabricatorPath.Id] = "Fabricator",
        };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        string[] expectedRoles =
        {
            "Fabricator, Smith, Tailor, Crafter",
            "Fabricator, Smith, Tailor, Crafter",
            "Fabricator, Smith, Tailor, Crafter",
            "Smith, Tailor, Crafter",
        };
        string[] expectedPaths =
        {
            "Tailor, Smith, Fabricator",
            "Tailor, Smith, Fabricator",
            "Tailor, Smith, Fabricator",
            "Fabricator, Tailor, Smith",
        };
        for (int pawnIndex = 0; pawnIndex < colony.Pawns.Count; pawnIndex++)
        {
            await Assert.That(NamesOfPaths(plan, pawnIndex, pathNames))
                .IsEqualTo(expectedPaths[pawnIndex]);
            await Assert.That(NamesOfRoles(plan, pawnIndex, roleNames))
                .IsEqualTo(expectedRoles[pawnIndex]);
        }
    }

    [Test]
    public async Task Slice07_PreservesAutomaticHunterFireAndRetainedWork()
    {
        /*
         * Default role recommendation order: Core > Doctor > Basics > Hunter >
         * Hauler > Fire Blocker.
         * Training paths: none.
         * Role scales: Doctor direct 1; Core and Basics automatic; Hunter uses
         * its weapon/tier policy; Hauler is retained unskilled work; Fire Blocker
         * is granted by fire fear.
         */
        RoleView core = RecsTestBed.Unskilled(
            100, "CoreWork", "DoctorJob", "CoreJob");
        core.AutoAssign = true;
        RoleView doctor = SkilledRole(
            101, "Doctoring", "Medicine", "DoctorJob");
        doctor.HolderMode = RoleHolderMode.Custom;
        doctor.RequiredTotal = 1;
        RoleView basics = RecsTestBed.Unskilled(102, "BasicWorker");
        basics.AutoAssign = true;
        RoleView hunter = SkilledRole(103, "Hunting", "Shooting");
        hunter.Hunting = true;
        RoleView hauler = RecsTestBed.Unskilled(104, "Hauling");
        RoleView fireBlocker = RecsTestBed.Unskilled(105, "Firefighting");
        fireBlocker.Blocker = true;

        PawnView pawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Medicine"] = (12, SignalBucket.Neutral),
                ["Shooting"] = (12, SignalBucket.Neutral),
            },
            ("CoreWork", SignalBucket.Neutral),
            ("Doctoring", SignalBucket.Neutral),
            ("BasicWorker", SignalBucket.Neutral),
            ("Hunting", SignalBucket.Neutral),
            ("Hauling", SignalBucket.Neutral),
            ("Firefighting", SignalBucket.Neutral));
        pawn.HasRangedWeapon = true;
        pawn.ShootingLevel = 12;
        pawn.FireFear = true;
        pawn.Existing.Add(new AssignmentView
        {
            RoleId = hauler.Id,
            Enabled = false,
        });
        pawn.Existing.Add(new AssignmentView
        {
            RoleId = doctor.Id,
            Enabled = true,
            Pinned = true,
        });
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView>
            {
                core, doctor, basics, hunter, hauler, fireBlocker,
            },
            pawn);
        colony.HunterRoleId = hunter.Id;
        colony.FireBlockerRoleId = fireBlocker.Id;
        var roleNames = new Dictionary<int, string>
        {
            [core.Id] = "Core",
            [doctor.Id] = "Doctor",
            [basics.Id] = "Basics",
            [hunter.Id] = "Hunter",
            [hauler.Id] = "Hauler",
            [fireBlocker.Id] = "Fire Blocker",
        };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        await Assert.That(NamesOfPaths(
                plan, 0, new Dictionary<int, string>()))
            .IsEqualTo("");
        await Assert.That(NamesOfRoles(plan, 0, roleNames))
            .IsEqualTo("Fire Blocker, Core, Basics, Hunter, Hauler, Doctor");
    }

    [Test]
    public async Task Slice07_DoesNotAddHunterWhenFinalRoleAlreadyCoversIt()
    {
        /*
         * Default role recommendation order: Sharpshooter > Hunter.
         * Training paths: none.
         * Role scales: Sharpshooter custom direct 1; Hunter uses its existing
         * weapon/tier policy but is redundant when Sharpshooter is selected.
         */
        RoleView sharpshooter = SkilledRole(
            106, "Sharpshooting", "Shooting", "Hunting", "MarksmanWork");
        sharpshooter.HolderMode = RoleHolderMode.Custom;
        sharpshooter.RequiredTotal = 1;
        RoleView hunter = SkilledRole(107, "Hunting", "Shooting", "Hunting");
        hunter.Hunting = true;
        PawnView pawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Shooting"] = (14, SignalBucket.Strong),
            },
            ("Sharpshooting", SignalBucket.Strong),
            ("Hunting", SignalBucket.Strong));
        pawn.HasRangedWeapon = true;
        pawn.ShootingLevel = 14;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { sharpshooter, hunter }, pawn);
        colony.HunterRoleId = hunter.Id;
        var roleNames = new Dictionary<int, string>
        {
            [sharpshooter.Id] = "Sharpshooter",
            [hunter.Id] = "Hunter",
        };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        await Assert.That(NamesOfPaths(
                plan, 0, new Dictionary<int, string>()))
            .IsEqualTo("");
        await Assert.That(NamesOfRoles(plan, 0, roleNames))
            .IsEqualTo("Sharpshooter");
    }

    private static PawnView PawnWith(
        (int Level, SignalBucket Verdict) medicine,
        (int Level, SignalBucket Verdict) cooking)
    {
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = medicine.Level;
        pawn.SignalBuckets["Medicine"] = medicine.Verdict;
        pawn.SkillLevels["Cooking"] = cooking.Level;
        pawn.SignalBuckets["Cooking"] = cooking.Verdict;
        return pawn;
    }

    private static RoleView CraftingRole(int id, string workType)
        => SkilledRole(id, workType, "Crafting");

    private static RoleView SkilledRole(
        int id,
        string workType,
        string skill,
        params string[] coverage)
    {
        RoleView role = RecsTestBed.Role(id, workType, coverage);
        role.PrimarySkill = skill;
        role.Skills.Add(new RoleSkillView
        {
            SkillDefName = skill,
            Primary = true,
        });
        return role;
    }

    private static PawnView CraftingPawn(
        int level,
        params (string WorkType, SignalBucket Verdict)[] signals)
    {
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = level;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        pawn.WorkTypeSignalBuckets = signals.ToDictionary(
            signal => signal.WorkType,
            signal => signal.Verdict);
        foreach ((string workType, _) in signals)
            pawn.CapableWorkTypes.Add(workType);
        return pawn;
    }

    private static PawnView RolePawn(
        int cooking,
        int? medicine,
        params (string WorkType, SignalBucket Verdict)[] signals)
    {
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = cooking;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Neutral;
        if (medicine.HasValue)
        {
            pawn.SkillLevels["Medicine"] = medicine.Value;
            pawn.SignalBuckets["Medicine"] = SignalBucket.Neutral;
        }
        pawn.WorkTypeSignalBuckets = signals.ToDictionary(
            signal => signal.WorkType,
            signal => signal.Verdict);
        foreach ((string workType, _) in signals)
            pawn.CapableWorkTypes.Add(workType);
        return pawn;
    }

    private static PawnView MultiSkillPawn(
        IReadOnlyDictionary<string, (int Level, SignalBucket Verdict)> skills,
        params (string WorkType, SignalBucket Verdict)[] workTypes)
    {
        PawnView pawn = RecsTestBed.Pawn();
        foreach (KeyValuePair<string, (int Level, SignalBucket Verdict)> skill
                 in skills)
        {
            pawn.SkillLevels[skill.Key] = skill.Value.Level;
            pawn.SignalBuckets[skill.Key] = skill.Value.Verdict;
        }
        pawn.WorkTypeSignalBuckets = workTypes.ToDictionary(
            workType => workType.WorkType,
            workType => workType.Verdict);
        foreach ((string workType, _) in workTypes)
            pawn.CapableWorkTypes.Add(workType);
        return pawn;
    }

    private static PawnView QualifiedCrafter(int level)
        => MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Crafting"] = (level, SignalBucket.Strong),
            },
            ("Tailoring", SignalBucket.Neutral),
            ("Smithing", SignalBucket.Neutral),
            ("Fabrication", SignalBucket.Neutral),
            ("CraftingWork", SignalBucket.Neutral));

    private static string NamesOfRoles(
        RecommendationPlan plan,
        int pawnIndex,
        IReadOnlyDictionary<int, string> names)
        => string.Join(", ", Enumerable.Range(0, plan.RoleCountAt(pawnIndex))
            .Select(index => names[plan.RoleAt(pawnIndex, index)]));

    private static string NamesOfPaths(
        RecommendationPlan plan,
        int pawnIndex,
        IReadOnlyDictionary<int, string> names)
        => string.Join(", ", Enumerable.Range(0, plan.PathCountAt(pawnIndex))
            .Select(index => names[plan.PathAt(pawnIndex, index)]));
}
