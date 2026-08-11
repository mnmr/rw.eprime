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

        // Equal importance AND training evidence: ordinal defName decides.
        List<RoleSkillView> byName = RoleSkillProfile.Build(new[]
        {
            new RoleSkillEvidence("Melee", usedJobs: 2, trainedJobs: 0, requiredContent: 0),
            new RoleSkillEvidence("Cooking", usedJobs: 2, trainedJobs: 0, requiredContent: 0),
        });

        await Assert.That(byName.Single(s => s.Primary).SkillDefName).IsEqualTo("Cooking");
    }

    /// A composite's displayed skills union its members' own profiles. Running
    /// the per-role share filter over the merged coverage instead would keep
    /// only the dominant member's skill, which is exactly the bug this pins.
    [Test]
    public async Task MergeKeepsEverySpecialistSkillTheShareFilterWouldCollapse()
    {
        // XP-training jobs weigh 4x in production; the weights and per-role
        // totals here mirror that.
        var doctorEvidence = new[]
        {
            new RoleSkillEvidence("Medicine", usedJobs: 6, trainedJobs: 6,
                requiredContent: 0, weightedJobs: 24),
        };
        var growerEvidence = new[]
        {
            new RoleSkillEvidence("Plants", usedJobs: 4, trainedJobs: 4,
                requiredContent: 0, weightedJobs: 16),
        };

        // Filtered over the union, Plants falls under the half-share bar.
        List<RoleSkillView> collapsed = RoleSkillProfile.Build(
            doctorEvidence.Concat(growerEvidence), roleWeight: 24 + 16);
        await Assert.That(collapsed.Select(s => s.SkillDefName))
            .IsEquivalentTo(new[] { "Medicine" });

        List<RoleSkillView> merged = RoleSkillProfile.Merge(new[]
        {
            RoleSkillProfile.Build(doctorEvidence, roleWeight: 24),
            RoleSkillProfile.Build(growerEvidence, roleWeight: 16),
        });
        await Assert.That(string.Join(",", merged.Select(s => s.SkillDefName)))
            .IsEqualTo("Medicine,Plants");
        await Assert.That(merged.Single(s => s.Primary).SkillDefName)
            .IsEqualTo("Medicine");
    }
}
