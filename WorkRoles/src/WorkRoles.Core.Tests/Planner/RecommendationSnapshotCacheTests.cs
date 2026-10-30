using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

// Identity assertions protect the producer's independent caching/ownership
// contract, which equal final assignments alone cannot prove. Outcomes also
// exercise a real training path, coverage, demand, and automatic assignments.
public class RecommendationSnapshotCacheTests
{
    [Test]
    public async Task PausedBlockerEditsKeepBadgesStableWithoutRebuildingHumanBadges()
    {
        // Badges rate skill fit only, so a blocker toggle leaves the robot's
        // and the human's Neutral hauling badge alone. Identity and
        // lazy-context checks protect the independent human cache contract.
        RoleView haul = RecsTestBed.Unskilled(1, "Hauling", "HaulGeneral");
        ColonyView Inputs(bool blocker, bool includeRobot)
        {
            RecommendationRolePolicy policy = RecommendationRolePolicy.From(haul);
            policy.Blocker = blocker;
            RoleView role = policy.Apply(haul);
            var human = new PawnView();
            var robot = new PawnView { IsMechanical = true, CapableWorkTypes = { "Cleaning" } };
            return includeRobot ? RecsTestBed.Colony([role], robot, human)
                : RecsTestBed.Colony([role], human);
        }

        var cache = new RecommendationSnapshotCache<object>();
        object owner = new();
        object[] pawns = [new(), new()];
        RecommendationSnapshot Read(bool blocker) => cache.For(owner, pawns,
            new object(), 0, 0, 0, RecommendationsTuningOptions.Default,
            (_, _) => Inputs(blocker, includeRobot: true));
        var original = Read(false).Suitability;
        await Assert.That(original[0][1]).IsEqualTo(SignalBucket.Neutral);
        // Catalog edits are immediate: no external refresh or clock advance.
        var blocked = Read(true).Suitability;
        await Assert.That(blocked[0][1]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(ReferenceEquals(original[1], blocked[1])).IsTrue();
        var restored = Read(false).Suitability;
        await Assert.That(restored[0][1]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(ReferenceEquals(original[1], restored[1])).IsTrue();

        var humanCache = new RecommendationSnapshotCache<object>();
        object[] humans = [pawns[1]];
        RecommendationSnapshot HumanRead(bool blocker) => humanCache.For(owner, humans,
            new object(), 0, 0, 0, RecommendationsTuningOptions.Default,
            (_, _) => Inputs(blocker, includeRobot: false));
        var humanBadges = HumanRead(false).Suitability;
        RecommendationSnapshot humanBlocked = HumanRead(true);
        await Assert.That(humanBlocked.Suitability[0][1]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(ReferenceEquals(humanBadges, humanBlocked.Suitability)).IsTrue();
        await Assert.That(humanBlocked.CachedContext).IsNull();
    }

    [Test]
    public async Task MechanicalSkillRefreshesBadgesWithoutChangingHumanRatings()
    {
        // A robot's published native skills (e.g. a Plants skill appearing
        // once its model gains growing work) refresh its badge under the
        // external gate. Badge identity is an independent cache contract:
        // identical role assignments cannot reveal a stale or unnecessarily
        // rebuilt verdict.
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new();
        object[] pawns = [new(), new()];
        ColonyView Inputs(bool skilled)
        {
            RoleView sow = RecsTestBed.Skilled(1, "Growing", "Plants", "GrowerSow");
            var robot = new PawnView { IsMechanical = true, CapableWorkTypes = { "Growing" } };
            if (skilled)
            {
                robot.SkillLevels["Plants"] = 20;
                robot.SignalBuckets["Plants"] = SignalBucket.Great;
            }
            var human = new PawnView { CapableWorkTypes = { "Growing" } };
            human.SkillLevels["Plants"] = 6; // Human rating stays Neutral throughout.
            return RecsTestBed.Colony([sow], robot, human);
        }
        var unskilled = cache.For(owner, pawns, catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => Inputs(false));
        var original = unskilled.Suitability;
        var skilled = cache.For(owner, pawns, catalog, 0, 1, 0,
            RecommendationsTuningOptions.Default, (_, _) => Inputs(true));
        await Assert.That(original[0][1]).IsEqualTo(SignalBucket.Awful);
        await Assert.That(skilled.Suitability[0][1]).IsEqualTo(SignalBucket.Great);
        await Assert.That(skilled.Suitability[1][1]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(ReferenceEquals(original[1], skilled.Suitability[1])).IsTrue();
        var unchanged = cache.For(owner, pawns, catalog, 0, 2, 0,
            RecommendationsTuningOptions.Default, (_, _) => Inputs(true));
        await Assert.That(ReferenceEquals(skilled, unchanged)).IsTrue();
        var restored = cache.For(owner, pawns, catalog, 0, 3, 0,
            RecommendationsTuningOptions.Default, (_, _) => Inputs(false));
        await Assert.That(restored.Suitability[0][1]).IsEqualTo(SignalBucket.Awful);
        await Assert.That(ReferenceEquals(original[1], restored.Suitability[1])).IsTrue();
    }

    [Test]
    public async Task ExclusionRefreshesPlansButReusesWorkOrderingAndSuitability()
    {
        var cache = new RecommendationSnapshotCache<object>();
        var orderingCache = new RecommendationOrderCache();
        var owner = new object();
        object[] pawns = [new(), new()];
        var initialColony = Colony();
        var first = cache.For(owner, pawns, initialColony.Roles, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => initialColony);
        var ratings = first.Suitability;
        var originalOrder = orderingCache.For(owner, initialColony.Roles, 0, initialColony.OrderTemplate);
        await Assert.That(RecsProjection.Holds(first.Plan, 0, 1)).IsTrue();
        var changed = Colony();
        for (int i = 0; i < changed.Roles.Count; i++)
        {
            var policy = RecommendationRolePolicy.From(initialColony.Roles[i]);
            policy.ExcludedFromRecommendations = i == 0;
            changed.Roles[i] = policy.Apply(initialColony.Roles[i]);
        }
        var excluded = cache.For(owner, pawns, changed.Roles, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => changed);
        await Assert.That(RecsProjection.Holds(excluded.Plan, 0, 1)).IsFalse();
        await Assert.That(ReferenceEquals(ratings, excluded.Suitability)).IsTrue();
        await Assert.That(ReferenceEquals(originalOrder, orderingCache.For(owner, changed.Roles, 0, changed.OrderTemplate))).IsTrue();
        await Assert.That(ReferenceEquals(initialColony.Roles[0].WorkSpec, changed.Roles[0].WorkSpec)).IsTrue();
        var equivalent = cache.For(owner, pawns, new object(), 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => changed);
        await Assert.That(ReferenceEquals(excluded, equivalent)).IsTrue();
        var otherWorld = cache.For(new object(), pawns, initialColony.Roles, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => initialColony);
        await Assert.That(RecsProjection.Holds(otherWorld.Plan, 0, 1)).IsTrue();
    }

    [Test]
    public async Task DeferredOldSnapshotCannotPublishVerdictsIntoTheReplacementOwner()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object[] pawns = [new(), new()];
        object oldOwner = new(), newOwner = new(), catalog = new();
        var old = cache.For(oldOwner, pawns, catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => Colony());
        cache.Reset();
        var current = cache.For(newOwner, pawns, catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => Colony());
        var oldVerdicts = old.Suitability;
        var currentVerdicts = current.Suitability;
        await Assert.That(ReferenceEquals(oldVerdicts[0], currentVerdicts[0])).IsFalse();
        await Assert.That(ReferenceEquals(oldVerdicts[1], currentVerdicts[1])).IsFalse();
        await Assert.That(currentVerdicts[1][1]).IsEqualTo(SignalBucket.Great);
        await Assert.That(string.Join(",", Roles(current.Plan))).IsEqualTo("3,2");
        await Assert.That(ReferenceEquals(current, cache.For(newOwner, pawns, catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => Colony()))).IsTrue();
    }

    [Test]
    public async Task PlanAndSuitabilityShareLazyContextWhileCachedVerdictsDoNotBuildOne()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new();
        object[] pawns = [new(), new()];
        var first = cache.For(owner, pawns, new object(), 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => Colony());
        await Assert.That(first.CachedContext).IsNull();
        var verdicts = first.Suitability;
        // Context identity is an independent work-sharing invariant; the same
        // final plan would not expose an accidentally duplicated constructor.
        await Assert.That(first.CachedContext).IsNotNull();
        EngineContext shared = first.CachedContext!;
        await Assert.That(ReferenceEquals(shared, first.Plan.Context)).IsTrue();
        await Assert.That(string.Join(",", Roles(first.Plan))).IsEqualTo("3,2");
        await Assert.That(verdicts[1][1]).IsEqualTo(SignalBucket.Great);
        var tuning = RecommendationsTuningOptions.Default.With(RecommendationTuningOption.ChampionSkillDivisor,
            RecommendationsTuningOptions.Default.Get(RecommendationTuningOption.ChampionSkillDivisor) + 1);
        var retuned = cache.For(owner, pawns, new object(), 0, 0, 0, tuning, (_, _) => Colony());
        await Assert.That(ReferenceEquals(shared, retuned.CachedContext)).IsTrue();
        await Assert.That(ReferenceEquals(shared, retuned.Plan.Context)).IsTrue();
        var disabled = cache.For(owner, pawns, new object(), 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => Colony(roleEnabled: false));
        await Assert.That(ReferenceEquals(verdicts, disabled.Suitability)).IsTrue();
        await Assert.That(disabled.CachedContext).IsNull();
        await Assert.That(string.Join(",", Roles(disabled.Plan))).IsEqualTo("3");
        await Assert.That(ReferenceEquals(shared, disabled.Plan.Context)).IsFalse();
    }

    private static ColonyView Colony(bool heldEnabled = true, bool roleEnabled = true,
        bool pinned = false, int skill = 8)
    {
        RoleView target = RecsTestBed.Skilled(1, "Crafting", "Crafting", "MakeWeapons");
        RoleView training = RecsTestBed.Skilled(2, "Training", "Crafting", "Practice");
        RoleView basics = RecsTestBed.Unskilled(3, "Hauling");
        basics.AutoAssign = true;
        target.Enabled = roleEnabled;
        training.Enabled = roleEnabled;
        RecsTestBed.Require(target, 2, 100);
        var pawn = new PawnView
        {
            CapableWorkTypes = { "Crafting", "Training", "Hauling" },
            SkillLevels = { ["Crafting"] = skill },
            SignalBuckets = { ["Crafting"] = SignalBucket.Great },
            Existing = { new AssignmentView { RoleId = 3, Enabled = heldEnabled, Pinned = pinned } },
        };
        var experienced = new PawnView
        {
            CapableWorkTypes = { "Crafting", "Training", "Hauling" },
            SkillLevels = { ["Crafting"] = 16 },
            SignalBuckets = { ["Crafting"] = SignalBucket.Great },
        };
        ColonyView colony = RecsTestBed.Colony([target, training, basics], experienced, pawn);
        colony.Paths.Add(RecsTestBed.Path(1, (2, 0, 12), (1, 12, 21)));
        colony.OrderTemplate = [3, 1, 2]; // Basics first, then the target/training block.
        return colony;
    }

    [Test]
    public async Task EquivalentConsumersShareProjectionPlanAndSuitabilityWithoutBuildingOnHits()
    {
        var cache = new RecommendationSnapshotCache<object>();
        var owner = new object();
        var catalog = new object();
        object[] pawns = [new(), new()];
        int projections = 0;
        ColonyView Build(IReadOnlyList<object> _, IReadOnlyList<PawnView>? previous) { projections++; return Colony(); }
        RecommendationSnapshot first = cache.For(owner, pawns, catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, Build);
        RecommendationPlan plan = first.Plan;
        var suitability = first.Suitability;
        RecommendationSnapshot second = cache.For(owner, [pawns[0], pawns[1]], catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, Build);

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(ReferenceEquals(plan, second.Plan)).IsTrue();
        await Assert.That(ReferenceEquals(suitability, second.Suitability)).IsTrue();
        await Assert.That(projections).IsEqualTo(1);
        await Assert.That(string.Join(",", Roles(plan))).IsEqualTo("3,2");
        await Assert.That(plan.PathAt(1, 0)).IsEqualTo(1);
        await Assert.That(plan.PathActivatedAt(1, 0)).IsTrue();
    }

    [Test]
    public async Task AssignmentStateAndEqualExternalRefreshPreservePlanWhileRoleActivationChangesOutcome()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new();
        object[] pawns = [new(), new()];
        RecommendationSnapshot Read(int revision, bool heldEnabled = true, bool roleEnabled = true)
            => cache.For(owner, pawns, catalog, revision, 0, 0,
                RecommendationsTuningOptions.Default, (_, previous) => Colony(heldEnabled, roleEnabled));
        RecommendationSnapshot first = Read(0);
        RecommendationPlan plan = first.Plan;
        var suitability = first.Suitability;
        RecommendationSnapshot disabledAssignment = Read(1, heldEnabled: false);
        await Assert.That(ReferenceEquals(first, disabledAssignment)).IsTrue();
        await Assert.That(ReferenceEquals(plan, disabledAssignment.Plan)).IsTrue();

        RecommendationSnapshot disabledRole = Read(2, roleEnabled: false);
        await Assert.That(ReferenceEquals(plan, disabledRole.Plan)).IsFalse();
        await Assert.That(ReferenceEquals(suitability, disabledRole.Suitability)).IsTrue();
        await Assert.That(string.Join(",", Roles(disabledRole.Plan))).IsEqualTo("3");
        await Assert.That(disabledRole.Plan.PathCountAt(1)).IsEqualTo(0);
        RecommendationSnapshot equalRefresh = Read(3, roleEnabled: false);
        await Assert.That(ReferenceEquals(disabledRole, equalRefresh)).IsTrue();
    }

    [Test]
    public async Task PinAndTrainingBandInputsRefreshPlanWithoutRecomputingUnchangedVerdicts()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new();
        object[] pawns = [new(), new()];
        RecommendationSnapshot Read(int revision, bool pinned = false, int skill = 8)
            => cache.For(owner, pawns, catalog, revision, revision, 0,
                RecommendationsTuningOptions.Default, (_, previous) => Colony(pinned: pinned, skill: skill));
        RecommendationSnapshot first = Read(0);
        RecommendationPlan original = first.Plan;
        var verdicts = first.Suitability;
        RecommendationSnapshot pinned = Read(1, pinned: true);
        await Assert.That(ReferenceEquals(original, pinned.Plan)).IsFalse();
        await Assert.That(ReferenceEquals(verdicts, pinned.Suitability)).IsTrue();
        RecommendationSnapshot trained = Read(2, skill: 14);
        await Assert.That(string.Join(",", Roles(trained.Plan))).IsEqualTo("3,1");
        await Assert.That(trained.Plan.PathCountAt(1)).IsEqualTo(1);
        await Assert.That(trained.Plan.PathAt(1, 0)).IsEqualTo(1);
        await Assert.That(ReferenceEquals(verdicts, trained.Suitability)).IsTrue();
    }

    [Test]
    public async Task CohortsStoresAndResetNeverLeakThePreviousOwnersSnapshot()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new(), veteran = new(), pawn = new(), other = new();
        RecommendationSnapshot Read(object store, object member) => cache.For(store, [veteran, member],
            catalog, 0, 0, 0, RecommendationsTuningOptions.Default, (_, previous) => Colony());
        RecommendationSnapshot first = Read(owner, pawn);
        RecommendationSnapshot secondCohort = Read(owner, other);
        await Assert.That(ReferenceEquals(first, secondCohort)).IsFalse();
        await Assert.That(ReferenceEquals(first, Read(owner, pawn))).IsTrue();
        await Assert.That(ReferenceEquals(first, Read(new object(), pawn))).IsFalse();
        RecommendationSnapshot beforeReset = Read(owner, pawn);
        cache.Reset();
        cache.Reset();
        await Assert.That(ReferenceEquals(beforeReset, Read(owner, pawn))).IsFalse();
    }

    private static int[] Roles(RecommendationPlan plan) => Enumerable.Range(0,
        plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index)).ToArray();

    [Test]
    public async Task RoleAndOrderChangesReuseDetachedPawnInputsUntilTheirOwnDependenciesChange()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new();
        object[] pawns = [new(), new()];
        int pawnProjections = 0;
        ColonyView Build(IReadOnlyList<object> _, IReadOnlyList<PawnView>? previous)
        {
            ColonyView colony = Colony();
            if (previous == null) pawnProjections++;
            else colony.Pawns = new List<PawnView>(previous);
            return colony;
        }
        RecommendationSnapshot first = cache.For(owner, pawns, new object(), 0, 0, 0,
            RecommendationsTuningOptions.Default, Build);
        cache.For(owner, pawns, new object(), 0, 0, 1, RecommendationsTuningOptions.Default, Build);
        await Assert.That(pawnProjections).IsEqualTo(1);
        cache.For(owner, pawns, new object(), 1, 0, 1, RecommendationsTuningOptions.Default, Build);
        await Assert.That(pawnProjections).IsEqualTo(2);
        cache.For(owner, pawns, new object(), 1, 1, 1, RecommendationsTuningOptions.Default, Build);
        await Assert.That(pawnProjections).IsEqualTo(3);
    }

    [Test]
    public async Task OverlappingCohortsSharePawnProjectionAndVerdictsAcrossSelectiveChanges()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new(), veteran = new(), apprentice = new(), visitor = new();
        var inputs = new Dictionary<object, PawnView> { [veteran] = Colony().Pawns[0], [apprentice] = Colony().Pawns[1], [visitor] = Colony().Pawns[0] };
        var revisions = new Dictionary<object, int> { [veteran] = 0, [apprentice] = 0, [visitor] = 0 };
        int builds = 0;
        PawnView Project(object pawn) { builds++; return inputs[pawn]; }
        ColonyView Build(IReadOnlyList<object> cohort, IReadOnlyList<PawnView>? previous)
        {
            ColonyView colony = Colony();
            colony.Pawns.Clear();
            for (int i = 0; i < cohort.Count; i++)
                colony.Pawns.Add(cache.PawnFor(owner, cohort[i], revisions[cohort[i]], inputs[cohort[i]], cohort[i], Project));
            return colony;
        }
        RecommendationSnapshot Read(object[] cohort, int revision) => cache.For(owner, cohort, catalog,
            revision, revision, 0, RecommendationsTuningOptions.Default, Build);
        RecommendationSnapshot map = Read([veteran, apprentice], 0);
        var mapVerdicts = map.Suitability;
        RecommendationSnapshot listed = Read([veteran, apprentice, visitor], 0);
        await Assert.That(builds).IsEqualTo(3);
        await Assert.That(ReferenceEquals(mapVerdicts[0], listed.Suitability[0])).IsTrue();
        await Assert.That(ReferenceEquals(mapVerdicts[1], listed.Suitability[1])).IsTrue();

        revisions[visitor]++;
        inputs[visitor] = Colony(pinned: true).Pawns[0];
        await Assert.That(ReferenceEquals(map, Read([veteran, apprentice], 1))).IsTrue();
        await Assert.That(builds).IsEqualTo(3);
        inputs[apprentice] = Colony(skill: 14).Pawns[1];
        RecommendationSnapshot trained = Read([veteran, apprentice], 2);
        await Assert.That(builds).IsEqualTo(4);
        await Assert.That(string.Join(",", Roles(trained.Plan))).IsEqualTo("3,1");
        await Assert.That(ReferenceEquals(mapVerdicts[1], trained.Suitability[1])).IsTrue();
        cache.Reset();
        Read([veteran, apprentice], 2);
        await Assert.That(builds).IsEqualTo(6);
    }

    [Test]
    public async Task OnePawnsChangedSignalsPreserveOtherPawnsVerdictIdentity()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new();
        object[] pawns = [new(), new()];
        RecommendationSnapshot first = cache.For(owner, pawns, catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, previous) => Colony());
        var original = first.Suitability;
        RecommendationSnapshot changed = cache.For(owner, pawns, catalog, 0, 1, 0,
            RecommendationsTuningOptions.Default, (_, previous) =>
            {
                ColonyView colony = Colony();
                colony.Pawns[1].SignalBuckets["Crafting"] = SignalBucket.Awful;
                return colony;
            });

        await Assert.That(ReferenceEquals(original[0], changed.Suitability[0])).IsTrue();
        await Assert.That(ReferenceEquals(original[1], changed.Suitability[1])).IsFalse();
        await Assert.That(changed.Suitability[1][1]).IsEqualTo(SignalBucket.Awful);
        await Assert.That(string.Join(",", Roles(changed.Plan))).IsEqualTo("3");
    }
}
