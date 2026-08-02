using System.Collections.Generic;
using System.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class PaletteCoverageEnforcerTests
{
    private static readonly Rgba S1 = new Rgba(0.2f, 0.4f, 0.6f, 1f);
    private static readonly Rgba S2 = new Rgba(0.9f, 0.1f, 0.1f, 1f);
    private static readonly Rgba C0 = new Rgba(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Rgba Undefined = new Rgba(0f, 0f, 0f, 0f);

    private static readonly Rgba[] Standard = { S1, S2 };

    [Test]
    public async Task CoveredColorsProduceAnEmptyPlan()
    {
        var plan = PaletteCoverageEnforcer.Plan(Standard,
            new List<Rgba> { C0 }, maxCustomSwatches: 4,
            new List<(int, Rgba)> { (1, S1), (2, C0) });
        await Assert.That(plan.IsEmpty).IsTrue();
    }

    [Test]
    public async Task OrphanRoleColorClaimsAFreeSlot()
    {
        var orphan = new Rgba(0.1f, 0.7f, 0.3f, 1f);
        var plan = PaletteCoverageEnforcer.Plan(Standard,
            new List<Rgba> { C0 }, maxCustomSwatches: 4,
            new List<(int, Rgba)> { (1, orphan) });
        await Assert.That(plan.DefineSlots.Count).IsEqualTo(1);
        await Assert.That(plan.DefineSlots[0].slot).IsEqualTo(1);
        await Assert.That(plan.DefineSlots[0].color.Matches(orphan)).IsTrue();
        await Assert.That(plan.SnapRoles).IsEmpty();
    }

    [Test]
    public async Task RolesSharingAnOrphanColorShareOneSlot()
    {
        var orphan = new Rgba(0.1f, 0.7f, 0.3f, 1f);
        var plan = PaletteCoverageEnforcer.Plan(Standard,
            new List<Rgba>(), maxCustomSwatches: 4,
            new List<(int, Rgba)> { (1, orphan), (2, orphan) });
        await Assert.That(plan.DefineSlots.Count).IsEqualTo(1);
        await Assert.That(plan.SnapRoles).IsEmpty();
    }

    [Test]
    public async Task ExhaustedCapacitySnapsToTheNearestPaletteColor()
    {
        var nearS2 = new Rgba(0.85f, 0.15f, 0.12f, 1f);
        var plan = PaletteCoverageEnforcer.Plan(Standard,
            new List<Rgba> { C0 }, maxCustomSwatches: 1,
            new List<(int, Rgba)> { (7, nearS2) });
        await Assert.That(plan.DefineSlots).IsEmpty();
        await Assert.That(plan.SnapRoles.Count).IsEqualTo(1);
        await Assert.That(plan.SnapRoles[0].roleId).IsEqualTo(7);
        await Assert.That(plan.SnapRoles[0].color.R).IsEqualTo(S2.R);
    }

    [Test]
    public async Task SnapPrefersACloserDefinedCustomSlot()
    {
        var nearC0 = new Rgba(0.52f, 0.48f, 0.5f, 1f);
        var plan = PaletteCoverageEnforcer.Plan(Standard,
            new List<Rgba> { C0 }, maxCustomSwatches: 1,
            new List<(int, Rgba)> { (3, nearC0) });
        await Assert.That(plan.SnapRoles.Count).IsEqualTo(1);
        await Assert.That(plan.SnapRoles[0].color.Matches(C0)).IsTrue();
    }

    [Test]
    public async Task OverflowClaimsSlotsInRoleOrderThenSnapsTheRest()
    {
        var orphanA = new Rgba(0.1f, 0.7f, 0.3f, 1f);
        var orphanB = new Rgba(0.7f, 0.1f, 0.7f, 1f);
        var plan = PaletteCoverageEnforcer.Plan(Standard,
            new List<Rgba> { C0 }, maxCustomSwatches: 2,
            new List<(int, Rgba)> { (1, orphanA), (2, orphanB) });
        await Assert.That(plan.DefineSlots.Count).IsEqualTo(1);
        await Assert.That(plan.DefineSlots[0].color.Matches(orphanA)).IsTrue();
        await Assert.That(plan.SnapRoles.Count).IsEqualTo(1);
        await Assert.That(plan.SnapRoles[0].roleId).IsEqualTo(2);
    }

    /// The invariant, executed: after applying any plan, every custom role
    /// color exists in the palette.
    [Test]
    public async Task AppliedPlansCoverEveryRoleColor()
    {
        var roles = new List<(int id, Rgba color)>
        {
            (1, new Rgba(0.1f, 0.7f, 0.3f, 1f)),
            (2, new Rgba(0.7f, 0.1f, 0.7f, 1f)),
            (3, new Rgba(0.33f, 0.44f, 0.55f, 1f)),
            (4, S1),
        };
        var custom = new List<Rgba> { C0, Undefined };
        var plan = PaletteCoverageEnforcer.Plan(Standard, custom,
            maxCustomSwatches: 2, roles);

        foreach (var (slot, color) in plan.DefineSlots)
        {
            while (custom.Count <= slot) custom.Add(Undefined);
            custom[slot] = color;
        }
        for (int i = 0; i < roles.Count; i++)
            foreach (var (roleId, color) in plan.SnapRoles)
                if (roles[i].id == roleId) roles[i] = (roleId, color);

        foreach (var (_, color) in roles)
        {
            bool covered = Standard.Any(s => s.Matches(color))
                || custom.Any(c => c.Defined && c.Matches(color));
            await Assert.That(covered).IsTrue();
        }
    }
}
