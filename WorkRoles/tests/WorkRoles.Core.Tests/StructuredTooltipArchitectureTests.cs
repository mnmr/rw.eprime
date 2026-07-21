namespace WorkRoles.Core.Tests;

public class StructuredTooltipArchitectureTests
{
    [Test]
    public async Task ActiveTipBridgeUsesPureRegistryAndFlushesAfterVanillaTooltipGui()
    {
        string models = Source("TipModels.cs");
        string patch = Source("Patches", "Patch_ActiveTip.cs");

        await Assert.That(models).Contains("internal sealed class StructuredTip");
        await Assert.That(models).Contains("internal string StableKey { get; }");
        await Assert.That(models).Contains("internal TipModel Model { get; }");
        await Assert.That(models).Contains("internal string PlainText { get; }");
        await Assert.That(models).Contains("PlainText = model.ToPlainText();");
        await Assert.That(models).Contains("Patch_ActiveTip_TipRect.Activate(this);");

        await Assert.That(patch)
            .Contains("OwnerGenerationRegistry<object, string, string, TipModel>");
        await Assert.That(patch).Contains("internal static void BeginGeneration(object owner)");
        await Assert.That(patch).Contains("internal static void EndGeneration(object owner)");
        await Assert.That(patch).Contains("internal static void ReleaseOwner(object owner)");
        await Assert.That(patch).Contains("internal static void Activate(StructuredTip tip)");
        await Assert.That(patch).DoesNotContain("Dictionary<string, TipModel>");
        await Assert.That(patch)
            .Contains("[HarmonyPatch(typeof(TooltipHandler), \"DoTooltipGUI\")]");

        string flushPatch = Method(patch,
            "public static class Patch_TooltipHandler_DoTooltipGUI");
        await Assert.That(flushPatch).Contains("[HarmonyPostfix]");
        await Assert.That(flushPatch)
            .Contains("Patch_ActiveTip_TipRect.FlushRetired();");
    }

    [Test]
    public async Task MainTabOwnsOnlyItsRepaintGenerationAndReleasesOnClose()
    {
        string source = Source("UI", "MainTabWindow_WorkRoles.cs");
        string draw = Method(source, "public override void DoWindowContents(");
        string close = Method(source, "public override void PostClose()");

        await Assert.That(source)
            .Contains("private readonly object structuredTipOwner = new object();");
        await Assert.That(draw)
            .Contains("Event.current.type == EventType.Repaint");
        await Assert.That(draw).Contains("BeginGeneration(structuredTipOwner);");
        await Assert.That(draw).Contains("try");
        await Assert.That(draw).Contains("finally");
        await Assert.That(draw).Contains("EndGeneration(structuredTipOwner);");
        await Assert.That(draw.IndexOf("BeginGeneration(", StringComparison.Ordinal))
            .IsLessThan(draw.IndexOf("try", StringComparison.Ordinal));
        await Assert.That(draw.IndexOf("finally", StringComparison.Ordinal))
            .IsLessThan(draw.IndexOf("EndGeneration(", StringComparison.Ordinal));
        await Assert.That(close).Contains("ReleaseOwner(structuredTipOwner);");
        await Assert.That(close).DoesNotContain("Patch_ActiveTip_TipRect.Clear();");
    }

    [Test]
    public async Task ClosingColonistsViewReleasesPawnAndStructuredTipSnapshots()
    {
        string source = Source("UI", "ColonistsTabView.cs");
        string release = Method(source, "internal void ReleaseSnapshots()");

        foreach (string required in new[]
                 {
                     "selectedPawn = null;",
                     "recommendationState.ReleaseSnapshots();",
                     "statsState.ReleaseSnapshots();",
                     "roleTipCache.Clear();", "roleTipStamp = ScopeCacheStamp.Invalid;",
                     "rosterState.ReleaseSnapshots();",
                     "chipLayouts.Clear();", "chipLayoutStamp = ScopeCacheStamp.Invalid;",
                     "rulesPassCache.Clear();", "rulesPassStamp = ScopeCacheStamp.Invalid;",
                 })
            await Assert.That(release).Contains(required);

        foreach (string reusableUiState in new[]
                 {
                     "paletteScroll =", "tableScroll =", "colonistFilter =", "roleFilterId =",
                     "scope =", "skillColumns.Clear",
                 })
            await Assert.That(release).DoesNotContain(reusableUiState);

        string rosterRelease = Method(Source("UI", "ColonistsRosterState.cs"),
            "internal void ReleaseSnapshots()");
        await Assert.That(rosterRelease).Contains("pawns = null;");
        await Assert.That(rosterRelease).Contains("pawnsStamp = ScopeCacheStamp.Invalid;");
        await Assert.That(rosterRelease).Contains("InvalidateSections();");
        string recommendationRelease = Method(
            Source("UI", "ColonistRecommendationState.cs"),
            "internal void ReleaseSnapshots()");
        await Assert.That(recommendationRelease).Contains("Reset();");
        string statsRelease = Method(Source("UI", "ColonistStatsState.cs"),
            "internal void ReleaseSnapshots()");
        await Assert.That(statsRelease).Contains("Reset();");
    }

    [Test]
    public async Task EachChangesPreviewOwnsASeparateVisibleRowGeneration()
    {
        string source = Source("UI", "Dialog_ChangesPreview.cs");
        string draw = Method(source, "public override void DoWindowContents(");
        string visible = Method(source, "private void DrawVisibleEntries(");
        string close = Method(source, "public override void PostClose()");

        await Assert.That(source)
            .Contains("private readonly object structuredTipOwner = new object();");
        await Assert.That(draw)
            .Contains("Event.current.type == EventType.Repaint");
        await Assert.That(draw).Contains("BeginGeneration(structuredTipOwner);");
        await Assert.That(draw).Contains("try");
        await Assert.That(draw).Contains("finally");
        await Assert.That(draw).Contains("EndGeneration(structuredTipOwner);");
        await Assert.That(close).Contains("ReleaseOwner(structuredTipOwner);");
        await Assert.That(draw).Contains("rowLayout.Calculate(");
        await Assert.That(visible)
            .Contains("for (int i = visibleRows.Start; i < visibleRows.EndExclusive; i++)");
        await Assert.That(visible).DoesNotContain("for (int i = 0; i < entries.Count; i++)");
    }

    [Test]
    public async Task LanguageAndSessionBoundariesStillClearAllOwners()
    {
        string language = Source("LanguageChangeCoordinator.cs");
        string session = Source("Patches", "Patch_PawnWorkSettings.cs");

        await Assert.That(language).Contains("Patch_ActiveTip_TipRect.Clear();");
        await Assert.That(session).Contains("Patch_ActiveTip_TipRect.Clear();");
    }

    [Test]
    public async Task HandlesCreatedBeforeGlobalClearCannotReactivateOldModels()
    {
        string models = Source("TipModels.cs");
        string patch = Source("Patches", "Patch_ActiveTip.cs");
        string clear = Method(patch, "internal static void Clear()");
        string activate = Method(patch, "internal static void Activate(StructuredTip tip)");

        await Assert.That(models).Contains("internal int RegistryEpoch { get; }");
        await Assert.That(models)
            .Contains("RegistryEpoch = Patches.Patch_ActiveTip_TipRect.CurrentRegistryEpoch;");
        await Assert.That(patch).Contains("internal static int CurrentRegistryEpoch");
        await Assert.That(clear).Contains("registryEpoch++;");
        await Assert.That(activate)
            .Contains("tip.RegistryEpoch != registryEpoch");
    }

    [Test]
    public async Task EndGenerationIsSafeAfterGlobalClearEndsTheActiveGeneration()
    {
        string patch = Source("Patches", "Patch_ActiveTip.cs");
        string end = Method(patch, "internal static void EndGeneration(object owner)");

        int inactiveGuard = end.IndexOf(
            "if (!generationActive) return;", StringComparison.Ordinal);
        int registryEnd = end.IndexOf("models.End(owner);", StringComparison.Ordinal);

        await Assert.That(inactiveGuard).IsGreaterThanOrEqualTo(0);
        await Assert.That(registryEnd).IsGreaterThan(inactiveGuard);
    }

    [Test]
    public async Task AllProducerFamiliesCacheHandlesWithStableUntranslatedKeys()
    {
        string jobs = Source("JobSkillProfiles.cs");
        string colonists = Source("UI", "ColonistsTabView.cs");
        string signals = Source("Signals", "SkillSignalPresentation.cs");
        string recommendations = Source("UI", "RecommendationPresentation.cs");
        string roles = Source("UI", "RolesTabView.cs");
        string roleEditor = Source("UI", "RoleEditorState.cs");
        string options = Source("UI", "OptionsTabState.cs");
        string producers = jobs + colonists + signals + recommendations
            + roleEditor + options;

        await Assert.That(producers)
            .DoesNotContain("Patch_ActiveTip_TipRect.Register(");
        await Assert.That(jobs).Contains("$\"job-giver:{defName}\"");
        await Assert.That(jobs).Contains("$\"work-type:{defName}\"");
        await Assert.That(colonists)
            .Contains("$\"role:{role.id}:{context}:{pawnId}\"");
        await Assert.That(signals)
            .Contains("$\"skill-signal:{pawn.thingIDNumber}:{skillDefName}\"");
        await Assert.That(recommendations)
            .Contains("$\"recommendation:{pawn.thingIDNumber}:{role.id}\"");
        await Assert.That(roleEditor).Contains("new StructuredTip(\"roles:blocker\"");
        await Assert.That(roleEditor).Contains("new StructuredTip(\"roles:holders\"");
        foreach (string key in new[]
                 {
                     "numeric", "vanilla-range", "recommendation-order", "training", "anchor",
                 })
            await Assert.That(options)
                .Contains($"new StructuredTip(\"options:{key}\"");

        foreach (string translatedPrefix in new[]
                 {
                     "job-giver:{\"WR_", "work-type:{\"WR_", "role:{\"WR_",
                     "skill-signal:{\"WR_", "recommendation:{\"WR_",
                 })
            await Assert.That(producers).DoesNotContain(translatedPrefix);
    }

    [Test]
    public async Task HandlesActivateAtTipRegionsWhilePublicStringSurfacesRemain()
    {
        string jobs = Source("JobSkillProfiles.cs");
        string colonists = Source("UI", "ColonistsTabView.cs");
        string signals = Source("Signals", "SkillSignalPresentation.cs");
        string recommendations = Source("UI", "RecommendationPresentation.cs");
        string roles = Source("UI", "RolesTabView.cs");
        string options = Source("UI", "OptionsTabView.cs");
        string preview = Source("UI", "Dialog_ChangesPreview.cs");

        await Assert.That(Occurrences(jobs, "public string TipCache;")).IsEqualTo(2);
        await Assert.That(jobs).Contains("public static string GiverTip(string defName)");
        await Assert.That(jobs).Contains("public static string WorkTypeTip(string defName)");
        await Assert.That(jobs).Contains("return profile.StructuredTipCache.Activate();");

        await Assert.That(colonists)
            .Contains("private readonly Dictionary<(int roleId, RoleTipContext context, Pawn pawn), StructuredTip>");
        await Assert.That(colonists).Contains("return tip.Activate();");
        await Assert.That(colonists).Contains("presentation.Tooltip.Activate()");
        await Assert.That(colonists).Contains("signalTip.Activate()");
        await Assert.That(colonists)
            .Contains("preview.Line?.StructuredTipAt(previewIndex)");
        await Assert.That(colonists)
            .DoesNotContain("previewStructuredTips[previewIndex]");
        string statsPanel = Method(colonists, "private void DrawStatsPanel(");
        int previewLoop = statsPanel.IndexOf("for (int previewIndex", StringComparison.Ordinal);
        string previewLoopSource = statsPanel.Substring(previewLoop);
        await Assert.That(previewLoopSource.IndexOf(
                "preview.Line?.StructuredTipAt(previewIndex)", StringComparison.Ordinal))
            .IsGreaterThan(previewLoopSource.IndexOf(
                "Mouse.IsOver(chipRect)", StringComparison.Ordinal));
        await Assert.That(signals).Contains("internal static StructuredTip CreateTooltip(");
        await Assert.That(recommendations).Contains("internal static StructuredTip CreateTooltip(");

        await Assert.That(roles).Contains("editorState.BlockerTip.Activate()");
        await Assert.That(roles).Contains("editorState.HoldersTip.Activate()");
        await Assert.That(options).Contains("state.NumericTip.Activate()");
        await Assert.That(options).Contains("state.RangeTip.Activate()");
        await Assert.That(options).Contains("tip.Activate()");
        await Assert.That(options).Contains("state.AnchorTip.Activate()");

        await Assert.That(preview)
            .Contains("public List<(Role role, ChipState state, string tip)> chips");
        await Assert.That(preview)
            .Contains("ParallelIndexGuard<Role, ChipState, string, StructuredTip>");
        string structuredTipAt = Method(preview, "internal StructuredTip StructuredTipAt(");
        await Assert.That(structuredTipAt).Contains("structuredTips.TryGet(");
        await Assert.That(structuredTipAt).Contains("chip.role, chip.state, chip.tip");
        await Assert.That(preview).Contains("structuredTip?.Activate() ?? tip");
        string drawState = Method(preview, "private static void DrawStateChip(");
        int hover = drawState.IndexOf("Mouse.IsOver(rect)", StringComparison.Ordinal);
        int validation = drawState.IndexOf("StructuredTipAt(sourceIndex)", StringComparison.Ordinal);
        await Assert.That(hover).IsGreaterThanOrEqualTo(0);
        await Assert.That(validation).IsGreaterThan(hover);
        string visible = Method(preview, "private void DrawVisibleEntries(");
        await Assert.That(visible).Contains("DrawStateChip(");
        await Assert.That(visible).Contains("chip.SourceLine, chip.SourceIndex");
        await Assert.That(visible).DoesNotContain("StructuredTipAt(");
        await Assert.That(preview).DoesNotContain("public StructuredTip StructuredTip { get; }");
        await Assert.That(visible).DoesNotContain("entries.Count");
    }

    [Test]
    public async Task JobTipFacadePreservesMutablePublicTipCacheSemantics()
    {
        string jobs = Source("JobSkillProfiles.cs");
        string giver = Method(jobs, "public static string GiverTip(string defName)");
        string workType = Method(jobs, "public static string WorkTypeTip(string defName)");

        foreach (string method in new[] { giver, workType })
        {
            int nullCheck = method.IndexOf(
                "if (profile.TipCache == null)", StringComparison.Ordinal);
            int rebuild = method.IndexOf(
                "profile.StructuredTipCache = new StructuredTip(", StringComparison.Ordinal);
            int customCheck = method.IndexOf(
                "profile.TipCache != profile.StructuredTipCache?.PlainText",
                StringComparison.Ordinal);
            int customReturn = method.IndexOf(
                "return profile.TipCache;", StringComparison.Ordinal);
            int activate = method.IndexOf(
                "return profile.StructuredTipCache.Activate();", StringComparison.Ordinal);

            await Assert.That(nullCheck).IsGreaterThanOrEqualTo(0);
            await Assert.That(rebuild).IsGreaterThan(nullCheck);
            await Assert.That(customCheck).IsGreaterThan(rebuild);
            await Assert.That(customReturn).IsGreaterThan(customCheck);
            await Assert.That(activate).IsGreaterThan(customReturn);
        }
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

    private static string Source(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "WorkRoles" }
            .Concat(path).ToArray()));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
