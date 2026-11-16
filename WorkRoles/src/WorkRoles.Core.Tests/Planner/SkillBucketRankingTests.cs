using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests.Planner;

/// Top() feeds the colonist-row caption: the pawn's best skills at Strong or
/// better, ordered by the engine's champion skill score (level and verdict
/// weighed together), so a high-level Strong outranks a level-1 Great.
public class SkillBucketRankingTests
{
    private static SkillBucketSnapshot Snapshot(params (string skill, SignalBucket bucket)[] buckets) =>
        new SkillBucketSnapshot(buckets.Select(entry => new SkillBucketSignal(entry.skill, entry.bucket, [])).ToList());

    [Test]
    public async Task TopFiltersAndSortsByChampionScore()
    {
        SkillBucketSnapshot snapshot = Snapshot(
            ("Medicine", SignalBucket.Exceptional),
            ("Animals", SignalBucket.Great),
            ("Intellectual", SignalBucket.Strong),
            ("Plants", SignalBucket.Strong),
            ("Construction", SignalBucket.Strong),
            ("Cooking", SignalBucket.Neutral),
            ("Crafting", SignalBucket.Awful)
        );
        SkillBucketCandidate[] candidates =
        [
            new SkillBucketCandidate("Cooking", 18),
            new SkillBucketCandidate("Animals", 1),
            new SkillBucketCandidate("Intellectual", 15),
            new SkillBucketCandidate("Medicine", 16),
            new SkillBucketCandidate("Plants", 9),
            new SkillBucketCandidate("Construction", 9),
            new SkillBucketCandidate("Crafting", 20),
        ];

        List<SkillBucketChoice> top = SkillBucketRanking.Top(snapshot, candidates, SignalBucket.Strong, max: 4, RecommendationsTuningOptions.Default);

        // Default scores (ceil(level/2) x multiplier): Medicine 80,
        // Intellectual 48, Plants/Construction 30 (candidate order breaks the
        // tie), Animals 8. Neutral Cooking and Awful Crafting are filtered
        // despite their levels; the cap drops level-1 Great Animals.
        await Assert.That(string.Join(",", top.Select(choice => choice.SkillDefName))).IsEqualTo("Medicine,Intellectual,Plants,Construction");
        await Assert.That(top[1].Bucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(top[1].SkillLevel).IsEqualTo(15);
    }

    [Test]
    public async Task TopReturnsEmptyWhenNothingReachesTheMinimum()
    {
        SkillBucketSnapshot snapshot = Snapshot(("Cooking", SignalBucket.Neutral));
        SkillBucketCandidate[] candidates =
        [
            new SkillBucketCandidate("Cooking", 12),
            new SkillBucketCandidate("Mining", 20), // no bucket entry: Neutral
        ];

        List<SkillBucketChoice> top = SkillBucketRanking.Top(snapshot, candidates, SignalBucket.Strong, max: 4, RecommendationsTuningOptions.Default);

        await Assert.That(top).IsEmpty();
    }

    [Test]
    public async Task CaptionFallsBackToTheSingleHighestSkillWhenNothingIsStrong()
    {
        // A colonist whose only passions are combat skills (excluded by the
        // caller): Plants 12 without a passion is still the skill to show.
        SkillBucketSnapshot snapshot = Snapshot(("Cooking", SignalBucket.Poor));
        SkillBucketCandidate[] candidates =
        [
            new SkillBucketCandidate("Mining", 2),
            new SkillBucketCandidate("Cooking", 1),
            new SkillBucketCandidate("Plants", 12),
            new SkillBucketCandidate("Social", 4),
        ];

        List<SkillBucketChoice> caption = SkillBucketRanking.Caption(snapshot, candidates, max: 4, RecommendationsTuningOptions.Default);

        await Assert.That(caption.Count).IsEqualTo(1);
        await Assert.That(caption[0].SkillDefName).IsEqualTo("Plants");
        await Assert.That(caption[0].SkillLevel).IsEqualTo(12);
        await Assert.That(caption[0].Bucket).IsEqualTo(SignalBucket.Neutral);
    }

    [Test]
    public async Task CaptionKeepsOnlyStrongSkillsWhenAnyExistAndShowsNothingAtLevelZero()
    {
        SkillBucketSnapshot snapshot = Snapshot(("Cooking", SignalBucket.Strong));
        SkillBucketCandidate[] candidates =
        [
            new SkillBucketCandidate("Plants", 18),
            new SkillBucketCandidate("Cooking", 6),
        ];

        List<SkillBucketChoice> caption = SkillBucketRanking.Caption(snapshot, candidates, max: 4, RecommendationsTuningOptions.Default);
        List<SkillBucketChoice> unskilled = SkillBucketRanking.Caption(Snapshot(),
            [new SkillBucketCandidate("Plants", 0)], max: 4, RecommendationsTuningOptions.Default);

        await Assert.That(string.Join(",", caption.Select(choice => choice.SkillDefName))).IsEqualTo("Cooking");
        await Assert.That(unskilled).IsEmpty();
    }
}
