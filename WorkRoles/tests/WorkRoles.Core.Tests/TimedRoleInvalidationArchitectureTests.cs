namespace WorkRoles.Core.Tests;

public class TimedRoleInvalidationArchitectureTests
{
    [Test]
    public async Task HourlyInvalidationPlansOneStoreScanAndPublishesOneRevisionLast()
    {
        string method = Method(Source("CompiledJobOrders.cs"),
            "public static void InvalidateAllTimeRuled()");

        await Assert.That(method).Contains("TimedRoleInvalidationPlanner.Plan(");
        await Assert.That(method).Contains("role.activeHours != Role.AllHours");
        await Assert.That(method).Contains("role.enabled");
        await Assert.That(method).Contains("role.blocker");
        await Assert.That(method).Contains("role.autoAssign");
        await Assert.That(method).Contains("assignment.enabled");
        await Assert.That(method).Contains("assignment.pinned");
        await Assert.That(method).Contains("if (rolesById != null)");
        await Assert.That(Occurrences(method,
            "rolesById[role.id] = role;")).IsEqualTo(1);
        await Assert.That(method).DoesNotContain("new List<TimedRoleAssignmentSource>");
        await Assert.That(method).DoesNotContain("plan.Apply(");
        await Assert.That(Occurrences(method, "store.pawnSets")).IsEqualTo(1);
        await Assert.That(method).DoesNotContain("PawnsWithRole");
        await Assert.That(method).DoesNotContain("InvalidateRole(");

        int coverage = method.IndexOf("role.InvalidateCoverage();", StringComparison.Ordinal);
        int eviction = method.IndexOf("cache.Remove(pawn)", StringComparison.Ordinal);
        int revision = method.IndexOf("UiVersion.Bump", StringComparison.Ordinal);
        await Assert.That(coverage).IsGreaterThan(-1);
        await Assert.That(eviction).IsGreaterThan(coverage);
        await Assert.That(revision).IsGreaterThan(eviction);
        await Assert.That(Occurrences(method, "UiVersion.Bump")).IsEqualTo(1);

        int timeRuleCheck = method.IndexOf(
            "role.activeHours != Role.AllHours", StringComparison.Ordinal);
        int sourceAllocation = method.IndexOf(
            "new List<TimedRoleInvalidationSource>", StringComparison.Ordinal);
        await Assert.That(sourceAllocation).IsGreaterThan(timeRuleCheck);
    }

    [Test]
    public async Task HourBoundaryCallsBatchDirectlyWithoutRolePrescan()
    {
        string method = Method(Source("WorkRolesGameComponent.cs"),
            "public override void GameComponentTick()");

        await Assert.That(Occurrences(method,
            "CompiledJobOrders.InvalidateAllTimeRuled();")).IsEqualTo(1);
        await Assert.That(method).DoesNotContain("store.roles");
        await Assert.That(method).DoesNotContain("role.activeHours");
    }

    [Test]
    public async Task CaravanCrossingRetainsBroadGateAndUsesOnePawnBatch()
    {
        string method = Method(Source("Patches", "Patch_CaravanTimezone.cs"),
            "public static void Postfix(WorldObject __instance, PlanetTile __state)");

        await Assert.That(method).Contains("if (store?.roles == null) return;");
        await Assert.That(method).Contains("role != null");
        await Assert.That(method).Contains("role.activeHours != Role.AllHours");
        await Assert.That(method).Contains(
            "CompiledJobOrders.InvalidateBatch(caravan.PawnsListForReading);");
        await Assert.That(method).DoesNotContain("CompiledJobOrders.Invalidate(pawn)");
        await Assert.That(method).DoesNotContain("foreach (var pawn in caravan.PawnsListForReading)");
    }

    [Test]
    public async Task PawnBatchDeduplicatesThenBumpsOnceWithoutCoverageMutation()
    {
        string method = Method(Source("CompiledJobOrders.cs"),
            "internal static void InvalidateBatch(IEnumerable<Pawn> pawns)");

        await Assert.That(method).Contains("ReferenceIdentityComparer<Pawn>.Instance");
        await Assert.That(method).Contains("cache.Remove(pawn)");
        await Assert.That(method).DoesNotContain("InvalidateCoverage");
        await Assert.That(Occurrences(method, "UiVersion.Bump();")).IsEqualTo(1);
        await Assert.That(method.IndexOf("cache.Remove(pawn)", StringComparison.Ordinal))
            .IsLessThan(method.IndexOf("UiVersion.Bump();", StringComparison.Ordinal));
    }

    [Test]
    public async Task SinglePawnInvalidationRemainsAllocationFreeAndUnchanged()
    {
        string method = Method(Source("CompiledJobOrders.cs"),
            "public static void Invalidate(Pawn pawn)");

        await Assert.That(method).Contains("cache.Remove(pawn)");
        await Assert.That(method).Contains("UiVersion.Bump();");
        await Assert.That(method).DoesNotContain("InvalidateBatch");
        await Assert.That(method).DoesNotContain("new ");
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
