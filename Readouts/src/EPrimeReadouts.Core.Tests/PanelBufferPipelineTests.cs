using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelBufferPipelineTests
{
    [Test]
    public async Task PublishBuildAndSwapRequireSeparateTransitions()
    {
        var pipeline = new PanelBufferPipeline();

        pipeline.PublishCounts();

        await Assert.That(pipeline.FrontGeneration).IsEqualTo(0L);
        await Assert.That(pipeline.TrySwapOnRepaint()).IsFalse();
        await Assert.That(pipeline.TryBeginBuild(out BufferBuildTicket ticket))
            .IsTrue();
        await Assert.That(pipeline.FrontGeneration).IsEqualTo(0L);
        await Assert.That(pipeline.TrySwapOnRepaint()).IsFalse();

        pipeline.CompleteBuild(ticket);

        await Assert.That(pipeline.FrontGeneration).IsEqualTo(0L);
        await Assert.That(pipeline.TrySwapOnRepaint()).IsTrue();
        await Assert.That(pipeline.FrontGeneration)
            .IsEqualTo(ticket.Generation);
    }

    [Test]
    public async Task NewerPublicationRejectsCompletedOlderBuild()
    {
        var pipeline = new PanelBufferPipeline();
        pipeline.PublishCounts();
        pipeline.TryBeginBuild(out BufferBuildTicket stale);

        pipeline.PublishCounts();
        pipeline.CompleteBuild(stale);

        await Assert.That(pipeline.TrySwapOnRepaint()).IsFalse();
        await Assert.That(pipeline.TryBeginBuild(out BufferBuildTicket latest))
            .IsTrue();
        await Assert.That(latest.Generation).IsGreaterThan(stale.Generation);
    }

    [Test]
    public async Task RepeatedPublicationsCoalesceIntoNewestBuild()
    {
        var pipeline = new PanelBufferPipeline();

        pipeline.PublishCounts();
        pipeline.PublishCounts();
        pipeline.PublishCounts();

        await Assert.That(pipeline.TryBeginBuild(out BufferBuildTicket ticket))
            .IsTrue();
        await Assert.That(ticket.Generation).IsEqualTo(3L);
        await Assert.That(pipeline.TryBeginBuild(out _)).IsFalse();
    }

    [Test]
    public async Task StructuralInvalidationMarksAndThenClearsBaseDirty()
    {
        var pipeline = new PanelBufferPipeline();

        pipeline.InvalidateBase();

        await Assert.That(pipeline.BaseDirty).IsTrue();
        pipeline.TryBeginBuild(out BufferBuildTicket ticket);
        await Assert.That(ticket.RebuildBase).IsTrue();

        pipeline.CompleteBuild(ticket);

        await Assert.That(pipeline.BaseDirty).IsFalse();
    }

    [Test]
    public async Task StaleBaseBuildDoesNotClearNewStructuralInvalidation()
    {
        var pipeline = new PanelBufferPipeline();
        pipeline.InvalidateBase();
        pipeline.TryBeginBuild(out BufferBuildTicket stale);

        pipeline.InvalidateBase();
        pipeline.CompleteBuild(stale);

        await Assert.That(pipeline.BaseDirty).IsTrue();
        await Assert.That(pipeline.TryBeginBuild(out BufferBuildTicket latest))
            .IsTrue();
        await Assert.That(latest.RebuildBase).IsTrue();
    }
}
