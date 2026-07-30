using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ThresholdEditorStateTests
{
    [Test]
    public async Task UnrelatedThresholdRevisionPreservesDraftValues()
    {
        var thresholds = new Dictionary<string, ThresholdSpec>
        {
            ["Steel"] = new ThresholdSpec(100, 20),
        };
        var state = new ThresholdEditorState();
        state.Select("Steel", 1, thresholds);
        state.LowValue = 150;
        state.LowBuffer = "150";

        thresholds["WoodLog"] = new ThresholdSpec(80, 10);
        state.Refresh(2, thresholds);

        await Assert.That(state.LowValue).IsEqualTo(150);
        await Assert.That(state.LowBuffer).IsEqualTo("150");
    }

    [Test]
    public async Task SelectedThresholdRevisionReplacesStaleDraftValues()
    {
        var thresholds = new Dictionary<string, ThresholdSpec>
        {
            ["Steel"] = new ThresholdSpec(100, 20),
        };
        var state = new ThresholdEditorState();
        state.Select("Steel", 1, thresholds);
        state.LowValue = 150;
        state.LowBuffer = "150";

        thresholds["Steel"] = new ThresholdSpec(120, 30);
        state.Refresh(2, thresholds);

        await Assert.That(state.LowValue).IsEqualTo(120);
        await Assert.That(state.LowBuffer).IsEqualTo("120");
        await Assert.That(state.CriticalValue).IsEqualTo(30);
        await Assert.That(state.CriticalBuffer).IsEqualTo("30");
    }

    [Test]
    public async Task ClearingSelectedThresholdResetsDraftValues()
    {
        var thresholds = new Dictionary<string, ThresholdSpec>
        {
            ["Steel"] = new ThresholdSpec(100, 20),
        };
        var state = new ThresholdEditorState();
        state.Select("Steel", 1, thresholds);

        thresholds.Remove("Steel");
        state.Refresh(2, thresholds);

        await Assert.That(state.LowValue).IsEqualTo(0);
        await Assert.That(state.LowBuffer).IsEqualTo("0");
        await Assert.That(state.CriticalValue).IsEqualTo(0);
        await Assert.That(state.CriticalBuffer).IsEqualTo("0");
    }
}
