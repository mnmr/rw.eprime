namespace WorkRoles.Core.Tests;

public class PawnManagementArchitectureTests
{
    [Test]
    public async Task FactionDepartureRoutesThroughCentralUnmanagePath()
    {
        string source = Source("Patches", "Patch_AutoAssignBasics.cs");

        await Assert.That(source).Contains("store.UnmanagePawn(__instance);");
        await Assert.That(source).DoesNotContain("store.pawnSets.Remove(__instance)");
    }

    [Test]
    public async Task DeleteRoleBatchesAllLifecycleUiInvalidations()
    {
        string source = Source("RoleCommands.cs");

        await Assert.That(source)
            .Contains("using var uiBatch = new UiInvalidationBatch(UiVersion.Bump);");
        await Assert.That(source)
            .Contains("Store.UnmanagePawn(pawn, uiBatch.Request);");
        await Assert.That(source)
            .Contains("CompiledJobOrders.InvalidateRole(roleId, uiBatch.Request);");
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
