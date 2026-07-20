namespace WorkRoles.Core.Tests;

public class MutableSignalCacheArchitectureTests
{
    [Test]
    public async Task GameSignatureCoversEverySnapshotInputCategory()
    {
        string collector = Source("Signals", "PawnSignalCollector.cs");
        string signature = Method(collector, "internal static MutableSignalSignature Signature(");

        string[] required =
        {
            "skill.TotallyDisabled",
            "skill.passion",
            "AppendPassionSignature",
            "AppendExpertiseSignature",
            "trait.Suppressed",
            "trait.Degree",
            "gene.Active",
            "hediff.Severity",
            "hediff.CurStageIndex",
            "AddProviderCondition",
        };
        foreach (string value in required)
            await Assert.That(signature).Contains(value)
                .Because($"mutable signal signatures must include {value}");
    }

    [Test]
    public async Task HotVseSignaturesUseCompiledUnboxedReads()
    {
        string reflection = Source("Signals", "VseSignalReflection.cs");
        string passion = Method(reflection,
            "internal static void AppendPassionSignature(");
        string expertise = Method(reflection,
            "internal static void AppendExpertiseSignature(");
        string initializeExpertise = Method(reflection,
            "private static void EnsureExpertise(");

        await Assert.That(expertise).Contains("IList");
        await Assert.That(expertise).Contains("expertiseTrackerRead(pawn)");
        await Assert.That(expertise).Contains("recordXpSinceLastLevelRead(record)");
        await Assert.That(expertise).Contains("recordXpRequiredForLevelUpRead(record)");
        await Assert.That(expertise).DoesNotContain("Expertises(pawn)");
        await Assert.That(initializeExpertise)
            .Contains("VseSignalApi.ExpertiseXpSinceLastLevelMember, typeof(float), false");
        await Assert.That(initializeExpertise)
            .Contains("VseSignalApi.ExpertiseXpRequiredForLevelUpMember, typeof(float), false");
        await Assert.That(passion).Contains("passionMutableState");
        await Assert.That(passion).Contains("metadata.AuthorTier");

        string[] forbidden =
        {
            ".GetValue(",
            ".Invoke(",
            "DynamicInvoke",
            "new object[",
            "Convert.To",
        };
        foreach (string value in forbidden)
        {
            await Assert.That(passion).DoesNotContain(value);
            await Assert.That(expertise).DoesNotContain(value);
        }

        await Assert.That(reflection).Contains("Expression.Lambda");
        await Assert.That(reflection).DoesNotContain("[ThreadStatic]");
        await Assert.That(reflection).DoesNotContain("AuthorTierHash");
    }

    [Test]
    public async Task VseGlobalSettingsUseTheBoundedObservationEpochSharedByPawns()
    {
        string reflection = Source("Signals", "VseSignalReflection.cs");
        string cache = Source("Signals", "PawnSignalSnapshotCache.cs");
        string get = Method(cache, "internal static PawnSignalSnapshot Get(");
        string observe = Method(reflection, "internal static void ObserveGlobalInputs(");
        string expertise = Method(reflection,
            "internal static void AppendExpertiseSignature(");

        await Assert.That(cache).Contains("MutableSignalObservationEpoch.FromClocks(");
        await Assert.That(cache).Contains("(int)Time.realtimeSinceStartup");
        await Assert.That(cache).Contains("tickManager?.Paused ?? true");
        await Assert.That(get).Contains("long observationEpoch = ObservationEpoch;");
        await Assert.That(get).Contains("ObserveGlobalInputs(observationEpoch)");
        await Assert.That(get).Contains("snapshots.Get(pawn, observationEpoch)");
        await Assert.That(observe).Contains("lastGlobalObservationEpoch == epoch");
        await Assert.That(observe).Contains("settingsRead()");
        await Assert.That(expertise).Contains("observedStatMultiplier");
        await Assert.That(expertise).Contains("observedCrossSkillEffects");
        await Assert.That(expertise).DoesNotContain("settingsRead()");
        await Assert.That(cache).DoesNotContain("Time.frameCount");
        await Assert.That(reflection).DoesNotContain("Time.frameCount");
    }

    [Test]
    public async Task KnownMutationAndLifecycleHooksInvalidateSignalSnapshots()
    {
        string workSettings = Source("Patches", "Patch_PawnWorkSettings.cs");
        string language = Source("LanguageChangeCoordinator.cs");

        await Assert.That(Method(workSettings,
                "public static class Patch_Pawn_NotifyDisabledWorkTypesChanged"))
            .Contains("PawnSignalSnapshotCache.Invalidate(__instance)");
        await Assert.That(Method(workSettings, "public static class Patch_Pawn_Destroy"))
            .Contains("PawnSignalSnapshotCache.Invalidate(__instance)");
        string teardown = Method(workSettings,
            "public static class Patch_MemoryUtility_ClearAllMapsAndWorld");
        await Assert.That(teardown)
            .Contains("DefinitionReloadCoordinator.ReleaseForTeardown()");
        await Assert.That(teardown).DoesNotContain("PawnSignalSnapshotCache.Clear()");
        await Assert.That(Method(language,
                "internal static void Complete()"))
            .Contains("PawnSignalSnapshotCache.Clear()");
    }

    [Test]
    public async Task ColonistsViewInvalidatesEverySignalDependentCacheTogether()
    {
        string source = Source("UI", "ColonistsTabView.cs");
        string invalidation = Method(source, "private void ObserveSignalRevision()");

        string[] dependents =
        {
            "planCache = null",
            "statsStamp = -1",
            "skillPresentations.Clear()",
            "roleTipCache.Clear()",
            "previewChips = null",
            "sizeStamp = ScopeCacheStamp.Invalid",
        };
        foreach (string value in dependents)
            await Assert.That(invalidation).Contains(value);

        string[] consumers =
        {
            "private List<PawnFixPlan> GetPlan()",
            "private void EnsurePreview(",
            "private SkillPresentation PresentationFor(",
            "private void EnsureStats(",
            "private void EnsureSizes()",
            "internal string RoleTipText(",
        };
        foreach (string consumer in consumers)
            await Assert.That(Method(source, consumer)).Contains("ObserveSignal")
                .Because($"{consumer} must observe signal revision before reusing cached data");

        await Assert.That(Method(source, "private List<PawnFixPlan> BuildColonyFixPlan("))
            .Contains("SignalSnapshotFor")
            .Because("a snapshot rebuilt during plan construction must publish its revision");
    }

    [Test]
    public async Task FullCohortObservationIsCoalescedWithoutSinglePawnPoisoning()
    {
        string source = Source("UI", "ColonistsTabView.cs");
        string listed = Method(source,
            "private void ObserveSignalChanges(IEnumerable<Pawn> pawns)");
        string map = Method(source, "private void ObserveSignalChanges(Map map)");
        string single = Method(source,
            "private PawnSignalSnapshot ObserveSignalChanges(Pawn pawn)");

        await Assert.That(source).Contains("ObservationEpochGate<ScopeCacheStamp>");
        await Assert.That(source).Contains("ObservationEpochGate<int>");
        await Assert.That(listed).Contains(
            "long observationEpoch = PawnSignalSnapshotCache.ObservationEpoch;");
        await Assert.That(map).Contains(
            "long observationEpoch = PawnSignalSnapshotCache.ObservationEpoch;");
        await Assert.That(listed).Contains("listedSignalObservations.Enter(");
        await Assert.That(map).Contains("mapSignalObservations.Enter(");
        await Assert.That(listed).DoesNotContain("Time.frameCount");
        await Assert.That(map).DoesNotContain("Time.frameCount");
        await Assert.That(single).DoesNotContain("SignalObservations.Enter(");
    }

    [Test]
    public async Task DefinitionInvalidationDropsEveryVseOwnerBeforeSnapshots()
    {
        string reflection = Source("Signals", "VseSignalReflection.cs");
        string invalidate = Method(reflection,
            "internal static void InvalidateDefinitions()");
        string coordinator = Source("DefinitionReloadCoordinator.cs");
        string release = Method(coordinator, "private static void ReleaseOwners()");

        string[] requiredClears =
        {
            "passionDefinitions.Invalidate();",
            "passionMutableState = null;",
            "passionIsBadRead = null;",
            "passionLearnRateRead = null;",
            "passionForgetRateRead = null;",
            "passionOtherLearnRateRead = null;",
            "expertiseInitialized = false;",
            "expertiseDisabled = false;",
            "expertiseTrackerRead = null;",
            "allExpertiseRead = null;",
            "recordDefRead = null;",
            "recordLevelRead = null;",
            "recordXpSinceLastLevelRead = null;",
            "recordXpRequiredForLevelUpRead = null;",
            "expertiseSkillRead = null;",
            "settingsRead = null;",
            "statMultiplierRead = null;",
            "crossSkillEffectsRead = null;",
            "fullDescription = null;",
            "lastGlobalObservationEpoch = long.MinValue;",
        };
        foreach (string clear in requiredClears)
            await Assert.That(invalidate).Contains(clear);

        int vse = release.IndexOf("VseSignalReflection.InvalidateDefinitions();",
            StringComparison.Ordinal);
        int snapshots = release.IndexOf("PawnSignalSnapshotCache.Clear();",
            StringComparison.Ordinal);
        await Assert.That(vse).IsGreaterThan(-1);
        await Assert.That(snapshots).IsGreaterThan(vse);
    }

    private static string Method(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return "";
        int open = source.IndexOf('{', start);
        if (open < 0) return "";
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(start, i - start + 1);
        }
        return "";
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "WorkRoles" }
            .Concat(parts).ToArray()));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
