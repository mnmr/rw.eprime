using RimShared.Common;

namespace RimShared.Common.Tests;

/// Vanilla-style mod requirements on exported elements: MayRequire is a
/// comma list where every packageId must be active, MayRequireAnyOf a comma
/// list where one is enough. The import formats drop elements that fail.
public class MayRequireRulesTests
{
    private static readonly HashSet<string> Active =
        new(StringComparer.Ordinal) { "ludeon.rimworld.biotech", "ludeon.rimworld.ideology" };

    private static bool IsActive(string packageId) => Active.Contains(packageId);

    [Test]
    public async Task MayRequireNeedsEveryListedModTrimmed()
    {
        await Assert.That(MayRequireRules.Satisfied(
            " ludeon.rimworld.biotech , ludeon.rimworld.ideology ", null, IsActive)).IsTrue();
        await Assert.That(MayRequireRules.Satisfied(
            "ludeon.rimworld.biotech,ludeon.rimworld.anomaly", null, IsActive)).IsFalse();
    }

    [Test]
    public async Task MayRequireAnyOfNeedsOneListedModTrimmed()
    {
        await Assert.That(MayRequireRules.Satisfied(
            null, "ludeon.rimworld.anomaly, ludeon.rimworld.biotech ", IsActive)).IsTrue();
        await Assert.That(MayRequireRules.Satisfied(
            null, "ludeon.rimworld.anomaly,ludeon.rimworld.odyssey", IsActive)).IsFalse();
    }

    [Test]
    public async Task BothAttributesMustHold()
    {
        await Assert.That(MayRequireRules.Satisfied(
            "ludeon.rimworld.biotech", "ludeon.rimworld.ideology,ludeon.rimworld.anomaly",
            IsActive)).IsTrue();
        await Assert.That(MayRequireRules.Satisfied(
            "ludeon.rimworld.anomaly", "ludeon.rimworld.ideology", IsActive)).IsFalse();
        await Assert.That(MayRequireRules.Satisfied(
            "ludeon.rimworld.biotech", "ludeon.rimworld.anomaly", IsActive)).IsFalse();
    }

    [Test]
    public async Task MissingOrEmptyRequirementsKeepTheElement()
    {
        await Assert.That(MayRequireRules.Satisfied(null, null, IsActive)).IsTrue();
        await Assert.That(MayRequireRules.Satisfied("", "", IsActive)).IsTrue();
    }

    [Test]
    public async Task NullPredicateKeepsEverything()
    {
        await Assert.That(MayRequireRules.Satisfied(
            "ludeon.rimworld.anomaly", "ludeon.rimworld.odyssey", null)).IsTrue();
    }
}
