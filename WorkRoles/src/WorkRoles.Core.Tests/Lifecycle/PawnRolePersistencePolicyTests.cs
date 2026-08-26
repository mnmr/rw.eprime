using WorkRoles.Core;

namespace WorkRoles.Core.Tests.Lifecycle;

public class PawnRolePersistencePolicyTests
{
    [Test]
    public async Task LivingColonyPawnIsAutoAssignedEvenBeforeAdmission()
    {
        // A quest-generated joiner is neither spawned nor on the roster yet;
        // assignment must still happen at the joining event so the roles
        // survive an intervening save (the pawn is anchored in WorldPawns).
        await Assert.That(PawnRolePersistencePolicy.ShouldAutoAssign(
            isAlive: true,
            isColonyMember: true)).IsTrue();
    }

    [Test]
    public async Task NonColonyPawnIsNeverAutoAssigned()
    {
        // Covers raiders/visitors and player-faction subhumans (ghouls):
        // both fail the colony-member fact.
        await Assert.That(PawnRolePersistencePolicy.ShouldAutoAssign(
            isAlive: true,
            isColonyMember: false)).IsFalse();
    }

    [Test]
    public async Task DeadPawnIsNeverAutoAssigned()
    {
        await Assert.That(PawnRolePersistencePolicy.ShouldAutoAssign(
            isAlive: false,
            isColonyMember: true)).IsFalse();
    }

    [Test]
    public async Task AnchoredLivingColonyPawnIsRetained()
    {
        // Anchor covers map-held pawns, world pawns, and any holder chain:
        // caravans, travelling transporters, gravship-held containers, and
        // modded holders such as vehicle passengers.
        await Assert.That(PawnRolePersistencePolicy.ShouldRetain(
            isAlive: true,
            isColonyMember: true,
            hasPersistenceAnchor: true)).IsTrue();
    }

    [Test]
    public async Task DetachedLivingColonyPawnIsNotRetained()
    {
        // Generated preview/simulation pawns have no anchor; saving their
        // reference keys would spam unresolvable-reference errors on load.
        await Assert.That(PawnRolePersistencePolicy.ShouldRetain(
            isAlive: true,
            isColonyMember: true,
            hasPersistenceAnchor: false)).IsFalse();
    }

    [Test]
    public async Task AnchoredNonColonyPawnIsNotRetained()
    {
        // A ghoul-turned colonist stays player faction on its map but is no
        // longer a colony member; its role set must not persist.
        await Assert.That(PawnRolePersistencePolicy.ShouldRetain(
            isAlive: true,
            isColonyMember: false,
            hasPersistenceAnchor: true)).IsFalse();
    }

    [Test]
    public async Task DeadAnchoredColonyPawnIsNotRetained()
    {
        await Assert.That(PawnRolePersistencePolicy.ShouldRetain(
            isAlive: false,
            isColonyMember: true,
            hasPersistenceAnchor: true)).IsFalse();
    }
}
