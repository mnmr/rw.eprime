using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Rule 8 (gun gate + shooting tiers, hardcoded) and rule 9 (fire-safety gene
/// grant, hardcoded).
public class RecsHunterFireTests
{
    private static ColonyView HunterColony(params PawnView[] pawns)
    {
        var hunter = RecsTestBed.Role(1, "Hunting");
        hunter.Hunting = true;
        var colony = RecsTestBed.Colony(new List<RoleView> { hunter }, pawns);
        colony.HunterRoleId = 1;
        return colony;
    }

    [Test]
    public async Task EveryCapableGunCarrierHuntsAtItsTier_OthersNever()
    {
        var low = RecsTestBed.Pawn(); low.HasRangedWeapon = true; low.ShootingLevel = 7;
        var mid = RecsTestBed.Pawn(); mid.HasRangedWeapon = true; mid.ShootingLevel = 16;
        var high = RecsTestBed.Pawn(); high.HasRangedWeapon = true; high.ShootingLevel = 20;
        var unarmed = RecsTestBed.Pawn();
        var context = new EngineContext(HunterColony(low, mid, high, unarmed));
        new HunterRule().Apply(context);
        await Assert.That(context.HunterTiers[0]).IsEqualTo(0);
        await Assert.That(context.HunterTiers[1]).IsEqualTo(1);
        await Assert.That(context.HunterTiers[2]).IsEqualTo(2);
        await Assert.That(context.HunterTiers[3]).IsEqualTo(-1);
        await Assert.That(context.Candidates[3].ContainsKey(1)).IsFalse();
        await Assert.That(context.Candidates[0][1].Reason.RuleId).IsEqualTo("hunter");
    }

    [Test]
    public async Task AtLeastOneTierZeroWheneverAnyoneHunts()
    {
        var sniperA = RecsTestBed.Pawn(); sniperA.HasRangedWeapon = true; sniperA.ShootingLevel = 19;
        var sniperB = RecsTestBed.Pawn(); sniperB.HasRangedWeapon = true; sniperB.ShootingLevel = 20;
        var context = new EngineContext(HunterColony(sniperA, sniperB));
        new HunterRule().Apply(context);
        // The best shooter is pulled down to tier 0 so food gets shot.
        await Assert.That(context.HunterTiers[1]).IsEqualTo(0);
        await Assert.That(context.HunterTiers[0]).IsEqualTo(2);
    }

    [Test]
    public async Task IrrelevantWithoutAResolvedOrNonVetoedHunterRole()
    {
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { RecsTestBed.Role(1, "Cooking") }, RecsTestBed.Pawn()));
        await Assert.That(new HunterRule().Relevant(context)).IsFalse();
        var vetoed = new EngineContext(HunterColony(RecsTestBed.Pawn()));
        vetoed.Vetoed.Add(1);
        await Assert.That(new HunterRule().Relevant(vetoed)).IsFalse();
    }

    [Test]
    public async Task FireFearGetsTheBlockerDespiteTheExclusionVeto()
    {
        var blocker = RecsTestBed.Role(1, "Cooking", "Firefight");
        blocker.Blocker = true;
        var scared = RecsTestBed.Pawn(); scared.FireFear = true;
        var calm = RecsTestBed.Pawn();
        var colony = RecsTestBed.Colony(new List<RoleView> { blocker }, scared, calm);
        colony.FireBlockerRoleId = 1;
        var context = new EngineContext(colony);
        new ExclusionsRule().Apply(context);
        new FireSafetyRule().Apply(context, 0);
        new FireSafetyRule().Apply(context, 1);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context.FireGranted[0]).IsTrue();
        await Assert.That(context.Candidates[1].ContainsKey(1)).IsFalse();
        await Assert.That(context.FireGranted[1]).IsFalse();
    }
}
