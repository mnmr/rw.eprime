namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class ResumeConditionTests
{
    private static CandidateFacts Facts(int skill = 10, bool inspired = false,
        int roleOffset = 0, bool workEnabled = true, bool meetsRecipeSkill = true, int id = 1)
        => new(id, skill, inspired, roleOffset, workEnabled, meetsRecipeSkill);

    [Test]
    public async Task SkillThresholdIsInclusive()
    {
        var c = new ResumeCondition(15, false, false);
        await Assert.That(c.IsSatisfiedBy(Facts(skill: 15))).IsTrue();
        await Assert.That(c.IsSatisfiedBy(Facts(skill: 14))).IsFalse();
    }

    [Test]
    public async Task InspirationRequirement()
    {
        var c = new ResumeCondition(0, requireInspired: true, requireSpecialist: false);
        await Assert.That(c.IsSatisfiedBy(Facts(inspired: true))).IsTrue();
        await Assert.That(c.IsSatisfiedBy(Facts(inspired: false))).IsFalse();
    }

    [Test]
    public async Task SpecialistRequirementUsesRoleOffset()
    {
        var c = new ResumeCondition(0, false, requireSpecialist: true);
        await Assert.That(c.IsSatisfiedBy(Facts(roleOffset: 1))).IsTrue();
        await Assert.That(c.IsSatisfiedBy(Facts(roleOffset: 0))).IsFalse();
    }

    [Test]
    public async Task ConditionIgnoresCapabilityFacts()
    {
        // Capability (work enabled, recipe skill requirements) is candidate
        // filtering, not the condition itself (spec §6).
        var c = new ResumeCondition(0, false, false);
        await Assert.That(c.IsSatisfiedBy(Facts(workEnabled: false, meetsRecipeSkill: false))).IsTrue();
    }

    [Test]
    public async Task MinSkillIsClampedToValidRange()
    {
        await Assert.That(new ResumeCondition(25, false, false).MinSkill).IsEqualTo(20);
        await Assert.That(new ResumeCondition(-5, false, false).MinSkill).IsEqualTo(0);
    }
}
