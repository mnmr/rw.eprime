using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Stable input-fact contracts used by the production planner.
public class RecsContextTests
{
    [Test]
    public async Task BestSignalConsumesThePrecomputedAggregateBucket()
    {
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 6;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Strong;
        var context = new EngineContext(RecsTestBed.Colony([RecsTestBed.Role(1, "Cooking")], pawn));

        SignalBucket bucket = context.BestSignal(0, context.RoleOf(1), out string skill, out SignalSource source);

        await Assert.That(bucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(skill).IsEqualTo("Cooking");
        await Assert.That(source).IsEqualTo(SignalSource.Aggregated);
    }

    [Test]
    public async Task BestSignalIsNeutralForRolesWithoutMappedSkills()
    {
        PawnView pawn = RecsTestBed.Pawn();
        var context = new EngineContext(RecsTestBed.Colony([RecsTestBed.Unskilled(1, "Hauling")], pawn));

        SignalBucket bucket = context.BestSignal(0, context.RoleOf(1), out string skill, out _);

        await Assert.That(bucket).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(skill == null).IsTrue();
    }

    [Test]
    [Arguments("äSkill,zSkill")]
    [Arguments("zSkill,äSkill")]
    public async Task BestSignalFallbackUsesOrdinalSkillTieBreakRegardlessOfInputOrder(string orderedSkills)
    {
        System.Globalization.CultureInfo priorCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");

            string result = FallbackSkill(orderedSkills.Split(','));

            await Assert.That(result).IsEqualTo("zSkill");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = priorCulture;
        }
    }

    [Test]
    public async Task BasePositionsUseTemplateSlotsAndNaturalFallback()
    {
        RoleView first = RecsTestBed.Role(1, "Cooking");
        first.NaturalPriority = 100f;
        RoleView second = RecsTestBed.Role(2, "Crafting");
        second.NaturalPriority = 50f;
        RoleView unlisted = RecsTestBed.Role(3, "Doctor");
        unlisted.NaturalPriority = 60f;

        Dictionary<int, long> positions = Ordering.BasePositions([first, second, unlisted], [first.Id, second.Id]);

        await Assert.That(positions[first.Id]).IsEqualTo(0L);
        await Assert.That(positions[second.Id]).IsEqualTo(Ordering.Slot);
        await Assert.That(positions[unlisted.Id] > 0L && positions[unlisted.Id] < Ordering.Slot).IsTrue();
    }

    [Test]
    public async Task BasePositionsPublishOneReadOnlyViewPerRun()
    {
        RoleView role = RecsTestBed.Role(1, "Cooking");
        var context = new EngineContext(RecsTestBed.Colony([role], RecsTestBed.Pawn()));

        IReadOnlyDictionary<int, long> first = context.BasePositions();
        IReadOnlyDictionary<int, long> second = context.BasePositions();

        await Assert.That(ReferenceEquals(first, second)).IsTrue();
        await Assert.That(() => ((IDictionary<int, long>)first).Add(99, 99L)).Throws<NotSupportedException>();
    }

    private static string FallbackSkill(IReadOnlyList<string> sourceOrder)
    {
        RoleView role = RecsTestBed.Unskilled(1, "Hauling");
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["äSkill"] = 8;
        pawn.SkillLevels["zSkill"] = 8;
        pawn.SignalBuckets["äSkill"] = SignalBucket.Strong;
        pawn.SignalBuckets["zSkill"] = SignalBucket.Strong;
        ColonyView colony = RecsTestBed.Colony([role], pawn);
        colony.WorkTypeSkills = new Dictionary<string, IReadOnlyList<string>> { ["Hauling"] = sourceOrder };
        var context = new EngineContext(colony);
        context.BestSignal(0, role, out string skill, out _);
        return skill;
    }
}
