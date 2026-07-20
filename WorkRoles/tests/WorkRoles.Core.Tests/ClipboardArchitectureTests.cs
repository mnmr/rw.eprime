namespace WorkRoles.Core.Tests;

public class ClipboardArchitectureTests
{
    [Test]
    public async Task ClipboardIsStoreOwnedAndReturnsSnapshots()
    {
        string clipboard = Source("UI", "RoleClipboard.cs");
        string colonistsTab = Source("UI", "ColonistsTabView.cs");

        await Assert.That(clipboard).Contains("private static RoleStore owner;");
        await Assert.That(clipboard).Contains("ClipboardRules.SnapshotForOwner(");
        await Assert.That(colonistsTab).Contains("RoleClipboard.CopyFrom(store, toCopy);");
    }

    [Test]
    public async Task SyncedPasteFiltersAgainstCurrentRolesAndUnmanagesAnEmptyResult()
    {
        string commands = Source("RoleCommands.cs");
        int pasteStart = commands.IndexOf("public static void PasteRoleSet", StringComparison.Ordinal);
        string paste = pasteStart < 0 ? "" : commands.Substring(pasteStart);

        await Assert.That(paste)
            .Contains("if (Store == null || pawn == null || source == null) return;");
        await Assert.That(paste).Contains("ClipboardRules.FilterValidDistinct(");
        await Assert.That(paste).Contains("assignment => assignment?.roleId");
        await Assert.That(paste).Contains("Store.roles.Select(role => role.id)");
        await Assert.That(paste).Contains("Store.UnmanagePawn(pawn);");
    }

    [Test]
    public async Task ClipboardClearsOnWindowCloseAndWorldTeardown()
    {
        string window = Source("UI", "MainTabWindow_WorkRoles.cs");
        string teardown = Source("Patches", "Patch_PawnWorkSettings.cs");

        await Assert.That(window).Contains("RoleClipboard.Clear();");
        await Assert.That(teardown).Contains("UI.RoleClipboard.Clear();");
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "WorkRoles" }.Concat(parts).ToArray()));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
