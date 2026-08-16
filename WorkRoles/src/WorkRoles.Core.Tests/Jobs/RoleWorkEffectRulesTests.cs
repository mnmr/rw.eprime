using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Jobs;

public class RoleWorkEffectRulesTests
{
    [Test]
    public async Task SurgerySkillCanAffectBothSpeedAndSuccess()
    {
        RoleWorkEffect effects = RoleWorkEffectRules.ForRecipe(
            affectsSpeed: true,
            affectsYield: false,
            affectsQuality: false,
            affectsSuccess: true);

        await Assert.That(effects).IsEqualTo(
            RoleWorkEffect.Speed | RoleWorkEffect.Success);
    }
}
