using RimShared.Common;

namespace RimShared.Common.Tests;

public class WelcomeTileLayoutTests
{
    [Test]
    public async Task ContentCanUseSpareHeightWithoutLeavingTheScreenOrOverlappingOtherWelcomes()
    {
        var single = WelcomeTileLayout.Create(1536, 864, 1).At(0);
        var expanded = single.FitContentHeight(720f);
        await Assert.That(expanded.Height).IsEqualTo(720f);
        await Assert.That(expanded.Y >= 12f && expanded.Y + expanded.Height <= 817f).IsTrue();
        await Assert.That(single.FitContentHeight(400f).Height).IsEqualTo(single.Height);

        var compact = WelcomeTileLayout.Create(1024, 576, 1).At(0).FitContentHeight(720f);
        await Assert.That(compact.Height < 720f).IsTrue();
        await Assert.That(compact.Y >= 12f && compact.Y + compact.Height <= 529f).IsTrue();

        var tiled = WelcomeTileLayout.Create(1536, 1440, 4);
        for (int i = 0; i < tiled.Count; i++)
        {
            var a = tiled.At(i).FitContentHeight(720f);
            await Assert.That(a.Height > 520f).IsTrue();
            await Assert.That(a.Y >= 12f && a.Y + a.Height <= 1393f).IsTrue();
            for (int j = 0; j < i; j++)
            {
                var b = tiled.At(j).FitContentHeight(720f);
                await Assert.That(a.X < b.X + b.Width && b.X < a.X + a.Width
                    && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height).IsFalse();
            }
        }
    }

    [Test]
    public async Task DisposingRejectsReentrantWindowCallbacksUntilTheNextGameStarts()
    {
        // A window removal can invoke every mod assembly's pending callback.
        // Geometry alone cannot prove those callbacks release old-save refs.
        var firstAssembly = new WelcomePendingWindows<object>();
        var secondAssembly = new WelcomePendingWindows<object>();
        object previousWindow = new();
        firstAssembly.Add("old-save", previousWindow);
        secondAssembly.Add("old-save", new());
        firstAssembly.Stop();
        secondAssembly.Stop();
        foreach (WelcomePendingWindows<object> assembly in new[] { firstAssembly, secondAssembly })
        {
            assembly.Add("old-save", previousWindow);
            await Assert.That(assembly.TryTake("old-save", true, out _)).IsFalse();
            assembly.Add("different-save", new());
            await Assert.That(assembly.TryTake("different-save", true, out _)).IsFalse();
            await Assert.That(assembly.Count).IsEqualTo(0);
            await Assert.That(assembly.IsStopped).IsTrue();
            assembly.Stop();
            assembly.Start();
            object nextWindow = new();
            assembly.Add("next-save", nextWindow);
            await Assert.That(assembly.TryTake("next-save", true, out object? actual)).IsTrue();
            await Assert.That(ReferenceEquals(nextWindow, actual)).IsTrue();
        }
    }

    [Test]
    public async Task ShrinkingSuspendsOverflowInsteadOfStackingDialogsAndGrowingRestoresThem()
    {
        WelcomeTilePlan large = WelcomeTileLayout.Create(1536, 864, 4);
        WelcomeTilePlan small = WelcomeTileLayout.Create(640, 360, 4);
        var pending = new WelcomePendingWindows<object>();
        object[] windows = [new(), new(), new(), new()];
        for (int i = 0; i < windows.Length; i++)
        {
            await Assert.That(large.TryGetTile(i, out _)).IsTrue();
            if (!small.TryGetTile(i, out _)) pending.Add("save-a", windows[i]);
        }
        await Assert.That(small.TryGetTile(1, out _)).IsFalse();
        await Assert.That(pending.Count).IsEqualTo(3);
        await Assert.That(pending.TryTake("save-a", false, out _)).IsFalse();
        for (int i = 1; i < windows.Length; i++)
        {
            await Assert.That(pending.TryTake("save-a", large.TryGetTile(i, out _), out object? resumed)).IsTrue();
            await Assert.That(ReferenceEquals(resumed, windows[i])).IsTrue();
        }
        await Assert.That(pending.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SuspendedWindowsCannotSurviveSaveChangesOrExplicitTeardown()
    {
        // Geometry cannot prove that a closed dialog releases its retained
        // callback/reference on world changes; this protects that lifecycle.
        var pending = new WelcomePendingWindows<object>();
        object old = new();
        pending.Add("save-a", old);
        pending.Add("save-a", old);
        await Assert.That(pending.Count).IsEqualTo(1);
        await Assert.That(pending.TryTake("save-b", true, out _)).IsFalse();
        await Assert.That(pending.Count).IsEqualTo(0);
        pending.Add("save-b", new());
        pending.Clear();
        pending.Clear();
        await Assert.That(pending.TryTake("save-b", true, out _)).IsFalse();
    }

    [Test]
    public async Task FourWelcomesFitWithoutOverlapAndSmallScreensQueueOverflow()
    {
        // The player must be able to distinguish all four mod welcomes at the
        // design floor; on a small display, queued welcomes must not be marked
        // seen merely because there is no room. We assert published geometry,
        // not a particular packing algorithm or array order.
        WelcomeTilePlan layout = WelcomeTileLayout.Create(1536, 864, 4);
        await Assert.That(layout.Count).IsEqualTo(4);
        for (int i = 0; i < layout.Count; i++)
        {
            WelcomeTile a = layout.At(i);
            await Assert.That(a.Width >= 360 && a.Height >= 280).IsTrue();
            await Assert.That(a.X >= 0 && a.Y >= 0
                && a.X + a.Width <= 1536 && a.Y + a.Height <= 829).IsTrue();
            for (int j = 0; j < i; j++)
            {
                WelcomeTile b = layout.At(j);
                bool overlaps = a.X < b.X + b.Width && b.X < a.X + a.Width
                    && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                await Assert.That(overlaps).IsFalse();
            }
        }
        WelcomeTilePlan compact = WelcomeTileLayout.Create(640, 360, 4);
        await Assert.That(compact.Count).IsEqualTo(1);
        WelcomeTile only = compact.At(0);
        await Assert.That(only.X + only.Width <= 640 && only.Y + only.Height <= 325).IsTrue();
        await Assert.That(WelcomeTileLayout.Create(1536, 864, 0).Count).IsEqualTo(0);
    }
}
