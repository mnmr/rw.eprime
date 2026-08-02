using System.Collections.Generic;
using System.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class SwatchPickPlannerTests
{
    private static readonly Rgba S1 = new Rgba(0.2f, 0.4f, 0.6f, 1f);
    private static readonly Rgba S2 = new Rgba(0.9f, 0.1f, 0.1f, 1f);
    private static readonly Rgba C0 = new Rgba(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Rgba C1 = new Rgba(0.3f, 0.8f, 0.3f, 1f);
    private static readonly Rgba Undefined = new Rgba(0f, 0f, 0f, 0f);

    private static readonly Rgba[] Standard = { S1, S2 };

    private static List<Rgba> Custom() => new List<Rgba> { C0, C1 };

    /// Custom-colored roles only: 1 follows slot 0, 4 follows slot 1,
    /// 3 wears a standard swatch (picks must leave it untouched).
    private static List<(int id, Rgba color)> Roles() => new List<(int, Rgba)>
    {
        (1, C0),
        (3, S2),
        (4, C1),
    };

    [Test]
    public async Task ReacceptingTheSlotsOwnColorChangesNothing()
    {
        var plan = SwatchPickPlanner.Plan(C0, slot: 0, Standard, Custom(),
            applyToEditedRole: false, editedRoleId: 1, Roles());
        await Assert.That(plan.ClearSlot).IsFalse();
        await Assert.That(plan.SetSlot).IsTrue();
        await Assert.That(plan.Applied.Matches(C0)).IsTrue();
        await Assert.That(plan.RecolorRoleIds).IsEmpty();
        await Assert.That(plan.RecolorEditedRole).IsFalse();
    }

    [Test]
    public async Task RedefiningToAStandardColorCollapsesTheSlotAndRecolorsFollowers()
    {
        var plan = SwatchPickPlanner.Plan(S2, slot: 0, Standard, Custom(),
            applyToEditedRole: false, editedRoleId: 1, Roles());
        await Assert.That(plan.ClearSlot).IsTrue();
        await Assert.That(plan.SetSlot).IsFalse();
        await Assert.That(plan.Applied.Matches(S2)).IsTrue();
        await Assert.That(plan.RecolorRoleIds).IsEquivalentTo(new[] { 1 });
        await Assert.That(plan.RecolorEditedRole).IsFalse();
    }

    [Test]
    public async Task RedefiningToANewCustomColorMovesFollowersWithTheSlot()
    {
        var picked = new Rgba(0.15f, 0.25f, 0.35f, 1f);
        var plan = SwatchPickPlanner.Plan(picked, slot: 0, Standard, Custom(),
            applyToEditedRole: false, editedRoleId: 1, Roles());
        await Assert.That(plan.ClearSlot).IsFalse();
        await Assert.That(plan.SetSlot).IsTrue();
        await Assert.That(plan.Applied.Matches(picked)).IsTrue();
        await Assert.That(plan.RecolorRoleIds).IsEquivalentTo(new[] { 1 });
    }

    [Test]
    public async Task DefiningAnEmptySlotWithAPaletteColorAppliesTheCanonicalMatch()
    {
        var custom = new List<Rgba> { C0, C1, Undefined };
        var nearS1 = new Rgba(0.201f, 0.4f, 0.6f, 1f);
        var plan = SwatchPickPlanner.Plan(nearS1, slot: 2, Standard, custom,
            applyToEditedRole: true, editedRoleId: 3, Roles());
        await Assert.That(plan.ClearSlot).IsFalse();
        await Assert.That(plan.SetSlot).IsFalse();
        // Canonical: the palette's exact color, not the near-identical pick.
        await Assert.That(plan.Applied.R).IsEqualTo(S1.R);
        await Assert.That(plan.RecolorEditedRole).IsTrue();
        await Assert.That(plan.RecolorRoleIds).IsEmpty();
    }

    [Test]
    public async Task PickMatchingAnotherCustomSlotCollapsesToThatSlot()
    {
        var plan = SwatchPickPlanner.Plan(C0, slot: 1, Standard, Custom(),
            applyToEditedRole: false, editedRoleId: 4, Roles());
        await Assert.That(plan.ClearSlot).IsTrue();
        await Assert.That(plan.SetSlot).IsFalse();
        await Assert.That(plan.Applied.Matches(C0)).IsTrue();
        await Assert.That(plan.RecolorRoleIds).IsEquivalentTo(new[] { 4 });
    }

    /// The palette invariant, executed: after applying any plan, every
    /// custom-colored role's color still exists in the palette (standard
    /// swatches or defined custom slots), so its chip stays highlighted.
    [Test]
    [Arguments(0.5f, 0.5f, 0.5f, 0, false, 1)]     // re-accept own color
    [Arguments(0.9f, 0.1f, 0.1f, 0, false, 1)]     // collapse to standard
    [Arguments(0.15f, 0.25f, 0.35f, 0, false, 1)]  // redefine to new custom
    [Arguments(0.5f, 0.5f, 0.5f, 1, false, 4)]     // collapse into other slot
    [Arguments(0.3f, 0.8f, 0.3f, 0, true, 3)]      // define matching other slot
    public async Task AppliedPlansNeverStrandARoleColorOutsideThePalette(
        float r, float g, float b, int slot, bool applyToEditedRole, int editedRoleId)
    {
        var picked = new Rgba(r, g, b, 1f);
        var custom = Custom();
        var roles = Roles();
        var plan = SwatchPickPlanner.Plan(picked, slot, Standard, custom,
            applyToEditedRole, editedRoleId, roles);

        if (plan.ClearSlot) custom[slot] = Undefined;
        if (plan.SetSlot)
        {
            while (custom.Count <= slot) custom.Add(Undefined);
            custom[slot] = plan.Applied;
        }
        for (int i = 0; i < roles.Count; i++)
            if (plan.RecolorRoleIds.Contains(roles[i].id)
                || (plan.RecolorEditedRole && roles[i].id == editedRoleId))
                roles[i] = (roles[i].id, plan.Applied);

        foreach (var (_, color) in roles)
        {
            bool inPalette = Standard.Any(s => s.Matches(color))
                || custom.Any(c => c.Defined && c.Matches(color));
            await Assert.That(inPalette).IsTrue();
        }
    }

    [Test]
    public async Task MatchesUsesByteQuantizationAndSumTolerance()
    {
        var baseColor = new Rgba(0.2f, 0.4f, 0.6f, 1f);
        // Every channel off by 0.001: same 32-bit color despite sum tolerance.
        await Assert.That(baseColor.Matches(
            new Rgba(0.201f, 0.401f, 0.601f, 1f))).IsTrue();
        // One channel off by 0.006: different byte, sum over tolerance.
        await Assert.That(baseColor.Matches(
            new Rgba(0.206f, 0.4f, 0.6f, 1f))).IsFalse();
    }
}
