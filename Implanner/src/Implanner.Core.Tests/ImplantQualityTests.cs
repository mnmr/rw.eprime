using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Implant quality (Quality Bionics Remastered, Vanilla Genetics Expanded):
/// which item quality a slot accepts, which stocked item it takes, and the
/// plan's minimum quality as player-configured, persisted and shared state.
public class ImplantQualityTests
{
    int nextPlanId = 1;

    int TakePlanId() => nextPlanId++;

    /// Quality Bionics Remastered's default multipliers applied to a bionic
    /// arm (125%): Awful 62.5% ... Legendary 250%.
    static readonly float[] BionicArm =
        { 0.625f, 0.9375f, 1.25f, 1.5625f, 1.875f, 2.125f, 2.5f };

    /// A slot accepts the lowest quality at or above the plan's minimum that
    /// leaves the part no worse than it is now: an Awful or Poor bionic arm
    /// never replaces a healthy arm (100%), but is welcome where the arm is
    /// missing (0%) or badly scarred. An implant with no quality reads the
    /// same efficiency at every quality, so only the plan minimum matters.
    /// Nothing acceptable reports None.
    [Test]
    public async Task ASlotNeverAcceptsAnImplantWorseThanThePartItReplaces()
    {
        await Assert.That(ImplantQuality.MinimumAcceptable(BionicArm, 1f, 0)).IsEqualTo(2);
        await Assert.That(ImplantQuality.MinimumAcceptable(BionicArm, 0f, 0)).IsEqualTo(0);
        await Assert.That(ImplantQuality.MinimumAcceptable(BionicArm, 0.9f, 0)).IsEqualTo(1);
        await Assert.That(ImplantQuality.MinimumAcceptable(BionicArm, 1f, 4)).IsEqualTo(4);
        // An installed Legendary archotech arm is better than any bionic.
        await Assert.That(ImplantQuality.MinimumAcceptable(BionicArm, 3f, 0))
            .IsEqualTo(ImplantQuality.None);

        // A drill arm is exactly 100% and still goes on a healthy arm.
        var drillArm = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        await Assert.That(ImplantQuality.MinimumAcceptable(drillArm, 1f, 0)).IsEqualTo(0);
    }

    /// Stock goes to the highest acceptable quality (lowest item id among
    /// equals), leaving nothing better unused; the preference flips to the
    /// lowest acceptable quality when better items are meant for
    /// higher-priority colonists.
    [Test]
    public async Task ItemsAreChosenByQualityThenByAge()
    {
        // Item qualities in item-id order.
        var qualities = new List<int> { 1, 4, 2, 4, 6 };

        await Assert.That(ImplantQuality.Choose(qualities, 2, preferLowest: false)).IsEqualTo(4);
        await Assert.That(ImplantQuality.Choose(qualities, 2, preferLowest: true)).IsEqualTo(2);
        qualities[4] = 0;
        await Assert.That(ImplantQuality.Choose(qualities, 3, preferLowest: false)).IsEqualTo(1);
        await Assert.That(ImplantQuality.Choose(qualities, 5, preferLowest: false)).IsEqualTo(-1);
    }

    /// The minimum quality is plan content: clamped to Awful..Legendary,
    /// no-op safe, copied from the base plan when an extending plan is
    /// created, and carried through export and import by quality name.
    [Test]
    public async Task PlanMinimumQualityIsSharedPlanContent()
    {
        var model = new PlannerModel();
        Plan essentials = model.CreatePlan("Essentials", TakePlanId)!;
        await Assert.That(essentials.MinQuality).IsEqualTo(0);

        await Assert.That(model.SetPlanMinQuality(essentials.Id, 3))
            .IsEqualTo(PlannerChange.Plans);
        await Assert.That(model.SetPlanMinQuality(essentials.Id, 3))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetPlanMinQuality(essentials.Id, 99))
            .IsEqualTo(PlannerChange.Plans);
        await Assert.That(essentials.MinQuality).IsEqualTo(ImplantQuality.Highest);
        model.SetPlanMinQuality(essentials.Id, 3);

        Plan full = model.CreatePlan("Full bionics", TakePlanId, essentials.Id)!;
        await Assert.That(full.MinQuality).IsEqualTo(3);
        model.SetPlanMinQuality(full.Id, 5);

        string xml = PlansXml.Export(model.Plans);
        await Assert.That(xml).Contains("MinQuality=\"Good\"");
        await Assert.That(xml).Contains("MinQuality=\"Masterwork\"");

        await Assert.That(PlansXml.TryImport(xml, out var parsed, out _)).IsTrue();
        var target = new PlannerModel();
        int planId = 100;
        target.ImportPlans(parsed, () => planId++);
        await Assert.That(target.Plans[0].MinQuality).IsEqualTo(3);
        await Assert.That(target.Plans[1].MinQuality).IsEqualTo(5);

        // Older exports and unknown names import as "any quality".
        await Assert.That(PlansXml.TryImport(
            "<ImplannerPlans><Plan Name=\"A\" MinQuality=\"Shiny\"/><Plan Name=\"B\"/></ImplannerPlans>",
            out parsed, out _)).IsTrue();
        await Assert.That(parsed[0].MinQuality).IsEqualTo(0);
        await Assert.That(parsed[1].MinQuality).IsEqualTo(0);
    }
}
