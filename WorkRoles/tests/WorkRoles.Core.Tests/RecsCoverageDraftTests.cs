using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Rule 6 (per-role scaling algorithm, 1-per-6 unit) and rule 5 (bucket-aware
/// best-in-colony draft for needed work).
public class RecsCoverageDraftTests
{
    [Test]
    public async Task UnitScalingCalculatesRequiredTotalsForAutoAndNeededRoles()
    {
        var scaling = new UnitScaling();
        var auto = RecsTestBed.Role(1, "Cooking");      // resolved -1
        auto.RequiredTotal = -1;
        var needed = RecsTestBed.Role(2, "Cooking");
        needed.RequiredTotal = 2;
        var interestOnly = RecsTestBed.Role(3, "Cooking");   // 0
        await Assert.That(scaling.Requirement(auto, 6).RequiredTotal).IsEqualTo(1);
        await Assert.That(scaling.Requirement(auto, 7).RequiredTotal).IsEqualTo(2);
        await Assert.That(scaling.Requirement(needed, 6).RequiredTotal).IsEqualTo(2);
        await Assert.That(scaling.Requirement(needed, 13).RequiredTotal).IsEqualTo(6);
        await Assert.That(scaling.Requirement(interestOnly, 50).RequiredTotal)
            .IsEqualTo(0);
        // Never-mode roles are guarded by ExclusionsRule/AddCandidate, not by
        // scaling: see RecsExclusionAutoTests.
    }

    [Test]
    public async Task CustomHolderMinimumIsAbsoluteAcrossColonySizes()
    {
        var scaling = new UnitScaling();
        var role = RecsTestBed.Role(1, "Cooking");
        role.HolderMode = RoleHolderMode.Custom;
        role.RequiredTotal = 2;

        await Assert.That(scaling.Requirement(role, 6).RequiredTotal).IsEqualTo(2);
        await Assert.That(scaling.Requirement(role, 13).RequiredTotal).IsEqualTo(2);
    }

    [Test]
    public async Task AutoHolderDemandCannotExceedTheColonySize()
    {
        var scaling = new UnitScaling();
        var role = RecsTestBed.Role(1, "Hauling");
        role.HolderMode = RoleHolderMode.Auto;
        role.RequiredTotal = 8;

        await Assert.That(scaling.Requirement(role, 7).RequiredTotal).IsEqualTo(7);
    }

    [Test]
    public async Task ScalingSkipsVetoedAndAutoAssignedButIncludesUnskilledAndHunting()
    {
        var needed = RecsTestBed.Role(1, "Cooking"); needed.RequiredTotal = 1;
        var auto = RecsTestBed.Role(2, "Crafting"); auto.AutoAssign = true; auto.RequiredTotal = 1;
        var grunt = RecsTestBed.Unskilled(3, "Hauling"); grunt.RequiredTotal = 1;
        var hunter = RecsTestBed.Role(4, "Hunting"); hunter.Hunting = true; hunter.RequiredTotal = 1;
        var vetoed = RecsTestBed.Role(5, "Doctor"); vetoed.RequiredTotal = 1;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { needed, auto, grunt, hunter, vetoed }, RecsTestBed.Pawn()));
        context.Vetoed.Add(5);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        await Assert.That(string.Join(",",
            context.RequiredTotal.Keys.OrderBy(id => id))).IsEqualTo("1,3,4");
    }

    [Test]
    public async Task DraftPrefersNeutralOverPoor_NeverDraftsAwful()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        cook.RequiredTotal = 2;
        var neutralHigh = RecsTestBed.Pawn(); neutralHigh.SkillLevels["Cooking"] = 9;
        var neutralLow = RecsTestBed.Pawn(); neutralLow.SkillLevels["Cooking"] = 3;
        var poor = RecsTestBed.Pawn(); poor.SkillLevels["Cooking"] = 0;
        poor.SignalBuckets["Cooking"] = SignalBucket.Poor;
        var awful = RecsTestBed.Pawn();
        awful.SkillLevels["Cooking"] = 12; awful.SignalBuckets["Cooking"] = SignalBucket.Awful;
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
        cook.RequiredTotal = 2;
        var neutral = RecsTestBed.Pawn(); neutral.SkillLevels["Cooking"] = 5;
        var poor = RecsTestBed.Pawn(); poor.SkillLevels["Cooking"] = 0;
        poor.SignalBuckets["Cooking"] = SignalBucket.Poor;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { cook }, neutral, poor));
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[1].ContainsKey(1)).IsTrue();
    }

    [Test]
    public async Task DraftRecordsTheCompleteRankingAndNumberOfOpenSlots()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        cook.RequiredTotal = 2;
        var best = RecsTestBed.Pawn(); best.SkillLevels["Cooking"] = 9;
        var second = RecsTestBed.Pawn(); second.SkillLevels["Cooking"] = 5;
        var third = RecsTestBed.Pawn(); third.SkillLevels["Cooking"] = 2;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { cook }, best, second, third));

        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);

        DraftRanking firstRank = context.DraftRankings[0][cook.Id];
        DraftRanking secondRank = context.DraftRankings[1][cook.Id];
        DraftRanking thirdRank = context.DraftRankings[2][cook.Id];
        await Assert.That(firstRank.Rank).IsEqualTo(1);
        await Assert.That(secondRank.Rank).IsEqualTo(2);
        await Assert.That(thirdRank.Rank).IsEqualTo(3);
        await Assert.That(thirdRank.EligibleCount).IsEqualTo(3);
        await Assert.That(thirdRank.OpenSlots).IsEqualTo(2);
        await Assert.That(thirdRank.SkillDefName).IsEqualTo("Cooking");
        await Assert.That(thirdRank.SkillLevel).IsEqualTo(2);
        await Assert.That(context.Candidates[2].ContainsKey(cook.Id)).IsFalse();
    }

    [Test]
    public async Task DraftRecordsRemainingCandidatesWhenCoverageWasAlreadyFull()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        cook.RequiredTotal = 1;
        var holder = RecsTestBed.Pawn(); holder.SkillLevels["Cooking"] = 3;
        var remaining = RecsTestBed.Pawn(); remaining.SkillLevels["Cooking"] = 9;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { cook }, holder, remaining));
        context.AddCandidate(0, cook.Id, new Reason
        {
            RuleId = "signals", SkillDefName = "Cooking", TowardRoleId = -1,
        }, SignalBucket.Strong);

        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);

        DraftRanking rank = context.DraftRankings[1][cook.Id];
        await Assert.That(rank.Rank).IsEqualTo(1);
        await Assert.That(rank.EligibleCount).IsEqualTo(1);
        await Assert.That(rank.OpenSlots).IsEqualTo(0);
        await Assert.That(context.Candidates[1].ContainsKey(cook.Id)).IsFalse();
    }

    [Test]
    public async Task CoveredRolesLeaveDealingToTheCoverer_UnlessEssentialOrPathMember()
    {
        var farmer = RecsTestBed.Role(1, "Cooking", "Grow", "Cut");
        var grower = RecsTestBed.Role(2, "Cooking", "Grow");           // covered, auto-coverage
        grower.RequiredTotal = -1; farmer.RequiredTotal = -1;
        var pawn = RecsTestBed.Pawn(); pawn.SkillLevels["Cooking"] = 5;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { farmer, grower }, pawn));
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();  // Farmer dealt
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsFalse(); // Grower skipped

        // Essential (RequiredTotal >= 1) covered roles are still dealt once the
        // required total exceeds what coverage supplies: the coverer's pawn counts as one
        // holder, the uncovered pawn fills the remaining slot directly.
        grower.RequiredTotal = 2;
        var uncovered = RecsTestBed.Pawn();
        uncovered.SkillLevels["Cooking"] = 4;
        var essential = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { farmer, grower }, pawn, uncovered));
        new CoverageScalingRule(new UnitScaling()).Apply(essential);
        new BestInColonyDraftRule().Apply(essential);
        await Assert.That(essential.Candidates[0].ContainsKey(2)).IsFalse();
        await Assert.That(essential.Candidates[1].ContainsKey(2)).IsTrue();
    }

    [Test]
    public async Task CoveredPathMemberIsStillDealtToItsOwnBandAudience()
    {
        // Same coverage shape as above, but the covered role belongs to a
        // training path: the path-member exception keeps it in the draft, so
        // its ranking is recorded for the uncovered pawn (coverage still
        // satisfies the required total itself, hence no extra candidate).
        var farmer = RecsTestBed.Role(1, "Cooking", "Grow", "Cut");
        var grower = RecsTestBed.Role(2, "Cooking", "Grow");
        farmer.RequiredTotal = -1; grower.RequiredTotal = -1;
        var covered = RecsTestBed.Pawn(); covered.SkillLevels["Cooking"] = 5;
        var audience = RecsTestBed.Pawn(); audience.SkillLevels["Cooking"] = 4;
        var colony = RecsTestBed.Colony(new List<RoleView> { farmer, grower }, covered, audience);
        colony.Paths.Add(RecsTestBed.Path(1, (2, 0, 21)));
        var context = new EngineContext(colony);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();  // coverer dealt first
        await Assert.That(context.DraftRankings[1].ContainsKey(2)).IsTrue();
        // Without the path, the covered role is skipped outright: no ranking.
        colony.Paths.Clear();
        var skipped = new EngineContext(colony);
        new CoverageScalingRule(new UnitScaling()).Apply(skipped);
        new BestInColonyDraftRule().Apply(skipped);
        await Assert.That(skipped.DraftRankings[1].ContainsKey(2)).IsFalse();
    }

    [Test]
    public async Task PartiallyCapableMixedCovererDoesNotSuppressRequiredSubroleDraft()
    {
        var mixed = RecsTestBed.Role(1, "Cooking", "Cook", "Craft");
        mixed.WorkTypes.Add("Crafting");
        var crafter = RecsTestBed.Role(2, "Crafting", "Craft");
        crafter.RequiredTotal = 1;
        var partial = RecsTestBed.Pawn();
        partial.CapableWorkTypes.Clear();
        partial.CapableWorkTypes.Add("Cooking");
        partial.SkillLevels["Cooking"] = 10;
        var available = RecsTestBed.Pawn();
        available.CapableWorkTypes.Clear();
        available.CapableWorkTypes.Add("Crafting");
        available.SkillLevels["Crafting"] = 8;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { mixed, crafter }, partial, available));
        context.AddCandidate(0, mixed.Id,
            new Reason { RuleId = "signals", TowardRoleId = -1 }, SignalBucket.Strong);

        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);

        await Assert.That(context.Candidates[0].ContainsKey(mixed.Id)).IsTrue();
        await Assert.That(context.Candidates[1].ContainsKey(crafter.Id)).IsTrue();
    }

    [Test]
    public async Task PartiallyCapableMixedCovererDoesNotSuppressAutoSubroleDraft()
    {
        var mixed = RecsTestBed.Role(1, "Cooking", "Cook", "Craft");
        mixed.WorkTypes.Add("Crafting");
        var crafter = RecsTestBed.Role(2, "Crafting", "Craft");
        crafter.RequiredTotal = -1;
        var partial = RecsTestBed.Pawn();
        partial.CapableWorkTypes.Clear();
        partial.CapableWorkTypes.Add("Cooking");
        partial.SkillLevels["Cooking"] = 10;
        var available = RecsTestBed.Pawn();
        available.CapableWorkTypes.Clear();
        available.CapableWorkTypes.Add("Crafting");
        available.SkillLevels["Crafting"] = 8;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { mixed, crafter }, partial, available));
        context.AddCandidate(0, mixed.Id,
            new Reason { RuleId = "signals", TowardRoleId = -1 }, SignalBucket.Strong);

        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);

        await Assert.That(context.RequiredTotal[crafter.Id]).IsEqualTo(1);
        await Assert.That(context.HoldersOf(crafter.Id)).IsEqualTo(1);
        await Assert.That(context.Candidates[1].ContainsKey(crafter.Id)).IsTrue();
    }

    [Test]
    public async Task PartialDirectMixedRoleAssignmentDoesNotSatisfyItsFloor()
    {
        var mixed = RecsTestBed.Role(1, "Cooking", "Cook", "Craft");
        mixed.WorkTypes.Add("Crafting");
        mixed.RequiredTotal = 1;
        var partial = RecsTestBed.Pawn();
        partial.CapableWorkTypes.Clear();
        partial.CapableWorkTypes.Add("Cooking");
        partial.SkillLevels["Cooking"] = 12;
        partial.Existing.Add(new AssignmentView { RoleId = mixed.Id, Pinned = true });
        var full = RecsTestBed.Pawn();
        full.SkillLevels["Cooking"] = 10;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { mixed }, partial, full));

        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);

        await Assert.That(context.CoversRole(0, mixed)).IsFalse();
        await Assert.That(context.Candidates[0].ContainsKey(mixed.Id)).IsFalse();
        await Assert.That(context.Candidates[1].ContainsKey(mixed.Id)).IsTrue();
    }

    [Test]
    public async Task PartialPawnsAreNotDraftedIntoAnUnmetMixedRoleFloor()
    {
        var mixed = RecsTestBed.Role(1, "Cooking", "Cook", "Craft");
        mixed.WorkTypes.Add("Crafting");
        mixed.RequiredTotal = 1;
        var first = RecsTestBed.Pawn();
        first.CapableWorkTypes.Clear();
        first.CapableWorkTypes.Add("Cooking");
        first.SkillLevels["Cooking"] = 12;
        var second = RecsTestBed.Pawn();
        second.CapableWorkTypes.Clear();
        second.CapableWorkTypes.Add("Cooking");
        second.SkillLevels["Cooking"] = 10;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { mixed }, first, second));

        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);

        await Assert.That(context.Candidates[0].ContainsKey(mixed.Id)).IsFalse();
        await Assert.That(context.Candidates[1].ContainsKey(mixed.Id)).IsFalse();
        await Assert.That(context.HoldersOf(mixed.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task MissingSecondarySkillNoLongerVetoesTheDraft()
    {
        var mixedSkill = RecsTestBed.Role(1, "Crafting");
        mixedSkill.RequiredTotal = 1;
        mixedSkill.Skills.Add(new RoleSkillView
        {
            SkillDefName = "Crafting", Primary = true, Importance = 3,
        });
        mixedSkill.Skills.Add(new RoleSkillView
        {
            SkillDefName = "Intellectual", Importance = 1,
        });
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 12;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Strong;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { mixedSkill }, pawn));

        SignalBucket bucket = context.BestSignal(
            0, mixedSkill, out string skill, out SignalSource source);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);

        // A disabled non-primary skill is the capability lane's business:
        // the verdict stays the primary's and the draft may still pick the pawn.
        await Assert.That(bucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(skill).IsEqualTo("Crafting");
        await Assert.That(source).IsEqualTo(SignalSource.Aggregated);
        await Assert.That(context.Candidates[0].ContainsKey(mixedSkill.Id)).IsTrue();
    }

    [Test]
    public async Task MissingRequiredSkillTieBreakIsOrdinalUnderNonDefaultCulture()
    {
        var priorCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            var role = RecsTestBed.Role(1, "Crafting");
            role.Skills.Add(new RoleSkillView { SkillDefName = "äSkill" });
            role.Skills.Add(new RoleSkillView { SkillDefName = "zSkill" });
            var context = new EngineContext(RecsTestBed.Colony(
                new List<RoleView> { role }, RecsTestBed.Pawn()));

            SignalBucket bucket = context.BestSignal(
                0, role, out string skill, out SignalSource source);

            await Assert.That(bucket).IsEqualTo(SignalBucket.Awful);
            await Assert.That(skill).IsEqualTo("zSkill");
            await Assert.That(source).IsEqualTo(SignalSource.Aggregated);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = priorCulture;
        }
    }

    [Test]
    public async Task FullyCapableDirectMixedRoleAssignmentStillSatisfiesItsFloor()
    {
        var mixed = RecsTestBed.Role(1, "Cooking", "Cook", "Craft");
        mixed.WorkTypes.Add("Crafting");
        mixed.RequiredTotal = 1;
        var holder = RecsTestBed.Pawn();
        holder.Existing.Add(new AssignmentView { RoleId = mixed.Id, Pinned = true });
        var available = RecsTestBed.Pawn();
        available.SkillLevels["Cooking"] = 10;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { mixed }, holder, available));

        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);

        await Assert.That(context.CoversRole(0, mixed)).IsTrue();
        await Assert.That(context.Candidates[1].ContainsKey(mixed.Id)).IsFalse();
    }

    [Test]
    public async Task DraftPrefersInBandButStillFillsTheFloor()
    {
        // Doctor path 15-21; one in-band pawn (16), one below (4). The in-band
        // pawn takes the single floor slot.
        var doctor = RecsTestBed.Role(1, "Doctor");
        doctor.RequiredTotal = 1;
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
        doctor.RequiredTotal = 1;
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
