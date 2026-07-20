using System;

namespace WorkRoles.Core
{
    /// <summary>
    /// Allocation-free composite key for UI snapshots that depend on both
    /// simulation-visible state and the owning view's current pawn scope.
    /// </summary>
    public readonly struct ScopeCacheStamp : IEquatable<ScopeCacheStamp>
    {
        public static readonly ScopeCacheStamp Invalid = new ScopeCacheStamp(-1, -1);

        public ScopeCacheStamp(int uiRevision, int pawnListRevision)
        {
            UiRevision = uiRevision;
            PawnListRevision = pawnListRevision;
        }

        public int UiRevision { get; }
        public int PawnListRevision { get; }

        public bool Equals(ScopeCacheStamp other)
            => UiRevision == other.UiRevision
                && PawnListRevision == other.PawnListRevision;

        public override bool Equals(object obj)
            => obj is ScopeCacheStamp other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (UiRevision * 397) ^ PawnListRevision;
            }
        }

        public static bool operator ==(ScopeCacheStamp left, ScopeCacheStamp right)
            => left.Equals(right);

        public static bool operator !=(ScopeCacheStamp left, ScopeCacheStamp right)
            => !left.Equals(right);
    }
}
