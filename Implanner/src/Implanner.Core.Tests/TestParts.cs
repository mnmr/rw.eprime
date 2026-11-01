global using static Implanner.Core.Tests.TestParts;

using Implanner.Core;

namespace Implanner.Core.Tests;

/// Anatomy-agnostic slot addresses for tests whose subject is not the
/// anatomy itself (bookkeeping, plan inheritance, batching): occurrence n
/// of one generic part kind, key "Part#n".
public static class TestParts
{
    public static PartAddress Part(int occurrence) => new("Part", occurrence);

    public static PartAddress[] Parts(params int[] occurrences)
    {
        var slots = new PartAddress[occurrences.Length];
        for (int i = 0; i < slots.Length; i++) slots[i] = Part(occurrences[i]);
        return slots;
    }
}
