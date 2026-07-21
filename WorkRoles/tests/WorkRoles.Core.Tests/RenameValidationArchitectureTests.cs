namespace WorkRoles.Core.Tests;

public class RenameValidationArchitectureTests
{
    [Test]
    public async Task DialogCachesValidationByInputAndUiRevision()
    {
        string dialog = DialogSource();

        await Assert.That(dialog).Contains("private string validatedName;");
        await Assert.That(dialog).Contains("private int validationRevision");
        await Assert.That(dialog).Contains("string.Equals(validatedName, name, StringComparison.Ordinal)");
        await Assert.That(dialog).Contains("validationRevision == revision");
        await Assert.That(dialog).Contains("trimmedName = (name ?? \"\").Trim();");
        await Assert.That(Occurrences(dialog, ".Trim()")).IsEqualTo(1);
        await Assert.That(dialog).Contains("EnsureValidation();");
    }

    [Test]
    public async Task CommitRevalidatesAndUsesTheCachedTrimmedName()
    {
        string dialog = DialogSource();

        await Assert.That(dialog).Contains("EnsureValidation(force: true);");
        await Assert.That(dialog).Contains("onConfirm(trimmedName);");
        await Assert.That(dialog).DoesNotContain("private bool NameTaken");
        await Assert.That(dialog).DoesNotContain("private bool NameValid");
        await Assert.That(dialog).DoesNotContain("active: NameValid");
    }

    private static int Occurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static string DialogSource() => File.ReadAllText(Path.Combine(
        RepoRoot(), "src", "WorkRoles", "UI", "Dialog_RenameRole.cs"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
