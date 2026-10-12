using WorkRoles.Core;

namespace WorkRoles.Core.Tests.Caching;

// Composite role dependencies are an independent cache contract: a player's
// block/unblock edit changes the union, while excluded veto skill gates cannot.
public class RoleRevisionDependenciesTests
{
    [Test]
    public async Task ImportedMemberBecomingBlockerExpiresTheOldCompositeUnion()
    {
        var revisions = new ModelRevisions();
        revisions.RoleChanged(3, ModelDomain.RoleWork);
        int prior = revisions.ForRole(3, ModelDomain.RoleWork);
        revisions.RoleChanged(3, RoleRevisionDependencies.ForComposite(
            ModelDomain.RoleRecommendation | ModelDomain.RoleCapability,
            compositeBlocker: false, memberBlocker: true, memberBlockerChanged: true));
        await Assert.That(revisions.ForRole(3, ModelDomain.RoleWork) > prior).IsTrue();
        revisions.RoleChanged(4, ModelDomain.RoleWork);
        int vetoUnion = revisions.ForRole(4, ModelDomain.RoleWork);
        revisions.RoleChanged(4, RoleRevisionDependencies.ForComposite(
            ModelDomain.RoleRecommendation | ModelDomain.RoleCapability,
            compositeBlocker: true, memberBlocker: true, memberBlockerChanged: true));
        await Assert.That(revisions.ForRole(4, ModelDomain.RoleWork)).IsEqualTo(vetoUnion);
    }

    [Test]
    public async Task VetoMemberGatesKeepOrdinaryCompositeWorkWarm()
    {
        var revisions = new ModelRevisions();
        revisions.RoleChanged(3, ModelDomain.RoleWork);
        int prior = revisions.ForRole(3, ModelDomain.RoleWork);
        revisions.RoleChanged(3, RoleRevisionDependencies.ForComposite(
            ModelDomain.RoleWork | ModelDomain.RoleCapability,
            compositeBlocker: false, memberBlocker: true, memberBlockerChanged: false));
        await Assert.That(revisions.ForRole(3, ModelDomain.RoleWork)).IsEqualTo(prior);
        revisions.RoleChanged(4, RoleRevisionDependencies.ForComposite(
            ModelDomain.RoleWork | ModelDomain.RoleCapability,
            compositeBlocker: true, memberBlocker: true, memberBlockerChanged: false));
        await Assert.That(revisions.ForRole(4, ModelDomain.RoleWork) > prior).IsTrue();
    }
}
