using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class ChipDisplayPreferenceCodecTests
{
    [Test]
    public async Task LegacyMinimalLoadsAsIcons()
    {
        await Assert.That(ChipDisplayPreferenceCodec.Decode("Minimal"))
            .IsEqualTo(2);
    }

    [Test]
    [Arguments(0, "Normal")]
    [Arguments(1, "Compact")]
    [Arguments(2, "Icons")]
    [Arguments(3, "CompactGrid")]
    [Arguments(4, "IconsGrid")]
    public async Task SavesCanonicalName(int value, string expected)
    {
        await Assert.That(ChipDisplayPreferenceCodec.Encode(value))
            .IsEqualTo(expected);
    }

    [Test]
    [Arguments("Normal", 0)]
    [Arguments("Compact", 1)]
    [Arguments("Icons", 2)]
    [Arguments("CompactGrid", 3)]
    [Arguments("IconsGrid", 4)]
    [Arguments("unknown", 0)]
    [Arguments(null, 0)]
    public async Task LoadsKnownNamesAndDefaultsInvalidValues(
        string? persisted, int expected)
    {
        await Assert.That(ChipDisplayPreferenceCodec.Decode(persisted))
            .IsEqualTo(expected);
    }
}
