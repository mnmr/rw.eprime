namespace RimShared.Common.Tests;

public class ParallelIndexGuardTests
{
    [Test]
    [Arguments(PublicMutation.Insert, "<miss>,<miss>,<miss>")]
    [Arguments(PublicMutation.Remove, "<miss>")]
    [Arguments(PublicMutation.Reverse, "<miss>,<miss>")]
    [Arguments(PublicMutation.ReplaceIdentity, "<miss>,second model")]
    [Arguments(PublicMutation.ReplaceState, "<miss>,second model")]
    [Arguments(PublicMutation.ReplaceText, "<miss>,second model")]
    public async Task PublicListMutationNeverPairsAnItemWithTheWrongHiddenValue(PublicMutation mutation, string expectedResults)
    {
        Identity first = new Identity("first");
        Identity second = new Identity("second");
        Identity replacement = new Identity("replacement");
        List<Chip> chips = [new Chip(first, ChipState.Kept, "first"), new Chip(second, ChipState.Added, "second")];
        var guard = new ParallelIndexGuard<Identity, ChipState, string, string>();
        guard.Add(first, ChipState.Kept, "first", "first model");
        guard.Add(second, ChipState.Added, "second", "second model");

        await Assert.That(guard.TryGet(0, first, ChipState.Kept, "first", out string? firstModel)).IsTrue();
        await Assert.That(firstModel).IsEqualTo("first model");
        await Assert.That(guard.TryGet(1, second, ChipState.Added, "second", out string? secondModel)).IsTrue();
        await Assert.That(secondModel).IsEqualTo("second model");

        switch (mutation)
        {
            case PublicMutation.Insert:
                chips.Insert(0, new Chip(replacement, ChipState.Added, "replacement"));
                break;
            case PublicMutation.Remove:
                chips.RemoveAt(0);
                break;
            case PublicMutation.Reverse:
                chips.Reverse();
                break;
            case PublicMutation.ReplaceIdentity:
                chips[0] = new Chip(replacement, ChipState.Kept, "first");
                break;
            case PublicMutation.ReplaceState:
                chips[0] = new Chip(first, ChipState.Removed, "first");
                break;
            case PublicMutation.ReplaceText:
                chips[0] = new Chip(first, ChipState.Kept, "changed text");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        string actualResults = string.Join(",", chips.Select((chip, index) => guard.TryGet(index, chip.Identity, chip.State, chip.Text, out string? model) ? model : "<miss>"));

        await Assert.That(actualResults).IsEqualTo(expectedResults);
    }

    [Test]
    public async Task ProducerInsertKeepsThePublicItemAndHiddenValueAligned()
    {
        Identity identity = new Identity("inserted");
        var guard = new ParallelIndexGuard<Identity, ChipState, string, string>();
        guard.Add(new Identity("tail"), ChipState.Kept, "tail", "tail model");

        guard.Insert(0, identity, ChipState.Added, "inserted", "inserted model");

        await Assert.That(guard.TryGet(0, identity, ChipState.Added, "inserted", out string? model)).IsTrue();
        await Assert.That(model).IsEqualTo("inserted model");
    }

    private sealed class Identity
    {
        internal Identity(string name) => Name = name;

        internal string Name { get; }
    }

    private enum ChipState
    {
        Kept,
        Added,
        Removed,
    }

    public enum PublicMutation
    {
        Insert,
        Remove,
        Reverse,
        ReplaceIdentity,
        ReplaceState,
        ReplaceText,
    }

    private readonly record struct Chip(Identity Identity, ChipState State, string Text);
}
