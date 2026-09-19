using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// The Mechanitor role goes to every mechanitor and to nobody else, however
/// good a non-mechanitor's crafting is, and a non-mechanitor holding it is
/// told why it is dropped.
public class MechanitorRoleScenarioTests
{
    [Test]
    public async Task OnlyMechanitorsAreRecommendedTheMechanitorRole()
    {
        var recs = new RecsProjection().WorkType(
            "Smithing", "Crafting", 400, "RepairMech", "DoBillsMechGestator", "SmithWeapons");
        RecommendationRoleSource mechanitor = recs.RoleByGiver(
            1, 0, 100, "RepairMech", "DoBillsMechGestator");
        mechanitor.SpecialRole = RecommendationSpecialRoleKind.Mechanitor;

        var crafter = new PawnView { CapableWorkTypes = { "Smithing" } };
        crafter.SkillLevels["Crafting"] = 18;
        crafter.SignalBuckets["Crafting"] = SignalBucket.Exceptional;
        crafter.Existing.Add(new AssignmentView { RoleId = mechanitor.Id, Enabled = true });
        var linked = new PawnView { CapableWorkTypes = { "Smithing" }, IsMechanitor = true };
        linked.SkillLevels["Crafting"] = 3;
        linked.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        var secondLinked = new PawnView { CapableWorkTypes = { "Smithing" }, IsMechanitor = true };
        secondLinked.SkillLevels["Crafting"] = 0;
        secondLinked.SignalBuckets["Crafting"] = SignalBucket.Poor;

        RecommendationPlan plan = recs.Plan(crafter, linked, secondLinked);

        await Assert.That(RecsProjection.Holds(plan, 0, mechanitor.Id)).IsFalse();
        await Assert.That(RecsProjection.Holds(plan, 1, mechanitor.Id)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 2, mechanitor.Id)).IsTrue();
        await Assert.That(plan.TryGetExplanation(0, mechanitor.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.RejectReason).IsEqualTo(PickRejectReason.NotMechanitor);
    }
}
