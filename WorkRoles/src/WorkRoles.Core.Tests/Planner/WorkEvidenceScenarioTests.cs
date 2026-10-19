using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class WorkEvidenceScenarioTests
{
    [Test]
    public async Task OptionalTrainingScoresTheWorkActuallyAssigned()
    {
        var target = RecsTestBed.Skilled(1, "Crafting", "Crafting", "Advanced");
        var trainer = RecsTestBed.Skilled(2, "Mining", "Crafting", "Practice");
        var pawn = Crafter(6);
        pawn.CapableWorkTypes.Add("Mining");
        ColonyView Input()
        {
            var colony = RecsTestBed.Colony([target, trainer], pawn);
            colony.Paths.Add(RecsTestBed.Path(1, (2, 0, 12), (1, 12, 21)));
            return colony;
        }
        // Opposing the future target does not penalize willing practice.
        pawn.WorkEvidence = Opposed();
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(Input()), 0, 2)).IsTrue();
        // Opposing the active practice work does affect optional selection.
        pawn.WorkEvidence = PawnWorkEvidence.Create([], [new("Mining", "OpposedPractice", "test")]);
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(Input()), 0, 2)).IsFalse();
    }

    [Test]
    public async Task LoneOpposedSurplusIsNotExplainedAsLosingToOtherPawns()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400);
        recs.RoleByWorkType(1, 0, 0, "Crafting");
        var pawn = Crafter(6);
        pawn.WorkEvidence = Opposed();
        pawn.Existing.Add(new AssignmentView { RoleId = 1 });
        var plan = recs.Plan(pawn);
        await Assert.That(RecsProjection.Holds(plan, 0, 1)).IsFalse();
        await Assert.That(plan.TryGetExplanation(0, 1, out var explanation)).IsTrue();
        await Assert.That(explanation.RejectReason).IsEqualTo(PickRejectReason.WorkAversion);
        await Assert.That(explanation.WorkAversions.Single().PreceptDefName).IsEqualTo("OpposedCraft");
        await Assert.That(explanation.BaseSignalBucket).IsEqualTo(SignalBucket.Strong);
    }

    [Test]
    public async Task WorkEvidenceInvalidatesPlanButRetainsRatingsAndEqualSnapshotIdentity()
    {
        // Published assignments cannot prove the independent cache ownership
        // contract, so this scenario also checks producer identities.
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400);
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        ColonyView Input(PawnWorkEvidence evidence)
        {
            var pawn = Crafter(16); pawn.WorkEvidence = evidence;
            return recs.Colony(pawn, Crafter(10));
        }
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(); object[] pawns = [new(), new()];
        RecommendationSnapshot Read(PawnWorkEvidence evidence) => cache.For(owner, pawns, new object(), 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => Input(evidence));
        var baseline = Read(PawnWorkEvidence.Empty);
        var ratings = baseline.Suitability;
        var changed = Read(Opposed());
        await Assert.That(RecsProjection.Holds(changed.Plan, 0, 1)).IsFalse();
        await Assert.That(ReferenceEquals(ratings, changed.Suitability)).IsTrue();
        await Assert.That(ReferenceEquals(changed, Read(Opposed()))).IsTrue();
        cache.Reset();
        await Assert.That(ReferenceEquals(changed, Read(Opposed()))).IsFalse();
        await Assert.That(RecsProjection.Holds(Read(PawnWorkEvidence.Empty).Plan, 0, 1)).IsTrue();
    }

    [Test]
    public async Task BlockedTrainingRecipeCannotBorrowAnUnrelatedJobsCapability()
    {
        var target = RecsTestBed.Skilled(1, "Crafting", "Crafting", "Advanced");
        var trainer = RecsTestBed.Skilled(2, "Crafting", "Crafting", "Practice");
        var recipe = new RoleWorkContentSpec(RoleWorkContentKind.Recipe, "PracticeRecipe", "Crafting",
            RoleWorkEffect.Speed, true, []);
        RecsTestBed.SetSpec(trainer, RecsTestBed.Capability("Crafting", 0,
            new RoleWorkGiverSpec("Practice", [new("Crafting", RoleWorkEffect.Speed)], ["Crafting"], [recipe]),
            RecsTestBed.Giver("Unrelated")));
        RecsTestBed.Require(target, 0, 100);
        var pawn = Crafter(6, SignalBucket.Exceptional);
        ColonyView Input() { var c = RecsTestBed.Colony([target, trainer], pawn); c.Paths.Add(RecsTestBed.Path(1, (2, 0, 12), (1, 12, 21))); return c; }
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(Input()), 0, 2)).IsTrue();
        pawn.WorkEvidence = PawnWorkEvidence.Create([new("Practice", "PracticeRecipe", "test", "recipe")], []);
        var plan = RecommendationPlan.Build(Input());
        await Assert.That(Enumerable.Range(0, plan.PathCountAt(0)).Any(i => plan.PathActivatedAt(0, i))).IsFalse();
        // A denied recipe is not a whole-giver block. The role's direct
        // eligibility remains intact, even though this path cannot train.
        await Assert.That(new EngineContext(Input()).MeetsCapabilityRequirement(0, trainer)).IsTrue();
    }

    private static PawnView Crafter(int level, SignalBucket signal = SignalBucket.Strong)
        => new() { CapableWorkTypes = { "Crafting" }, SkillLevels = { ["Crafting"] = level }, SignalBuckets = { ["Crafting"] = signal } };

    private static PawnWorkEvidence Opposed() => PawnWorkEvidence.Create([], [new("Crafting", "OpposedCraft", "test")]);

    [Test]
    public async Task BroadProtectedRoleCannotFillBlockedSpecialistDemand()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400, "Ordinary", "Restricted");
        recs.RoleByGiver(1, 1, 0, "Restricted");
        recs.RoleByWorkType(2, 0, 0, "Crafting");
        var human = Crafter(18, SignalBucket.Exceptional);
        human.WorkEvidence = PawnWorkEvidence.Create([new("Restricted", null, "test", "restricted")], []);
        human.Existing.Add(new AssignmentView { RoleId = 2, Pinned = true });
        var worker = Crafter(8, SignalBucket.Neutral);
        var plan = recs.Plan(human, worker);
        await Assert.That(RecsProjection.Holds(plan, 0, 2)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 0, 1)).IsFalse();
        await Assert.That(RecsProjection.Holds(plan, 1, 1)).IsTrue();
    }

    [Test]
    public async Task AversionChangesCloseChampionDecisionButRetainsEssentialCoverage()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400);
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        var opposed = Crafter(16);
        opposed.WorkEvidence = Opposed();
        var willing = Crafter(10);
        var plan = recs.Plan(opposed, willing);
        await Assert.That(RecsProjection.Holds(plan, 1, 1)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 0, 1)).IsFalse();
        var alone = recs.Plan(opposed);
        await Assert.That(RecsProjection.Holds(alone, 0, 1)).IsTrue();
        opposed.SignalBuckets["Crafting"] = SignalBucket.Exceptional;
        opposed.SkillLevels["Crafting"] = 20;
        willing.SignalBuckets["Crafting"] = SignalBucket.Poor;
        var exceptional = recs.Plan(opposed, willing);
        await Assert.That(RecsProjection.Holds(exceptional, 0, 1)).IsTrue();
        await Assert.That(RecsProjection.Holds(exceptional, 1, 1)).IsFalse();
    }

    [Test]
    public async Task DuplicatePreceptsHaveOnePenaltyAndNoEffectOnRawRating()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400);
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        var pawn = Crafter(16);
        pawn.WorkEvidence = Opposed();
        var once = recs.Plan(pawn, Crafter(10));
        pawn.WorkEvidence = PawnWorkEvidence.Create([], [new("Crafting", "A", "test"), new("Crafting", "B", "test"), new("Crafting", "A", "test")]);
        var twice = recs.Plan(pawn, Crafter(10));
        for (int p = 0; p < 2; p++)
        {
            await Assert.That(twice.RoleCountAt(p)).IsEqualTo(once.RoleCountAt(p));
            for (int r = 0; r < once.RoleCountAt(p); r++)
                await Assert.That(twice.RoleAt(p, r)).IsEqualTo(once.RoleAt(p, r));
        }
        var colony = recs.Colony(pawn);
        await Assert.That(new EngineContext(colony).BestSignal(0, colony.Roles[0], out _, out _)).IsEqualTo(SignalBucket.Strong);
    }
}
