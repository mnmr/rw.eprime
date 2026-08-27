using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelBaseRevisionTests
{
    [Test]
    public async Task SameInputsProduceTheSameBaseRevision()
    {
        var model = new RenderModel();
        var left = new PanelBaseRevision(
            model, 140, 400, 7, 11, 13,
            new PanelVisualOptions(0.35f));
        var right = new PanelBaseRevision(
            model, 140, 400, 7, 11, 13,
            new PanelVisualOptions(0.35f));

        await Assert.That(left).IsEqualTo(right);
    }

    [Test]
    public async Task ModelIdentityParticipatesInBaseRevision()
    {
        var left = Revision(new RenderModel());
        var right = Revision(new RenderModel());

        await Assert.That(left).IsNotEqualTo(right);
    }

    [Test]
    [Arguments(141, 400, 7, 11, 13, 0.35f)]
    [Arguments(140, 401, 7, 11, 13, 0.35f)]
    [Arguments(140, 400, 8, 11, 13, 0.35f)]
    [Arguments(140, 400, 7, 12, 13, 0.35f)]
    [Arguments(140, 400, 7, 11, 14, 0.35f)]
    [Arguments(140, 400, 7, 11, 13, 0.25f)]
    public async Task EveryVisualDependencyParticipatesInBaseRevision(
        int width, int height, int uiRevision,
        int iconScaleRevision, int iconDataRevision, float opacity)
    {
        var model = new RenderModel();
        PanelBaseRevision baseline = Revision(model);
        var changed = new PanelBaseRevision(
            model, width, height, uiRevision,
            iconScaleRevision, iconDataRevision,
            new PanelVisualOptions(opacity));

        await Assert.That(changed).IsNotEqualTo(baseline);
    }

    [Test]
    public async Task CounterMutationDoesNotChangeBaseRevision()
    {
        var model = new RenderModel();
        model.Cells.Add(new RenderCell
        {
            Kind = CellKind.Counter,
            Text = "10",
        });
        PanelBaseRevision before = Revision(model);

        RenderCell counter = model.Cells[0];
        counter.Text = "11";
        model.Cells[0] = counter;

        await Assert.That(Revision(model)).IsEqualTo(before);
    }

    private static PanelBaseRevision Revision(RenderModel model) =>
        new PanelBaseRevision(
            model, 140, 400, 7, 11, 13,
            new PanelVisualOptions(0.35f));
}
