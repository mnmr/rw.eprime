using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Invariants of per-pawn recommendations: signal-driven SELECTION (with
/// reasons), template-driven ORDER (training roles after their target),
/// gates, and covered-role suppression.
public class RecommendationEngineTests
{
    /// Coverage tokens stand in for expanded giver sets; the default is one
    /// token named after the work type.
    private static RecRole Skilled(int id, string workType, params string[] coverage) => new()
    {
        Id = id,
        WorkTypes = { workType },
        Coverage = coverage.Length > 0 ? new HashSet<string>(coverage) : new HashSet<string> { workType },
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> Skills = new()
    {
        ["Cooking"] = new List<string> { "Cooking" },
        ["Crafting"] = new List<string> { "Crafting" },
        ["Doctor"] = new List<string> { "Medicine" },
        ["Warden"] = new List<string> { "Social" },
    };

    private static readonly Dictionary<string, int> NoBest = new();

    private static RecPawn Pawn() => new()
    {
        CapableWorkTypes = { "Cooking", "Crafting", "Doctor", "Warden", "Hauling", "Hunting" },
    };

    private static string Ids(IEnumerable<Recommendation> recs) =>
        string.Join(",", recs.Select(r => r.RoleId));

    private static List<int> Template(params int[] ids) => ids.ToList();

    [Test]
    public async Task OrderFollowsTheTemplate_ReasonsReportTheSignal()
    {
        // Signal strength picks candidates and reasons; the template alone
        // decides the order (crafting expertise no longer outranks cooking).
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 5; pawn.PassionScores["Cooking"] = 2;
        pawn.SkillLevels["Crafting"] = 12; pawn.ExpertiseSkills.Add("Crafting");
        pawn.SkillLevels["Medicine"] = 3; pawn.Aptitudes["Medicine"] = 4;
        var catalog = new List<RecRole>
        {
            Skilled(1, "Cooking"), Skilled(2, "Crafting"), Skilled(3, "Doctor"),
        };
        var recs = RecommendationEngine.Compute(catalog, pawn, NoBest, Skills, Template(3, 1, 2));
        await Assert.That(Ids(recs)).IsEqualTo("3,1,2");
        await Assert.That(recs[0].Reason).IsEqualTo(RecReason.Aptitude);
        await Assert.That(recs[1].Reason).IsEqualTo(RecReason.MajorPassion);
        await Assert.That(recs[2].Reason).IsEqualTo(RecReason.Expertise);
    }

    [Test]
    public async Task ApathyMutesEverySignalForTheSkill()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 8; pawn.PassionScores["Cooking"] = 2;
        pawn.Aptitudes["Cooking"] = -4; // VSE apathy
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { Skilled(1, "Cooking") }, pawn,
            new Dictionary<string, int> { ["Cooking"] = 8 }, Skills, Template(1));
        await Assert.That(recs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TrainingBands_MinWithBestInColonyEscape_MaxPromotesToTarget()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Medicine"] = 4; pawn.PassionScores["Medicine"] = 1;
        var doctor = Skilled(1, "Doctor");
        doctor.TrainSkill = "Medicine"; doctor.TrainMin = 10;
        var medic = Skilled(2, "Doctor", "DoctorLite");
        medic.TrainSkill = "Medicine"; medic.TrainMax = 10; medic.TrainTargets.Add(1);
        var catalog = new List<RecRole> { doctor, medic };
        var template = Template(1); // medic trains toward doctor: not in the template

        // Below Doctor's band: Medic only.
        var below = RecommendationEngine.Compute(catalog, pawn,
            new Dictionary<string, int> { ["Medicine"] = 12 }, Skills, template);
        await Assert.That(Ids(below)).IsEqualTo("2");

        // Best in colony at 4: the min band opens (escape); Medic still fits
        // and slots right after its target.
        var best = RecommendationEngine.Compute(catalog, pawn,
            new Dictionary<string, int> { ["Medicine"] = 4 }, Skills, template);
        await Assert.That(Ids(best)).IsEqualTo("1,2");

        // Past Medic's ceiling: outgrown — only the target qualifies.
        pawn.SkillLevels["Medicine"] = 12;
        var outgrown = RecommendationEngine.Compute(catalog, pawn,
            new Dictionary<string, int> { ["Medicine"] = 12 }, Skills, template);
        await Assert.That(Ids(outgrown)).IsEqualTo("1");
    }

    [Test]
    public async Task TrainingChainsSlotAfterTheirTransitiveTarget()
    {
        // Nurse -> Medic -> Doctor: only Doctor is in the template; each hop
        // lands one step later.
        var pawn = Pawn();
        pawn.SkillLevels["Medicine"] = 2; pawn.PassionScores["Medicine"] = 2;
        pawn.SkillLevels["Cooking"] = 6; pawn.PassionScores["Cooking"] = 2;
        var doctor = Skilled(1, "Doctor");
        var medic = Skilled(2, "Doctor", "Tend");
        medic.TrainTargets.Add(1);
        var nurse = Skilled(3, "Doctor", "Feed");
        nurse.TrainTargets.Add(2);
        var cook = Skilled(4, "Cooking");
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { doctor, medic, nurse, cook }, pawn, NoBest, Skills,
            Template(1, 4));
        await Assert.That(Ids(recs)).IsEqualTo("1,2,3,4");
    }

    [Test]
    public async Task NeverDealtRolesAreStillRecommendedByInterest()
    {
        // MinHolders 0 keeps the PLANNER from dealing the role; passion-driven
        // recommendations are a different thing and stay.
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 8; pawn.PassionScores["Cooking"] = 2;
        var cook = Skilled(1, "Cooking");
        cook.MinHolders = 0;
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { cook }, pawn, NoBest, Skills, Template(1));
        await Assert.That(Ids(recs)).IsEqualTo("1");
    }

    [Test]
    public async Task BestInColonyOnlyDraftsIntoNeededWork()
    {
        // No passion, no aptitude — the pawn is merely the colony's best cook.
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 8;
        var optional = Skilled(1, "Cooking");
        var best = new Dictionary<string, int> { ["Cooking"] = 8 };

        var quiet = RecommendationEngine.Compute(
            new List<RecRole> { optional }, pawn, best, Skills, Template(1));
        await Assert.That(quiet.Count).IsEqualTo(0);

        var needed = Skilled(2, "Cooking");
        needed.MinHolders = 1;
        var drafted = RecommendationEngine.Compute(
            new List<RecRole> { needed }, pawn, best, Skills, Template(2));
        await Assert.That(Ids(drafted)).IsEqualTo("2");
        await Assert.That(drafted[0].Reason).IsEqualTo(RecReason.Best);
    }

    [Test]
    public async Task UnavailableRolesAreNeverRecommended()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 8; pawn.PassionScores["Cooking"] = 2;
        var cook = Skilled(1, "Cooking");
        cook.Available = false; // bench research unfinished
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { cook }, pawn, NoBest, Skills, Template(1));
        await Assert.That(recs.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CoveredTrainTargetSurvivesTheComboDrop_AndLeadsItsTrainer()
    {
        // Fabricator-under-Smith: the subset specialization is the coverer's
        // train target, so both are recommended, target first.
        var pawn = Pawn();
        pawn.SkillLevels["Crafting"] = 9; pawn.PassionScores["Crafting"] = 2;
        var smith = Skilled(1, "Crafting", "MakeWeapons", "Fabricate");
        var fabricator = Skilled(2, "Crafting", "Fabricate");
        fabricator.TrainMin = 7; fabricator.TrainSkill = "Crafting";
        smith.TrainSkill = "Crafting"; smith.TrainTargets.Add(2);
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { smith, fabricator }, pawn, NoBest, Skills, Template(2));
        await Assert.That(Ids(recs)).IsEqualTo("2,1");
    }

    [Test]
    public async Task ContentKeys_AutosLead_GruntByTemplate_HuntingNeedsAGun()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 6; pawn.PassionScores["Cooking"] = 1;
        var basics = new RecRole { Id = 1, AutoAssign = true, NaturalPriority = 100f, WorkTypes = { "Hauling" }, Coverage = ["Firefighter", "Patient"] };
        var grunt = new RecRole { Id = 2, Unskilled = true, WorkTypes = { "Hauling" }, Coverage = ["Hauling"] };
        var hunter = new RecRole { Id = 3, Hunting = true, WorkTypes = { "Hunting" }, Coverage = ["Hunting"] };
        var cook = Skilled(4, "Cooking");
        var catalog = new List<RecRole> { basics, grunt, hunter, cook };
        var template = Template(3, 4, 2); // vanilla-ish: hunt, cook, haul

        var unarmed = RecommendationEngine.Compute(catalog, pawn, NoBest, Skills, template);
        await Assert.That(Ids(unarmed)).IsEqualTo("1,4,2"); // no gun: no hunter

        pawn.HasRangedWeapon = true;
        pawn.ShootingLevel = 7;
        var armed = RecommendationEngine.Compute(catalog, pawn, NoBest, Skills, template);
        await Assert.That(Ids(armed)).IsEqualTo("1,3,4,2");
    }

    [Test]
    public async Task CoveredRolesAreDroppedInFavorOfTheCombo()
    {
        var pawn = Pawn();
        var firefighter = new RecRole { Id = 1, Unskilled = true, WorkTypes = { "Hauling" }, Coverage = ["Firefighter"] };
        var basics = new RecRole { Id = 2, AutoAssign = true, NaturalPriority = 1f, WorkTypes = { "Hauling" }, Coverage = ["Firefighter", "Patient"] };
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { firefighter, basics }, pawn, NoBest, Skills, Template(1));
        await Assert.That(Ids(recs)).IsEqualTo("2");
    }

    [Test]
    public async Task RulesRolesAndBlockersAreNeverRecommended()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 8; pawn.PassionScores["Cooking"] = 2;
        var auto = Skilled(1, "Cooking"); auto.HasRules = true;
        var blocker = Skilled(2, "Cooking"); blocker.Blocker = true;
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { auto, blocker }, pawn, NoBest, Skills, Template());
        await Assert.That(recs.Count).IsEqualTo(0);
    }
}
