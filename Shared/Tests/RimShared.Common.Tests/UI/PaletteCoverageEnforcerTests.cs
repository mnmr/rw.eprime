namespace RimShared.Common.Tests;

public class PaletteCoverageEnforcerTests
{
    private static readonly Rgba S1 = new Rgba(0.2f, 0.4f, 0.6f, 1f);
    private static readonly Rgba S2 = new Rgba(0.9f, 0.1f, 0.1f, 1f);
    private static readonly Rgba C0 = new Rgba(0.5f, 0.5f, 0.5f, 1f);
    private static readonly Rgba Undefined = new Rgba(0f, 0f, 0f, 0f);

    private static readonly Rgba[] Standard = [S1, S2];

    [Test]
    public async Task CoveredColorsProduceAnEmptyPlan()
    {
        var plan = PaletteCoverageEnforcer.Plan(Standard, [C0], maxCustomSwatches: 4, [(1, S1), (2, C0)]);
        await Assert.That(plan.IsEmpty).IsTrue();
    }

    [Test]
    public async Task RolesSharingAnOrphanColorShareOneSlot()
    {
        var orphan = new Rgba(0.1f, 0.7f, 0.3f, 1f);
        var plan = PaletteCoverageEnforcer.Plan(Standard, [], maxCustomSwatches: 4, [(1, orphan), (2, orphan)]);
        await Assert.That(plan.DefineSlots.Count).IsEqualTo(1);
        await Assert.That(plan.SnapRoles).IsEmpty();
    }

    /// The complete overflow interaction: slots claim in role order until
    /// capacity, then roles snap to the nearest palette color — an exact
    /// standard swatch for one, a defined custom slot for the other (snapping
    /// searches standard AND custom, unlike seeding's standard-only rule).
    /// Result order doubles as the determinism contract for MP and load.
    [Test]
    public async Task OverflowClaimsSlotsInRoleOrderThenSnapsTheRest()
    {
        var orphanA = new Rgba(0.1f, 0.7f, 0.3f, 1f);
        var nearS2 = new Rgba(0.85f, 0.15f, 0.12f, 1f);
        var nearC0 = new Rgba(0.52f, 0.48f, 0.5f, 1f);
        var plan = PaletteCoverageEnforcer.Plan(Standard, [C0], maxCustomSwatches: 2, [(1, orphanA), (2, nearS2), (3, nearC0)]);
        await Assert.That(plan.DefineSlots.Count).IsEqualTo(1);
        await Assert.That(plan.DefineSlots[0].slot).IsEqualTo(1);
        await Assert.That(plan.DefineSlots[0].color.Matches(orphanA)).IsTrue();
        await Assert.That(plan.SnapRoles.Count).IsEqualTo(2);
        await Assert.That(plan.SnapRoles[0].roleId).IsEqualTo(2);
        await Assert.That(plan.SnapRoles[0].color.R).IsEqualTo(S2.R);
        await Assert.That(plan.SnapRoles[1].roleId).IsEqualTo(3);
        await Assert.That(plan.SnapRoles[1].color.Matches(C0)).IsTrue();
    }

    /// The invariant, executed: after applying any plan, every custom role
    /// color exists in the palette.
    [Test]
    public async Task AppliedPlansCoverEveryRoleColor()
    {
        List<(int id, Rgba color)> roles = [(1, new Rgba(0.1f, 0.7f, 0.3f, 1f)), (2, new Rgba(0.7f, 0.1f, 0.7f, 1f)), (3, new Rgba(0.33f, 0.44f, 0.55f, 1f)), (4, S1)];
        List<Rgba> custom = [C0, Undefined];
        var plan = PaletteCoverageEnforcer.Plan(Standard, custom, maxCustomSwatches: 2, roles);

        foreach (var (slot, color) in plan.DefineSlots)
        {
            while (custom.Count <= slot)
                custom.Add(Undefined);
            custom[slot] = color;
        }
        for (int i = 0; i < roles.Count; i++)
            foreach (var (roleId, color) in plan.SnapRoles)
                if (roles[i].id == roleId)
                    roles[i] = (roleId, color);

        foreach (var (_, color) in roles)
        {
            bool covered = Standard.Any(s => s.Matches(color)) || custom.Any(c => c.Defined && c.Matches(color));
            await Assert.That(covered).IsTrue();
        }
    }
}
