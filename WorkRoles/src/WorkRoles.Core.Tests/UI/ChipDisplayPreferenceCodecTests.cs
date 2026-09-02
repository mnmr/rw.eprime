using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class ChipDisplayPreferenceCodecTests
{
    [Test]
    public async Task LegacyMinimalLoadsAsIcons()
    {
        ChipDisplayPreference loaded =
            ChipDisplayPreferenceCodec.Decode("Minimal", persistedGrid: false);

        await Assert.That(loaded.Mode).IsEqualTo(2);
        await Assert.That(loaded.Grid).IsFalse();
    }

    [Test]
    [Arguments("CompactGrid", 1)]
    [Arguments("IconsGrid", 2)]
    public async Task LegacyGridModesLoadAsTheirDisplayWithGridOn(
        string persisted, int expectedMode)
    {
        ChipDisplayPreference loaded =
            ChipDisplayPreferenceCodec.Decode(persisted, persistedGrid: false);

        await Assert.That(loaded.Mode).IsEqualTo(expectedMode);
        await Assert.That(loaded.Grid).IsTrue();
    }

    [Test]
    [Arguments(0, "Normal")]
    [Arguments(1, "Compact")]
    [Arguments(2, "Icons")]
    [Arguments(7, "Normal")]
    public async Task SavesCanonicalName(int value, string expected)
    {
        await Assert.That(ChipDisplayPreferenceCodec.Encode(value))
            .IsEqualTo(expected);
    }

    [Test]
    [Arguments("Normal", false, 0, false)]
    [Arguments("Normal", true, 0, true)]
    [Arguments("Compact", true, 1, true)]
    [Arguments("Icons", false, 2, false)]
    [Arguments("unknown", true, 0, true)]
    [Arguments(null, false, 0, false)]
    public async Task LoadsKnownNamesWithTheSeparateGridFlag(
        string? persisted, bool persistedGrid, int expectedMode,
        bool expectedGrid)
    {
        ChipDisplayPreference loaded =
            ChipDisplayPreferenceCodec.Decode(persisted, persistedGrid);

        await Assert.That(loaded.Mode).IsEqualTo(expectedMode);
        await Assert.That(loaded.Grid).IsEqualTo(expectedGrid);
    }
}
