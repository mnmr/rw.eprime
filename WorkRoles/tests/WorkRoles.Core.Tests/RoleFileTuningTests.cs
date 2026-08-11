using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Tuning round-trips through the role file format, and pre-tuning files keep
/// parsing with hasTuning=false so imports derive their classification.
public class RoleFileTuningTests
{
    [Test]
    public async Task TuningRoundTripsThroughRoleFiles()
    {
        var doc = new RoleFileDocument();
        doc.roles.Add(new FileRole
        {
            label = "Doctor",
            holderScale = "Doctoring",
            hasTuning = true,
            category = RoleCategory.Important,
            time = RoleTime.PartTime,
            championPenalty = false,
            minAge = 10,
            requiredSkills = new List<string> { "Medicine" },
            optionalSkills = new List<string> { "Social" },
            entries = new List<JobEntry> { new JobEntry(JobEntryKind.WorkType, "Doctor") },
        });
        doc.roles.Add(new FileRole
        {
            label = "Chores",
            hasTuning = true,
            entries = new List<JobEntry> { new JobEntry(JobEntryKind.WorkType, "Hauling") },
        });

        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));

        FileRole doctor = parsed.roles.First(r => r.label == "Doctor");
        await Assert.That(doctor.hasTuning).IsTrue();
        await Assert.That(doctor.category).IsEqualTo(RoleCategory.Important);
        await Assert.That(doctor.time).IsEqualTo(RoleTime.PartTime);
        await Assert.That(doctor.championPenalty).IsFalse();
        await Assert.That(doctor.minAge).IsEqualTo(10);
        await Assert.That(string.Join(",", doctor.requiredSkills)).IsEqualTo("Medicine");
        await Assert.That(string.Join(",", doctor.optionalSkills)).IsEqualTo("Social");

        // Authored-but-default tuning stays marked authored after a round trip.
        FileRole chores = parsed.roles.First(r => r.label == "Chores");
        await Assert.That(chores.hasTuning).IsTrue();
        await Assert.That(chores.championPenalty).IsTrue();
        // An unset age floor stays absent, so imports derive it.
        await Assert.That(chores.minAge).IsEqualTo(-1);
        await Assert.That(chores.category).IsEqualTo(RoleCategory.None);
        await Assert.That(chores.time).IsEqualTo(RoleTime.None);
        await Assert.That(chores.requiredSkills.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PreTuningFilesParseWithoutTuning()
    {
        var doc = new RoleFileDocument();
        doc.roles.Add(new FileRole
        {
            label = "Old",
            holderScale = "Never",
            entries = new List<JobEntry> { new JobEntry(JobEntryKind.WorkType, "Mining") },
        });

        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));
        await Assert.That(parsed.roles[0].hasTuning).IsFalse();
        await Assert.That(parsed.roles[0].championPenalty).IsTrue();
    }
}
