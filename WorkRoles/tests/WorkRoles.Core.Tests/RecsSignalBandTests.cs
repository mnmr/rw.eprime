using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests;

/// Rule 3 (Strong+ interest makes a skilled role a candidate) and rule 4
/// (path bands gate candidates on the measured skill).
public class RecsSignalBandTests
{
    [Test]
    public async Task RequiredAwfulSkillVetoesRoleAndSecondaryStrengthCannotQualifyIt()
    {
        var role = RecsTestBed.Role(1, "Crafting");
        role.Skills.Add(new RoleSkillView { SkillDefName = "Crafting", Primary = true, Importance = 3 });
        role.Skills.Add(new RoleSkillView { SkillDefName = "Intellectual", Importance = 1 });
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 12;
        pawn.SkillLevels["Intellectual"] = 8;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Strong;
        pawn.SignalBuckets["Intellectual"] = SignalBucket.Awful;
        var context = new EngineContext(RecsTestBed.Colony(new List<RoleView> { role }, pawn));

        SignalBucket vetoed = context.BestSignal(0, role, out string vetoSkill, out _);
        await Assert.That(vetoed).IsEqualTo(SignalBucket.Awful);
        await Assert.That(vetoSkill).IsEqualTo("Intellectual");

        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        pawn.SignalBuckets["Intellectual"] = SignalBucket.Exceptional;
        SignalBucket secondaryOnly = context.BestSignal(0, role, out string primarySkill, out _);
        await Assert.That(secondaryOnly).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(primarySkill).IsEqualTo("Crafting");
    }

    [Test]
    public async Task AwfulWorkTypeVetoesOnlyRolesContainingThatExactWorkType()
    {
        var cooking = RecsTestBed.Role(1, "Cooking");
        var butchering = RecsTestBed.Role(2, "Butchering");
        butchering.Skills.Add(new RoleSkillView
        {
            SkillDefName = "Cooking",
            Primary = true,
        });
        var hauling = RecsTestBed.Unskilled(3, "Hauling");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 10;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        pawn.WorkTypeSignalBuckets = new Dictionary<string, SignalBucket>
        {
            ["Cooking"] = SignalBucket.Awful,
            ["Hauling"] = SignalBucket.Awful,
        };
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { cooking, butchering, hauling }, pawn));

        await Assert.That(context.BestSignal(0, cooking, out string cookingTarget, out _))
            .IsEqualTo(SignalBucket.Awful);
        await Assert.That(cookingTarget == null).IsTrue();
        await Assert.That(context.BestSignal(0, butchering, out _, out _))
            .IsEqualTo(SignalBucket.Great);
        await Assert.That(context.BestSignal(0, hauling, out string haulingTarget, out _))
            .IsEqualTo(SignalBucket.Awful);
        await Assert.That(haulingTarget == null).IsTrue();
    }

    [Test]
    public async Task WorkAversionSignalFlowsFromPawnSnapshotIntoExistingRoleVerdict()
    {
        var hatedCooking = new Signal(
            SignalType.Active,
            new WorkRoles.Core.Signals.SignalSource(SignalSourceKind.WorkAversion,
                "HatedWork", "void.MoreThanCapable"),
            skillDefName: null,
            effects: Array.Empty<SignalEffect>(),
            new SignalUi("hated cooking", null, null, null, null,
                "More Than Capable"),
            workTypeDefName: "Cooking");
        PawnSignalSnapshot snapshot = PawnSignalSnapshot.Create(
            new[] { "Cooking" }, new SignalSnapshot(new[] { hatedCooking }));
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 10;
        PawnSignalViewProjection.Apply(snapshot, pawn);
        RoleView role = RecsTestBed.Role(1, "Cooking");
        role.MinHolders = 1;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { role }, pawn);
        var context = new EngineContext(colony);

        await Assert.That(context.BestSignal(0, role, out _, out _))
            .IsEqualTo(SignalBucket.Awful);
        new CoverageScalingRule(new UnitScaling()).Apply(context);
        new BestInColonyDraftRule().Apply(context);
        await Assert.That(context.Candidates[0].ContainsKey(role.Id)).IsFalse();

        PawnResult result = RecsEngine.Run(colony).Single();
        await Assert.That(result.Assignments.Any(x => x.RoleId == role.Id)).IsFalse();
    }

    [Test]
    public async Task StrictRoleBandRequiresEveryRequiredSkillInsideTheBand()
    {
        var role = RecsTestBed.Role(1, "Crafting");
        role.Skills.Add(new RoleSkillView { SkillDefName = "Crafting", Primary = true, Importance = 3 });
        role.Skills.Add(new RoleSkillView { SkillDefName = "Intellectual", Importance = 1 });
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 12;
        pawn.SkillLevels["Intellectual"] = 4;
        var colony = RecsTestBed.Colony(new List<RoleView> { role }, pawn);
        colony.Paths.Add(RecsTestBed.Path(1, (role.Id, 8, 21)));
        var context = new EngineContext(colony);

        await Assert.That(context.PassesBands(0, role)).IsFalse();
        pawn.SkillLevels["Intellectual"] = 8;
        await Assert.That(context.PassesBands(0, role)).IsTrue();
    }

    [Test]
    public async Task StrongOrBetterInterestCreatesCandidatesWithTheBucketAsStrength()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        var crafter = RecsTestBed.Role(2, "Crafting");
        var doctor = RecsTestBed.Role(3, "Doctor");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 8; pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        pawn.SkillLevels["Crafting"] = 12; pawn.SignalBuckets["Crafting"] = SignalBucket.Exceptional;
        pawn.SkillLevels["Medicine"] = 9; // level alone = Neutral, no candidate
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { cook, crafter, doctor }, pawn));
        new SignalCandidatesRule().Apply(context, 0);
        await Assert.That(context.Candidates[0][1].Strength).IsEqualTo(SignalBucket.Great);
        await Assert.That(context.Candidates[0][1].Reason.SkillDefName).IsEqualTo("Cooking");
        await Assert.That(context.Candidates[0][2].Strength).IsEqualTo(SignalBucket.Exceptional);
        await Assert.That(context.Candidates[0][2].Reason.Source)
            .IsEqualTo(WorkRoles.Core.Recs.SignalSource.Aggregated);
        await Assert.That(context.Candidates[0].ContainsKey(3)).IsFalse();
    }

    [Test]
    public async Task AutoUnskilledHuntingAndVetoedRolesAreNotSignalCandidates()
    {
        var auto = RecsTestBed.Role(1, "Cooking"); auto.AutoAssign = true;
        var grunt = RecsTestBed.Unskilled(2, "Hauling");
        var hunter = RecsTestBed.Role(3, "Hunting"); hunter.Hunting = true;
        var vetoed = RecsTestBed.Role(4, "Cooking", "CookVetoed");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 8; pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        pawn.SkillLevels["Shooting"] = 8; pawn.SignalBuckets["Shooting"] = SignalBucket.Great;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { auto, grunt, hunter, vetoed }, pawn));
        context.Vetoed.Add(4);
        new SignalCandidatesRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].Count).IsEqualTo(0);
    }

    [Test]
    public async Task BandsRemoveOutOfBandCandidates_OverlapCoexists_DisjointSupersedes()
    {
        // Farmer path shape: cutter 0-12, grower 4-21, farmer 12-21.
        var cutter = RecsTestBed.Role(1, "Cooking", "Cut");
        var grower = RecsTestBed.Role(2, "Cooking", "Grow");
        var farmer = RecsTestBed.Role(3, "Cooking", "Farm");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 14; pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        var colony = RecsTestBed.Colony(new List<RoleView> { cutter, grower, farmer }, pawn);
        colony.Paths.Add(RecsTestBed.Path(1, (1, 0, 12), (2, 4, 21), (3, 12, 21)));
        var context = new EngineContext(colony);
        new SignalCandidatesRule().Apply(context, 0);
        new BandGatingRule().Apply(context, 0);
        // 14: cutter superseded (past 12), grower and farmer coexist (overlap).
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsFalse();
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsTrue();
        await Assert.That(context.Candidates[0].ContainsKey(3)).IsTrue();
    }

    [Test]
    public async Task BandsAreIrrelevantWithoutPaths_AndNonMembersPassUntouched()
    {
        var cook = RecsTestBed.Role(1, "Cooking");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 2; pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        var context = new EngineContext(RecsTestBed.Colony(new List<RoleView> { cook }, pawn));
        await Assert.That(new BandGatingRule().Relevant(context)).IsFalse();
        new SignalCandidatesRule().Apply(context, 0);
        new BandGatingRule().Apply(context, 0); // harmless even when irrelevant
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
    }
}
