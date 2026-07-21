namespace WorkRoles.Core.Tests;

public class LanguageInvalidationArchitectureTests
{
    [Test]
    public async Task LanguageSelectionRequestsAndLateInjectionCompletes()
    {
        string source = Source("Patches", "Patch_Language.cs");
        string select = Method(source,
            "public static class Patch_LanguageDatabase_SelectLanguage");
        string inject = Method(source,
            "public static class Patch_LoadedLanguage_InjectIntoData_AfterImpliedDefs");

        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(LanguageDatabase), nameof(LanguageDatabase.SelectLanguage))]");
        await Assert.That(select).Contains("LanguageChangeCoordinator.Request();");
        await Assert.That(select).DoesNotContain("LanguageChangeCoordinator.Complete();");
        await Assert.That(select).DoesNotContain("UiVersion.Bump();");
        await Assert.That(select).DoesNotContain(".Clear();");

        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(LoadedLanguage), nameof(LoadedLanguage.InjectIntoData_AfterImpliedDefs))]");
        await Assert.That(inject).Contains("[HarmonyPriority(Priority.Last)]");
        await Assert.That(inject).Contains("public static void Postfix()");
        await Assert.That(inject).Contains("LanguageChangeCoordinator.Complete();");
        await Assert.That(source).DoesNotContain("LoadAllPlayData");
    }

    [Test]
    public async Task CoordinatorCompletesOwnersInOneOrderedMainThreadPath()
    {
        string source = Source("LanguageChangeCoordinator.cs");
        string request = Method(source, "internal static void Request()");
        string complete = Method(source, "internal static void Complete()");

        await Assert.That(source).Contains(
            "private static readonly DeferredInvalidationRevision deferredRevision");
        await Assert.That(source).Contains("internal static int Revision => deferredRevision.Current;");
        await Assert.That(request).Contains("deferredRevision.Request();");
        await Assert.That(request).DoesNotContain("UiVersion.Bump();");

        string[] ordered =
        {
            "if (!deferredRevision.Complete()) return;",
            "UiVersion.Bump();",
            "UI.GroupSources.InvalidateLanguageCaches();",
            "UI.WorkJobLabels.InvalidateLanguageCaches();",
            "UI.ColonistsTabView.InvalidateSharedLanguageCaches();",
            "UI.RolesTabView.InvalidateSharedLanguageCaches();",
            "JobSkillProfiles.InvalidateLanguageCaches();",
            "ColonyScope.InvalidateLanguageCaches();",
            "PawnSignalSnapshotCache.Clear();",
            "Patch_ActiveTip_TipRect.Clear();",
        };
        int prior = -1;
        foreach (string call in ordered)
        {
            int current = complete.IndexOf(call, StringComparison.Ordinal);
            await Assert.That(current).IsGreaterThan(prior)
                .Because($"{call} must run after the preceding completion step");
            prior = current;
        }

        foreach (string forbidden in new[]
                 {
                     ".Translate(", "DefDatabase<", "WorkRolesMod.Settings",
                     ".Write();", "RoleCommands.", "LoadAllPlayData",
                 })
            await Assert.That(complete).DoesNotContain(forbidden);
    }

    [Test]
    public async Task EverySharedLanguageOwnerHasAnExplicitInvalidator()
    {
        var owners = new[]
        {
            (file: new[] { "UI", "GroupSources.cs" },
                signature: "internal static void InvalidateLanguageCaches()",
                required: new[] { "all = null;" }),
            (file: new[] { "UI", "ColonistsTabView.cs" },
                signature: "internal static void InvalidateSharedLanguageCaches()",
                required: new[] { "skillHeaderLabels.Clear();" }),
            (file: new[] { "UI", "RolesTabView.cs" },
                signature: "internal static void InvalidateSharedLanguageCaches()",
                required: new[] { "RolesListState.InvalidateSectionsSnapshot();" }),
            (file: new[] { "UI", "WorkJobLabels.cs" },
                signature: "internal static void InvalidateLanguageCaches()",
                required: new[] { "giverDisplayCache = null" }),
            (file: new[] { "JobSkillProfiles.cs" },
                signature: "internal static void InvalidateLanguageCaches()",
                required: new[] { "byGiver = null;", "byType = null;" }),
            (file: new[] { "ColonyScope.cs" },
                signature: "internal static void InvalidateLanguageCaches()",
                required: new[]
                {
                    "locationsCache = null;", "locationsStamp = -1;",
                    "locationsMapCount = -1;",
                }),
        };

        foreach (var owner in owners)
        {
            string invalidator = Method(Source(owner.file), owner.signature);
            foreach (string required in owner.required)
                await Assert.That(invalidator).Contains(required)
                    .Because($"{string.Join("/", owner.file)} owns language-derived state");
        }
    }

    [Test]
    public async Task WindowObservesRevisionAtOpenAndAtTheStartOfEveryDraw()
    {
        string source = Source("UI", "MainTabWindow_WorkRoles.cs");
        string constructor = Method(source, "public MainTabWindow_WorkRoles()");
        string observe = Method(source, "private void ObserveLanguageRevision()");
        string preOpen = Method(source, "public override void PreOpen()");
        string draw = Method(source, "public override void DoWindowContents(");

        await Assert.That(constructor)
            .Contains("observedLanguageRevision = LanguageChangeCoordinator.Revision;");
        await Assert.That(observe).Contains("int current = LanguageChangeCoordinator.Revision;");
        await Assert.That(observe).Contains("if (observedLanguageRevision == current) return;");
        await Assert.That(Occurrences(observe, "observedLanguageRevision == current")).IsEqualTo(1);
        await Assert.That(observe).Contains("observedLanguageRevision = current;");
        await Assert.That(observe).Contains("tabs = null;");
        await Assert.That(observe).Contains("colonistsTab.InvalidateLanguageCaches();");
        await Assert.That(observe).Contains("rolesTab.InvalidateLanguageCaches();");
        await Assert.That(observe).Contains("optionsTab.InvalidateLanguageCaches();");
        await Assert.That(observe).DoesNotContain("Reset();");

        await Assert.That(Occurrences(preOpen, "ObserveLanguageRevision();")).IsEqualTo(1);
        await Assert.That(preOpen.IndexOf("ObserveLanguageRevision();", StringComparison.Ordinal))
            .IsLessThan(preOpen.IndexOf("base.PreOpen();", StringComparison.Ordinal));
        await Assert.That(Occurrences(draw, "ObserveLanguageRevision();")).IsEqualTo(1);
        await Assert.That(draw.IndexOf("ObserveLanguageRevision();", StringComparison.Ordinal))
            .IsLessThan(draw.IndexOf("Patch_ActiveTip_TipRect.BeginGeneration(", StringComparison.Ordinal));
        await Assert.That(Occurrences(source, "LanguageChangeCoordinator.Revision")).IsEqualTo(2)
            .Because("language revision checks belong only to construction and the observer");
    }

    [Test]
    public async Task ViewInvalidatorsClearLanguageCachesWithoutDiscardingUiState()
    {
        var views = new[]
        {
            (file: "ColonistsTabView.cs",
                required: new[]
                {
                    "colonistHeaderCache = null;", "sizeStamp = ScopeCacheStamp.Invalid;",
                    "paletteStamp = -1;", "paletteLabels.Clear();",
                    "statsState.InvalidateLanguageCaches();",
                    "roleTipCache.Clear();",
                    "recommendationState.InvalidateLanguageCaches();",
                    "rosterState.InvalidateLanguageCaches();",
                },
                forbidden: new[]
                {
                    "Reset();", "selectedPawn =", "colonistFilter =", "roleFilterId =",
                    "tableScroll =", "paletteScroll =", "scope =",
                }),
            (file: "ColonistsRosterState.cs",
                required: new[]
                {
                    "scopeOptions = null;", "InvalidatePawnSnapshot();",
                    "InvalidateSections();",
                },
                forbidden: new[]
                {
                    "Reset();", "Search =", "RoleFilterId =", "scope =",
                    "skillColumns.Clear();",
                }),
            (file: "ColonistRecommendationState.cs",
                required: new[] { "ClearPreview();" },
                forbidden: new[] { "Reset();", "plans = null;" }),
            (file: "ColonistStatsState.cs",
                required: new[] { "InvalidatePresentations();" },
                forbidden: new[] { "Reset();", "PawnSignalSnapshotCache.Clear();" }),
            (file: "RolesTabView.cs",
                required: new[]
                {
                    "listState.InvalidateLanguageCaches();",
                    "editorState.InvalidateLanguageCaches();",
                },
                forbidden: new[]
                {
                    "Reset();", "selectedRoleId =", "Filter =", "RoleSearch =",
                    "listScroll =", "entriesScroll =", "treeScroll =",
                    "rulesRevealed.Clear();", "tuningExpanded.Clear();",
                }),
            (file: "RoleEditorState.cs",
                required: new[]
                {
                    "tipsStamp = -1;", "blockerTip = null;", "holdersTip = null;",
                    "skillsUsed = null;", "ClearEntryLabels();",
                    "uncoveredGivers = null;", "treeNodes = null;",
                },
                forbidden: new[]
                {
                    "Reset();", "Filter =", "expandedWorkTypes.Clear();",
                }),
            (file: "RolesListState.cs",
                required: new[]
                {
                    "displayRows = null;", "snapshot = null;", "displayStamp = -1;",
                },
                forbidden: new[]
                {
                    "Reset();", "RoleSearch =", "JobFilterDefName =",
                }),
            (file: "OptionsTabState.cs",
                required: new[]
                {
                    "orderStamp = -1;", "orderLayout.Clear();", "pathStamp = -1;",
                    "pathChips.Clear();", "tipsStamp = -1;",
                    "NumericTip = null;", "AnchorTip = null;",
                },
                forbidden: new[]
                {
                    "Reset();", "SelectedPathId =", "tabScroll =",
                    "anchorRevealed.Clear();", "ClearBandDrag();",
                }),
        };

        foreach (var view in views)
        {
            string invalidator = Method(Source("UI", view.file),
                "internal void InvalidateLanguageCaches()");
            foreach (string required in view.required)
                await Assert.That(invalidator).Contains(required)
                    .Because($"{view.file} caches language-derived presentation");
            foreach (string forbidden in view.forbidden)
                await Assert.That(invalidator).DoesNotContain(forbidden)
                    .Because($"{view.file} must preserve selection, filters, scroll, and disclosure");
        }
    }

    [Test]
    public async Task EveryStructuredTipProducerIsCoveredBeforeRegistryClear()
    {
        string[] producerFiles = Directory.EnumerateFiles(
                Path.Combine(RepoRoot(), "src", "WorkRoles"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(
                "new StructuredTip(", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        await Assert.That(producerFiles).IsEquivalentTo(new[]
        {
            "ColonistsTabView.cs", "JobSkillProfiles.cs", "OptionsTabState.cs",
            "RecommendationPresentation.cs", "RoleEditorState.cs", "SkillSignalPresentation.cs",
        });

        string colonists = Method(Source("UI", "ColonistsTabView.cs"),
            "internal void InvalidateLanguageCaches()");
        await Assert.That(colonists).Contains("statsState.InvalidateLanguageCaches();");
        await Assert.That(colonists).Contains("roleTipCache.Clear();");
        await Assert.That(colonists)
            .Contains("recommendationState.InvalidateLanguageCaches();");
        string recommendationState = Source("UI", "ColonistRecommendationState.cs");
        await Assert.That(Method(recommendationState, "private void ClearPreview()"))
            .Contains("preview = null;");
        string statsState = Source("UI", "ColonistStatsState.cs");
        await Assert.That(Method(statsState, "private void InvalidatePresentations()"))
            .Contains("presentations.Clear();");

        string roles = Method(Source("UI", "RoleEditorState.cs"),
            "internal void InvalidateLanguageCaches()");
        await Assert.That(roles).Contains("blockerTip = null;");
        await Assert.That(roles).Contains("holdersTip = null;");

        string options = Method(Source("UI", "OptionsTabState.cs"),
            "internal void InvalidateLanguageCaches()");
        await Assert.That(options).Contains("NumericTip = null;");
        await Assert.That(options).Contains("AnchorTip = null;");

        string complete = Method(Source("LanguageChangeCoordinator.cs"),
            "internal static void Complete()");
        await Assert.That(complete.LastIndexOf("Patch_ActiveTip_TipRect.Clear();",
                StringComparison.Ordinal))
            .IsGreaterThan(complete.LastIndexOf("PawnSignalSnapshotCache.Clear();",
                StringComparison.Ordinal));
    }

    private static int Occurrences(string source, string value)
    {
        int count = 0;
        for (int index = 0; (index = source.IndexOf(
                 value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            count++;
        return count;
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
