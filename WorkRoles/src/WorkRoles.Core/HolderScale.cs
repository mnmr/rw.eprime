using System;

namespace WorkRoles.Core
{
    /// Banded holder demand for one named, shareable scale: 12 colony-size
    /// bands of 3 colonists (1-3 .. 34+), each holding a required total,
    /// training-waiver count, and maximum.
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
        /// Total required holders, including slots that training roles may fill.
        public int[] RequiredTotals = new int[Bands];
        /// Required-total slots that training roles may fill.
        public int[] TrainingWaivers = new int[Bands];
        public int[] Max = UncappedRow();

        public static int BandOf(int colonists) =>
            Math.Min(Bands - 1, Math.Max(0, (colonists - 1) / BandSize));

        public int RequiredTotalAt(int colonists) =>
            RequiredTotals[BandOf(colonists)];
        public int TrainingWaiversAt(int colonists) =>
            TrainingWaivers[BandOf(colonists)];
        public HolderRequirement RequirementAt(int colonists) =>
            new HolderRequirement(
                RequiredTotalAt(colonists),
                TrainingWaiversAt(colonists));
        public int MaxAt(int colonists) => Max[BandOf(colonists)];

        /// Enforces the per-band invariants: training waivers never exceed the
        /// required total, and max never falls below that total. Bands are
        /// independent; negative inputs clamp to zero.
        public void Normalize()
        {
            for (int i = 0; i < Bands; i++)
            {
                RequiredTotals[i] = Math.Max(0, RequiredTotals[i]);
                TrainingWaivers[i] = Math.Max(
                    0, Math.Min(TrainingWaivers[i], RequiredTotals[i]));
                if (Max[i] != RoleHolderRange.Uncapped)
                    Max[i] = Math.Max(Math.Max(0, Max[i]), RequiredTotals[i]);
            }
        }

        public HolderScale Copy() => new HolderScale
        {
            Name = Name,
            Preset = Preset,
            RequiredTotals = (int[])RequiredTotals.Clone(),
            TrainingWaivers = (int[])TrainingWaivers.Clone(),
            Max = (int[])Max.Clone(),
        };

        /// The all-zero scale: max 0 in every band means the role is never
        /// recommended (required totals and training waivers are zero too).
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
                if (RequiredTotals[i] != other.RequiredTotals[i]
                    || TrainingWaivers[i] != other.TrainingWaivers[i]
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

    /// A holder requirement in the same terms used by configuration and UI.
    /// Training waivers are part of the required total; the direct minimum is
    /// therefore derived once here rather than reinterpreted by each consumer.
    public readonly struct HolderRequirement
    {
        public HolderRequirement(int requiredTotal, int trainingWaivers)
        {
            RequiredTotal = Math.Max(0, requiredTotal);
            TrainingWaivers = Math.Max(
                0, Math.Min(trainingWaivers, RequiredTotal));
        }

        public int RequiredTotal { get; }
        public int TrainingWaivers { get; }
        public int DirectMinimum => RequiredTotal - TrainingWaivers;
    }
}
