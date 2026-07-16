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

            var defNames = set.Select(r => r.DefName).ToHashSet();
            foreach (var spec in set)
            {
                foreach (var target in spec.TrainTargets)
                    await Assert.That(defNames.Contains(target)).IsTrue()
                        .Because($"{name}/{spec.DefName}: unknown train target '{target}'");
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
        var recs = StaticRoles.ToRecRoles(set);
        var targets = StaticRoles.ToTargetRoles(set);
        await Assert.That(recs.Count).IsEqualTo(set.Length);
        await Assert.That(targets.Count).IsEqualTo(set.Length);

        for (int i = 0; i < set.Length; i++)
        {
            var spec = set[i];
            var rec = recs[i];
            await Assert.That(rec.Id).IsEqualTo(i + 1);
            await Assert.That(rec.AutoAssign).IsEqualTo(spec.AutoAssign).Because(spec.DefName);
            await Assert.That(rec.Blocker).IsEqualTo(spec.Blocker).Because(spec.DefName);
            await Assert.That(rec.TrainSkill).IsEqualTo(spec.TrainSkill).Because(spec.DefName);
            await Assert.That(rec.TrainMin).IsEqualTo(spec.TrainMin).Because(spec.DefName);
            await Assert.That(rec.TrainMax).IsEqualTo(spec.TrainMax).Because(spec.DefName);
            await Assert.That(rec.MinHolders).IsEqualTo(spec.MinHolders).Because(spec.DefName);
            // Targets resolve to the ids of their defNames' table positions.
            var expectedTargets = spec.TrainTargets
                .Select(defName => System.Array.FindIndex(set, r => r.DefName == defName) + 1)
                .ToList();
            await Assert.That(string.Join(",", rec.TrainTargets))
                .IsEqualTo(string.Join(",", expectedTargets)).Because(spec.DefName);

            var target = targets[i];
            await Assert.That(target.Id).IsEqualTo(rec.Id);
            await Assert.That(target.Coverage.SetEquals(rec.Coverage)).IsTrue()
                .Because(spec.DefName);
            await Assert.That(target.OrderedCoverage.ToHashSet().SetEquals(rec.Coverage))
                .IsTrue().Because($"{spec.DefName}: ordered coverage diverges");
        }

        // Derived flags, spot-checked against known roles.
        RecRole Of(string defName) => recs[System.Array.FindIndex(set, r => r.DefName == defName)];
        await Assert.That(Of("WS_Hunter").Hunting).IsTrue();
        await Assert.That(Of("WS_Grunt").Unskilled).IsTrue();
        await Assert.That(Of("WS_Cook").Unskilled).IsFalse();
        await Assert.That(Of("WS_Core").AutoAssign).IsTrue();
        await Assert.That(Of("WS_NoFirefighting").Blocker).IsTrue();
        await Assert.That(Of("WS_Doctor").WorkTypes.Contains("Doctor")).IsTrue();
    }
}
