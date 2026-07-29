using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class TemplatePlacementTests
{
    private static JobEntry Type(string name) => new(JobEntryKind.WorkType, name);
    private static JobEntry Giver(string name) => new(JobEntryKind.WorkGiver, name);

    [Test]
    public async Task InsertsAfterNearestPrecedingTemplateSibling()
    {
        var template = new[] { Type("BedRest"), Type("HaulUrgent"), Type("Basic") };
        var live = new List<JobEntry> { Type("BedRest"), Type("Basic"), Type("Extra") };
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 1)).IsEqualTo(1);
    }

    [Test]
    public async Task FallsBackToFollowingSiblingWhenNoPrecedingPresent()
    {
        var template = new[] { Type("FinishOff"), Type("Hunting") };
        var live = new List<JobEntry> { Type("Extra"), Type("Hunting") };
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 0)).IsEqualTo(1);
    }

    [Test]
    public async Task AppendsWhenNoTemplateSiblingSurvivesInLive()
    {
        var template = new[] { Type("A"), Type("New") };
        var live = new List<JobEntry> { Type("X"), Type("Y") };
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 1)).IsEqualTo(2);
    }

    [Test]
    public async Task FollowsUserReorderingRatherThanTemplatePosition()
    {
        // The user moved A behind B; the new entry still lands right after A.
        var template = new[] { Type("A"), Type("New"), Type("B") };
        var live = new List<JobEntry> { Type("B"), Type("A") };
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 1)).IsEqualTo(2);
    }

    [Test]
    public async Task MatchRequiresKindAndDefName()
    {
        // A giver anchor never matches a work-type entry of the same defName.
        var template = new[] { Giver("A"), Type("New") };
        var live = new List<JobEntry> { Type("A") };
        await Assert.That(TemplatePlacement.AnchoredInsertIndex(live, template, 1)).IsEqualTo(1);
    }

    [Test]
    public async Task RestoreOrderDoesNotChangeTheResultingSequence()
    {
        // Basics shape: two compat types between BedRest and Basic.
        var template = new[] { Type("BedRest"), Type("HaulAT"), Type("HaulKAU"), Type("Basic") };

        var live = new List<JobEntry> { Type("BedRest"), Type("Basic") };
        live.Insert(TemplatePlacement.AnchoredInsertIndex(live, template, 2), template[2]);
        live.Insert(TemplatePlacement.AnchoredInsertIndex(live, template, 1), template[1]);
        await Assert.That(live.SequenceEqual(template)).IsTrue();

        live = new List<JobEntry> { Type("BedRest"), Type("Basic") };
        live.Insert(TemplatePlacement.AnchoredInsertIndex(live, template, 1), template[1]);
        live.Insert(TemplatePlacement.AnchoredInsertIndex(live, template, 2), template[2]);
        await Assert.That(live.SequenceEqual(template)).IsTrue();
    }
}
