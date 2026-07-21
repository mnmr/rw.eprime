namespace WorkRoles.Core.Tests;

public class CrossViewPresentationArchitectureTests
{
    [Test]
    public async Task WorkGiverLabelsAreOwnedByAViewNeutralSharedCache()
    {
        string colonists = Source("ColonistsTabView.cs");
        string roles = Source("RolesTabView.cs");
        string editor = Source("RoleEditorState.cs");
        string labels = Source("WorkJobLabels.cs");

        await Assert.That(colonists).Contains("WorkJobLabels.GiverDisplayName(");
        await Assert.That(roles).Contains("WorkJobLabels.GiverDisplayName(");
        await Assert.That(editor).Contains("WorkJobLabels.GiverDisplayName(");
        await Assert.That(editor).DoesNotContain("giverDisplayCache");

        await Assert.That(labels).Contains("internal static class WorkJobLabels");
        await Assert.That(labels).Contains("internal static string GiverDisplayName(");
        await Assert.That(labels).Contains("internal static void InvalidateLanguageCaches()");
        await Assert.That(labels).Contains("StringComparer.OrdinalIgnoreCase");
    }

    [Test]
    public async Task TrainingPathTargetAndInheritedColorShareOneDefinition()
    {
        string colonists = Source("ColonistsTabView.cs");
        string options = Source("OptionsTabState.cs");
        string presentation = Source("TrainingPathPresentation.cs");

        await Assert.That(colonists).Contains(
            "TrainingPathPresentation.HighestBandRoleId(path)");
        await Assert.That(colonists).DoesNotContain("private static int PathTargetRoleId(");
        await Assert.That(options).Contains(
            "TrainingPathPresentation.ColorFor(store, path)");
        await Assert.That(options).DoesNotContain("private static Color PathColor(");

        await Assert.That(presentation).Contains(
            "internal static int HighestBandRoleId(TrainingPath path)");
        await Assert.That(presentation).Contains(
            "Role target = store.RoleById(HighestBandRoleId(path));");
    }

    [Test]
    public async Task SharedVisualTokensAreNotOwnedByAView()
    {
        string colonists = Source("ColonistsTabView.cs");
        string roles = Source("RolesTabView.cs");
        string options = Source("OptionsTabView.cs");
        string style = Source("WrStyle.cs");

        await Assert.That(colonists).Contains("WrStyle.MinorAccent");
        await Assert.That(roles).Contains("WrStyle.MinorAccent");
        await Assert.That(options).Contains("WrStyle.PanelBackground");
        await Assert.That(roles).DoesNotContain("ColonistsTabView.ColorPassMinor");
        await Assert.That(colonists).DoesNotContain("ColorPassMinor");

        foreach (string token in new[]
                 {
                     "PanelBackground", "PanelOutline", "CaptionText",
                     "DimText", "DisabledText", "MinorAccent",
                 })
            await Assert.That(style).Contains($"static readonly Color {token}");
    }

    [Test]
    public async Task ColorPickerUsesTheSwatchOwnerDirectly()
    {
        string dialog = Source("Dialog_RoleColorPicker.cs");

        await Assert.That(dialog).Contains("SwatchPalette.Swatches");
        await Assert.That(dialog).DoesNotContain("RolesTabView.Swatches");
    }

    private static string Source(string file)
    {
        string path = Path.Combine(RepoRoot(), "src", "WorkRoles", "UI", file);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
