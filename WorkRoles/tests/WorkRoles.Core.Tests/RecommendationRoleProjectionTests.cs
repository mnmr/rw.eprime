using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecommendationRoleProjectionTests
{
    [Test]
    public async Task ProjectionCopiesStableWorkTypePriorityAndLiteralMetadata()
    {
        var workTypes = new List<RecommendationWorkTypeEvidence>
        {
            new("Crafting", 7),
            new("Hunting", 3),
            new("Crafting", 99),
        };
        var literals = new List<string> { "MissingModType", "Hunting", "Hunting" };

        var projection = new RecommendationRoleProjection(
            workTypes, literals, Array.Empty<RoleSkillEvidence>());
        workTypes.Clear();
        literals.Clear();

        await Assert.That(string.Join(",", projection.WorkTypes))
            .IsEqualTo("Crafting,Hunting");
        await Assert.That(projection.NaturalPriorities["Crafting"]).IsEqualTo(7);
        await Assert.That(projection.Hunting).IsTrue();
        await Assert.That(projection.MaxNaturalPriority).IsEqualTo(7);
        await Assert.That(projection.HasLiteralWorkType("MissingModType")).IsTrue();
        await Assert.That(projection.HasLiteralWorkType("Crafting")).IsFalse();
        await Assert.That(() => ((IList<string>)projection.WorkTypes).Add("Mining"))
            .Throws<NotSupportedException>();
        await Assert.That(() => ((IDictionary<string, int>)projection.NaturalPriorities)
                .Add("Mining", 2))
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task ProjectionOwnsAggregatedSkillEvidenceAndReturnsFreshMutableViews()
    {
        var evidence = new List<RoleSkillEvidence>
        {
            new("Crafting", usedJobs: 2, trainedJobs: 1, requiredContent: 0),
            new("Intellectual", usedJobs: 0, trainedJobs: 1, requiredContent: 2),
            new("Crafting", usedJobs: 1, trainedJobs: 1, requiredContent: 1),
        };

        var projection = new RecommendationRoleProjection(
            Array.Empty<RecommendationWorkTypeEvidence>(),
            Array.Empty<string>(), evidence);
        evidence.Clear();

        await Assert.That(projection.PrimarySkill).IsEqualTo("Crafting");
        await Assert.That(projection.HasSkillEvidence).IsTrue();
        await Assert.That(projection.SkillEvidence.Count).IsEqualTo(2);
        await Assert.That(projection.SkillEvidence.Single(skill => skill.Primary)
            .SkillDefName).IsEqualTo("Crafting");

        List<RoleSkillView> first = projection.CopySkillViews();
        first[0].SkillDefName = "mutated";
        first.Clear();
        List<RoleSkillView> second = projection.CopySkillViews();

        await Assert.That(second.Count).IsEqualTo(2);
        await Assert.That(second.Single(skill => skill.Primary).SkillDefName)
            .IsEqualTo("Crafting");
    }
}
