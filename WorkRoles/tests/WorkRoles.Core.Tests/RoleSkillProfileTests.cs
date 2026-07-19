using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RoleSkillProfileTests
{
    [Test]
    public async Task BuilderAggregatesJobEvidenceAndSelectsOneStablePrimarySkill()
    {
        List<RoleSkillView> profile = RoleSkillProfile.Build(new[]
        {
            new RoleSkillEvidence("Crafting", usedJobs: 2, trainedJobs: 1, requiredContent: 0),
            new RoleSkillEvidence("Intellectual", usedJobs: 0, trainedJobs: 1, requiredContent: 2),
            new RoleSkillEvidence("Crafting", usedJobs: 1, trainedJobs: 1, requiredContent: 1),
        });

        await Assert.That(profile.Select(s => s.SkillDefName))
            .IsEquivalentTo(new[] { "Crafting", "Intellectual" });
        RoleSkillView crafting = profile.Single(s => s.SkillDefName == "Crafting");
        RoleSkillView intellectual = profile.Single(s => s.SkillDefName == "Intellectual");
        await Assert.That(crafting.Primary).IsTrue();
        await Assert.That(intellectual.Primary).IsFalse();
        await Assert.That(crafting.Importance > intellectual.Importance).IsTrue();
        await Assert.That(profile.All(s => s.Required)).IsTrue();
    }

    [Test]
    public async Task PrimaryTieBreakPrefersMoreTrainingEvidenceThenDefName()
    {
        List<RoleSkillView> profile = RoleSkillProfile.Build(new[]
        {
            new RoleSkillEvidence("Social", usedJobs: 2, trainedJobs: 0, requiredContent: 0),
            new RoleSkillEvidence("Medicine", usedJobs: 0, trainedJobs: 1, requiredContent: 0),
        });

        await Assert.That(profile.Single(s => s.Primary).SkillDefName).IsEqualTo("Medicine");
    }
}
