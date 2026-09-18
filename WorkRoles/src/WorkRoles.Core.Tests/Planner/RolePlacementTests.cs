using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Where a palette role lands in a colonist's assignment list for the
/// "after Basics" and "where recommendations would put it" gestures.
public class RolePlacementTests
{
    private const int Core = 1, Doctor = 2, Basics = 3, Cook = 4, Builder = 5, Miner = 6;

    private static readonly Dictionary<int, long> Positions = new()
    {
        [Core] = 0,
        [Doctor] = 1 * Ordering.Slot,
        [Basics] = 2 * Ordering.Slot,
        [Cook] = 6 * Ordering.Slot,
        [Builder] = 7 * Ordering.Slot,
        [Miner] = 9 * Ordering.Slot,
    };

    [Test]
    public async Task AfterLeadingLandsRightAfterBasicsWhenAssigned()
    {
        int[] existing = [Core, Basics, Doctor, Builder];
        int at = RolePlacement.AfterLeadingIndex(existing, Basics, Positions, Positions[Basics]);
        await Assert.That(at).IsEqualTo(2);
    }

    [Test]
    public async Task AfterLeadingFallsBackToTheLastRoleRankedAtOrBeforeBasics()
    {
        int[] existing = [Core, Doctor, Builder];
        int at = RolePlacement.AfterLeadingIndex(existing, Basics, Positions, Positions[Basics]);
        await Assert.That(at).IsEqualTo(2);
    }

    [Test]
    public async Task AfterLeadingWithoutAnyLeadRoleIsTheFront()
    {
        int[] existing = [Builder, Miner];
        await Assert.That(RolePlacement.AfterLeadingIndex(existing, -1, Positions, long.MinValue)).IsEqualTo(0);
        await Assert.That(RolePlacement.AfterLeadingIndex([], Basics, Positions, Positions[Basics])).IsEqualTo(0);
    }

    /// The engine's own per-colonist ordering decides where a hand-added
    /// role goes: it moves ahead of every role it outranks and stops at a
    /// pinned or rule-bound role, while the other roles keep their places.
    [Test]
    public async Task RecommendedPlacementFollowsTheColonistsOrderingScoreAndBarriers()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        var crafter = RecsTestBed.Role(2, "Crafting");
        var doctor = RecsTestBed.Role(3, "Doctor");
        var hauler = RecsTestBed.Unskilled(4, "Hauling");
        var nightShift = RecsTestBed.Unskilled(5, "Hauling", "NightHaul");
        nightShift.HasRules = true;
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 15;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Exceptional;
        pawn.SkillLevels["Crafting"] = 6;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        pawn.SkillLevels["Medicine"] = 1;
        pawn.SignalBuckets["Medicine"] = SignalBucket.Poor;
        ColonyView colony = RecsTestBed.Colony([cook, crafter, doctor, hauler, nightShift], pawn);
        RecommendationPlan plan = RecommendationPlan.Build(colony);

        // The exceptional cook outranks the hand-ordered hauler and crafter.
        await Assert.That(plan.PlacementIndex(0, [hauler.Id, crafter.Id], cook.Id)).IsEqualTo(0);
        // A pinned crafter is a barrier: the cook lands right after it.
        pawn.Existing.Add(new AssignmentView { RoleId = crafter.Id, Enabled = true, Pinned = true });
        await Assert.That(plan.PlacementIndex(0, [crafter.Id, hauler.Id], cook.Id)).IsEqualTo(1);
        // A poor doctor outranks nothing and stays last.
        await Assert.That(plan.PlacementIndex(0, [crafter.Id, hauler.Id], doctor.Id)).IsEqualTo(2);
        // Rule-bound roles keep their own order and always append.
        await Assert.That(plan.PlacementIndex(0, [hauler.Id], nightShift.Id)).IsEqualTo(1);
        // Roles the plan does not know append too.
        await Assert.That(plan.PlacementIndex(0, [hauler.Id], 99)).IsEqualTo(1);
    }
}
