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

    /// The shipped Mechanitor holds its slot right after Basics no matter how
    /// the mechanitor's crafting scores against their champion roles.
    [Test]
    public async Task ShippedMechanitorSitsRightAfterBasicsRegardlessOfScore()
    {
        RoleView basics = RecsTestBed.Unskilled(1, "BasicWorker");
        basics.TemplateDefName = "WS_Basics";
        basics.AutoAssign = true;
        RecsTestBed.Require(basics, 0, 100);
        // Placement policy and tuning come from the shipped def, so this fails
        // if Roles.xml stops pinning the role.
        WorkRoles.Core.Tests.SampleColony.RoleDefaults.DefTuning shipped =
            WorkRoles.Core.Tests.SampleColony.RoleDefaults.ByDefName["WS_Mechanitor"];
        RoleView mechanitor = RecsTestBed.Role(2, "Smithing", "RepairMech", "DoBillsMechGestator");
        mechanitor.TemplateDefName = "WS_Mechanitor";
        mechanitor.RequiresMechanitor = true;
        mechanitor.PreserveRecommendationOrder = shipped.PreserveRecommendationOrder;
        mechanitor.Category = shipped.Category;
        mechanitor.Time = shipped.Time;
        RecsTestBed.Require(mechanitor, shipped.ColonyMin, shipped.Coverage);
        RoleView cook = RecsTestBed.Role(3, "Cooking");
        cook.TemplateDefName = "WS_Cook";
        RecsTestBed.Require(cook, 1);
        RoleView crafter = RecsTestBed.Role(4, "Crafting");
        crafter.TemplateDefName = "WS_Crafter";
        RecsTestBed.Require(crafter, 1);

        // An exceptional cook and crafter with only neutral crafting for mech work
        // (the mechanitor role shares the Crafting skill).
        var pawn = RecsTestBed.Pawn();
        pawn.CapableWorkTypes.Add("Smithing");
        pawn.CapableWorkTypes.Add("BasicWorker");
        pawn.IsMechanitor = true;
        pawn.SkillLevels["Cooking"] = 18;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Exceptional;
        pawn.SkillLevels["Crafting"] = 3;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony([basics, mechanitor, cook, crafter], pawn);

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        string order = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(i => plan.RoleAt(0, i)));
        await Assert.That(order).StartsWith($"{basics.Id},{mechanitor.Id},");
        await Assert.That(RecsProjection.Holds(plan, 0, cook.Id)).IsTrue();
    }
}
