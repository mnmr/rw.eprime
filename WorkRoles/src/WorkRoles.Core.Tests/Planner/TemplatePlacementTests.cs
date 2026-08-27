namespace WorkRoles.Core.Tests.Planner;

public class TemplatePlacementTests
{
    private static JobEntry Type(string name) => new(JobEntryKind.WorkType, name);

    private static JobEntry Giver(string name) => new(JobEntryKind.WorkGiver, name);

    [Test]
    public async Task InsertsAfterNearestPrecedingTemplateSibling()
    {
        JobEntry[] template = [Type("BedRest"), Type("HaulUrgent"), Type("Basic")];
        List<JobEntry> live = [Type("BedRest"), Type("Basic"), Type("Extra")];
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 1)).IsEqualTo(1);
    }

    [Test]
    public async Task FallsBackToFollowingSiblingWhenNoPrecedingPresent()
    {
        JobEntry[] template = [Type("FinishOff"), Type("Hunting")];
        List<JobEntry> live = [Type("Extra"), Type("Hunting")];
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 0)).IsEqualTo(1);
    }

    [Test]
    public async Task AppendsWhenNoTemplateSiblingSurvivesInLive()
    {
        JobEntry[] template = [Type("A"), Type("New")];
        List<JobEntry> live = [Type("X"), Type("Y")];
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 1)).IsEqualTo(2);
    }

    [Test]
    public async Task FollowsUserReorderingRatherThanTemplatePosition()
    {
        // The user moved A behind B; the new entry still lands right after A.
        JobEntry[] template = [Type("A"), Type("New"), Type("B")];
        List<JobEntry> live = [Type("B"), Type("A")];
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 1)).IsEqualTo(2);
    }

    [Test]
    public async Task MatchRequiresKindAndDefName()
    {
        // A giver anchor never matches a work-type entry of the same defName.
        JobEntry[] template = [Giver("A"), Type("New")];
        List<JobEntry> live = [Type("A")];
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 1)).IsEqualTo(1);
    }

    [Test]
    public async Task RestoringLaterTemplateSiblingFirstProducesTemplateOrder()
    {
        JobEntry[] template = [Type("BedRest"), Type("HaulAT"), Type("HaulKAU"), Type("Basic")];
        List<JobEntry> live = [Type("BedRest"), Type("Basic")];

        live.Insert(TemplatePlacement.AnchoredInsertIndex(live, template, 2), template[2]);
        live.Insert(TemplatePlacement.AnchoredInsertIndex(live, template, 1), template[1]);

        await Assert.That(string.Join(",", live.Select(entry => entry.DefName))).IsEqualTo("BedRest,HaulAT,HaulKAU,Basic");
    }

    [Test]
    public async Task RestoringEarlierTemplateSiblingFirstProducesTemplateOrder()
    {
        JobEntry[] template = [Type("BedRest"), Type("HaulAT"), Type("HaulKAU"), Type("Basic")];
        List<JobEntry> live = [Type("BedRest"), Type("Basic")];

        live.Insert(TemplatePlacement.AnchoredInsertIndex(live, template, 1), template[1]);
        live.Insert(TemplatePlacement.AnchoredInsertIndex(live, template, 2), template[2]);

        await Assert.That(string.Join(",", live.Select(entry => entry.DefName))).IsEqualTo("BedRest,HaulAT,HaulKAU,Basic");
    }
}
