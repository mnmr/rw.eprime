using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// A downed colonist cannot work: the engine neither recommends roles to
/// them nor counts what they hold toward colony needs, and leaves their
/// assignments exactly as they are for when they recover.
public class IncapacitatedPawnScenarioTests
{
    [Test]
    public async Task DownedHolderIsSkippedAndTheRoleGoesToSomeoneElse()
    {
        var recs = new RecsProjection()
            .WorkType("Doctor", "Medicine", 800, "Tend")
            .WorkType("Hauling", null, 100, "Haul");
        RecommendationRoleSource doctor = recs.RoleByWorkType(1, 1, 0, "Doctor");
        RecommendationRoleSource hauler = recs.RoleByWorkType(2, 0, 100, "Hauling");

        // The best medic is in a coma, holding a pinned Doctor and Hauler.
        var comatose = new PawnView { CapableWorkTypes = { "Doctor", "Hauling" }, Incapacitated = true };
        comatose.SkillLevels["Medicine"] = 15;
        comatose.SignalBuckets["Medicine"] = SignalBucket.Exceptional;
        comatose.Existing.Add(new AssignmentView { RoleId = doctor.Id, Enabled = true, Pinned = true });
        comatose.Existing.Add(new AssignmentView { RoleId = hauler.Id, Enabled = true });
        var backup = new PawnView { CapableWorkTypes = { "Doctor", "Hauling" } };
        backup.SkillLevels["Medicine"] = 4;
        backup.SignalBuckets["Medicine"] = SignalBucket.Neutral;

        RecommendationPlan plan = recs.Plan(comatose, backup);

        // The backup covers the colony's one required doctor.
        await Assert.That(RecsProjection.Holds(plan, 1, doctor.Id)).IsTrue();
        // The comatose colonist keeps exactly what they had, in order.
        await Assert.That(string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(i => plan.RoleAt(0, i))))
            .IsEqualTo($"{doctor.Id},{hauler.Id}");
        await Assert.That(plan.TryGetExplanation(0, doctor.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SpecialPickReason).IsEqualTo(SpecialPickReason.Incapacitated);
    }
}
