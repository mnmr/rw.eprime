using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// The shared context surface every rule builds on: signal lookup, candidate
/// bookkeeping (strongest wins), coverage queries and band gating.
public class RecsContextTests
{
    [Test]
    public async Task BestSignalConsumesThePrecomputedAggregateBucket()
    {
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 6;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Strong;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { RecsTestBed.Role(1, "Cooking") }, pawn));
        var bucket = context.BestSignal(0, context.RoleOf(1), out string skill, out var source);
        await Assert.That(bucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(skill).IsEqualTo("Cooking");
        await Assert.That(source).IsEqualTo(SignalSource.Aggregated);
    }

    [Test]
    public async Task BestSignalIsNeutralForRolesWithoutMappedSkills()
    {
        var pawn = RecsTestBed.Pawn();
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { RecsTestBed.Unskilled(1, "Hauling") }, pawn));
        var bucket = context.BestSignal(0, context.RoleOf(1), out string skill, out _);
        await Assert.That(bucket).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(skill == null).IsTrue();
    }

    [Test]
    public async Task AddCandidateKeepsTheStrongestEntry_AndVetoBlocksUnlessForced()
    {
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { RecsTestBed.Role(1, "Cooking") }, RecsTestBed.Pawn()));
        context.AddCandidate(0, 1, new Reason { RuleId = "a", TowardRoleId = -1 }, SignalBucket.Strong);
        context.AddCandidate(0, 1, new Reason { RuleId = "b", TowardRoleId = -1 }, SignalBucket.Neutral);
        await Assert.That(context.Candidates[0][1].Reason.RuleId).IsEqualTo("a");
        context.AddCandidate(0, 1, new Reason { RuleId = "c", TowardRoleId = -1 }, SignalBucket.Great);
        await Assert.That(context.Candidates[0][1].Reason.RuleId).IsEqualTo("c");

        context.Vetoed.Add(1);
        context.RemoveCandidate(0, 1);
        context.AddCandidate(0, 1, new Reason { RuleId = "d", TowardRoleId = -1 }, SignalBucket.Great);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsFalse();
        context.AddCandidate(0, 1, new Reason { RuleId = "e", TowardRoleId = -1 }, SignalBucket.Great, force: true);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
    }

    [Test]
    public async Task CoversRoleSeesTheRoleItselfAndCoveringCandidates()
    {
        var farmer = RecsTestBed.Role(1, "Cooking", "Grow", "Cut");
        var grower = RecsTestBed.Role(2, "Cooking", "Grow");
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { farmer, grower }, RecsTestBed.Pawn()));
        await Assert.That(context.CoversRole(0, grower)).IsFalse();
        context.AddCandidate(0, 1, new Reason { RuleId = "x", TowardRoleId = -1 }, SignalBucket.Neutral);
        await Assert.That(context.CoversRole(0, grower)).IsTrue();
        await Assert.That(context.CoversRole(0, farmer)).IsTrue();
        await Assert.That(context.HoldersOf(2)).IsEqualTo(1);
    }

    [Test]
    public async Task PassesBands_UsesStrictIntervalsAndOpenTop()
    {
        var medic = RecsTestBed.Role(1, "Doctor", "Tend");
        var doctor = RecsTestBed.Role(2, "Doctor");
        var low = RecsTestBed.Pawn(); low.SkillLevels["Medicine"] = 4;
        var high = RecsTestBed.Pawn(); high.SkillLevels["Medicine"] = 16;
        var colony = RecsTestBed.Colony(new List<RoleView> { medic, doctor }, low, high);
        colony.Paths.Add(RecsTestBed.Path(1, (1, 5, 15), (2, 15, 21)));
        var context = new EngineContext(colony);

        // low (4): outside Medic's strict 5-15 interval.
        await Assert.That(context.PassesBands(0, medic)).IsFalse();
        // high (16): inside Doctor's open-top 15-21; outgrew Medic.
        await Assert.That(context.PassesBands(1, doctor)).IsTrue();
        await Assert.That(context.PassesBands(1, medic)).IsFalse();

        // Removing stronger colonists does not relax a strict band.
        var alone = new EngineContext(RecsTestBed.Colony(new List<RoleView> { medic, doctor }, low));
        alone.Colony.Paths.Add(RecsTestBed.Path(1, (1, 5, 15), (2, 15, 21)));
        await Assert.That(alone.PassesBands(0, medic)).IsFalse();
        low.SkillLevels["Medicine"] = 0;
        var zero = new EngineContext(RecsTestBed.Colony(new List<RoleView> { medic, doctor }, low));
        zero.Colony.Paths.Add(RecsTestBed.Path(1, (1, 5, 15), (2, 15, 21)));
        await Assert.That(zero.PassesBands(0, medic)).IsFalse();

        // Targets are strict too; the draft may still promote a pawn for need.
        var best = RecsTestBed.Pawn(); best.SkillLevels["Medicine"] = 10;
        var soloDoc = new EngineContext(RecsTestBed.Colony(new List<RoleView> { medic, doctor }, best));
        soloDoc.Colony.Paths.Add(RecsTestBed.Path(1, (1, 5, 15), (2, 15, 21)));
        await Assert.That(soloDoc.PassesBands(0, doctor)).IsFalse();
    }

    [Test]
    public async Task PathMath_TargetsAndUnskilledEntries()
    {
        var path = RecsTestBed.Path(1, (1, 0, 5), (2, 5, 15), (3, 15, 21));
        await Assert.That(PathMath.IsTarget(path, 0)).IsFalse();
        await Assert.That(PathMath.IsTarget(path, 2)).IsTrue();
        await Assert.That(string.Join(",", PathMath.LowerBandEntries(path, 2))).IsEqualTo("0,1");
        await Assert.That(PathMath.InsideBand(path, 2, 20)).IsTrue();  // 21 = open top
        await Assert.That(PathMath.InsideBand(path, 1, 15)).IsFalse(); // [min, max)

        // A role in no path never gates; an unskilled path entry never gates.
        var unskilled = RecsTestBed.Unskilled(4, "Hauling");
        var colony = RecsTestBed.Colony(new List<RoleView> { unskilled }, RecsTestBed.Pawn());
        colony.Paths.Add(RecsTestBed.Path(2, (4, 15, 21)));
        var context = new EngineContext(colony);
        await Assert.That(context.PassesBands(0, unskilled)).IsTrue();
    }

    [Test]
    public async Task BasePositions_TemplateSlotsAndNaturalFallback()
    {
        var a = RecsTestBed.Role(1, "Cooking"); a.NaturalPriority = 100f;
        var b = RecsTestBed.Role(2, "Crafting"); b.NaturalPriority = 50f;
        var unlisted = RecsTestBed.Role(3, "Doctor"); unlisted.NaturalPriority = 60f;
        var positions = Ordering.BasePositions(
            new List<RoleView> { a, b, unlisted }, new List<int> { 1, 2 });
        await Assert.That(positions[1]).IsEqualTo(0L);
        await Assert.That(positions[2]).IsEqualTo(Ordering.Slot);
        // Unlisted role of priority 60 lands after the last >= 60 entry (a, slot 0).
        await Assert.That(positions[3] > 0L && positions[3] < Ordering.Slot).IsTrue();
        // Higher than everything pinned: before the whole template.
        unlisted.NaturalPriority = 200f;
        positions = Ordering.BasePositions(new List<RoleView> { a, b, unlisted }, new List<int> { 1, 2 });
        await Assert.That(positions[3] < 0L).IsTrue();
    }
}
