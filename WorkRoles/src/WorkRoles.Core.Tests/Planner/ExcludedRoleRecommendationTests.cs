using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class ExcludedRoleRecommendationTests
{
    [Test]
    public async Task ApplyingExclusionsPreservesPinsWithoutRecommendingThem()
    {
        var recs = new RecsProjection()
            .WorkType("Cooking", "Cooking", 500)
            .WorkType("Cleaning", null, 100)
            .WorkType("Hauling", null, 200);
        var cook = recs.RoleByWorkType(1, 1, 100, "Cooking");
        cook.ExcludedFromRecommendations = true;
        var cleaner = recs.RoleByWorkType(2, 0, 100, "Cleaning");
        var bundle = recs.RoleByWorkType(3, 0, 0, "Cooking");
        bundle.MemberRoleIds = [cook.Id];
        var hauler = recs.RoleByWorkType(4, 0, 100, "Hauling");
        hauler.ExcludedFromRecommendations = true;
        PawnView Pawn(bool downed) => new()
        {
            Incapacitated = downed,
            CapableWorkTypes = { "Cooking", "Cleaning", "Hauling" },
            SkillLevels = { ["Cooking"] = 12 },
            SignalBuckets = { ["Cooking"] = SignalBucket.Great },
            Existing =
            {
                new() { RoleId = cook.Id, Pinned = true, Enabled = false },
                new() { RoleId = cleaner.Id, Enabled = true },
                new() { RoleId = bundle.Id, Pinned = true, Enabled = true },
                new() { RoleId = hauler.Id, Enabled = true },
            },
        };
        var pawns = new[] { Pawn(false), Pawn(true) };
        ColonyView colony = recs.Colony(pawns);
        RecommendationPlan recommendations = RecommendationPlan.Build(colony);
        var targets = ColonyFixPlanner.Build(colony, recommendations);
        for (int pawn = 0; pawn < pawns.Length; pawn++)
        {
            await Assert.That(Roles(recommendations, pawn)).IsEqualTo("2");
            await Assert.That(string.Join(",", targets[pawn].RoleIds)).IsEqualTo("1,2,3");
            await Assert.That(targets[pawn].Changed).IsTrue();
            pawns[pawn].Existing.RemoveAll(a => !targets[pawn].RoleIds.Contains(a.RoleId));
        }
        var repeated = ColonyFixPlanner.Build(recs.Colony(pawns));
        for (int pawn = 0; pawn < pawns.Length; pawn++)
        {
            await Assert.That(repeated[pawn].Changed).IsFalse();
            pawns[pawn].Existing = pawns[pawn].Existing.Select(a =>
                new AssignmentView { RoleId = a.RoleId, Enabled = a.Enabled }).ToList();
        }
        var unpinned = ColonyFixPlanner.Build(recs.Colony(pawns));
        foreach (var target in unpinned)
            await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("2");
    }

    [Test]
    public async Task ExclusionsOverrideDemandAutomaticHuntingAndRetainedAssignments()
    {
        var recs = new RecsProjection()
            .WorkType("Cooking", "Cooking", 500)
            .WorkType("Hauling", null, 100)
            .WorkType("Hunting", "Shooting", 400)
            .WorkType("Firefighter", null, 900)
            .WorkType("Cleaning", null, 90);
        var cook = recs.RoleByWorkType(1, 1, 100, "Cooking");
        var automatic = recs.RoleByWorkType(2, 0, 0, "Hauling");
        automatic.AutoAssign = true;
        var hunter = recs.RoleByWorkType(3, 0, 0, "Hunting");
        hunter.SpecialRole = RecommendationSpecialRoleKind.Hunter;
        var blocker = recs.RoleByWorkType(4, 0, 0, "Firefighter");
        blocker.Blocker = true;
        blocker.SpecialRole = RecommendationSpecialRoleKind.FireBlocker;
        var allowed = recs.RoleByWorkType(5, 0, 100, "Cleaning");
        var working = new PawnView
        {
            CapableWorkTypes = { "Cooking", "Hauling", "Hunting", "Firefighter", "Cleaning" },
            SkillLevels = { ["Cooking"] = 12, ["Shooting"] = 8 },
            SignalBuckets = { ["Cooking"] = SignalBucket.Great, ["Shooting"] = SignalBucket.Great },
            HasRangedWeapon = true, ShootingLevel = 8, FireFear = true,
            Existing = { new AssignmentView { RoleId = cook.Id, Enabled = true, Pinned = true } },
        };
        var downed = new PawnView
        {
            Incapacitated = true,
            Existing =
            {
                new AssignmentView { RoleId = cook.Id, Enabled = true, Pinned = true },
                new AssignmentView { RoleId = allowed.Id, Enabled = true },
            },
        };
        var before = recs.Plan(working, downed);
        foreach (var role in new[] { cook, automatic, hunter, blocker, allowed })
            await Assert.That(RecsProjection.Holds(before, 0, role.Id)).IsTrue();

        foreach (var role in new[] { cook, automatic, hunter, blocker })
            role.ExcludedFromRecommendations = true;
        var excluded = recs.Plan(working, downed);
        for (int pawn = 0; pawn < excluded.PawnCount; pawn++)
        {
            await Assert.That(excluded.RoleCountAt(pawn)).IsEqualTo(1);
            await Assert.That(excluded.RoleAt(pawn, 0)).IsEqualTo(allowed.Id);
        }
        // A recommendation edit does not mutate existing player assignments.
        await Assert.That(working.Existing[0].RoleId).IsEqualTo(cook.Id);
        foreach (var role in new[] { cook, automatic, hunter, blocker })
            role.ExcludedFromRecommendations = false;
        var restored = recs.Plan(working, downed);
        for (int pawn = 0; pawn < before.PawnCount; pawn++)
            await Assert.That(Roles(restored, pawn)).IsEqualTo(Roles(before, pawn));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExcludedTrainingStepCannotReturnThroughAnEligibleTargetPath(bool compositeStep)
    {
        var recs = new RecsProjection()
            .WorkType("Doctor", "Medicine", 600, "Surgery", "Tend");
        var doctor = recs.RoleByGiver(1, 2, 100, "Surgery");
        var nurse = recs.RoleByGiver(2, 0, 0, "Tend");
        var step = nurse;
        if (compositeStep)
        {
            step = recs.RoleByGiver(3, 0, 0, "Tend");
            step.MemberRoleIds = [nurse.Id];
        }
        recs.Path(RecsTestBed.Path(doctor.Id, (step.Id, 0, 12), (doctor.Id, 12, 21)));
        var expert = new PawnView
        {
            CapableWorkTypes = { "Doctor" }, SkillLevels = { ["Medicine"] = 15 },
            SignalBuckets = { ["Medicine"] = SignalBucket.Great },
        };
        var learner = new PawnView
        {
            CapableWorkTypes = { "Doctor" }, SkillLevels = { ["Medicine"] = 5 },
            SignalBuckets = { ["Medicine"] = SignalBucket.Great },
        };
        var before = recs.Plan(expert, learner);
        await Assert.That(RecsProjection.Holds(before, 0, doctor.Id)).IsTrue();
        await Assert.That(RecsProjection.Holds(before, 1, step.Id)).IsTrue();

        nurse.ExcludedFromRecommendations = true;
        var after = recs.Plan(expert, learner);
        await Assert.That(RecsProjection.Holds(after, 0, doctor.Id)).IsTrue();
        await Assert.That(RecsProjection.Holds(after, 1, nurse.Id)).IsFalse();
        await Assert.That(RecsProjection.Holds(after, 1, step.Id)).IsFalse();
        // The remaining target may still satisfy its authored minimum through
        // the existing below-band fallback; the excluded trainee never does.
        await Assert.That(RecsProjection.Holds(after, 1, doctor.Id)).IsTrue();
    }

    [Test]
    public async Task ExcludedCompositeLeavesItsEligibleMembersRecommendedWithoutOrderingChips()
    {
        var first = RecsTestBed.Unskilled(1, "Hauling");
        var second = RecsTestBed.Unskilled(2, "Cleaning");
        first.AutoAssign = second.AutoAssign = true;
        var bundle = RecsTestBed.Unskilled(3, "Hauling");
        bundle.MemberRoleIds = [first.Id, second.Id];
        var pawn = new PawnView { CapableWorkTypes = { "Hauling", "Cleaning" } };
        var colony = RecsTestBed.Colony([first, second, bundle], pawn);
        colony.OrderTemplate.Clear();
        var before = ColonyFixPlanner.Build(colony);
        await Assert.That(string.Join(",", before[0].RoleIds)).IsEqualTo("3");

        bundle.ExcludedFromRecommendations = true;
        var after = ColonyFixPlanner.Build(colony);
        await Assert.That(string.Join(",", after[0].RoleIds)).IsEqualTo("1,2");
        first.ExcludedFromRecommendations = true;
        bundle.ExcludedFromRecommendations = false;
        var memberExcluded = ColonyFixPlanner.Build(colony);
        await Assert.That(string.Join(",", memberExcluded[0].RoleIds)).IsEqualTo("2");

        pawn.Incapacitated = true;
        pawn.Existing = [new() { RoleId = bundle.Id }, new() { RoleId = second.Id }];
        var resting = ColonyFixPlanner.Build(colony);
        await Assert.That(string.Join(",", resting[0].RoleIds)).IsEqualTo("2");
    }

    private static string Roles(RecommendationPlan plan, int pawn) => string.Join(",",
        Enumerable.Range(0, plan.RoleCountAt(pawn)).Select(index => plan.RoleAt(pawn, index)));
}
