namespace WorkRoles.Core.Tests;

public class RoleDragReleaseArchitectureTests
{
    [Test]
    public async Task DeferredClickRequiresReleaseInsideCapturedSourceRect()
    {
        string drag = Source("UI", "RoleDrag.cs");

        await Assert.That(drag)
            .Contains("public static void OnPress(Rect sourceRect, int roleId, Pawn source, Action clickAction)");
        await Assert.That(drag)
            .Contains("public static void OnPressGroup(Rect sourceRect, int groupId, Action clickAction)");
        await Assert.That(drag).Contains("pendingSourceRect = ToScreenRect(sourceRect);");
        await Assert.That(drag)
            .Contains("pendingSourceRect.Contains(GUIUtility.GUIToScreenPoint(Event.current.mousePosition))");

        int contains = drag.IndexOf("pendingSourceRect.Contains", StringComparison.Ordinal);
        int invoke = drag.IndexOf("pendingClickAction?.Invoke();", StringComparison.Ordinal);
        await Assert.That(contains).IsGreaterThanOrEqualTo(0);
        await Assert.That(invoke).IsGreaterThan(contains);
    }

    [Test]
    public async Task DeferredClickCallSitesPassTheirInteractionRects()
    {
        string chips = Source("UI", "RoleChipUI.cs");
        string roles = Source("UI", "RolesTabView.cs");

        await Assert.That(chips).Contains("RoleDrag.OnPress(rect, role.id, dragSource, onClick);");
        await Assert.That(roles)
            .Contains("RoleDrag.OnPress(row, capturedId, null, () => SelectRole(capturedId));");
        await Assert.That(roles)
            .Contains("() => RolesListState.ToggleSectionCollapsed(key));");
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
