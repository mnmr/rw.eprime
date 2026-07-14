using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Invariants of the colony-wide passes: coverage scaling, essentials, hunter
/// tiers, the doctoring floor and fire safety.
public class ColonyPlannerTests
{
    private static readonly Dictionary<string, IReadOnlyList<string>> Skills = new()
    {
        ["Doctor"] = new List<string> { "Medicine" },
        ["Cooking"] = new List<string> { "Cooking" },
        ["Hunting"] = new List<string> { "Shooting" },
    };

    /// Coverage tokens stand in for expanded giver sets, one per work type.
    private static RecRole Role(int id, string workType) => new()
    {
        Id = id, WorkTypes = { workType }, Coverage = [workType],
    };

    private static PlanPawn Pawn(int medicine = 0, int medicinePassion = 0, bool gun = false, int shooting = 0)
    {
        var pawn = new PlanPawn();
        pawn.Rec.CapableWorkTypes.UnionWith(new[] { "Doctor", "Cooking", "Hunting", "Firefighter" });
        pawn.Rec.SkillLevels["Medicine"] = medicine;
        pawn.Rec.PassionScores["Medicine"] = medicinePassion;
        pawn.Rec.SkillLevels["Shooting"] = shooting;
        pawn.Rec.HasRangedWeapon = gun;
        pawn.Rec.ShootingLevel = shooting;
        return pawn;
    }

    private static readonly Dictionary<string, int> NoBest = new();
    private static readonly Dictionary<int, int> NoEssentials = new();

    [Test]
    public async Task CoverageScalesWithColonySize()
    {
        var cook = Role(1, "Cooking");
        var pawns = Enumerable.Range(0, 13).Select(_ => Pawn()).ToList();
        foreach (var p in pawns) p.Rec.SkillLevels["Cooking"] = 5;
        var result = ColonyPlanner.Compute(new List<RecRole> { cook }, pawns,
            NoBest, Skills, NoEssentials, -1, -1, -1, -1);
        // 13 pawns -> ceil(13/6) = 3 holders.
        await Assert.That(result.VirtualSets.Count(ids => ids.Contains(1))).IsEqualTo(3);
    }

    [Test]
    public async Task EssentialGrantsArePromotedInRankOrder()
    {
        var doctor = Role(1, "Doctor");
        var cook = Role(2, "Cooking");
        var pawn = Pawn(medicine: 5);
        pawn.Rec.SkillLevels["Cooking"] = 5;
        var result = ColonyPlanner.Compute(new List<RecRole> { cook, doctor },
            new List<PlanPawn> { pawn }, NoBest, Skills,
            new Dictionary<int, int> { [1] = 0, [2] = 1 }, // Doctor rank 0, Cook rank 1
            -1, -1, -1, -1);
        await Assert.That(string.Join(",", result.Promoted[0])).IsEqualTo("1,2");
        await Assert.That(result.Grants.Count(g => g.Reason == PlanReason.Essential)).IsEqualTo(2);
    }

    [Test]
    public async Task EveryGunCarrierHuntsTiered_AtLeastOneTierZero()
    {
        var hunter = Role(1, "Hunting");
        var pawns = new List<PlanPawn>
        {
            Pawn(gun: true, shooting: 16),
            Pawn(gun: true, shooting: 20),
            Pawn(gun: false, shooting: 3),
        };
        var result = ColonyPlanner.Compute(new List<RecRole> { hunter }, pawns,
            NoBest, Skills, NoEssentials, hunterRoleId: 1, -1, -1, -1);
        // Both shooters hunt; nobody is under 15, so the best (20) is forced tier 0.
        await Assert.That(result.HunterTiers[0]).IsEqualTo(1);
        await Assert.That(result.HunterTiers[1]).IsEqualTo(0);
        await Assert.That(result.HunterTiers[2]).IsEqualTo(-1);
        await Assert.That(result.VirtualSets[2].Contains(1)).IsFalse();
    }

    [Test]
    public async Task DoctoringFloor_MedicTraineePreferred_ThenGatesWaived()
    {
        var doctor = Role(1, "Doctor");
        doctor.GateSkill = "Medicine"; doctor.GateMinLevel = 10; doctor.Gated = true;
        var medic = new RecRole
        {
            Id = 2, WorkTypes = { "Doctor" }, Coverage = ["Tend"],
            GateSkill = "Medicine", GateMaxLevel = 10, GateNeedsPassion = true, Gated = true,
        };
        var catalog = new List<RecRole> { doctor, medic };
        var essentials = new Dictionary<int, int> { [1] = 0 };

        // Pawn 0 is colony-best (level 8, escape opens Doctor); pawn 1 has a
        // passion at 3 -> Medic trainee takes the backup slot.
        var withTrainee = ColonyPlanner.Compute(catalog,
            new List<PlanPawn> { Pawn(medicine: 8), Pawn(medicine: 3, medicinePassion: 1) },
            new Dictionary<string, int> { ["Medicine"] = 8 }, Skills, essentials, -1, 1, 2, -1);
        await Assert.That(withTrainee.VirtualSets[0].Contains(1)).IsTrue();
        await Assert.That(withTrainee.VirtualSets[1].Contains(2)).IsTrue();

        // Nobody has a passion: the backup still lands, gates waived, as Medic.
        var waived = ColonyPlanner.Compute(catalog,
            new List<PlanPawn> { Pawn(medicine: 8), Pawn(medicine: 3) },
            new Dictionary<string, int> { ["Medicine"] = 8 }, Skills, essentials, -1, 1, 2, -1);
        await Assert.That(waived.VirtualSets[1].Contains(2)).IsTrue();
        await Assert.That(waived.Grants.Any(g => g.Reason == PlanReason.Essential && g.EssentialRank == -1)).IsTrue();
    }

    [Test]
    public async Task FireFearingPawnsGetTheBlocker()
    {
        var blocker = new RecRole
        {
            Id = 9, Blocker = true, WorkTypes = { "Firefighter" },
            Coverage = ["Firefighter"],
        };
        var scared = Pawn(); scared.FireFear = true;
        var calm = Pawn();
        var result = ColonyPlanner.Compute(new List<RecRole> { blocker },
            new List<PlanPawn> { scared, calm }, NoBest, Skills, NoEssentials, -1, -1, -1, 9);
        await Assert.That(result.FireGranted[0]).IsTrue();
        await Assert.That(result.VirtualSets[0].Contains(9)).IsTrue();
        await Assert.That(result.FireGranted[1]).IsFalse();
        await Assert.That(result.Grants.Any(g => g.Reason == PlanReason.FireFear)).IsTrue();
    }

    [Test]
    public async Task ExistingRuleCarryingAssignmentJoinsTheVirtualSet_AndItsCoverageSuppressesDealing()
    {
        // The pawn holds a rule-carrying combo spanning Cooking+Doctor: it
        // joins the virtual view and, covering the single Cook role, stops
        // the coverage pass from dealing Cook on top.
        var nightShift = new RecRole
        {
            Id = 5, HasRules = true, WorkTypes = { "Cooking", "Doctor" },
            Coverage = ["Cooking", "Doctor"],
        };
        var cook = Role(1, "Cooking");
        var pawn = Pawn();
        pawn.Rec.SkillLevels["Cooking"] = 5;
        pawn.Existing.Add(new PlannedAssignment { RoleId = 5 });

        var result = ColonyPlanner.Compute(new List<RecRole> { cook, nightShift },
            new List<PlanPawn> { pawn }, NoBest, Skills, NoEssentials, -1, -1, -1, -1);
        await Assert.That(result.VirtualSets[0].Contains(5)).IsTrue();
        await Assert.That(result.VirtualSets[0].Contains(1)).IsFalse();
    }

    [Test]
    public async Task DoctoringBackup_GatePasserGetsDoctor_NoMedicRoleWaivesTheGate()
    {
        var doctor = Role(1, "Doctor");
        doctor.GateSkill = "Medicine"; doctor.GateMinLevel = 10; doctor.Gated = true;
        var medic = new RecRole
        {
            Id = 2, WorkTypes = { "Doctor" }, Coverage = ["Tend"],
            GateSkill = "Medicine", GateMaxLevel = 10, GateNeedsPassion = true, Gated = true,
        };
        var essentials = new Dictionary<int, int> { [1] = 0 };

        // The backup candidate passes Doctor's min-10 gate: full Doctor, not Medic.
        var gatePasser = ColonyPlanner.Compute(new List<RecRole> { doctor, medic },
            new List<PlanPawn> { Pawn(medicine: 15), Pawn(medicine: 12) },
            new Dictionary<string, int> { ["Medicine"] = 15 }, Skills, essentials, -1, 1, 2, -1);
        await Assert.That(gatePasser.VirtualSets[1].Contains(1)).IsTrue();
        await Assert.That(gatePasser.VirtualSets[1].Contains(2)).IsFalse();

        // No medic-style role in the catalog: Doctor lands with the gate waived.
        var noMedic = ColonyPlanner.Compute(new List<RecRole> { doctor },
            new List<PlanPawn> { Pawn(medicine: 15), Pawn(medicine: 2) },
            new Dictionary<string, int> { ["Medicine"] = 15 }, Skills, essentials, -1, 1, -1, -1);
        await Assert.That(noMedic.VirtualSets[1].Contains(1)).IsTrue();
    }

    [Test]
    public async Task SubRolesAreNotDealt_TheirCovererIs()
    {
        var grower = new RecRole { Id = 1, WorkTypes = { "Cooking" }, Coverage = ["Cooking"] };
        var farmer = new RecRole
        {
            Id = 2, WorkTypes = { "Cooking" },
            Coverage = ["Cooking", "Doctor"],
        };
        var pawn = Pawn(); pawn.Rec.SkillLevels["Cooking"] = 6;
        var result = ColonyPlanner.Compute(new List<RecRole> { grower, farmer },
            new List<PlanPawn> { pawn }, NoBest, Skills, NoEssentials, -1, -1, -1, -1);
        await Assert.That(result.VirtualSets[0].Contains(1)).IsFalse(); // covered: skipped
        await Assert.That(result.VirtualSets[0].Contains(2)).IsTrue();
    }
}
