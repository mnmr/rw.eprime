using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using EPrimeReadouts;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

internal static class Program
{
    private const string GameRoot = "C:/Program Files (x86)/Steam/steamapps/common/RimWorld";
    private static readonly Type Planned = typeof(GameCounts).Assembly.GetType("EPrimeReadouts.PlannedWorkCounts", true)!;
    private static readonly MethodInfo Accumulate = Planned.GetMethod("AccumulateBill", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static int failures;

    private static void Main()
    {
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(GameRoot, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        Run();
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        BillRepeatModeDefOf.RepeatCount = new BillRepeatModeDef { defName = "RepeatCount" };
        BillRepeatModeDefOf.TargetCount = new BillRepeatModeDef { defName = "TargetCount" };
        BillRepeatModeDefOf.Forever = new BillRepeatModeDef { defName = "Forever" };
        var material = new ThingDef { defName = "Cloth", resourceReadoutPriority = ResourceCountPriority.Middle };
        var otherFabric = new ThingDef { defName = "DevilstrandCloth", resourceReadoutPriority = ResourceCountPriority.Middle };
        var product = new ThingDef { defName = "Apparel_Duster" };
        var ingredient = new IngredientCount();
        ingredient.filter.SetAllow(material, true);
        ingredient.filter.SetAllow(otherFabric, true);
        ingredient.SetBaseCount(80);
        var recipe = new RecipeDef { defName = "Make_Apparel_Duster" };
        recipe.ingredients.Add(ingredient);
        recipe.products.Add(new ThingDefCountClass(product, 1));
        recipe.fixedIngredientFilter.SetAllow(material, true);
        recipe.fixedIngredientFilter.SetAllow(otherFabric, true);
        var bill = (Bill_ProductionWithUft)RuntimeHelpers.GetUninitializedObject(typeof(Bill_ProductionWithUft));
        bill.recipe = recipe;
        bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
        bill.repeatCount = 1;
        bill.billStack = new BillStack(null);
        bill.ingredientFilter = new ThingFilter();
        bill.ingredientFilter.SetAllow(material, true);
        Print("Before starting: 160 cloth in stock, one 80-cloth duster", bill, material, 160, 80);
        // The vanilla Toils_Recipe transition despawns ingredients and binds them
        // to the unfinished object, without decrementing repeatCount until completion.
        var unfinished = (UnfinishedThing)RuntimeHelpers.GetUninitializedObject(typeof(UnfinishedThing));
        unfinished.ingredients = new List<Thing> { new Thing { def = material, stackCount = 80 } };
        typeof(Bill_ProductionWithUft).GetField("boundUftInt", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(bill, unfinished);
        Print("After vanilla unfinished-item transition: 80 cloth remains in stock", bill, material, 80, 80);
        bill.repeatCount = 2;
        Print("Two queued dusters, first already owns 80 cloth: 160 stock remains", bill, material, 160, 80);
        bill.repeatCount = 0;
        Print("Completed control: no remaining iterations, 80 cloth remains", bill, material, 80, 80);
        bill.repeatCount = 1;
        var managedType = typeof(GameCounts).Assembly.GetType("EPrimeReadouts.QualityJobsBridge+ManagedBillJob", true)!;
        var managed = Activator.CreateInstance(managedType, BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object?[] { null, bill, recipe, product, 1, 0.5 }, null)!;
        var managedCounts = new CountAccumulator();
        Planned.GetMethod("AccumulateManagedBill", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { managed, managedCounts });
        int managedDebt = managedCounts.ToSnapshot().DebtOf(material.defName).Bills;
        if (managedDebt != 80) failures++;
        Console.WriteLine($"QJA 50% quality chance, first attempt owns 80 cloth: debt={managedDebt}; expected future debt=80; {(managedDebt == 80 ? "PASS" : "BUG CONFIRMED")}");

        StoneAndPauseScenarios();
    }

    private static void StoneAndPauseScenarios()
    {
        var map = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
        var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
        typeof(Game).GetField("maps", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, new List<Map> { map });
        typeof(Current).GetField("gameInt", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, game);
        map.resourceCounter = new ResourceCounter(map);
        var giver = (Building_WorkTable)RuntimeHelpers.GetUninitializedObject(typeof(Building_WorkTable));
        typeof(Thing).GetField("mapIndexOrState", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(giver, (sbyte)0);
        var chunk = new ThingDef { defName = "ChunkGranite" };
        var slateChunk = new ThingDef { defName = "ChunkSlate" };
        var blocks = new ThingDef { defName = "BlocksGranite", resourceReadoutPriority = ResourceCountPriority.Middle };
        chunk.butcherProducts = new List<ThingDefCountClass> { new(blocks, 20) };
        DefDatabase<ThingDef>.Add(chunk);
        DefDatabase<ThingDef>.Add(slateChunk);
        DefDatabase<ThingDef>.Add(blocks);
        ThingCategoryDefOf.StoneBlocks = new ThingCategoryDef { childThingDefs = new List<ThingDef> { blocks } };
        map.resourceCounter.AllCountedAmounts[blocks] = 0;
        var ingredient = new IngredientCount();
        ingredient.filter.SetAllow(chunk, true);
        ingredient.filter.SetAllow(slateChunk, true);
        ingredient.SetBaseCount(1);
        var recipe = new RecipeDef { defName = "Make_StoneBlocksAny", workerCounterClass = typeof(RecipeWorkerCounter_MakeStoneBlocks),
            specialProducts = new List<SpecialProductType> { SpecialProductType.StoneBlocks } };
        recipe.ingredients.Add(ingredient);
        recipe.fixedIngredientFilter.SetAllow(chunk, true);
        recipe.fixedIngredientFilter.SetAllow(slateChunk, true);
        var bill = (Bill_Production)RuntimeHelpers.GetUninitializedObject(typeof(Bill_Production));
        bill.recipe = recipe;
        bill.repeatMode = BillRepeatModeDefOf.TargetCount;
        bill.targetCount = 100;
        bill.billStack = new BillStack(giver);
        bill.ingredientFilter = new ThingFilter();
        bill.ingredientFilter.SetAllow(chunk, true);
        bill.ingredientFilter.SetAllow(slateChunk, true);
        Print("Control generic stone bill, unrestricted choice between granite and slate", bill, chunk, 20, 20);
        bill.ingredientFilter.SetAllow(slateChunk, false);
        Print("Make any stone blocks, granite only, target100 blocks, 20 chunks in stock", bill, chunk, 20, 15);
        recipe.products.Add(new ThingDefCountClass(blocks, 20));
        recipe.specialProducts = null;
        Print("Control equivalent Make granite blocks, same20 chunks and target100", bill, chunk, 20, 15);

        bill.pauseWhenSatisfied = true;
        bill.unpauseWhenYouHave = 50;
        map.resourceCounter.AllCountedAmounts[blocks] = 100;
        Console.WriteLine($"Initial satisfied bill: ShouldDoNow={bill.ShouldDoNow()}, paused={bill.paused}");
        map.resourceCounter.AllCountedAmounts[blocks] = 40;
        Print("Paused bill product drops40 below restart50; before next workgiver/UI poll", bill, chunk, 20, 17, diagnostic: true);
        Console.WriteLine($"Vanilla reevaluation: ShouldDoNow={bill.ShouldDoNow()}, paused={bill.paused}");
        Print("Same bill and same stock after vanilla reevaluation", bill, chunk, 20, 17);
    }

    private static void Print(string scenario, Bill_Production bill, ThingDef material, int stock, int expected, bool diagnostic = false)
    {
        var counts = new CountAccumulator();
        counts.Add(material.defName, 1, stock);
        Accumulate.Invoke(null, new object[] { bill, counts });
        var snapshot = counts.ToSnapshot();
        int debt = snapshot.DebtOf(material.defName).Bills;
        int displayed = CountBasis.Displayed(new SearchCount(stock, stock, stock, stock), false, false, debt, true);
        if (displayed != expected && !diagnostic) failures++;
        Console.WriteLine($"{scenario}: debt={debt}, displayed={displayed}, expected={expected}; {(displayed == expected ? "PASS" : diagnostic ? "CANDIDATE OBSERVED (semantics not established)" : "BUG CONFIRMED")}");
    }
}
