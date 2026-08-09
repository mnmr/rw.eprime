using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RoleFileScaleTests
{
    [Test]
    public async Task ScalesAndRoleScaleReferencesRoundTripThroughTheFileFormat()
    {
        var bands = new HolderScale();
        for (int i = 0; i < HolderScale.Bands; i++)
        {
            bands.RequiredTotals[i] = 1 + i / 2;
            bands.TrainingWaivers[i] = i / 3;
            bands.Max[i] = 6;
        }
        var scale = new RoleAssignmentStrategy
        {
            Name = "Gentle", Mode = ScaleMode.Skilled, Scale = bands,
        };
        scale.Normalize();
        var doc = new RoleFileDocument();
        doc.scales.Add(scale);
        doc.roles.Add(new FileRole
        {
            label = "Doctor",
            holderScale = "Gentle",
            entries = { new JobEntry(JobEntryKind.WorkType, "Doctor") },
        });

        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));

        await Assert.That(parsed.error == null).IsTrue();
        await Assert.That(parsed.scales.Count).IsEqualTo(1);
        await Assert.That(parsed.scales[0].Name).IsEqualTo("Gentle");
        await Assert.That(parsed.scales[0].SameValuesAs(scale)).IsTrue();
        await Assert.That(parsed.roles[0].holderScale).IsEqualTo("Gentle");
    }

    [Test]
    public async Task BehavioralModesRoundTripAndNeverDropsNumerics()
    {
        var doc = new RoleFileDocument();
        doc.scales.Add(RoleAssignmentStrategy.Never("Never"));
        doc.scales.Add(RoleAssignmentStrategy.Unskilled("Unskilled"));

        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));

        RoleAssignmentStrategy never = parsed.scales
            .Single(s => s.Name == "Never");
        RoleAssignmentStrategy unskilled = parsed.scales
            .Single(s => s.Name == "Unskilled");
        await Assert.That(never.Mode).IsEqualTo(ScaleMode.Never);
        await Assert.That(never.Scale == null).IsTrue();
        await Assert.That(unskilled.Mode).IsEqualTo(ScaleMode.Unskilled);
        await Assert.That(unskilled.Scale.RequiredTotalAt(12)).IsEqualTo(1);
        await Assert.That(unskilled.Scale.RequiredTotalAt(9)).IsEqualTo(0);
    }

    [Test]
    public async Task DocumentsWithoutScalesParseEmpty()
    {
        var doc = new RoleFileDocument();
        doc.roles.Add(new FileRole { label = "Cook" });
        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));
        await Assert.That(parsed.scales.Count).IsEqualTo(0);
        await Assert.That(parsed.roles[0].holderScale == null).IsTrue();
    }
}
