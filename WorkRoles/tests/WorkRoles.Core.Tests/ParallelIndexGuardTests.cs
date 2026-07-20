using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class ParallelIndexGuardTests
{
    [Test]
    public async Task PublicListMutationsNeverPairAnItemWithTheWrongHiddenValue()
    {
        Identity first = new Identity("first");
        Identity second = new Identity("second");
        Identity replacement = new Identity("replacement");

        await AssertMutationFallsBack(chips =>
            chips.Insert(0, new Chip(replacement, ChipState.Added, "replacement")));
        await AssertMutationFallsBack(chips => chips.RemoveAt(0));
        await AssertMutationFallsBack(chips => chips.Reverse());
        await AssertMutationFallsBack(chips =>
            chips[0] = new Chip(replacement, ChipState.Kept, "first"));
        await AssertMutationFallsBack(chips =>
            chips[0] = new Chip(first, ChipState.Removed, "first"));
        await AssertMutationFallsBack(chips =>
            chips[0] = new Chip(first, ChipState.Kept, "changed text"));

        async Task AssertMutationFallsBack(Action<List<Chip>> mutate)
        {
            var chips = new List<Chip>
            {
                new Chip(first, ChipState.Kept, "first"),
                new Chip(second, ChipState.Added, "second"),
            };
            var guard = new ParallelIndexGuard<Identity, ChipState, string, string>();
            guard.Add(first, ChipState.Kept, "first", "first model");
            guard.Add(second, ChipState.Added, "second", "second model");

            await Assert.That(guard.TryGet(0, first, ChipState.Kept, "first",
                out string firstModel)).IsTrue();
            await Assert.That(firstModel).IsEqualTo("first model");
            await Assert.That(guard.TryGet(1, second, ChipState.Added, "second",
                out string secondModel)).IsTrue();
            await Assert.That(secondModel).IsEqualTo("second model");

            mutate(chips);

            for (int index = 0; index < chips.Count; index++)
            {
                Chip chip = chips[index];
                bool matched = guard.TryGet(index, chip.Identity, chip.State, chip.Text,
                    out string model);
                if (matched)
                {
                    string expected = ReferenceEquals(chip.Identity, first)
                        ? "first model"
                        : ReferenceEquals(chip.Identity, second) ? "second model" : null;
                    await Assert.That(model).IsEqualTo(expected);
                }
            }

            Chip firstVisible = chips[0];
            await Assert.That(guard.TryGet(0, firstVisible.Identity, firstVisible.State,
                firstVisible.Text, out _)).IsFalse();
        }
    }

    [Test]
    public async Task ProducerInsertKeepsThePublicItemAndHiddenValueAligned()
    {
        Identity identity = new Identity("inserted");
        var guard = new ParallelIndexGuard<Identity, ChipState, string, string>();
        guard.Add(new Identity("tail"), ChipState.Kept, "tail", "tail model");

        guard.Insert(0, identity, ChipState.Added, "inserted", "inserted model");

        await Assert.That(guard.TryGet(0, identity, ChipState.Added, "inserted",
            out string model)).IsTrue();
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

    private readonly record struct Chip(Identity Identity, ChipState State, string Text);
}
