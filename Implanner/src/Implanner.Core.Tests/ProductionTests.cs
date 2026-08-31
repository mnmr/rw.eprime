using Implanner.Core;

namespace Implanner.Core.Tests;

/// Production options and owned production-bill bookkeeping: exact change
/// reporting, sparse reserves, and concurrency clamping.
public class ProductionTests
{
    /// Automation ships enabled: production, idle-bench restriction, and
    /// intermediaries all default on, with 3 benches and crafting skill 8.
    [Test]
    public async Task ProductionOptionsToggleWithNoOpPreservation()
    {
        var model = new PlannerModel();

        await Assert.That(model.SetAutoProduction(true)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetAutoProduction(false)).IsEqualTo(PlannerChange.Production);
        await Assert.That(model.SetAutoProduction(false)).IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task ConcurrencyClampsToItsBounds()
    {
        var model = new PlannerModel();

        await Assert.That(model.ProductionConcurrency)
            .IsEqualTo(PlannerModel.ConcurrencyDefault);
        await Assert.That(model.SetProductionConcurrency(PlannerModel.ConcurrencyDefault))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetProductionConcurrency(99))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ProductionConcurrency)
            .IsEqualTo(PlannerModel.ConcurrencyMax);
        await Assert.That(model.SetProductionConcurrency(-5))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ProductionConcurrency)
            .IsEqualTo(PlannerModel.ConcurrencyMin);
    }

    [Test]
    public async Task ResourceReservesStoreSparselyAndPreserveNoOps()
    {
        var model = new PlannerModel();

        // Uranium has no baseline reserve.
        await Assert.That(model.ResourceReserveOf("Uranium")).IsEqualTo(0);
        await Assert.That(model.SetResourceReserve("Uranium", 0))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetResourceReserve("Uranium", 120))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.SetResourceReserve("Uranium", 120))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.ResourceReserveOf("Uranium")).IsEqualTo(120);

        // Matching the (zero) default removes the entry; negatives
        // normalize to zero.
        await Assert.That(model.SetResourceReserve("Uranium", -3))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ResourceReserves.Count).IsEqualTo(0);
    }

    /// The common implant ingredients carry baseline reserves (advanced
    /// components 5, components 20, gold 100, plasteel 500, steel 2000)
    /// until the player overrides them — including an explicit zero.
    [Test]
    public async Task DefaultReservesApplyUntilOverridden()
    {
        var model = new PlannerModel();

        await Assert.That(model.ResourceReserveOf("ComponentSpacer")).IsEqualTo(5);
        await Assert.That(model.ResourceReserveOf("ComponentIndustrial")).IsEqualTo(20);
        await Assert.That(model.ResourceReserveOf("Gold")).IsEqualTo(100);
        await Assert.That(model.ResourceReserveOf("Plasteel")).IsEqualTo(500);
        await Assert.That(model.ResourceReserveOf("Steel")).IsEqualTo(2000);

        // Matching the default stores nothing; an explicit zero overrides.
        await Assert.That(model.SetResourceReserve("Plasteel", 500))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetResourceReserve("Plasteel", 0))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ResourceReserveOf("Plasteel")).IsEqualTo(0);
        await Assert.That(model.ResourceReserves.Count).IsEqualTo(1);

        // Returning to the default removes the override again.
        await Assert.That(model.SetResourceReserve("Plasteel", 500))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ResourceReserves.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ProductionRestrictionsToggleWithNoOpPreservation()
    {
        var model = new PlannerModel();

        await Assert.That(model.SetOnlyIdleBenches(true)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetOnlyIdleBenches(false)).IsEqualTo(PlannerChange.Production);
        await Assert.That(model.SetAllowIntermediaries(true)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetAllowIntermediaries(false)).IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ProductionSkill)
            .IsEqualTo(PlannerModel.ProductionSkillDefault);
        await Assert.That(model.SetProductionSkill(PlannerModel.ProductionSkillDefault))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetProductionSkill(25)).IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ProductionSkill).IsEqualTo(20);
    }

    /// Demand and stock are items; bills are crafts. A multi-output recipe
    /// must not over-produce by the output factor (a deficit of 4 items from
    /// a 2-per-craft recipe is 2 crafts, not 4), and pending crafts count
    /// their full output against the demand.
    [Test]
    public async Task CraftsNeededConvertsItemDeficitsToWholeCrafts()
    {
        // Single-output recipes pass through unchanged.
        await Assert.That(ProductionMath.CraftsNeeded(4, 1, 0, 1)).IsEqualTo(3);
        // 2 items per craft: 4 missing items are 2 crafts.
        await Assert.That(ProductionMath.CraftsNeeded(4, 0, 0, 2)).IsEqualTo(2);
        // Partial crafts round up: 3 missing items still need 2 crafts.
        await Assert.That(ProductionMath.CraftsNeeded(3, 0, 0, 2)).IsEqualTo(2);
        // A pending bill's crafts cover output * crafts items.
        await Assert.That(ProductionMath.CraftsNeeded(4, 0, 2, 2)).IsEqualTo(0);
        await Assert.That(ProductionMath.CraftsNeeded(4, 0, 1, 2)).IsEqualTo(1);
        // Stock and over-supply produce nothing, as does a zero-output recipe.
        await Assert.That(ProductionMath.CraftsNeeded(4, 5, 0, 1)).IsEqualTo(0);
        await Assert.That(ProductionMath.CraftsNeeded(4, 0, 0, 0)).IsEqualTo(0);
    }

    [Test]
    public async Task OwnedProductionBillsAreNoOpSafe()
    {
        var model = new PlannerModel();

        await Assert.That(model.SetOwnedProductionBill("Bill_Make_7", "BionicLeg"))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.SetOwnedProductionBill("Bill_Make_7", "BionicLeg"))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.RemoveOwnedProductionBill("Bill_Make_7"))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.RemoveOwnedProductionBill("Bill_Make_7"))
            .IsEqualTo(PlannerChange.None);
    }
}
