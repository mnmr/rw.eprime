namespace WorkRoles.Core.Tests;

public class WindowResizeArchitectureTests
{
    [Test]
    public async Task WindowUpdateIsTheOnlyRuntimeRectangleApplicationPoint()
    {
        string source = WindowSource();
        string update = Method(source, "public override void WindowUpdate()");

        await Assert.That(update).Contains("pendingWindowRect.TryConsume(out var nextWindowRect)");
        await Assert.That(update).Contains("windowRect = nextWindowRect;");
        await Assert.That(Occurrences(source, "windowRect =")).IsEqualTo(1);

        foreach (string signature in new[]
                 {
                     "public override void DoWindowContents(",
                     "private void DrawContents(",
                     "private void GripContents(",
                 })
        {
            string method = Method(source, signature);
            await Assert.That(method).DoesNotContain("windowRect.width =");
            await Assert.That(method).DoesNotContain("windowRect.height =");
            await Assert.That(method).DoesNotContain("windowRect.x =");
            await Assert.That(method).DoesNotContain("windowRect.y =");
            await Assert.That(method).DoesNotContain("SetInitialSizeAndPosition();");
        }

        await Assert.That(Occurrences(source, "SetInitialSizeAndPosition()")).IsEqualTo(2)
            .Because("only the override and its base call should remain");
    }

    [Test]
    public async Task UserResizeAndAutomaticGrowthUsePriorityAwareQueueRoutes()
    {
        string source = WindowSource();
        string grip = Method(source, "private void GripContents(");
        string draw = Method(source, "private void DrawContents(");

        await Assert.That(grip).Contains("pendingWindowRect.QueueUser(nextWindowRect);")
            .Because("manual repaint samples must be deferred as user updates");
        await Assert.That(grip).Contains("pendingWindowRect.QueueUser(AutoSizeRect());")
            .Because("double-click auto-size is an intentional user resize that may shrink");
        await Assert.That(draw).Contains("if (!resizing)");
        await Assert.That(draw).Contains("pendingWindowRect.QueueAutomatic(nextWindowRect);")
            .Because("grow-only content sizing must defer to pending user geometry");
    }

    [Test]
    public async Task MouseUpPersistsPendingUserSampleBeforeAppliedRectangle()
    {
        string grip = Method(WindowSource(), "private void GripContents(");

        await Assert.That(grip)
            .Contains("pendingWindowRect.TryGetUser(out var pendingUserRect)");
        await Assert.That(grip).Contains("var persistedRect = pendingWindowRect.TryGetUser(");
        await Assert.That(grip).Contains("? pendingUserRect : windowRect;");
        await Assert.That(grip).Contains("settings.windowWidth = persistedRect.width;");
        await Assert.That(grip).Contains("settings.windowHeight = persistedRect.height;");
    }

    [Test]
    public async Task InitializationAndCloseDiscardResizeState()
    {
        string source = WindowSource();
        string initial = Method(source, "protected override void SetInitialSizeAndPosition()");
        string close = Method(source, "public override void PostClose()");

        foreach (string method in new[] { initial, close })
        {
            await Assert.That(method).Contains("resizing = false;");
            await Assert.That(method).Contains("pendingWindowRect.Clear();");
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

    private static string WindowSource() => File.ReadAllText(Path.Combine(
        RepoRoot(), "src", "WorkRoles", "UI", "MainTabWindow_WorkRoles.cs"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
