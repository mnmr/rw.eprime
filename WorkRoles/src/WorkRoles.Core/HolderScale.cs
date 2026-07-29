using System;

namespace WorkRoles.Core
{
    /// Banded holder demand for one named, shareable scale: 12 colony-size
    /// bands of 3 colonists (1-3 .. 34+), each holding min/train/max counts.
    /// Values are direct lookups — no formulas, and bands are independent
    /// (no cross-band coupling). Max is stored banded for format
    /// future-proofing but is not player-editable (uncapped in practice).
    public sealed class HolderScale
    {
        public const int Bands = 12;
        public const int BandSize = 3;

        public string Name;
        /// Shipped/seeded scales are immutable in the editor: the first edit
        /// forks them into a uniquely named user scale.
        public bool Preset;
        public int[] Min = new int[Bands];
        public int[] Train = new int[Bands];
        public int[] Max = UncappedRow();

        public static int BandOf(int colonists) =>
            Math.Min(Bands - 1, Math.Max(0, (colonists - 1) / BandSize));

        public int MinAt(int colonists) => Min[BandOf(colonists)];
        public int TrainAt(int colonists) => Train[BandOf(colonists)];
        public int MaxAt(int colonists) => Max[BandOf(colonists)];

        /// Enforces the per-band invariants: train never exceeds its band's
        /// min, and max never falls below min. Bands are independent; negative
        /// inputs clamp to zero.
        public void Normalize()
        {
            for (int i = 0; i < Bands; i++)
            {
                Min[i] = Math.Max(0, Min[i]);
                Train[i] = Math.Max(0, Math.Min(Train[i], Min[i]));
                if (Max[i] != RoleHolderRange.Uncapped)
                    Max[i] = Math.Max(Math.Max(0, Max[i]), Min[i]);
            }
        }

        public HolderScale Copy() => new HolderScale
        {
            Name = Name,
            Preset = Preset,
            Min = (int[])Min.Clone(),
            Train = (int[])Train.Clone(),
            Max = (int[])Max.Clone(),
        };

        /// The all-zero scale: max 0 in every band means the role is never
        /// recommended (min and train are implicitly zero too).
        public static HolderScale Never(string name)
        {
            var scale = new HolderScale { Name = name, Preset = true };
            for (int i = 0; i < Bands; i++) scale.Max[i] = 0;
            return scale;
        }

        /// True when max 0 suppresses every band: the Never semantics.
        public bool IsNever
        {
            get
            {
                for (int i = 0; i < Bands; i++)
                    if (Max[i] != 0) return false;
                return true;
            }
        }

        /// Value-level equality (name excluded): import uses this to flag
        /// which same-named scales actually change.
        public bool SameValuesAs(HolderScale other)
        {
            if (other == null) return false;
            for (int i = 0; i < Bands; i++)
                if (Min[i] != other.Min[i]
                    || Train[i] != other.Train[i]
                    || Max[i] != other.Max[i])
                    return false;
            return true;
        }

        private static int[] UncappedRow()
        {
            var row = new int[Bands];
            for (int i = 0; i < Bands; i++) row[i] = RoleHolderRange.Uncapped;
            return row;
        }
    }
}
