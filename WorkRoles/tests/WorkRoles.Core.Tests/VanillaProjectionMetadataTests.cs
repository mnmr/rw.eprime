using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class VanillaProjectionMetadataTests
{
    [Test]
    public async Task ColumnsFollowFirstSeenPriorityOrderAndMissingUsesMaxValue()
    {
        var definitions = new VanillaProjectionDefinitionMetadata(new[]
        {
            Source("Doctor", skilled: true),
            Source("Hauling"),
            Source("Research", skilled: true, research: true),
        });
        var metadata = definitions.WithBasics(Array.Empty<string>());

        await Assert.That(metadata.ColumnOf("Doctor")).IsEqualTo(0);
        await Assert.That(metadata.ColumnOf("Hauling")).IsEqualTo(1);
        await Assert.That(metadata.ColumnOf("Research")).IsEqualTo(2);
        await Assert.That(metadata.ColumnOf("Missing")).IsEqualTo(int.MaxValue);
        await Assert.That(metadata.ColumnOf(null)).IsEqualTo(int.MaxValue);
    }

    [Test]
    public async Task CategoriesPreserveBasicsSkilledResearchAndGruntSemantics()
    {
        var definitions = new VanillaProjectionDefinitionMetadata(new[]
        {
            Source("BasicsUnskilled"),
            Source("Doctor", skilled: true),
            Source("Research", skilled: true, research: true),
            Source("Hauling"),
            Source("FalseResearch", research: true),
        });
        var metadata = definitions.WithBasics(new[] { "BasicsUnskilled" });

        await Assert.That(string.Join(",", metadata.Basics)).IsEqualTo("BasicsUnskilled");
        await Assert.That(string.Join(",", metadata.Skilled)).IsEqualTo("Doctor,Research");
        await Assert.That(string.Join(",", metadata.Research)).IsEqualTo("Research");
        await Assert.That(string.Join(",", metadata.Grunt)).IsEqualTo("Hauling,FalseResearch");
        await Assert.That(metadata.IsBasics("BasicsUnskilled")).IsTrue();
        await Assert.That(metadata.IsGrunt("BasicsUnskilled")).IsFalse();
    }

    [Test]
    public async Task DuplicateDefinitionsUseFirstSourceAndCompactUniqueColumns()
    {
        var definitions = new VanillaProjectionDefinitionMetadata(new[]
        {
            Source("A"),
            Source("A", skilled: true, research: true),
            Source("B", skilled: true),
        });
        var metadata = definitions.WithBasics(Array.Empty<string>());

        await Assert.That(string.Join(",", metadata.WorkTypes)).IsEqualTo("A,B");
        await Assert.That(metadata.ColumnOf("A")).IsEqualTo(0);
        await Assert.That(metadata.ColumnOf("B")).IsEqualTo(1);
        await Assert.That(metadata.IsSkilled("A")).IsFalse();
        await Assert.That(metadata.IsResearch("A")).IsFalse();
        await Assert.That(metadata.IsGrunt("A")).IsTrue();
    }

    [Test]
    public async Task BasicsDeduplicateInFirstSeenOrderIncludingMissingDefinitions()
    {
        var definitions = new VanillaProjectionDefinitionMetadata(new[]
        {
            Source("A"),
            Source("B"),
        });
        var metadata = definitions.WithBasics(new[] { "Missing", "A", "Missing", "A" });

        await Assert.That(string.Join(",", metadata.Basics)).IsEqualTo("Missing,A");
        await Assert.That(metadata.ColumnOf("Missing")).IsEqualTo(int.MaxValue);
        await Assert.That(metadata.IsBasics("Missing")).IsTrue();
        await Assert.That(metadata.IsGrunt("A")).IsFalse();
        await Assert.That(metadata.IsGrunt("B")).IsTrue();
    }

    [Test]
    public async Task SeparatePriorityOrderKeepsCategoryOnlyDefinitionsColumnless()
    {
        var definitions = new VanillaProjectionDefinitionMetadata(
            new[]
            {
                Source("A"),
                Source("HiddenSkilled", skilled: true),
                Source("B"),
            },
            new[] { "B", "A", "B" });
        var metadata = definitions.WithBasics(Array.Empty<string>());

        await Assert.That(metadata.ColumnOf("B")).IsEqualTo(0);
        await Assert.That(metadata.ColumnOf("A")).IsEqualTo(1);
        await Assert.That(metadata.ColumnOf("HiddenSkilled")).IsEqualTo(int.MaxValue);
        await Assert.That(metadata.IsSkilled("HiddenSkilled")).IsTrue();
    }

    [Test]
    public async Task MetadataDefensivelyOwnsInputsAndPublishesReadOnlyViews()
    {
        var sources = new List<VanillaProjectionWorkTypeSource> { Source("A") };
        var priorityOrder = new List<string> { "A" };
        var basics = new List<string> { "A" };
        var definitions = new VanillaProjectionDefinitionMetadata(sources, priorityOrder);
        var metadata = definitions.WithBasics(basics);

        sources.Clear();
        priorityOrder.Clear();
        basics.Clear();

        await Assert.That(metadata.ColumnOf("A")).IsEqualTo(0);
        await Assert.That(metadata.IsBasics("A")).IsTrue();
        await Assert.That(() => ((IDictionary<string, int>)metadata.Columns).Add("B", 1))
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<string>)metadata.WorkTypes).Add("B"))
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IList<string>)metadata.Basics).Clear())
            .Throws<NotSupportedException>();
    }

    private static VanillaProjectionWorkTypeSource Source(
        string name, bool skilled = false, bool research = false) =>
        new VanillaProjectionWorkTypeSource(name, skilled, research);
}
