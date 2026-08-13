namespace WorkRoles.Core.Tests.Roles;

public class RoleCopyValuesTests
{
    [Test]
    public async Task PlayerDuplicateCopiesEveryValueButDropsTemplateAndAutoOwnership()
    {
        RoleCopyValues<int> source = CompleteSource();

        RoleCopyValues<int> copy = source.ForPlayerDuplicate();

        await Assert.That(copy.Enabled).IsFalse();
        await Assert.That(copy.HasCustomColor).IsTrue();
        await Assert.That(copy.Color).IsEqualTo(8675309);
        await Assert.That(copy.IconPath).IsEqualTo("WorkRoles/Icons/Builder");
        await Assert.That(copy.Blocker).IsTrue();
        await Assert.That(copy.MinAge).IsEqualTo(13);
        await Assert.That(copy.MaxAge).IsEqualTo(17);
        await Assert.That(copy.ColonyMin).IsEqualTo(2);
        await Assert.That(copy.Coverage).IsEqualTo(19);
        await Assert.That(copy.GroupId).IsEqualTo(42);
        await Assert.That(copy.ActiveHours).IsEqualTo(0x00F0F0);
        await Assert.That(copy.TemplateDefName).IsNull();
        await Assert.That(copy.TemplateVersion).IsNull();
        await Assert.That(copy.TemplateHash).IsEqualTo(0u);
        await Assert.That(copy.AutoAssign).IsFalse();
        await Assert.That(string.Join(";", copy.LocationTokens)).IsEqualTo("Settlements;settlement:17");
        await Assert.That(string.Join(";", copy.Entries.Select(entry => entry.Encode()))).IsEqualTo("WorkType:Crafting;WorkGiver:DoBillsCook");
        await Assert.That(string.Join(";", copy.WorkTypeSnapshots["Crafting"])).IsEqualTo("DoBillsCook;DoBillsSmith");
        await Assert.That(copy.WorkTypeSnapshots["Empty"].Count).IsEqualTo(0);
    }

    [Test]
    public async Task PlayerDuplicateDeepCopiesEveryMutableCollectionBothDirections()
    {
        RoleCopyValues<int> source = CompleteSource();

        RoleCopyValues<int> copy = source.ForPlayerDuplicate();

        await Assert.That(ReferenceEquals(copy.LocationTokens, source.LocationTokens)).IsFalse();
        await Assert.That(ReferenceEquals(copy.Entries, source.Entries)).IsFalse();
        await Assert.That(ReferenceEquals(copy.WorkTypeSnapshots, source.WorkTypeSnapshots)).IsFalse();
        await Assert.That(ReferenceEquals(copy.WorkTypeSnapshots.Comparer, StringComparer.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(string.Join(";", copy.WorkTypeSnapshots["crafting"])).IsEqualTo("DoBillsCook;DoBillsSmith");
        foreach (string key in source.WorkTypeSnapshots.Keys)
            await Assert.That(ReferenceEquals(copy.WorkTypeSnapshots[key], source.WorkTypeSnapshots[key])).IsFalse().Because(key + " giver list must be independently owned");

        source.LocationTokens.Add("source:only");
        source.Entries.Add(new JobEntry(JobEntryKind.WorkGiver, "SourceOnly"));
        source.WorkTypeSnapshots["Crafting"].Add("SourceSnapshotOnly");
        source.WorkTypeSnapshots["Empty"].Add("SourceEmptyOnly");
        source.WorkTypeSnapshots.Add("SourceOuterOnly", ["Source"]);

        await Assert.That(copy.LocationTokens.Contains("source:only")).IsFalse();
        await Assert.That(copy.Entries.Any(entry => entry.DefName == "SourceOnly")).IsFalse();
        await Assert.That(copy.WorkTypeSnapshots["Crafting"].Contains("SourceSnapshotOnly")).IsFalse();
        await Assert.That(copy.WorkTypeSnapshots["Empty"].Count).IsEqualTo(0);
        await Assert.That(copy.WorkTypeSnapshots.ContainsKey("SourceOuterOnly")).IsFalse();

        copy.LocationTokens.Add("copy:only");
        copy.Entries.Add(new JobEntry(JobEntryKind.WorkGiver, "CopyOnly"));
        copy.WorkTypeSnapshots["Crafting"].Add("CopySnapshotOnly");
        copy.WorkTypeSnapshots["Empty"].Add("CopyEmptyOnly");
        copy.WorkTypeSnapshots.Add("CopyOuterOnly", ["Copy"]);

        await Assert.That(source.LocationTokens.Contains("copy:only")).IsFalse();
        await Assert.That(source.Entries.Any(entry => entry.DefName == "CopyOnly")).IsFalse();
        await Assert.That(source.WorkTypeSnapshots["Crafting"].Contains("CopySnapshotOnly")).IsFalse();
        await Assert.That(source.WorkTypeSnapshots["Empty"].Contains("CopyEmptyOnly")).IsFalse();
        await Assert.That(source.WorkTypeSnapshots.ContainsKey("CopyOuterOnly")).IsFalse();
    }

    private static RoleCopyValues<int> CompleteSource() =>
        new()
        {
            Enabled = false,
            HasCustomColor = true,
            Color = 8675309,
            IconPath = "WorkRoles/Icons/Builder",
            TemplateDefName = "WS_Builder",
            TemplateVersion = "1.2.3",
            TemplateHash = 0xDEADBEEFu,
            AutoAssign = true,
            Blocker = true,
            MinAge = 13,
            MaxAge = 17,
            ColonyMin = 2,
            Coverage = 19,
            GroupId = 42,
            ActiveHours = 0x00F0F0,
            LocationTokens = ["Settlements", "settlement:17"],
            Entries = [new(JobEntryKind.WorkType, "Crafting"), new(JobEntryKind.WorkGiver, "DoBillsCook")],
            WorkTypeSnapshots = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Crafting"] = new() { "DoBillsCook", "DoBillsSmith" },
                ["Empty"] = new(),
            },
        };
}
