using Implanner.Core;

namespace Implanner.Core.Tests;

public class ProductionSupplyTests
{
    // One arm is a colonist's final goal; two more are ingredients for upgrades.
    // Treating pending crafts separately for those uses double-spends the arm,
    // or cancels the ingredient bill merely because the arm is also a goal.
    [Test]
    public async Task SurgeryAndUpgradeDemandShareEachBillsActualOutput()
    {
        var supply = new ProductionSupply<string>();
        supply.Add("Arm", "single", 1, 3);
        supply.Add("Arm", "pair", 2, 1);
        supply.Add("Arm", "unused", 1, 1);
        await Assert.That(supply.Take("Arm", 1, minimum: 3)).IsEqualTo(0);
        await Assert.That(supply.Take("Arm", 2)).IsEqualTo(0);
        await Assert.That(supply.Uses("single")).IsTrue();
        await Assert.That(supply.Uses("pair")).IsTrue();
        await Assert.That(supply.Uses("unused")).IsFalse();
        await Assert.That(supply.Take("Arm", 2)).IsEqualTo(1);
        await Assert.That(supply.Take("Arm", 1)).IsEqualTo(1);
    }
}
