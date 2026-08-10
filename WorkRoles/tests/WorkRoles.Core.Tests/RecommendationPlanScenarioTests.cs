using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecommendationPlanScenarioTests
{
    [Test]
    public async Task ScaleRequiredTotalIncludesTrainingWaiverAssignments()
    {
        RoleView target = CraftingRole(100, "TargetWork");
        target.Skills.Add(new RoleSkillView
        {
            SkillDefName = "Intellectual",
            RequiredContent = 1,
        });
        target.Scale = new HolderScale();
        target.Scale.RequiredTotals[0] = 3;
        target.Scale.TrainingWaivers[0] = 1;
        target.Scale.Max[0] = 4;

        RoleView craftTrainee = CraftingRole(101, "CraftTraineeWork");
        RoleView researchTrainee = SkilledRole(
            102, "ResearchTraineeWork", "Intellectual");

        PathView path = RecsTestBed.Path(
            200,
            (craftTrainee.Id, 0, 15),
            (researchTrainee.Id, 0, 15),
            (target.Id, 15, 21));
        path.AnchorRoleId = target.Id;
        PathView lessSuitablePath = RecsTestBed.Path(
            201,
            (craftTrainee.Id, 0, 15),
            (researchTrainee.Id, 0, 15),
            (target.Id, 18, 21));
        lessSuitablePath.AnchorRoleId = target.Id;

        PawnView firstDirect = CraftingPawn(
            16,
            ("TargetWork", SignalBucket.Neutral),
            ("CraftTraineeWork", SignalBucket.Neutral),
            ("ResearchTraineeWork", SignalBucket.Neutral));
        firstDirect.SkillLevels["Intellectual"] = 16;
        firstDirect.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        PawnView secondDirect = CraftingPawn(
            15,
            ("TargetWork", SignalBucket.Neutral),
            ("CraftTraineeWork", SignalBucket.Neutral),
            ("ResearchTraineeWork", SignalBucket.Neutral));
        secondDirect.SkillLevels["Intellectual"] = 15;
        secondDirect.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        PawnView trainingWaiver = CraftingPawn(
            10,
            ("TargetWork", SignalBucket.Neutral),
            ("CraftTraineeWork", SignalBucket.Neutral),
            ("ResearchTraineeWork", SignalBucket.Neutral));
        trainingWaiver.SkillLevels["Intellectual"] = 11;
        trainingWaiver.SignalBuckets["Intellectual"] = SignalBucket.Neutral;

        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { target, craftTrainee, researchTrainee },
            firstDirect,
            secondDirect,
            trainingWaiver);
        colony.Paths.Add(path);
        colony.Paths.Add(lessSuitablePath);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        var roleNames = new Dictionary<int, string>
        {
            [target.Id] = "Target",
            [craftTrainee.Id] = "Craft trainee",
            [researchTrainee.Id] = "Research trainee",
        };

        await Assert.That(NamesOfRoles(plan, 0, roleNames)).IsEqualTo("Target");
        await Assert.That(NamesOfRoles(plan, 1, roleNames)).IsEqualTo("Target");
        await Assert.That(NamesOfRoles(plan, 2, roleNames))
            .IsEqualTo("Research trainee, Craft trainee");
        await Assert.That(plan.PathAt(2, 0)).IsEqualTo(path.Id);
        await Assert.That(plan.PathActivatedAt(2, 0)).IsTrue();
        await Assert.That(plan.TryGetExplanation(
            0, target.Id, out RoleRecommendationExplanation first)).IsTrue();
        await Assert.That(first.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(first.SelectionSlot).IsEqualTo(1);
        await Assert.That(first.SelectionSlotCount).IsEqualTo(3);
        await Assert.That(plan.TryGetExplanation(
            1, target.Id, out RoleRecommendationExplanation second)).IsTrue();
        await Assert.That(second.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(second.SelectionSlot).IsEqualTo(2);
        await Assert.That(second.SelectionSlotCount).IsEqualTo(3);
        await Assert.That(plan.TryGetExplanation(
            2,
            craftTrainee.Id,
            out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.RelatedRoleId).IsEqualTo(target.Id);
        await Assert.That(explanation.RequiredTotal).IsEqualTo(3);
        await Assert.That(explanation.TrainingWaivers).IsEqualTo(1);
        await Assert.That(explanation.ConfiguredMaximum).IsEqualTo(4);
        await Assert.That(explanation.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.TrainingWaiver);
        await Assert.That(explanation.CandidateRank).IsEqualTo(3);
        await Assert.That(explanation.CandidateCount).IsEqualTo(3);
        await Assert.That(explanation.StageRank).IsEqualTo(1);
        await Assert.That(explanation.SelectionSlot).IsEqualTo(3);
        await Assert.That(explanation.SelectionSlotCount).IsEqualTo(3);
        await Assert.That(explanation.TrainingSkills.Count).IsEqualTo(2);
        await Assert.That(explanation.TrainingSkills[0].SkillDefName)
            .IsEqualTo("Crafting");
        await Assert.That(explanation.TrainingSkills[0].PawnLevel).IsEqualTo(10);
        await Assert.That(explanation.TrainingSkills[0].TargetMinimum)
            .IsEqualTo(15);
        await Assert.That(explanation.TrainingSkills[1].SkillDefName)
            .IsEqualTo("Intellectual");
        await Assert.That(explanation.TrainingSkills[1].PawnLevel).IsEqualTo(11);
        await Assert.That(explanation.TrainingSkills[1].TargetMinimum)
            .IsEqualTo(15);
        await Assert.That(plan.TryGetExplanation(
            2,
            researchTrainee.Id,
            out RoleRecommendationExplanation researchExplanation)).IsTrue();
        await Assert.That(researchExplanation.RelatedRoleId)
            .IsEqualTo(target.Id);
        await Assert.That(researchExplanation.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.TrainingWaiver);
        await Assert.That(researchExplanation.SelectionSlot).IsEqualTo(3);
        await Assert.That(researchExplanation.SelectionSlotCount).IsEqualTo(3);
        await Assert.That(researchExplanation.TrainingSkills.Count).IsEqualTo(2);
        await Assert.That(researchExplanation.TrainingSkills[0].TargetMinimum)
            .IsEqualTo(15);
        await Assert.That(researchExplanation.TrainingSkills[1].TargetMinimum)
            .IsEqualTo(15);
    }

    [Test]
    public async Task UnskilledStrategyAssignsEveryCapablePawnAndNamesAChampion()
    {
        // Through the real projection: a skill-less role (Hauling) with the
        // Unskilled strategy and one required champion per band.
        RecommendationPlan PlanFor(ScaleMode mode)
        {
            var recs = new RecsProjection().WorkType("Hauling", null, 100);
            recs.RoleByWorkType(
                1, mode, RecsProjection.Scale(requiredTotal: 1), "Hauling");
            PawnView Worker() =>
                new PawnView { CapableWorkTypes = { "Hauling" } };
            return recs.Plan(Worker(), Worker(), Worker());
        }

        RecommendationPlan unskilledPlan = PlanFor(ScaleMode.Unskilled);
        int held = 0;
        int champions = 0;
        for (int pawn = 0; pawn < 3; pawn++)
        {
            if (RecsProjection.Holds(unskilledPlan, pawn, 1)) held++;
            if (unskilledPlan.TryGetExplanation(
                    pawn, 1, out RoleRecommendationExplanation explanation)
                && explanation.SelectionStage
                    == RecommendationSelectionStage.Required)
                champions++;
        }
        await Assert.That(held).IsEqualTo(3);
        await Assert.That(champions).IsEqualTo(1);

        // Without the Unskilled strategy the same skill-less role is a retained
        // chore: nobody is assigned it who does not already hold it. This is the
        // gap the Unskilled strategy closes.
        RecommendationPlan skilledPlan = PlanFor(ScaleMode.Skilled);
        int skilledHeld = 0;
        for (int pawn = 0; pawn < 3; pawn++)
            if (RecsProjection.Holds(skilledPlan, pawn, 1)) skilledHeld++;
        await Assert.That(skilledHeld).IsEqualTo(0);
    }

    [Test]
    public async Task UnskilledNoMinScaleAssignsEveryoneThroughTheRealProjection()
    {
        // Full recommendation pipeline: role sources -> catalog projection ->
        // colony -> plan, the same path the game adapter feeds. Grunt is an
        // Unskilled "NoMin" role: an all-zero scale, including Max. Everyone
        // capable must still be recommended it.
        var jobs = new FakeCatalog()
            .WithWorkType("Hauling", "Haul")
            .WithWorkType("Cleaning", "Clean");
        var allZero = new HolderScale();
        Array.Fill(allZero.Max, 0);
        var grunt = new RecommendationRoleSource
        {
            Id = 1,
            Mode = ScaleMode.Unskilled,
            Scale = allZero,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkType, "Hauling"),
                new JobEntry(JobEntryKind.WorkType, "Cleaning"),
            },
        };
        RecommendationCatalogProjection projection =
            RecommendationCatalogBuilder.Build(
                new[] { grunt },
                Array.Empty<PathView>(),
                jobs,
                new Dictionary<string, int>
                {
                    ["Hauling"] = 100,
                    ["Cleaning"] = 50,
                },
                UnskilledJobProfiles());
        PawnView Worker() => new PawnView
        {
            CapableWorkTypes = { "Hauling", "Cleaning" },
        };
        ColonyView colony = projection.CreateColony(
            new[] { grunt.Id }, new[] { Worker(), Worker(), Worker() });

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        int held = 0;
        for (int pawn = 0; pawn < 3; pawn++)
            if (Holds(plan, pawn, grunt.Id)) held++;
        await Assert.That(held).IsEqualTo(3);
    }

    private static JobProfileIndex UnskilledJobProfiles()
    {
        var builder = new JobProfileIndexBuilder();
        var none = Array.Empty<JobProfileSkillSource>();
        builder.AddWorkType(1, "Hauling", none, new[] { "Haul" });
        builder.AddWorkType(2, "Cleaning", none, new[] { "Clean" });
        builder.AddGiver("Haul", 1, none,
            hasCuratedXp: true, curatedXpSkillDefNames: Array.Empty<string>());
        builder.AddGiver("Clean", 2, none,
            hasCuratedXp: true, curatedXpSkillDefNames: Array.Empty<string>());
        return builder.Build();
    }

    [Test]
    public async Task SurplusMedicPawnGetsNeverTraineeRoleViaPath()
    {
        // Through the real projection: Medic is a Never training role
        // controlled by Doctor; a surplus medical pawn in the Medic band should
        // still receive it via the path.
        var recs = new RecsProjection()
            .WorkType("Doctoring", "Medicine", 100, "Operate", "Tend");
        RecommendationRoleSource doctor = recs.RoleByWorkType(
            1, ScaleMode.Skilled, RecsProjection.Scale(requiredTotal: 1),
            "Doctoring");
        RecommendationRoleSource medic = recs.RoleByGiver(
            2, ScaleMode.Never, null, "Tend");
        PathView path = RecsTestBed.Path(
            10, (medic.Id, 5, 15), (doctor.Id, 15, 21));
        path.AnchorRoleId = doctor.Id;
        recs.Path(path);

        RecommendationPlan plan = recs.Plan(
            MedicPawn(18, SignalBucket.Strong),
            MedicPawn(11, SignalBucket.Strong));

        // Strong doctor fills the required slot; the surplus medical pawn gets
        // the Never Medic trainee via the path, not nothing.
        await Assert.That(RecsProjection.Holds(plan, 0, doctor.Id)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 1, medic.Id)).IsTrue();
    }

    private static PawnView MedicPawn(int medicine, SignalBucket signal)
    {
        var pawn = new PawnView { CapableWorkTypes = { "Doctoring" } };
        pawn.SkillLevels["Medicine"] = medicine;
        pawn.SignalBuckets["Medicine"] = signal;
        return pawn;
    }

    [Test]
    public async Task NeverTraineeExplainsItsControllingTrainingTarget()
    {
        // Medic/Nurse are Never training roles controlled by Doctor. A pawn that
        // does not receive Medic must not be told the role is "not configured";
        // its explanation names the Doctor that controls it.
        var recs = new RecsProjection()
            .WorkType("Doctoring", "Medicine", 100, "Operate", "Tend", "Feed");
        RecommendationRoleSource doctor = recs.RoleByGiver(
            1, ScaleMode.Skilled, RecsProjection.Scale(requiredTotal: 1),
            "Operate", "Tend", "Feed");
        RecommendationRoleSource medic = recs.RoleByGiver(
            2, ScaleMode.Never, null, "Tend", "Feed");
        PathView path = RecsTestBed.Path(
            10, (medic.Id, 5, 15), (doctor.Id, 15, 21));
        path.AnchorRoleId = doctor.Id;
        recs.Path(path);

        // A strong doctor fills Doctor directly and never becomes a Medic; the
        // pawn currently holds Medic, so it gets a Medic explanation.
        PawnView doc = MedicPawn(18, SignalBucket.Strong);
        doc.Existing.Add(new AssignmentView { RoleId = medic.Id, Enabled = true });
        RecommendationPlan plan = recs.Plan(doc);

        await Assert.That(RecsProjection.Holds(plan, 0, doctor.Id)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 0, medic.Id)).IsFalse();
        await Assert.That(plan.TryGetExplanation(
            0, medic.Id, out RoleRecommendationExplanation medicExplanation))
            .IsTrue();
        await Assert.That(medicExplanation.RejectReason)
            .IsEqualTo(PickRejectReason.ControlledByTarget);
        await Assert.That(medicExplanation.RelatedRoleId).IsEqualTo(doctor.Id);
    }

    [Test]
    public async Task PinnedAssignmentToANeverRoleIsCarriedIntoRecommendations()
    {
        // Through the real projection: a pawn pinned to a Never role keeps it.
        var recs = new RecsProjection().WorkType("Hauling", null, 100);
        RecommendationRoleSource never = recs.RoleByWorkType(
            1, ScaleMode.Never, null, "Hauling");
        var pawn = new PawnView { CapableWorkTypes = { "Hauling" } };
        pawn.Existing.Add(new AssignmentView
        {
            RoleId = never.Id, Enabled = true, Pinned = true,
        });

        RecommendationPlan plan = recs.Plan(pawn);

        await Assert.That(RecsProjection.Holds(plan, 0, never.Id)).IsTrue();
    }

    [Test]
    public async Task CoverageKeepsRequiredHoldersOfACoveredSubRole()
    {
        // Through the real projection: Grunt covers Hauling + Cleaning; Hauler
        // is the Hauling subset.
        var recs = new RecsProjection()
            .WorkType("Hauling", null, 100)
            .WorkType("Cleaning", null, 50);
        RecommendationRoleSource grunt = recs.RoleByWorkType(
            1, ScaleMode.Unskilled, RecsProjection.Scale(), "Hauling", "Cleaning");
        RecommendationRoleSource hauler = recs.RoleByWorkType(
            2, ScaleMode.Unskilled, RecsProjection.Scale(requiredTotal: 2),
            "Hauling");
        PawnView Capable() => new PawnView
        {
            CapableWorkTypes = { "Hauling", "Cleaning" },
        };

        RecommendationPlan plan = recs.Plan(
            Capable(), Capable(), Capable(), Capable());

        int gruntHeld = 0;
        int haulerHeld = 0;
        for (int pawn = 0; pawn < 4; pawn++)
        {
            if (RecsProjection.Holds(plan, pawn, grunt.Id)) gruntHeld++;
            if (RecsProjection.Holds(plan, pawn, hauler.Id)) haulerHeld++;
        }
        // Grunt covers everyone; Hauler's surplus folds under it, but its two
        // required holders survive and coexist with Grunt.
        await Assert.That(gruntHeld).IsEqualTo(4);
        await Assert.That(haulerHeld).IsEqualTo(2);
    }

    [Test]
    public async Task AutoAssignRolesFollowTheDefaultOrderNotExistingOrder()
    {
        // Auto-assign roles are automatic, not user-placed: they follow the
        // default recommendation order (front), even if the pawn's existing
        // order lists a late chore ahead of them.
        var recs = new RecsProjection()
            .WorkType("Essential", null, 1000, "Ess")
            .WorkType("Hauling", null, 50, "Haul");
        recs.RoleByWorkType(1, ScaleMode.Skilled, RecsProjection.Scale(), "Essential");
        recs.AutoAssign(1);
        recs.RoleByWorkType(2, ScaleMode.Unskilled, RecsProjection.Scale(), "Hauling");
        var pawn = new PawnView { CapableWorkTypes = { "Essential", "Hauling" } };
        // Existing order puts the late chore before the auto role.
        pawn.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
        pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = true });

        RecommendationPlan plan = recs.Plan(pawn);

        await Assert.That(plan.RoleAt(0, 0)).IsEqualTo(1);
    }

    [Test]
    public async Task UnskilledSubRolesFoldUnderACoveringUnskilledRole()
    {
        // Grunt (Unskilled NoMin) covers Hauling + Cleaning; Hauler and Cleaner
        // are req-0 Unskilled subsets. Everyone gets Grunt; the covered
        // sub-roles fold away entirely (no dedicated req to keep).
        var recs = new RecsProjection()
            .WorkType("Hauling", null, 100)
            .WorkType("Cleaning", null, 50);
        RecommendationRoleSource grunt = recs.RoleByWorkType(
            1, ScaleMode.Unskilled, RecsProjection.Scale(), "Hauling", "Cleaning");
        RecommendationRoleSource hauler = recs.RoleByWorkType(
            2, ScaleMode.Unskilled, RecsProjection.Scale(), "Hauling");
        RecommendationRoleSource cleaner = recs.RoleByWorkType(
            3, ScaleMode.Unskilled, RecsProjection.Scale(), "Cleaning");
        // Pawns already hold all three (as the real colony does): the covered
        // sub-roles must still fold, not be retained as chores.
        PawnView Worker()
        {
            var pawn = new PawnView
            {
                CapableWorkTypes = { "Hauling", "Cleaning" },
            };
            pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = true });
            pawn.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
            pawn.Existing.Add(new AssignmentView { RoleId = 3, Enabled = true });
            return pawn;
        }

        RecommendationPlan plan = recs.Plan(Worker(), Worker(), Worker());

        int gruntHeld = 0;
        int subHeld = 0;
        for (int pawn = 0; pawn < 3; pawn++)
        {
            if (RecsProjection.Holds(plan, pawn, grunt.Id)) gruntHeld++;
            if (RecsProjection.Holds(plan, pawn, hauler.Id)) subHeld++;
            if (RecsProjection.Holds(plan, pawn, cleaner.Id)) subHeld++;
        }
        await Assert.That(gruntHeld).IsEqualTo(3);
        await Assert.That(subHeld).IsEqualTo(0);
    }

    [Test]
    public async Task PinnedRoleKeepsItsExistingOrder()
    {
        // A pinned role is user-placed: it stays at its existing slot even when
        // the default order would sort it earlier.
        var recs = new RecsProjection()
            .WorkType("Essential", null, 1000, "Ess")
            .WorkType("Hauling", null, 50, "Haul");
        recs.RoleByWorkType(1, ScaleMode.Skilled, RecsProjection.Scale(), "Essential");
        recs.AutoAssign(1);
        RecommendationRoleSource pinned = recs.RoleByWorkType(
            2, ScaleMode.Skilled, RecsProjection.Scale(requiredTotal: 1),
            "Hauling");
        var pawn = new PawnView { CapableWorkTypes = { "Essential", "Hauling" } };
        pawn.SkillLevels["Hauling"] = 0;
        // Pinned to the late chore, listed first; the pin holds it ahead of the
        // auto role.
        pawn.Existing.Add(new AssignmentView
        {
            RoleId = pinned.Id, Enabled = true, Pinned = true,
        });
        pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = true });

        RecommendationPlan plan = recs.Plan(pawn);

        await Assert.That(plan.RoleAt(0, 0)).IsEqualTo(pinned.Id);
    }

    [Test]
    public async Task WaiverSlotFillsWithTraineeNotAWeakDirectTarget()
    {
        // Through the real projection: Artist covers all Art work (Sculpt +
        // Paint); Painter is the paint subset and Never (no own demand),
        // mirroring the shipped WS_PathArtist Painter -> Artist path.
        var recs = new RecsProjection()
            .WorkType("Art", "Artistic", 100, "Sculpt", "Paint");
        RecommendationRoleSource artist = recs.RoleByWorkType(
            1, ScaleMode.Skilled,
            RecsProjection.Scale(requiredTotal: 2, trainingWaivers: 1), "Art");
        RecommendationRoleSource painter = recs.RoleByGiver(
            2, ScaleMode.Never, null, "Paint");
        PathView path = RecsTestBed.Path(
            10, (painter.Id, 0, 8), (artist.Id, 8, 21));
        path.AnchorRoleId = artist.Id;
        recs.Path(path);

        RecommendationPlan plan = recs.Plan(ArtPawn(12), ArtPawn(5));

        // Slot 1 = strong direct Artist; slot 2 = the Painter trainee, never a
        // weak direct Artist.
        await Assert.That(RecsProjection.Holds(plan, 0, artist.Id)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 1, painter.Id)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 1, artist.Id)).IsFalse();
    }

    private static PawnView ArtPawn(int artistic)
    {
        var pawn = new PawnView { CapableWorkTypes = { "Art" } };
        pawn.SkillLevels["Artistic"] = artistic;
        pawn.SignalBuckets["Artistic"] = SignalBucket.Neutral;
        return pawn;
    }

    private static bool Holds(RecommendationPlan plan, int pawn, int roleId)
    {
        for (int index = 0; index < plan.RoleCountAt(pawn); index++)
            if (plan.RoleAt(pawn, index) == roleId) return true;
        return false;
    }

    [Test]
    public async Task ProtectedAssignmentOffsetsPublishedSelectionSlot()
    {
        RoleView role = CraftingRole(105, "TargetWork");
        RecsTestBed.Require(role, 2);
        PawnView protectedHolder = CraftingPawn(
            8, ("TargetWork", SignalBucket.Neutral));
        protectedHolder.Existing.Add(new AssignmentView
        {
            RoleId = role.Id,
            Enabled = true,
            Pinned = true,
        });
        PawnView selected = CraftingPawn(
            12, ("TargetWork", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { role }, protectedHolder, selected);

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        await Assert.That(plan.RoleAt(0, 0)).IsEqualTo(role.Id);
        await Assert.That(plan.RoleAt(1, 0)).IsEqualTo(role.Id);
        await Assert.That(plan.TryGetExplanation(
            1, role.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(explanation.SelectionSlot).IsEqualTo(2);
        await Assert.That(explanation.SelectionSlotCount).IsEqualTo(2);
    }

    [Test]
    public async Task TargetQualifiedWaiverSlotPublishesDirectTargetSelection()
    {
        // Through the real projection: single-skill Crafting target + trainee on
        // a path; a pawn already in the target band fills the waiver slot as a
        // direct target.
        var recs = new RecsProjection()
            .WorkType("TargetWork", "Crafting", 100)
            .WorkType("TraineeWork", "Crafting", 90);
        RecommendationRoleSource target = recs.RoleByWorkType(
            106, ScaleMode.Skilled,
            RecsProjection.Scale(requiredTotal: 1, trainingWaivers: 1),
            "TargetWork");
        RecommendationRoleSource trainee = recs.RoleByWorkType(
            107, ScaleMode.Skilled, RecsProjection.Scale(), "TraineeWork");
        recs.Path(RecsTestBed.Path(
            206, (trainee.Id, 0, 21), (target.Id, 8, 21)));
        var qualified = new PawnView
        {
            CapableWorkTypes = { "TargetWork", "TraineeWork" },
        };
        qualified.SkillLevels["Crafting"] = 20;
        qualified.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        var roleNames = new Dictionary<int, string>
        {
            [target.Id] = "Target",
            [trainee.Id] = "Trainee",
        };

        RecommendationPlan plan = recs.Plan(qualified);

        await Assert.That(NamesOfRoles(plan, 0, roleNames))
            .IsEqualTo("Target, Trainee");
        await Assert.That(plan.TryGetExplanation(
            0,
            target.Id,
            out RoleRecommendationExplanation targetExplanation)).IsTrue();
        await Assert.That(targetExplanation.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(targetExplanation.SelectionSlot).IsEqualTo(1);
        await Assert.That(targetExplanation.SelectionSlotCount).IsEqualTo(1);
        await Assert.That(plan.TryGetExplanation(
            0,
            trainee.Id,
            out RoleRecommendationExplanation traineeExplanation)).IsTrue();
        await Assert.That(traineeExplanation.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.None);
        await Assert.That(traineeExplanation.RelatedRoleId)
            .IsEqualTo(target.Id);
    }

    [Test]
    public async Task UnfilledTrainingWaiverPublishesRankedDirectFallback()
    {
        RoleView target = CraftingRole(110, "TargetWork");
        RecsTestBed.Require(target, 2, trainingWaivers: 1);
        RoleView trainee = CraftingRole(111, "TraineeWork");
        PathView path = RecsTestBed.Path(
            210, (trainee.Id, 0, 5), (target.Id, 10, 21));

        PawnView first = CraftingPawn(
            16,
            ("TargetWork", SignalBucket.Neutral),
            ("TraineeWork", SignalBucket.Neutral));
        PawnView fallback = CraftingPawn(
            7,
            ("TargetWork", SignalBucket.Neutral),
            ("TraineeWork", SignalBucket.Neutral));
        PawnView lowerRankedFallback = CraftingPawn(
            6,
            ("TargetWork", SignalBucket.Neutral),
            ("TraineeWork", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { target, trainee },
            first, fallback, lowerRankedFallback);
        colony.Paths.Add(path);

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        await Assert.That(plan.TryGetExplanation(
            1, target.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(explanation.CandidateRank).IsEqualTo(2);
        await Assert.That(explanation.CandidateCount).IsEqualTo(3);
        await Assert.That(explanation.StageRank).IsEqualTo(1);
        await Assert.That(explanation.SelectionSlot).IsEqualTo(2);
        await Assert.That(explanation.SelectionSlotCount).IsEqualTo(2);
        await Assert.That(plan.RoleCountAt(2)).IsEqualTo(0);
    }

    [Test]
    public async Task AllocatesConfiguredWantAndStrongSurplusWithoutAwfulPawns()
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
        RecsTestBed.Require(doctor, 2, trainingWaivers: 1);

        RoleView cook = RecsTestBed.Role(2, "Cooking");
        RecsTestBed.Require(cook, 2);

        RoleView hauler = RecsTestBed.Unskilled(3, "Hauling");

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
    public async Task SkillLevelPromotionMakesMedicASurplusAssignmentWithinItsBand()
    {
        RoleView doctor = SkilledRole(
            4, "Doctor", "Medicine", "Rescue", "Tend", "Operate");
        RoleView medic = SkilledRole(
            5, "Doctor", "Medicine", "Rescue", "Tend");
        RoleView nurse = SkilledRole(
            6, "Doctor", "Medicine", "Rescue");
        PathView path = RecsTestBed.Path(
            7, (nurse.Id, 0, 5), (medic.Id, 5, 15), (doctor.Id, 15, 21));

        PawnView doctorPawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Medicine"] = (16, SignalBucket.Neutral),
            },
            ("Doctor", SignalBucket.Neutral));
        PawnView medicPawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Medicine"] = (11, SignalBucket.Neutral),
            },
            ("Doctor", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { doctor, medic, nurse }, doctorPawn, medicPawn);
        colony.Paths.Add(path);
        var roleNames = new Dictionary<int, string>
        {
            [doctor.Id] = "Doctor",
            [medic.Id] = "Medic",
            [nurse.Id] = "Nurse",
        };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        await Assert.That(NamesOfRoles(plan, 0, roleNames)).IsEqualTo("Doctor");
        await Assert.That(NamesOfRoles(plan, 1, roleNames)).IsEqualTo("Medic");
        await Assert.That(plan.TryGetExplanation(
            1, medic.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SignalBucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(explanation.BaseSignalBucket)
            .IsEqualTo(SignalBucket.Neutral);
        await Assert.That(explanation.SignalSkillLevel).IsEqualTo(11);
        await Assert.That(explanation.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Surplus);
        await Assert.That(explanation.SelectionSignalBucket)
            .IsEqualTo(SignalBucket.Strong);
        await Assert.That(explanation.SurplusMinimumSignalBucket)
            .IsEqualTo(SignalBucket.Strong);
        await Assert.That(explanation.SurplusQualifiedBySignal).IsTrue();
    }

    [Test]
    public async Task EarlierCoveringPickSuppressesOnlyThatPawnsStrongSurplusRole()
    {
        RoleView cook = SkilledRole(
            8, "CookingAll", "Cooking", "Cook", "Brew");
        RoleView brewer = SkilledRole(
            9, "Brewing", "Cooking", "Brew");

        PawnView cookAndBrewer = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Cooking"] = (10, SignalBucket.Strong),
            },
            ("CookingAll", SignalBucket.Neutral),
            ("Brewing", SignalBucket.Neutral));
        PawnView brewerOnly = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Cooking"] = (10, SignalBucket.Strong),
            },
            ("CookingAll", SignalBucket.Awful),
            ("Brewing", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { cook, brewer }, cookAndBrewer, brewerOnly);
        var roleNames = new Dictionary<int, string>
        {
            [cook.Id] = "Cook",
            [brewer.Id] = "Brewer",
        };

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        await Assert.That(NamesOfRoles(plan, 0, roleNames)).IsEqualTo("Cook");
        await Assert.That(NamesOfRoles(plan, 1, roleNames)).IsEqualTo("Brewer");
    }

    [Test]
    public async Task SubstitutesEachWaiverThroughItsUniqueTargetPath()
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
        RecsTestBed.Require(fabricator, 2, trainingWaivers: 1);
        RoleView drugMaker = CraftingRole(11, "DrugMaking");
        RecsTestBed.Require(drugMaker, 1, trainingWaivers: 1);
        RoleView smith = CraftingRole(12, "Smithing");
        RecsTestBed.Require(smith, 1, trainingWaivers: 1);
        RoleView tailor = CraftingRole(13, "Tailoring");

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

        RecommendationPlan plan = RecommendationPlan.Build(
            colony, WithoutLevelPromotions());

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
    public async Task IgnoresMalformedAlternativeAfterAValidPath()
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
        RecsTestBed.Require(specialist, 1, trainingWaivers: 1);
        RoleView trainee = CraftingRole(25, "TraineeWork");
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
    public async Task OrdersTargetsThroughAssignedAndVirtualAnchors()
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
        RecsTestBed.Require(warden, 1);
        RoleView artist = SkilledRole(51, "ArtMaking", "Artistic");
        RecsTestBed.Require(artist, 1);
        RoleView tailor = CraftingRole(52, "Tailoring");
        RoleView smith = CraftingRole(53, "Smithing");
        RecsTestBed.Require(smith, 1, trainingWaivers: 1);
        RoleView researcher = SkilledRole(54, "Researching", "Intellectual");
        RecsTestBed.Require(researcher, 1);
        RoleView fabricator = CraftingRole(55, "Fabrication");
        RecsTestBed.Require(fabricator, 1, trainingWaivers: 1);
        RoleView drugMaker = CraftingRole(56, "DrugMaking");
        RecsTestBed.Require(drugMaker, 1, trainingWaivers: 1);
        RoleView crafter = CraftingRole(57, "CraftingWork");
        RecsTestBed.Require(crafter, 1, trainingWaivers: 1);

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

        RecommendationPlan plan = RecommendationPlan.Build(
            colony, WithoutLevelPromotions());

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
    public async Task DirectSpecialistSurvivesAndPrecedesItsCoveringTrainer()
    {
        /*
         * Default role recommendation order: Crafter > Fabricator.
         * Training paths: Fabricator = Crafter[0,21), Fabricator[8,21),
         * anchored before Crafter.
         * Role scales: Crafter direct 1; Fabricator direct 1.
         */
        RoleView crafter = SkilledRole(
            70, "CraftingWork", "Crafting", "Fabricate", "Stonecut");
        RecsTestBed.Require(crafter, 1);
        RoleView fabricator = SkilledRole(
            71, "Fabrication", "Crafting", "Fabricate");
        RecsTestBed.Require(fabricator, 1);
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
    public async Task OrdersConnectedTargetsWithoutLeadDiversification()
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
        RoleView smith = CraftingRole(81, "Smithing");
        RoleView fabricator = CraftingRole(82, "Fabrication");
        RoleView crafter = CraftingRole(83, "CraftingWork");
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
    public async Task PreservesAutomaticHunterFireAndRetainedWork()
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
        core.Scale = null;
        RoleView doctor = SkilledRole(
            101, "Doctoring", "Medicine", "DoctorJob");
        RecsTestBed.Require(doctor, 1);
        RoleView basics = RecsTestBed.Unskilled(102, "BasicWorker");
        basics.AutoAssign = true;
        basics.Scale = null;
        RoleView hunter = SkilledRole(103, "Hunting", "Shooting");
        hunter.Hunting = true;
        hunter.Scale = null;
        RoleView hauler = RecsTestBed.Unskilled(104, "Hauling");
        RoleView fireBlocker = RecsTestBed.Unskilled(105, "Firefighting");
        fireBlocker.Blocker = true;
        fireBlocker.Scale = null;

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
        await Assert.That(plan.TryGetExplanation(
            0, hunter.Id, out RoleRecommendationExplanation hunterExplanation))
            .IsTrue();
        await Assert.That(hunterExplanation.HolderScaleApplies).IsFalse();
        await Assert.That(plan.TryGetExplanation(
            0, hauler.Id, out RoleRecommendationExplanation haulerExplanation))
            .IsTrue();
        await Assert.That(haulerExplanation.HolderScaleApplies).IsTrue();
    }

    [Test]
    public async Task DoesNotAddHunterWhenFinalRoleAlreadyCoversIt()
    {
        /*
         * Default role recommendation order: Sharpshooter > Hunter.
         * Training paths: none.
         * Role scales: Sharpshooter custom direct 1; Hunter uses its existing
         * weapon/tier policy but is redundant when Sharpshooter is selected.
         */
        RoleView sharpshooter = SkilledRole(
            106, "Sharpshooting", "Shooting", "Hunting", "MarksmanWork");
        RecsTestBed.Require(sharpshooter, 1);
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

    private static RecommendationsTuningOptions WithoutLevelPromotions() =>
        RecommendationsTuningOptions.Default
            .With(RecommendationTuningOption.OptionalTargetGreatLevel, 21)
            .With(RecommendationTuningOption.OptionalTargetStrongLevel, 21);

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
