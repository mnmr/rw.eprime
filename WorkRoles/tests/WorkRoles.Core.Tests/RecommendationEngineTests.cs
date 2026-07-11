using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Invariants of per-pawn recommendation scoring: signal precedence, gates,
/// duty pinning, content-keyed groups and covered-role suppression.
public class RecommendationEngineTests
{
    private static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);

    private static RecRole Skilled(int id, string workType, params JobEntry[] entries) => new()
    {
        Id = id,
        WorkTypes = { workType },
        Entries = entries.Length > 0 ? entries.ToList() : new List<JobEntry> { WT(workType) },
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

    [Test]
    public async Task SignalPrecedence_ExpertiseOverPassionOverBestOverAptitude()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 5; pawn.PassionScores["Cooking"] = 2;
        pawn.SkillLevels["Crafting"] = 12; pawn.ExpertiseSkills.Add("Crafting");
        pawn.SkillLevels["Medicine"] = 3; pawn.Aptitudes["Medicine"] = 4;
        var catalog = new List<RecRole>
        {
            Skilled(1, "Cooking"), Skilled(2, "Crafting"), Skilled(3, "Doctor"),
        };
        var recs = RecommendationEngine.Compute(catalog, pawn, NoBest, Skills);
        await Assert.That(Ids(recs)).IsEqualTo("2,1,3");
        await Assert.That(recs[0].Reason).IsEqualTo(RecReason.Expertise);
        await Assert.That(recs[1].Reason).IsEqualTo(RecReason.MajorPassion);
        await Assert.That(recs[2].Reason).IsEqualTo(RecReason.Aptitude);
    }

    [Test]
    public async Task WithinAGroupHigherSkillWins()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 4; pawn.PassionScores["Cooking"] = 2;
        pawn.SkillLevels["Crafting"] = 9; pawn.PassionScores["Crafting"] = 2;
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { Skilled(1, "Cooking"), Skilled(2, "Crafting") },
            pawn, NoBest, Skills);
        await Assert.That(Ids(recs)).IsEqualTo("2,1");
    }

    [Test]
    public async Task DutyRolesPinAboveVocationsWhateverTriggeredThem()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Social"] = 3; pawn.PassionScores["Social"] = 1;   // warden: minor
        pawn.SkillLevels["Cooking"] = 10; pawn.PassionScores["Cooking"] = 2; // cook: major
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { Skilled(1, "Cooking"), Skilled(2, "Warden") },
            pawn, NoBest, Skills);
        await Assert.That(Ids(recs)).IsEqualTo("2,1");
        await Assert.That(recs[0].Reason).IsEqualTo(RecReason.Duty);
    }

    [Test]
    public async Task GatesMinLevelWithBestInColonyEscape_MaxLevelWithPassion()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Medicine"] = 4; pawn.PassionScores["Medicine"] = 1;
        var doctor = Skilled(1, "Doctor");
        doctor.GateSkill = "Medicine"; doctor.GateMinLevel = 10;
        var medic = Skilled(2, "Doctor", WT("DoctorLite"));
        medic.GateSkill = "Medicine"; medic.GateMaxLevel = 10; medic.GateNeedsPassion = true;
        var catalog = new List<RecRole> { doctor, medic };

        // Below the gate with passion: Medic only.
        var below = RecommendationEngine.Compute(catalog, pawn, new Dictionary<string, int> { ["Medicine"] = 12 }, Skills);
        await Assert.That(Ids(below)).IsEqualTo("2");

        // Best in colony at 4: the min gate opens (escape), max gate keeps Medic too.
        var best = RecommendationEngine.Compute(catalog, pawn, new Dictionary<string, int> { ["Medicine"] = 4 }, Skills);
        await Assert.That(Ids(best)).IsEqualTo("1,2");

        // No passion: Medic's gate closes.
        pawn.PassionScores["Medicine"] = 0;
        var noPassion = RecommendationEngine.Compute(catalog, pawn, new Dictionary<string, int> { ["Medicine"] = 12 }, Skills);
        await Assert.That(Ids(noPassion)).IsEqualTo("");
    }

    [Test]
    public async Task ContentKeys_EveryoneLeadsGruntTrails_HuntingNeedsAGun()
    {
        var pawn = Pawn();
        pawn.SkillLevels["Cooking"] = 6; pawn.PassionScores["Cooking"] = 1;
        var basics = new RecRole { Id = 1, AutoAssign = true, NaturalPriority = 100f, WorkTypes = { "Hauling" }, Entries = new List<JobEntry> { WT("Firefighter"), WT("Patient") } };
        var grunt = new RecRole { Id = 2, Unskilled = true, WorkTypes = { "Hauling" }, Entries = new List<JobEntry> { WT("Hauling") } };
        var hunter = new RecRole { Id = 3, Hunting = true, WorkTypes = { "Hunting" }, Entries = new List<JobEntry> { WT("Hunting") } };
        var cook = Skilled(4, "Cooking");
        var catalog = new List<RecRole> { basics, grunt, hunter, cook };

        var unarmed = RecommendationEngine.Compute(catalog, pawn, NoBest, Skills);
        await Assert.That(Ids(unarmed)).IsEqualTo("1,4,2"); // no gun: no hunter

        pawn.HasRangedWeapon = true;
        pawn.ShootingLevel = 7;
        var armed = RecommendationEngine.Compute(catalog, pawn, NoBest, Skills);
        await Assert.That(Ids(armed)).IsEqualTo("1,3,4,2"); // hunter above the skilled work
    }

    [Test]
    public async Task CoveredRolesAreDroppedInFavorOfTheCombo()
    {
        var pawn = Pawn();
        var firefighter = new RecRole { Id = 1, Unskilled = true, WorkTypes = { "Hauling" }, Entries = new List<JobEntry> { WT("Firefighter") } };
        var basics = new RecRole { Id = 2, AutoAssign = true, NaturalPriority = 1f, WorkTypes = { "Hauling" }, Entries = new List<JobEntry> { WT("Firefighter"), WT("Patient") } };
        var recs = RecommendationEngine.Compute(
            new List<RecRole> { firefighter, basics }, pawn, NoBest, Skills);
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
            new List<RecRole> { auto, blocker }, pawn, NoBest, Skills);
        await Assert.That(recs.Count).IsEqualTo(0);
    }
}
