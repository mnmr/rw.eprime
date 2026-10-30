using Implanner.Core;

namespace Implanner.Core.Tests;

public class ProductionRouteTests
{
    [Test]
    public async Task DiverseCorpsesCannotFundAnImpossibleFrameQuality()
    {
        var corpses = new List<(string, int)>();
        var budget = new ProductionBudget<string>();
        for (int i = 0; i < 15; i++)
        { string corpse = "Corpse" + i; corpses.Add((corpse, 1)); budget.Track(corpse, 1, 0); }
        var recipes = new Dictionary<string, IReadOnlyList<ProductionRecipe<string>>>();
        recipes["Frame0"] = new[] { new ProductionRecipe<string>("Frame0", "Frame0", 1,
            Array.Empty<(string, int)>(), true,
            alternatives: new[] { (IReadOnlyList<(string, int)>)corpses }) };
        var intermediaries = new HashSet<string> { "Frame0" };
        for (int i = 1; i <= 4; i++)
        {
            string frame = "Frame" + i;
            recipes[frame] = new[] { new ProductionRecipe<string>(frame, frame, 1,
                new[] { ("Frame" + (i - 1), 2) }, true) };
            intermediaries.Add(frame);
        }
        // VGE's Excellent frame needs sixteen corpses, irrespective of species.
        await Assert.That(ProductionRoute<string>.Select("Frame4", recipes, budget, intermediaries).Blocked).IsTrue();
        var feasible = ProductionRoute<string>.Select("Frame2", recipes, budget, intermediaries);
        await Assert.That(feasible.Blocked).IsFalse();
        await VerifyCompletes(feasible, budget, "Frame2");
        recipes["Genome"] = new[] { new ProductionRecipe<string>("Genome", "Genome", 1,
            new[] { ("Corpse0", 1) }, true) };
        recipes["Claw"] = new[] { new ProductionRecipe<string>("Claw", "Claw", 1,
            new[] { ("Genome", 1), ("Frame3", 1) }, true) };
        intermediaries.Add("Genome");
        for (int i = 8; i < 15; i++) budget.Track("Corpse" + i, 0, 0);
        // The genome's sibling consumes the ninth corpse. The capacity bound
        // must also run after a preceding sibling has spent shared stock.
        await Assert.That(ProductionRoute<string>.Select("Claw", recipes, budget, intermediaries).Blocked).IsTrue();
        budget.Track("Corpse8", 1, 0);
        feasible = ProductionRoute<string>.Select("Claw", recipes, budget, intermediaries);
        await Assert.That(feasible.Blocked).IsFalse();
        await VerifyCompletes(feasible, budget, "Claw");
    }

    [Test]
    public async Task CategoryIngredientMustBeStockedOrCraftedBeforeTheImplant()
    {
        var recipes = new Dictionary<string, IReadOnlyList<ProductionRecipe<string>>>
        {
            ["Claw"] = new[] { new ProductionRecipe<string>("Claw", "Claw", 1,
                new[] { ("Genome", 2) }, true, alternatives: new[] {
                    (IReadOnlyList<(string, int)>)new[] { ("NormalFrame", 1), ("ExcellentFrame", 1) } }) },
            ["NormalFrame"] = new[] { new ProductionRecipe<string>("Frame", "NormalFrame", 1,
                new[] { ("Steel", 5) }, true) }
        };
        var budget = new ProductionBudget<string>();
        budget.Track("Genome", 2, 0);
        budget.Track("Steel", 5, 0);
        var route = ProductionRoute<string>.Select("Claw", recipes, budget, new HashSet<string>());
        await Assert.That(route.Blocked).IsTrue();
        route = ProductionRoute<string>.Select("Claw", recipes, budget, new HashSet<string> { "NormalFrame" });
        await Assert.That(route.Blocked).IsFalse();
        await Assert.That(route.Crafts.Count).IsEqualTo(2);
        await VerifyCompletes(route, budget, "Claw");
        // Use an already available acceptable alternative without spending
        // another bench slot manufacturing the first-listed option.
        budget.Track("ExcellentFrame", 1, 0);
        route = ProductionRoute<string>.Select("Claw", recipes, budget, new HashSet<string> { "NormalFrame" });
        await Assert.That(route.Crafts.Count).IsEqualTo(1);
        await Assert.That(route.Recipe!.Costs).Contains(("ExcellentFrame", 1));
        await VerifyCompletes(route, budget, "Claw");
    }

    [Test]
    public async Task PrerequisitesLeaveEnoughMaterialsToFinishTheParentAndItsSiblings()
    {
        var recipes = new Dictionary<string, IReadOnlyList<ProductionRecipe<string>>>
        {
            ["Upgrade"] = new[] { new ProductionRecipe<string>("Upgrade", "Upgrade", 1,
                new[] { ("Base", 1), ("Steel", 5) }, true, 1) },
            ["Base"] = new[]
            {
                new ProductionRecipe<string>("SteelBase", "Base", 1, new[] { ("Steel", 10) }, true, 1),
                new ProductionRecipe<string>("PlasteelBase", "Base", 1, new[] { ("Plasteel", 10) }, true, 2),
            },
            ["Other"] = new[] { new ProductionRecipe<string>("Other", "Other", 1,
                new[] { ("Steel", 10) }, true, 1) },
        };
        var budget = new ProductionBudget<string>();
        budget.Track("Steel", 10, 0);
        budget.Track("Plasteel", 10, 0);
        var intermediaries = new HashSet<string> { "Base", "Other" };
        var route = ProductionRoute<string>.Select("Upgrade", recipes, budget, intermediaries);
        await Assert.That(route.Blocked).IsFalse();
        await VerifyCompletes(route, budget, "Upgrade");
        // A later sibling can require revisiting an earlier prerequisite's recipe.
        recipes["Upgrade"] = new[] { new ProductionRecipe<string>("Upgrade", "Upgrade", 1,
            new[] { ("Base", 1), ("Other", 1) }, true, 1) };
        route = ProductionRoute<string>.Select("Upgrade", recipes, budget, intermediaries);
        await Assert.That(route.Blocked).IsFalse();
        await VerifyCompletes(route, budget, "Upgrade");
    }

    // EPOE's two real routes have equal full material costs. These scenarios
    // catch selection of hidden legacy defs and dead-end assembly recipes.
    [Test]
    public async Task AvailableStockChoosesAUsableAdvancedArmRoute()
    {
        var recipes = Recipes();
        var budget = Budget(arms: 0, plasteel: 20, components: 6);
        var chain = new HashSet<string> { "Arm", "Component" };
        await Assert.That(ProductionRoute<string>.Select("AdvancedArm", recipes, budget, chain).Recipe?.Id)
            .IsEqualTo("Make_AdvancedArm");
        budget = Budget(arms: 1, plasteel: 20, components: 6);
        await Assert.That(ProductionRoute<string>.Select("AdvancedArm", recipes, budget, chain).Recipe?.Id)
            .IsEqualTo("Assemble_AdvancedArm");
        await Assert.That(ProductionRoute<string>.Select("Arm", recipes, budget, chain).Recipe?.Id)
            .IsEqualTo("Make_Arm");
    }

    [Test]
    public async Task UpgradeOnlyCanBootstrapButRawShortfallsRemainVisible()
    {
        var recipes = Recipes(direct: false);
        var chain = new HashSet<string> { "Arm" };
        var ready = ProductionRoute<string>.Select("AdvancedArm", recipes,
            Budget(0, 20, 6), chain);
        await Assert.That(ready.Recipe?.Id).IsEqualTo("Assemble_AdvancedArm");
        await Assert.That(ready.Blocked).IsFalse();
        var blocked = ProductionRoute<string>.Select("AdvancedArm", recipes,
            Budget(0, 19, 6), chain);
        await Assert.That(blocked.Blocked).IsTrue();
        await Assert.That(blocked.Blocker).IsEqualTo("Plasteel");
        var disabled = ProductionRoute<string>.Select("AdvancedArm", recipes,
            Budget(0, 20, 6), new HashSet<string>());
        await Assert.That(disabled.Blocker).IsEqualTo("Arm");
    }

    // A cycle is not a production route; another usable recipe must still win.
    // Candidate order is deliberately reversed to prove stable recipe selection.
    [Test]
    public async Task CyclesDoNotHideAnAlternativeOrSpendTheCallersBudget()
    {
        var recipes = Recipes();
        recipes["Arm"] = new[]
        {
            new ProductionRecipe<string>("RecycleArm", "Arm", 1,
                new[] { ("AdvancedArm", 1) }, true, 1),
        };
        var budget = Budget(0, 20, 6);
        var intermediaries = new HashSet<string> { "Arm", "AdvancedArm" };
        var first = ProductionRoute<string>.Select("AdvancedArm", recipes, budget, intermediaries);
        await Assert.That(first.Recipe?.Id).IsEqualTo("Make_AdvancedArm");
        recipes["AdvancedArm"] = recipes["AdvancedArm"].Reverse().ToArray();
        await Assert.That(ProductionRoute<string>.Select("AdvancedArm", recipes, budget, intermediaries).Recipe?.Id)
            .IsEqualTo("Make_AdvancedArm");
        await Assert.That(budget.TryCommit(new[] { ("Plasteel", 20), ("Component", 6) })).IsTrue();
        var blocked = ProductionRoute<string>.Select("AdvancedArm", recipes, budget, intermediaries);
        await Assert.That(blocked.Blocked).IsTrue();
    }

    [Test]
    public async Task ReservesAndQueuedCostsApplyAcrossTheWholeUpgradeChain()
    {
        var recipes = Recipes(direct: false);
        var budget = Budget(0, 25, 8);
        budget.Track("Plasteel", 25, 5);
        budget.Track("Component", 8, 2);
        var intermediaries = new HashSet<string> { "Arm" };
        await Assert.That(ProductionRoute<string>.Select("AdvancedArm", recipes, budget, intermediaries).Blocked)
            .IsFalse();
        budget.Commit("Plasteel", 1);
        var route = ProductionRoute<string>.Select("AdvancedArm", recipes, budget, intermediaries);
        await Assert.That(route.Blocked).IsTrue();
        await Assert.That(route.Blocker).IsEqualTo("Plasteel");
    }

    [Test]
    public async Task PrerequisiteBatchOutputIsSpentInItemUnits()
    {
        var recipes = Recipes(direct: false);
        recipes["Arm"] = new[] { new ProductionRecipe<string>("MakePair", "Arm", 2,
            new[] { ("Plasteel", 15), ("Component", 4) }, true, 26000) };
        recipes["AdvancedArm"] = new[] { new ProductionRecipe<string>("AssemblePair", "AdvancedArm", 2,
            new[] { ("Arm", 2), ("Plasteel", 5), ("Component", 2) }, true, 14000) };
        await Assert.That(ProductionRoute<string>.Select("AdvancedArm", recipes, Budget(0, 20, 6),
            new HashSet<string> { "Arm" }).Blocked).IsFalse();
    }

    // Execute the public route as a consumer would: every craft must be payable
    // from real stock and earlier output, regardless of sibling scheduling order.
    static async Task VerifyCompletes(ProductionRoute<string> route,
        ProductionBudget<string> stock, string product)
    {
        var available = stock.Copy();
        foreach (var craft in route.Crafts)
        {
            await Assert.That(available.TryCommit(craft.Costs)).IsTrue();
            available.Supply(craft.Item, craft.Output);
        }
        await Assert.That(available.Shortfall(product, 1)).IsEqualTo(0);
    }

    static Dictionary<string, IReadOnlyList<ProductionRecipe<string>>> Recipes(bool direct = true) => new()
    {
        ["AdvancedArm"] = new[]
        {
            new ProductionRecipe<string>("Assemble_AdvancedArm", "AdvancedArm", 1,
                new[] { ("Arm", 1), ("Plasteel", 5), ("Component", 2) }, true, 14000),
            new ProductionRecipe<string>("Make_AdvancedArm", "AdvancedArm", 1,
                new[] { ("Plasteel", 20), ("Component", 6) }, direct, 40000),
        },
        ["Arm"] = new[]
        {
            new ProductionRecipe<string>("Create_Arm", "Arm", 1,
                new[] { ("Plasteel", 15), ("Component", 4) }, false, 26000),
            new ProductionRecipe<string>("Make_Arm", "Arm", 1,
                new[] { ("Plasteel", 15), ("Component", 4) }, true, 26000),
        },
    };

    static ProductionBudget<string> Budget(int arms, int plasteel, int components)
    {
        var result = new ProductionBudget<string>();
        result.Track("Arm", arms, 0);
        result.Track("Plasteel", plasteel, 0);
        result.Track("Component", components, 0);
        return result;
    }
}
