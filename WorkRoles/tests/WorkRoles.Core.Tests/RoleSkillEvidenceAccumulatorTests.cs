using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RoleSkillEvidenceAccumulatorTests
{
    [Test]
    public async Task ScratchDeduplicatesSourcesAndPerSourceFactsThenResetsForNextRole()
    {
        var scratch = new RoleSkillEvidenceAccumulator();
        scratch.BeginRole();

        await Assert.That(scratch.BeginSource("giver-a")).IsTrue();
        scratch.AddUsedSkill("Crafting");
        scratch.AddUsedSkill("Crafting");
        scratch.AddTrainedSkill("Crafting");
        scratch.AddTrainedSkill("Crafting");
        scratch.AddRequiredContent("Crafting", 0);
        scratch.AddRequiredContent("Crafting", 2);
        await Assert.That(scratch.BeginSource("giver-a")).IsFalse();

        await Assert.That(scratch.BeginSource("giver-b")).IsTrue();
        scratch.AddUsedSkill("Crafting");
        scratch.AddTrainedSkill("Intellectual");
        IReadOnlyList<RoleSkillEvidence> first = scratch.CompleteRole();

        await Assert.That(string.Join(",", first.Select(e => e.SkillDefName)))
            .IsEqualTo("Crafting,Intellectual");
        RoleSkillEvidence crafting = first.Single(e => e.SkillDefName == "Crafting");
        await Assert.That(crafting.UsedJobs).IsEqualTo(2);
        await Assert.That(crafting.TrainedJobs).IsEqualTo(1);
        await Assert.That(crafting.RequiredContent).IsEqualTo(3);

        scratch.BeginRole();
        await Assert.That(scratch.BeginSource("giver-a")).IsTrue();
        scratch.AddTrainedSkill("Crafting");
        IReadOnlyList<RoleSkillEvidence> second = scratch.CompleteRole();

        await Assert.That(second.Count).IsEqualTo(1);
        await Assert.That(second[0].UsedJobs).IsEqualTo(0);
        await Assert.That(second[0].TrainedJobs).IsEqualTo(1);
        await Assert.That(second[0].RequiredContent).IsEqualTo(0);
    }

    [Test]
    public async Task StampRolloverResetsBeforeIncrementWithoutLosingCurrentRoleEvidence()
    {
        var scratch = new RoleSkillEvidenceAccumulator();
        scratch.BeginRole();
        scratch.BeginSource("giver-a");
        scratch.AddUsedSkill("Crafting");

        var sourceStamp = typeof(RoleSkillEvidenceAccumulator).GetField(
            "sourceStamp", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!;
        sourceStamp.SetValue(scratch, long.MaxValue);

        scratch.BeginSource("giver-b");
        scratch.AddTrainedSkill("Intellectual");
        IReadOnlyList<RoleSkillEvidence> evidence = scratch.CompleteRole();

        await Assert.That((long)sourceStamp.GetValue(scratch)!).IsEqualTo(1L);
        await Assert.That(evidence.Single(e => e.SkillDefName == "Crafting").UsedJobs)
            .IsEqualTo(1);
        await Assert.That(evidence.Single(e => e.SkillDefName == "Intellectual").TrainedJobs)
            .IsEqualTo(1);

        var roleStamp = typeof(RoleSkillEvidenceAccumulator).GetField(
            "roleStamp", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!;
        roleStamp.SetValue(scratch, long.MaxValue);
        scratch.BeginRole();

        await Assert.That((long)roleStamp.GetValue(scratch)!).IsEqualTo(1L);
    }
}
