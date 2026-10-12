using WorkRoles.Core;

namespace WorkRoles.Core.Tests.Caching;

// Independent invalidation contract: stale/rebuilt identities cannot be
// demonstrated through a planner result when equal content masks the churn.
public class ModelRevisionsTests
{
    [Test]
    public async Task GlobalPublicationReachesKeyedConsumersButTargetedEditsRemainLocal()
    {
        var revisions = new ModelRevisions();
        revisions.RoleChanged(1, ModelDomain.RolePresentation);
        revisions.PawnChanged(10, ModelDomain.RuntimeRules);
        revisions.Changed(ModelDomain.RolePresentation | ModelDomain.RuntimeRules);
        int global = revisions.Current;
        await Assert.That(revisions.ForRole(1, ModelDomain.RolePresentation)).IsEqualTo(global);
        await Assert.That(revisions.ForRole(2, ModelDomain.RolePresentation)).IsEqualTo(global);
        await Assert.That(revisions.ForPawn(10, ModelDomain.RuntimeRules)).IsEqualTo(global);
        await Assert.That(revisions.ForPawn(11, ModelDomain.RuntimeRules)).IsEqualTo(global);
        revisions.AggregateChanged(ModelDomain.RuntimeRules);
        await Assert.That(revisions.For(ModelDomain.RuntimeRules)).IsEqualTo(revisions.Current);
        await Assert.That(revisions.ForPawn(11, ModelDomain.RuntimeRules)).IsEqualTo(global);
        revisions.RoleChanged(1, ModelDomain.RolePresentation);
        revisions.PawnChanged(10, ModelDomain.RuntimeRules);
        await Assert.That(revisions.ForRole(2, ModelDomain.RolePresentation)).IsEqualTo(global);
        await Assert.That(revisions.ForPawn(11, ModelDomain.RuntimeRules)).IsEqualTo(global);
    }

    [Test]
    public async Task PawnTogglesLeavePlanInputsAndOtherPawnsUnchanged()
    {
        var revisions = new ModelRevisions();
        revisions.PawnChanged(1, ModelDomain.Assignments | ModelDomain.AssignmentPresentation);
        revisions.PawnChanged(2, ModelDomain.Assignments | ModelDomain.AssignmentPresentation);
        int plan = revisions.For(ModelDomain.Assignments);
        int other = revisions.ForPawn(2, ModelDomain.AssignmentPresentation);
        revisions.PawnChanged(1, ModelDomain.AssignmentPresentation);
        await Assert.That(revisions.For(ModelDomain.Assignments)).IsEqualTo(plan);
        await Assert.That(revisions.ForPawn(2, ModelDomain.AssignmentPresentation)).IsEqualTo(other);
        await Assert.That(revisions.ForPawn(1, ModelDomain.AssignmentPresentation)).IsEqualTo(revisions.Current);
        revisions.PawnChanged(1, ModelDomain.Assignments | ModelDomain.AssignmentPresentation);
        await Assert.That(revisions.For(ModelDomain.Assignments)).IsEqualTo(revisions.Current);
    }

    [Test]
    public async Task ActivationAndRuntimeRulesDoNotExpireWorkOrRecommendationFacts()
    {
        var revisions = new ModelRevisions();
        revisions.RoleChanged(1, ModelDomain.RoleWork | ModelDomain.RoleCapability);
        revisions.RoleChanged(2, ModelDomain.RoleRecommendation);
        int work = revisions.For(ModelDomain.RoleWork);
        int recommendation = revisions.For(ModelDomain.RoleRecommendation);
        revisions.RoleChanged(1, ModelDomain.RoleActivation);
        revisions.PawnChanged(10, ModelDomain.RuntimeRules);
        await Assert.That(revisions.For(ModelDomain.RoleWork)).IsEqualTo(work);
        await Assert.That(revisions.ForRole(1, ModelDomain.RoleWork)).IsEqualTo(work);
        await Assert.That(revisions.For(ModelDomain.RoleRecommendation)).IsEqualTo(recommendation);
        await Assert.That(revisions.ForPawn(11, ModelDomain.RuntimeRules)).IsEqualTo(0);
    }

    [Test]
    public async Task BatchPublishesOneSequenceAndEmptyChangesPreserveEveryStamp()
    {
        var revisions = new ModelRevisions();
        using (revisions.Batch())
        {
            revisions.RoleChanged(1, ModelDomain.RolePresentation);
            using (revisions.Batch()) revisions.PawnChanged(2, ModelDomain.Assignments);
            revisions.RoleChanged(3, ModelDomain.RoleWork);
        }
        int sequence = revisions.Current;
        await Assert.That(sequence).IsEqualTo(1);
        await Assert.That(revisions.ForRole(1, ModelDomain.RolePresentation)).IsEqualTo(sequence);
        await Assert.That(revisions.ForPawn(2, ModelDomain.Assignments)).IsEqualTo(sequence);
        using (revisions.Batch()) revisions.Changed(ModelDomain.None);
        await Assert.That(revisions.Current).IsEqualTo(sequence);
        revisions.RoleChanged(3, ModelDomain.RolePresentation);
        await Assert.That(revisions.Current).IsEqualTo(2);
    }

    [Test]
    public async Task StoreOwnershipAndTeardownReleaseIdentityState()
    {
        var first = new ModelRevisions();
        var second = new ModelRevisions();
        first.PawnChanged(1, ModelDomain.Assignments);
        first.RoleChanged(2, ModelDomain.RoleWork);
        await Assert.That(second.ForPawn(1, ModelDomain.Assignments)).IsEqualTo(0);
        first.ForgetPawn(1);
        first.ForgetRole(2);
        await Assert.That(first.ForPawn(1, ModelDomain.Assignments)).IsEqualTo(0);
        await Assert.That(first.ForRole(2, ModelDomain.RoleWork)).IsEqualTo(0);
        first.Reset(); first.Reset();
        await Assert.That(first.Current).IsEqualTo(0);
    }
}
