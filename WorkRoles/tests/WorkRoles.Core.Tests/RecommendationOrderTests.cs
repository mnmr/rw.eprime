using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// The template is a pure override: stored pins win, deleted roles drop, and
/// unlisted roles get calculated positions (trainers after their furthest
/// target, covered roles after their coverer, hunting in the duty slot,
/// anything else by work-type priority).
public class RecommendationOrderTests
{
    private static RecRole Role(int id, float naturalPriority = 0f, params string[] coverage)
    {
        var role = new RecRole { Id = id, NaturalPriority = naturalPriority };
        role.Coverage.UnionWith(coverage);
        return role;
    }

    private static Dictionary<int, RecRole> ById(params RecRole[] roles) =>
        roles.ToDictionary(r => r.Id);

    private static Dictionary<int, int> Index(params int[] template)
    {
        var index = new Dictionary<int, int>();
        for (int i = 0; i < template.Length; i++) index[template[i]] = i;
        return index;
    }

    [Test]
    public async Task EmptyStoredOrderYieldsTheDerivedDefault()
    {
        // Distinct coverage, priority order 3 > 1 > 2.
        var catalog = new List<RecRole>
        {
            Role(1, 200f, "A"), Role(2, 100f, "B"), Role(3, 300f, "C"),
        };
        var resolved = RecommendationOrder.ResolveTemplate(new List<int>(), catalog);
        await Assert.That(string.Join(",", resolved)).IsEqualTo("3,1,2");
    }

    [Test]
    public async Task StoredOrderIsAuthoritative_DeletedRolesDrop_UnlistedRolesStayOut()
    {
        // 9 was deleted; 5 exists but is unpinned — it floats via PositionOf
        // instead of merging back in.
        var catalog = new List<RecRole>
        {
            Role(1, 200f, "A"), Role(2, 100f, "B"), Role(5, 150f, "E"),
        };
        var resolved = RecommendationOrder.ResolveTemplate(new List<int> { 9, 2, 1 }, catalog);
        await Assert.That(string.Join(",", resolved)).IsEqualTo("2,1");
    }

    [Test]
    public async Task RolesCreatedAfterTheStoredOrderAreAddable()
    {
        // The in-game Add-bug shape: a save whose stored order predates a
        // newly created, coverage-maximal role. The new role is not a chip
        // (stored wins) so Add MUST offer it.
        var doctor = Role(1, 1300f, "Tend");
        var cook = Role(2, 1000f, "Cook");
        var core = Role(3, 1400f, "Fight", "Rescue"); // created later
        var catalog = new List<RecRole> { doctor, cook, core };

        var resolved = RecommendationOrder.ResolveTemplate(new List<int> { 1, 2 }, catalog);
        await Assert.That(string.Join(",", resolved)).IsEqualTo("1,2");
        await Assert.That(RecommendationOrder.AddCandidates(catalog, resolved))
            .Contains(3);

        // Never-edited store: the same role enters the DEFAULT template by
        // itself — a chip, and therefore absent from Add.
        var derived = RecommendationOrder.ResolveTemplate(new List<int>(), catalog);
        await Assert.That(derived.Contains(3)).IsTrue();
        await Assert.That(RecommendationOrder.AddCandidates(catalog, derived).Contains(3))
            .IsFalse();
    }

    [Test]
    public async Task AutoRolesAreNeitherPinnedNorAddable()
    {
        // Always-on roles interleave by work priority on their own; pinning
        // them is meaningless, so both surfaces exclude them BY DESIGN.
        var doctor = Role(1, 1300f, "Tend");
        var core = Role(3, 1400f, "Fight", "Rescue");
        core.AutoAssign = true;
        var catalog = new List<RecRole> { doctor, core };

        var derived = RecommendationOrder.ResolveTemplate(new List<int>(), catalog);
        await Assert.That(derived.Contains(3)).IsFalse();
        await Assert.That(RecommendationOrder.AddCandidates(catalog, derived).Contains(3))
            .IsFalse();
    }

    [Test]
    public async Task EveryPinnableRoleIsPinnedOrAddable()
    {
        // The invariant behind the Options Add menu: no role a player creates
        // can be neither listed nor offered.
        var covered = Role(1, 100f, "A");
        var coverer = Role(2, 200f, "A", "B");
        var auto = Role(3, 900f, "X"); auto.AutoAssign = true;
        var hunter = Role(4, 300f, "H"); hunter.Hunting = true;
        var trainer = Role(5, 150f, "T"); trainer.TrainTargets.Add(2);
        var catalog = new List<RecRole> { covered, coverer, auto, hunter, trainer };

        var template = RecommendationOrder.ResolveTemplate(new List<int>(), catalog);
        var addable = RecommendationOrder.AddCandidates(catalog, template);
        var reachable = template.Concat(addable).ToHashSet();
        foreach (var role in catalog.Where(RecommendationOrder.IsPinnable))
            await Assert.That(reachable.Contains(role.Id)).IsTrue()
                .Because($"role {role.Id} is neither pinned nor addable");
        await Assert.That(reachable.Contains(auto.Id)).IsFalse();
    }

    [Test]
    public async Task TrainersSlotAfterTheirFurthestTarget()
    {
        // Crafter trains toward BOTH DrugMaker (slot 0) and Fabricator
        // (slot 1): targets must always precede their trainer.
        var drugMaker = Role(1);
        var fabricator = Role(2);
        var crafter = Role(3);
        crafter.TrainTargets.AddRange(new[] { 1, 2 });
        var byId = ById(drugMaker, fabricator, crafter);
        var index = Index(1, 2);

        long fabricatorPos = RecommendationOrder.PositionOf(fabricator, index, byId);
        long crafterPos = RecommendationOrder.PositionOf(crafter, index, byId);
        await Assert.That(crafterPos > fabricatorPos).IsTrue();
        await Assert.That(RecommendationOrder.InsertIndex(crafter, new List<int> { 1, 2 }, byId))
            .IsEqualTo(2);
    }

    [Test]
    public async Task CoveredRolesSlotAfterTheirTightestCoverer()
    {
        var warden = Role(1, coverage: new[] { "Recruit", "Feed" });
        var jailor = Role(2, coverage: new[] { "Feed" });
        var cook = Role(3, coverage: new[] { "Cook" });
        var byId = ById(warden, jailor, cook);
        var index = Index(1, 3);

        long jailorPos = RecommendationOrder.PositionOf(jailor, index, byId);
        await Assert.That(jailorPos > RecommendationOrder.PositionOf(warden, index, byId)).IsTrue();
        await Assert.That(jailorPos < RecommendationOrder.PositionOf(cook, index, byId)).IsTrue();
    }

    [Test]
    public async Task CoveredTrainTargetPrecedesItsCoveringTrainer()
    {
        // Smith covers Fabricator AND trains toward it; neither is pinned.
        // Targets must still precede their trainer.
        var smith = Role(1, naturalPriority: 470f, coverage: new[] { "MakeWeapons", "Fabricate" });
        smith.TrainTargets.Add(2);
        var fabricator = Role(2, naturalPriority: 470f, coverage: new[] { "Fabricate" });
        var byId = ById(smith, fabricator);
        var index = Index();

        await Assert.That(RecommendationOrder.PositionOf(fabricator, index, byId)
            < RecommendationOrder.PositionOf(smith, index, byId)).IsTrue();
    }

    [Test]
    public async Task HuntingRolesTakeTheDutySlot()
    {
        var doctor = Role(1, coverage: new[] { "Tend" });
        var warden = Role(2, coverage: new[] { "Recruit" });
        warden.WorkTypes.Add("Warden");
        var cook = Role(3, coverage: new[] { "Cook" });
        var hunter = Role(4, coverage: new[] { "Hunt" });
        hunter.Hunting = true;
        var byId = ById(doctor, warden, cook, hunter);
        var index = Index(1, 2, 3);

        long hunterPos = RecommendationOrder.PositionOf(hunter, index, byId);
        await Assert.That(hunterPos > RecommendationOrder.PositionOf(warden, index, byId)).IsTrue();
        await Assert.That(hunterPos < RecommendationOrder.PositionOf(cook, index, byId)).IsTrue();
    }

    [Test]
    public async Task PinnedHuntingRolesObeyTheirPin()
    {
        var hunter = Role(4, coverage: new[] { "Hunt" });
        hunter.Hunting = true;
        var cook = Role(3, coverage: new[] { "Cook" });
        var byId = ById(hunter, cook);
        var index = Index(4, 3);

        await Assert.That(RecommendationOrder.PositionOf(hunter, index, byId)
            < RecommendationOrder.PositionOf(cook, index, byId)).IsTrue();
    }

    [Test]
    public async Task UnanchoredRolesFallBackToWorkTypePriority()
    {
        var hauler = Role(1, naturalPriority: 300f, coverage: new[] { "Haul" });
        var researcher = Role(2, naturalPriority: 100f, coverage: new[] { "Research" });
        var custom = Role(3, naturalPriority: 200f, coverage: new[] { "Custom" });
        var byId = ById(hauler, researcher, custom);
        var index = Index(1, 2);

        long customPos = RecommendationOrder.PositionOf(custom, index, byId);
        await Assert.That(customPos > RecommendationOrder.PositionOf(hauler, index, byId)).IsTrue();
        await Assert.That(customPos < RecommendationOrder.PositionOf(researcher, index, byId)).IsTrue();
        await Assert.That(RecommendationOrder.InsertIndex(custom, new List<int> { 1, 2 }, byId))
            .IsEqualTo(1);
    }
}
