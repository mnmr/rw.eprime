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
        var resolved = RecommendationOrder.Resolve(
            new List<int>(), new List<int> { 3, 1, 2 }, new HashSet<int> { 1, 2, 3 });
        await Assert.That(string.Join(",", resolved)).IsEqualTo("3,1,2");
    }

    [Test]
    public async Task StoredOrderIsAuthoritative_DeletedRolesDrop_UnlistedRolesStayOut()
    {
        // 9 was deleted; 5 exists but is unpinned — it floats via PositionOf
        // instead of merging back in.
        var resolved = RecommendationOrder.Resolve(
            stored: new List<int> { 9, 2, 1 },
            derived: new List<int> { 1, 5, 2 },
            valid: new HashSet<int> { 1, 2, 5 });
        await Assert.That(string.Join(",", resolved)).IsEqualTo("2,1");
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
