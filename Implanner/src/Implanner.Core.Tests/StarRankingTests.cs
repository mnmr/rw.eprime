using Implanner.Core;

namespace Implanner.Core.Tests;

/// Global implant star rankings: manual player choices with a three-star
/// default. Stars clamp to 1–5 and the default tier is stored sparsely
/// (tier dispatch order is covered by SurgeryPlannerTests).
public class StarRankingTests
{
    [Test]
    public async Task UnrankedImplantSitsAtTheThreeStarDefault()
    {
        var model = new PlannerModel();

        await Assert.That(model.ImplantStarsOf("BionicArm"))
            .IsEqualTo(PlannerModel.DefaultStars);
        // Setting the default tier on an unranked implant is a no-op.
        await Assert.That(model.SetImplantStars("BionicArm", PlannerModel.DefaultStars))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.ImplantStars.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SetStarsClampsAndPreservesNoOps()
    {
        var model = new PlannerModel();

        await Assert.That(model.SetImplantStars("BionicArm", 1))
            .IsEqualTo(PlannerChange.Rankings);
        await Assert.That(model.SetImplantStars("BionicArm", 1))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetImplantStars("BionicArm", 9))
            .IsEqualTo(PlannerChange.Rankings);
        await Assert.That(model.ImplantStarsOf("BionicArm")).IsEqualTo(5);
    }

    /// Dragging an implant back to the three-star tier removes the stored
    /// entry: the default never accumulates in saves.
    [Test]
    public async Task ReturningToTheDefaultTierStoresNothing()
    {
        var model = new PlannerModel();
        model.SetImplantStars("BionicArm", 5);

        await Assert.That(model.SetImplantStars("BionicArm", PlannerModel.DefaultStars))
            .IsEqualTo(PlannerChange.Rankings);
        await Assert.That(model.ImplantStarsOf("BionicArm"))
            .IsEqualTo(PlannerModel.DefaultStars);
        await Assert.That(model.ImplantStars.Count).IsEqualTo(0);
    }

    /// Dropping an implant at an exact position materializes the tier's
    /// order: every listed kind takes the tier's stars and its index, and
    /// unlisted kinds sort after ordered ones (ImplantOrderOf = MaxValue).
    [Test]
    public async Task ApplyTierOrderSetsStarsAndPositions()
    {
        var model = new PlannerModel();
        model.SetImplantStars("BionicArm", 5);

        // BionicLeg arrives from another tier and lands between the others.
        var change = model.ApplyTierOrder(5,
            new[] { "BionicEye", "BionicLeg", "BionicArm" });

        await Assert.That(change).IsEqualTo(PlannerChange.Rankings);
        await Assert.That(model.ImplantStarsOf("BionicEye")).IsEqualTo(5);
        await Assert.That(model.ImplantStarsOf("BionicLeg")).IsEqualTo(5);
        await Assert.That(model.ImplantOrderOf("BionicEye")).IsEqualTo(0);
        await Assert.That(model.ImplantOrderOf("BionicLeg")).IsEqualTo(1);
        await Assert.That(model.ImplantOrderOf("BionicArm")).IsEqualTo(2);
        await Assert.That(model.ImplantOrderOf("PowerClaw"))
            .IsEqualTo(int.MaxValue);

        // Re-applying the identical sequence is a no-op.
        await Assert.That(model.ApplyTierOrder(5,
            new[] { "BionicEye", "BionicLeg", "BionicArm" }))
            .IsEqualTo(PlannerChange.None);
    }

    /// Implant reservations hold stock back from surgery automation.
    [Test]
    public async Task ImplantReservesStoreSparselyAndPreserveNoOps()
    {
        var model = new PlannerModel();

        await Assert.That(model.ImplantReserveOf("BionicLeg")).IsEqualTo(0);
        await Assert.That(model.SetImplantReserve("BionicLeg", 0))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetImplantReserve("BionicLeg", 2))
            .IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.SetImplantReserve("BionicLeg", 2))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.ImplantReserveOf("BionicLeg")).IsEqualTo(2);

        // Zero removes the entry; negatives normalize to zero.
        await Assert.That(model.SetImplantReserve("BionicLeg", -1))
            .IsEqualTo(PlannerChange.Surgery);
        await Assert.That(model.ImplantReserves.Count).IsEqualTo(0);
    }
}
