using System;

namespace Implanner.Core
{
    /// A logical reservation binding one concrete stored implant item to the
    /// pawn and goal it was allocated for. Enforcement is deliberately
    /// minimal: release-and-report for every consumer.
    public readonly struct ItemReservation : IEquatable<ItemReservation>
    {
        public ItemReservation(int pawnId, string goalKey)
        {
            PawnId = pawnId;
            GoalKey = goalKey;
        }

        public int PawnId { get; }
        public string GoalKey { get; }

        public bool Equals(ItemReservation other) =>
            PawnId == other.PawnId
            && string.Equals(GoalKey, other.GoalKey, StringComparison.Ordinal);

        public override bool Equals(object? obj) =>
            obj is ItemReservation other && Equals(other);

        public override int GetHashCode() =>
            unchecked(PawnId * 397 ^ (GoalKey?.GetHashCode() ?? 0));
    }
}
