using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class TierBlobCodecTests
{
    [Test]
    public async Task RoundTripsMultiTierLayout()
    {
        var tiers = new List<List<string>>
        {
            new() { "Steel", "WoodLog" },
            new() { "Silver" },
        };
        var decoded = TierBlobCodec.Decode(TierBlobCodec.Encode(tiers));
        await Assert.That(decoded.Count).IsEqualTo(2);
        await Assert.That(string.Join(",", decoded[0])).IsEqualTo("Steel,WoodLog");
        await Assert.That(string.Join(",", decoded[1])).IsEqualTo("Silver");
    }

    [Test]
    public async Task EmptyLayoutRoundTripsToEmpty()
    {
        await Assert.That(TierBlobCodec.Encode(new List<List<string>>())).IsEqualTo("");
        await Assert.That(TierBlobCodec.Decode("").Count).IsEqualTo(0);
        await Assert.That(TierBlobCodec.Decode(null).Count).IsEqualTo(0);
        await Assert.That(TierBlobCodec.Encode(null)).IsEqualTo("");
    }

    [Test]
    public async Task SeparatorsInsideSlotTextRoundTripUnchanged()
    {
        // Slot text is not limited to defName characters (imported files,
        // modded tokens), so the blob separators and the escape character
        // itself must survive the save round trip as one slot each.
        var tiers = new List<List<string>>
        {
            new() { "Steel|Plasteel|Gold|Silver", "a,b", @"back\slash", @"end\" },
            new() { "Silver" },
        };
        var decoded = TierBlobCodec.Decode(TierBlobCodec.Encode(tiers));
        await Assert.That(decoded.Count).IsEqualTo(2);
        await Assert.That(decoded[0]).IsEquivalentTo(tiers[0]);
        await Assert.That(decoded[1]).IsEquivalentTo(tiers[1]);
    }

    [Test]
    public async Task ImportedGroupWithSeparatorTextStaysEditableAfterReload()
    {
        // Import a slot containing '|', save (encode) and reload (decode):
        // the group must keep one tier and accept edits. Before escaping the
        // reload split it into four tiers, over MaxTiers, and SetTiers then
        // rejected every edit of that group.
        const string xml = "<Readouts><Groups><Group Name=\"G\"><Tier>"
            + "<Slot>Steel|Plasteel|Gold|Silver</Slot><Slot>WoodLog</Slot>"
            + "</Tier></Group></Groups></Readouts>";
        await Assert.That(ReadoutsXml.TryImport(xml, out var pools, out var groups, out _)).IsTrue();
        var model = new ReadoutModel();
        int nextId = 1;
        model.ApplyImport(pools, groups, () => nextId++, () => nextId++);

        var reloaded = TierBlobCodec.Decode(TierBlobCodec.Encode(model.Groups[0].Tiers));
        model.Groups[0].Tiers = reloaded;
        await Assert.That(reloaded.Count).IsEqualTo(1);
        await Assert.That(reloaded[0][0]).IsEqualTo("Steel|Plasteel|Gold|Silver");

        var edited = new List<List<string>> { new() { "WoodLog" } };
        await Assert.That(model.SetTiers(model.Groups[0].Id, edited)).IsTrue();
    }

    [Test]
    public async Task BlobsWrittenBeforeEscapingDecodeAsBefore()
    {
        // Existing saves hold defName-only blobs without backslashes.
        var decoded = TierBlobCodec.Decode("Steel,~#3|@MeatRaw,WoodLog");
        await Assert.That(decoded.Count).IsEqualTo(2);
        await Assert.That(string.Join(",", decoded[0])).IsEqualTo("Steel,~#3");
        await Assert.That(string.Join(",", decoded[1])).IsEqualTo("@MeatRaw,WoodLog");
    }

    [Test]
    public async Task DecodeDropsEmptyNamesAndTiers()
    {
        var decoded = TierBlobCodec.Decode("Steel,,WoodLog||Silver");
        await Assert.That(decoded.Count).IsEqualTo(2);
        await Assert.That(string.Join(",", decoded[0])).IsEqualTo("Steel,WoodLog");
        await Assert.That(string.Join(",", decoded[1])).IsEqualTo("Silver");
    }
}
