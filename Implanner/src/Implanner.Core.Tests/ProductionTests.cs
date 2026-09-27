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
        await Assert.That(model.SetProductionConcurrency(11))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ProductionConcurrency).IsEqualTo(11);
        await Assert.That(model.SetProductionConcurrency(50))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.ProductionConcurrency).IsEqualTo(50);
        await Assert.That(model.SetProductionConcurrency(99))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.ProductionConcurrency)
            .IsEqualTo(50);
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
        await Assert.That(model.ProductionSkill).IsEqualTo(25);
        await Assert.That(model.SetProductionSkill(25)).IsEqualTo(PlannerChange.None);
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

    /// A new one-craft bill fits only when stock covers its cost plus the
    /// materials already promised to queued (not started) bills plus the
    /// reserve. Bionic leg: 15 plasteel and 4 advanced components per
    /// craft. Plasteel: 100 in stock, 50 kept back, one queued leg already
    /// promised 15, so two more legs fit and a third does not. A craft that
    /// fails any ingredient promises nothing, and the shortfall says how
    /// much is missing for it.
    [Test]
    public async Task NewBillsFitStockAfterQueuedBillsAndReserves()
    {
        var budget = new ProductionBudget<string>();
        budget.Track("Plasteel", stock: 100, reserve: 50);
        budget.Track("ComponentSpacer", stock: 43, reserve: 5);
        var leg = new List<(string, int)> { ("Plasteel", 15), ("ComponentSpacer", 4) };
        budget.Commit("Plasteel", 15);         // the queued leg bill
        budget.Commit("ComponentSpacer", 4);

        await Assert.That(budget.TryCommit(leg)).IsTrue();
        await Assert.That(budget.TryCommit(leg)).IsTrue();
        await Assert.That(budget.TryCommit(leg)).IsFalse();
        // 15 more on top of 45 promised and 50 kept back: 10 short.
        await Assert.That(budget.Shortfall("Plasteel", 15)).IsEqualTo(10);
        // The failed craft promised no advanced components: 12 are
        // promised, so 43 - 12 - 5 = 26 still leave room for 6 crafts.
        await Assert.That(budget.Shortfall("ComponentSpacer", 24)).IsEqualTo(0);
        await Assert.That(budget.Shortfall("ComponentSpacer", 28)).IsEqualTo(2);
    }

    /// Production follows the surgery rollout: demand arrives as one entry
    /// per missing implant slot, already in rollout order. Stock and
    /// pending bills cover the earliest entries; every entry left over is
    /// one craft, returned in the same order. Pending crafts nobody needs
    /// are reported unused so their bills can be withdrawn.
    [Test]
    public async Task CraftsFollowTheRolloutOrderAfterStockAndPendingBills()
    {
        // Full sets: colonist 1's leg and arm, then colonist 2's.
        var rollout = Needs("Leg", "Arm", "Leg", "Arm", "Eye");
        var stock = new Dictionary<string, List<int>> { ["Leg"] = new List<int> { 0 } };
        var pending = new Dictionary<string, List<int>>
        {
            ["Arm"] = new List<int> { ProductionQueue.UnknownQuality },
            ["Jaw"] = new List<int> { ProductionQueue.UnknownQuality, ProductionQueue.UnknownQuality },
        };
        var output = new Dictionary<string, int> { ["Leg"] = 1, ["Arm"] = 1, ["Eye"] = 1, ["Jaw"] = 1 };
        var used = new Dictionary<string, List<int>>();

        List<CraftNeed<string>> crafts = ProductionQueue.UncoveredCrafts(
            rollout, stock, pending, output, used);

        // Leg 1 from stock, arm 1 from the pending bill; colonist 2's leg
        // and arm and the eye still need crafting, in rollout order.
        await Assert.That(Items(crafts)).IsEqualTo("Leg,Arm,Eye");
        await Assert.That(used.TryGetValue("Arm", out List<int>? arms) ? arms.Count : 0).IsEqualTo(1);
        // Nobody needs a jaw: both pending jaw crafts are unused.
        await Assert.That(used.ContainsKey("Jaw")).IsFalse();

        // A craft yielding two items covers the next entry of its kind too.
        output["Leg"] = 2;
        stock.Clear();
        pending.Clear();
        used.Clear();
        await Assert.That(Items(ProductionQueue.UncoveredCrafts(
            rollout, stock, pending, output, used))).IsEqualTo("Leg,Arm,Arm,Eye");
    }

    /// Quality Bionics Remastered: a plan's minimum quality decides which
    /// stocked arm covers a slot. Stock goes to the rollout in order, each
    /// slot taking the best arm it accepts, the way surgery allocates it.
    /// A pending bill counts toward a slot only when it promises enough
    /// quality: a bill Quality Jobs manages at Good covers a Good slot, a
    /// bill with no promise (no Quality Jobs) is trusted until its product
    /// lands, and a new craft targets the minimum of the slot it covers.
    [Test]
    public async Task StockAndPendingBillsCoverOnlySlotsTheirQualityMeets()
    {
        var output = new Dictionary<string, int> { ["Arm"] = 1 };
        // Colonist 1's plan wants Good (3), colonist 2 accepts anything.
        var rollout = new List<CraftNeed<string>>
        {
            new CraftNeed<string>("Arm", 3), new CraftNeed<string>("Arm", 0),
        };
        var used = new Dictionary<string, List<int>>();

        // A Normal (2) and a Masterwork (5) arm: the Good slot takes the
        // Masterwork, the other slot the Normal one; nothing to craft.
        var stock = new Dictionary<string, List<int>> { ["Arm"] = new List<int> { 2, 5 } };
        var none = new Dictionary<string, List<int>>();
        await Assert.That(ProductionQueue.UncoveredCrafts(
            rollout, stock, none, output, used).Count).IsEqualTo(0);

        // Only the Normal arm: colonist 1 still needs a Good one crafted.
        stock["Arm"] = new List<int> { 2 };
        List<CraftNeed<string>> crafts = ProductionQueue.UncoveredCrafts(
            rollout, stock, none, output, used);
        await Assert.That(crafts.Count).IsEqualTo(1);
        await Assert.That(crafts[0].MinQuality).IsEqualTo(3);

        // A pending bill managed at Poor (1) does not cover the Good slot,
        // so it covers colonist 2 and one Good craft is still queued.
        stock.Clear();
        var pending = new Dictionary<string, List<int>> { ["Arm"] = new List<int> { 1 } };
        crafts = ProductionQueue.UncoveredCrafts(rollout, stock, pending, output, used);
        await Assert.That(crafts.Count).IsEqualTo(1);
        await Assert.That(crafts[0].MinQuality).IsEqualTo(3);
        await Assert.That(used["Arm"]).IsEquivalentTo(new[] { 1 });

        // A bill without a promise covers the first slot it meets.
        used.Clear();
        pending["Arm"] = new List<int> { ProductionQueue.UnknownQuality };
        crafts = ProductionQueue.UncoveredCrafts(rollout, stock, pending, output, used);
        await Assert.That(crafts.Count).IsEqualTo(1);
        await Assert.That(crafts[0].MinQuality).IsEqualTo(0);
    }

    static List<CraftNeed<string>> Needs(params string[] items)
    {
        var needs = new List<CraftNeed<string>>();
        foreach (string item in items) needs.Add(new CraftNeed<string>(item, 0));
        return needs;
    }

    static string Items(List<CraftNeed<string>> crafts)
    {
        var names = new List<string>();
        foreach (CraftNeed<string> craft in crafts) names.Add(craft.Item);
        return string.Join(",", names);
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

    /// A production bill's quality promise (what Quality Jobs was asked to
    /// deliver, or the genoframe tiers the bill allows) lives beside its
    /// record and goes with it; a bill without one reports Unknown.
    [Test]
    public async Task ProductionBillQualityPromisesFollowTheirRecord()
    {
        var model = new PlannerModel();
        model.SetOwnedProductionBill("Bill_Make_7", "BionicArm");

        await Assert.That(model.ProductionBillQualityOf("Bill_Make_7"))
            .IsEqualTo(ProductionQueue.UnknownQuality);
        await Assert.That(model.SetProductionBillQuality("Bill_Make_7", 3))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.SetProductionBillQuality("Bill_Make_7", 3))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.ProductionBillQualityOf("Bill_Make_7")).IsEqualTo(3);
        // No record, no promise.
        await Assert.That(model.SetProductionBillQuality("Bill_Other_8", 3))
            .IsEqualTo(PlannerChange.None);

        model.RemoveOwnedProductionBill("Bill_Make_7");
        await Assert.That(model.ProductionBillQualityOf("Bill_Make_7"))
            .IsEqualTo(ProductionQueue.UnknownQuality);
    }
}
