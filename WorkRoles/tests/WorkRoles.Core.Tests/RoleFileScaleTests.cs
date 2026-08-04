using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class RoleFileScaleTests
{
    [Test]
    public async Task ScalesAndRoleScaleReferencesRoundTripThroughTheFileFormat()
    {
        var scale = new HolderScale { Name = "Gentle" };
        for (int i = 0; i < HolderScale.Bands; i++)
        {
            scale.RequiredTotals[i] = 1 + i / 2;
            scale.TrainingWaivers[i] = i / 3;
            scale.Max[i] = 6;
        }
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
    public async Task DocumentsWithoutScalesParseEmpty()
    {
        var doc = new RoleFileDocument();
        doc.roles.Add(new FileRole { label = "Cook" });
        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));
        await Assert.That(parsed.scales.Count).IsEqualTo(0);
        await Assert.That(parsed.roles[0].holderScale == null).IsTrue();
    }
}
