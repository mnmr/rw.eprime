using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Certifies the frozen role sets and their engine projections, so tests
/// downstream can trust the catalog inputs regardless of future def edits.
public class StaticRolesTests
{
    [Test]
    public async Task EverySetIsInternallyConsistent()
    {
        foreach (var (name, set) in StaticRoles.Sets)
        {
            await Assert.That(set.Length > 0).IsTrue().Because($"{name} is empty");
            await Assert.That(set.Select(r => r.DefName).Distinct().Count())
                .IsEqualTo(set.Length).Because($"{name}: duplicate defNames");
            foreach (var spec in set)
            {
                await Assert.That(StaticRoles.ParsedEntries(spec).Count)
                    .IsEqualTo(spec.Entries.Length)
                    .Because($"{name}/{spec.DefName}: unparseable entry");
                await Assert.That(spec.Entries.Length > 0).IsTrue()
                    .Because($"{name}/{spec.DefName}: no entries");
            }
        }
    }

    [Test]
    public async Task DefaultSetProjectsFaithfully()
    {
        var set = StaticRoles.Default;
        var views = StaticRoles.ToRoleViews(set);
        await Assert.That(views.Count).IsEqualTo(set.Length);
        // Direct field copies are the projection's own assignments; only the
        // derived values below can actually diverge.
        for (int i = 0; i < set.Length; i++)
        {
            await Assert.That(views[i].OrderedCoverage.ToHashSet().SetEquals(views[i].Coverage))
                .IsTrue().Because($"{set[i].DefName}: ordered coverage diverges");
        }

        // Derived flags and primary skills, spot-checked against known roles.
        WorkRoles.Core.Recs.RoleView Of(string defName)
            => views[System.Array.FindIndex(set, r => r.DefName == defName)];
        await Assert.That(Of("WS_Hunter").Hunting).IsTrue();
        await Assert.That(Of("WS_Grunt").Unskilled).IsTrue();
        await Assert.That(Of("WS_Grunt").PrimarySkill == null).IsTrue();
        await Assert.That(Of("WS_Cook").PrimarySkill).IsEqualTo("Cooking");
        await Assert.That(Of("WS_Doctor").PrimarySkill).IsEqualTo("Medicine");
        await Assert.That(Of("WS_Core").AutoAssign).IsTrue();
        await Assert.That(Of("WS_NoFirefighting").Blocker).IsTrue();
        await Assert.That(Of("WS_Doctor").WorkTypes.Contains("Doctor")).IsTrue();
    }
}
