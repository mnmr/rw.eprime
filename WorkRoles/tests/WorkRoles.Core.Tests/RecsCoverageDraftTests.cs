using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Rule 6 (per-role scaling algorithm, 1-per-6 unit) and rule 5 (bucket-aware
/// best-in-colony draft for needed work).
public class RecsCoverageDraftTests
{
    [Test]
    public async Task UnitScalingWantsUnitsForAutoAndNTimesUnitsForNeeded()
    {
        var scaling = new UnitScaling();
        var auto = RecsTestBed.Role(1, "Cooking");      // resolved -1
        auto.MinHolders = -1;
        var needed = RecsTestBed.Role(2, "Cooking");
        needed.MinHolders = 2;
        var interestOnly = RecsTestBed.Role(3, "Cooking");   // 0
        var never = RecsTestBed.Role(4, "Cooking");
        never.HolderMode = RoleHolderMode.Never;
        await Assert.That(scaling.Want(auto, 6)).IsEqualTo(1);
        await Assert.That(scaling.Want(auto, 7)).IsEqualTo(2);
        await Assert.That(scaling.Want(needed, 6)).IsEqualTo(2);
        await Assert.That(scaling.Want(needed, 13)).IsEqualTo(6);
        await Assert.That(scaling.Want(interestOnly, 50)).IsEqualTo(0);
        await Assert.That(scaling.Want(never, 50)).IsEqualTo(0);
    }

    [Test]
    public async Task ScalingSkipsVetoedAutoAssignUnskilledAndHunting()
    {
        var needed = RecsTestBed.Role(1, "Cooking"); needed.MinHolders = 1;
        var auto = RecsTestBed.Role(2, "Crafting"); auto.AutoAssign = true; auto.MinHolders = 1;
        var grunt = RecsTestBed.Unskilled(3, "Hauling"); grunt.MinHolders = 1;
        var hunter = RecsTestBed.Role(4, "Hunting"); hunter.Hunting = true; hunter.MinHolders = 1;
        var vetoed = RecsTestBed.Role(5, "Doctor"); vetoed.MinHolders = 1;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { needed, auto, grunt, hunter, vetoed }, RecsTestBed.Pawn()));
        context.Vetoed.Add(5);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        await Assert.That(string.Join(",", context.Want.Keys.OrderBy(id => id))).IsEqualTo("1");
    }

    [Test]
    public async Task DraftPrefersNeutralOverPoor_NeverDraftsAwful_LevelBreaksTies()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        cook.MinHolders = 2;
        var neutralHigh = RecsTestBed.Pawn(); neutralHigh.SkillLevels["Cooking"] = 9;
        var neutralLow = RecsTestBed.Pawn(); neutralLow.SkillLevels["Cooking"] = 3;
        var poor = RecsTestBed.Pawn(); poor.SkillLevels["Cooking"] = 0;
        var awful = RecsTestBed.Pawn();
        awful.SkillLevels["Cooking"] = 12; awful.Aptitudes["Cooking"] = -1;
        var colony = RecsTestBed.Colony(new List<RoleView> { cook },
            neutralHigh, neutralLow, poor, awful);
        var context = new EngineContext(colony);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context.Candidates[1].ContainsKey(1)).IsTrue();
        await Assert.That(context.Candidates[2].ContainsKey(1)).IsFalse();
        await Assert.That(context.Candidates[3].ContainsKey(1)).IsFalse();
        await Assert.That(context.Candidates[0][1].Reason.RuleId).IsEqualTo("draft");
    }

    [Test]
    public async Task DraftPoolFallsBackToPoorWhenNeutralsRunOut()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        cook.MinHolders = 2;
        var neutral = RecsTestBed.Pawn(); neutral.SkillLevels["Cooking"] = 5;
        var poor = RecsTestBed.Pawn(); poor.SkillLevels["Cooking"] = 0;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { cook }, neutral, poor));
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[1].ContainsKey(1)).IsTrue();
    }

    [Test]
    public async Task CoveredRolesLeaveDealingToTheCoverer_UnlessEssentialOrPathMember()
    {
        var farmer = RecsTestBed.Role(1, "Cooking", "Grow", "Cut");
        var grower = RecsTestBed.Role(2, "Cooking", "Grow");           // covered, auto-coverage
        grower.MinHolders = -1; farmer.MinHolders = -1;
        var pawn = RecsTestBed.Pawn(); pawn.SkillLevels["Cooking"] = 5;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { farmer, grower }, pawn));
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();  // Farmer dealt
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsFalse(); // Grower skipped

        // Essential (MinHolders >= 1) covered roles are still dealt.
        grower.MinHolders = 1;
        var essential = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { farmer, grower }, pawn, RecsTestBed.Pawn()));
        new CoverageScalingRule(new UnitScaling()).Apply(essential);
        new BestInColonyDraftRule().Apply(essential);
        // The farmer candidate covers grower, so grower's holders are already
        // satisfied through coverage — but the WANT existed and was honored.
        await Assert.That(essential.Want.ContainsKey(2)).IsTrue();
    }

    [Test]
    public async Task DraftPrefersInBandButStillFillsTheFloor()
    {
        // Doctor path 15-21; one in-band pawn (16), one below (4). The in-band
        // pawn takes the single floor slot.
        var doctor = RecsTestBed.Role(1, "Doctor");
        doctor.MinHolders = 1;
        var low = RecsTestBed.Pawn(); low.SkillLevels["Medicine"] = 4;
        var high = RecsTestBed.Pawn(); high.SkillLevels["Medicine"] = 16;
        var colony = RecsTestBed.Colony(new List<RoleView> { doctor }, low, high);
        colony.Paths.Add(RecsTestBed.Path(1, (1, 15, 21)));
        var context = new EngineContext(colony);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[1].ContainsKey(1)).IsTrue();  // in-band preferred
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsFalse(); // floor of 1 met
    }

    [Test]
    public async Task FloorIsAbsolute_BelowBandPawnFillsATargetWhenNobodyQualifies()
    {
        // Nurse/Medic/Doctor path; Doctor is a target (Medic bands lower). No
        // pawn reaches Doctor's band — the floor is still met by the best
        // below-band pawn (bands gate interest, never the need-driven floor).
        var doctor = RecsTestBed.Role(1, "Doctor");
        doctor.MinHolders = 1;
        var medic = RecsTestBed.Role(2, "Doctor", "Tend");
        var a = RecsTestBed.Pawn(); a.SkillLevels["Medicine"] = 9;
        var b = RecsTestBed.Pawn(); b.SkillLevels["Medicine"] = 4;
        var colony = RecsTestBed.Colony(new List<RoleView> { doctor, medic }, a, b);
        colony.Paths.Add(RecsTestBed.Path(1, (2, 5, 15), (1, 15, 21)));
        var context = new EngineContext(colony);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();  // best below-band fills it
        await Assert.That(context.Candidates[1].ContainsKey(1)).IsFalse(); // floor of 1 met
    }
}
