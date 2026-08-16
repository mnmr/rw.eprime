namespace WorkRoles.Core.Tests.Persistence;

/// Tuning and opt-in skill gates round-trip through the current role-file
/// format. Legacy skill classifications are not reinterpreted as hard gates.
public class RoleFileTuningTests
{
    [Test]
    public async Task TuningRoundTripsThroughRoleFiles()
    {
        var doc = new RoleFileDocument();
        doc.roles.Add(
            new FileRole
            {
                label = "Doctor",
                hasTuning = true,
                category = RoleCategory.Important,
                time = RoleTime.PartTime,
                championPenalty = false,
                minAge = 10,
                maxAge = 12,
                requiredSkills = ["Medicine"],
                entries = [new JobEntry(JobEntryKind.WorkType, "Doctor")],
            }
        );

        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));

        FileRole doctor = parsed.roles.First(r => r.label == "Doctor");
        await Assert.That(doctor.hasTuning).IsTrue();
        await Assert.That(doctor.category).IsEqualTo(RoleCategory.Important);
        await Assert.That(doctor.time).IsEqualTo(RoleTime.PartTime);
        await Assert.That(doctor.championPenalty).IsFalse();
        await Assert.That(doctor.minAge).IsEqualTo(10);
        await Assert.That(doctor.maxAge).IsEqualTo(12);
        await Assert.That(string.Join(",", doctor.requiredSkills)).IsEqualTo("Medicine");
    }

    [Test]
    public async Task LegacySkillClassificationDoesNotBecomeAHardGate()
    {
        const string legacy = """
            <WorkRoles version="12">
              <Roles>
                <Role name="Doctor">
                  <Options>
                    <Tuning>
                      <RequiredSkills>Medicine</RequiredSkills>
                      <OptionalSkills>Social</OptionalSkills>
                    </Tuning>
                  </Options>
                  <Jobs><WorkType>Doctor</WorkType></Jobs>
                </Role>
              </Roles>
            </WorkRoles>
            """;

        RoleFileDocument parsed = RoleFile.Parse(legacy);

        await Assert.That(parsed.roles.Single().hasTuning).IsTrue();
        await Assert.That(parsed.roles.Single().requiredSkills).IsEmpty();
    }

    [Test]
    public async Task AuthoredDefaultTuningStaysAuthoredThroughRoleFiles()
    {
        var doc = new RoleFileDocument();
        doc.roles.Add(
            new FileRole
            {
                label = "Chores",
                hasTuning = true,
                entries = [new JobEntry(JobEntryKind.WorkType, "Hauling")],
            }
        );

        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));

        FileRole chores = parsed.roles.Single();
        await Assert.That(chores.hasTuning).IsTrue();
        await Assert.That(chores.championPenalty).IsTrue();
        await Assert.That(chores.minAge).IsEqualTo(-1);
        await Assert.That(chores.maxAge).IsEqualTo(0);
        await Assert.That(chores.category).IsEqualTo(RoleCategory.None);
        await Assert.That(chores.time).IsEqualTo(RoleTime.None);
        await Assert.That(chores.requiredSkills.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PreTuningFilesParseWithoutTuning()
    {
        var doc = new RoleFileDocument();
        doc.roles.Add(new FileRole { label = "Old", entries = [new JobEntry(JobEntryKind.WorkType, "Mining")] });

        RoleFileDocument parsed = RoleFile.Parse(RoleFile.Build(doc));
        await Assert.That(parsed.roles[0].hasTuning).IsFalse();
        await Assert.That(parsed.roles[0].championPenalty).IsTrue();
    }
}
