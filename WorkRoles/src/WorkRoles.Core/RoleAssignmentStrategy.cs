namespace WorkRoles.Core
{
    /// The named assignment strategy a role references: a fill mode plus an
    /// optional numeric holder scale. Never carries no numerics (Scale null);
    /// Skilled and Unskilled carry a scale whose required totals still drive
    /// champions. Shipped/seeded strategies are immutable Presets that fork on
    /// first edit.
    public sealed class RoleAssignmentStrategy
    {
        public string Name;
        public bool Preset;
        public ScaleMode Mode = ScaleMode.Skilled;
        /// Banded holder demand; null for Never.
        public HolderScale Scale;

        public bool IsNever => Mode == ScaleMode.Never;

        public int RequiredTotalAt(int colonists) =>
            Scale?.RequiredTotalAt(colonists) ?? 0;
        public int TrainingWaiversAt(int colonists) =>
            Scale?.TrainingWaiversAt(colonists) ?? 0;
        public int MaxAt(int colonists) => Scale?.MaxAt(colonists) ?? 0;

        public void Normalize() => Scale?.Normalize();

        public RoleAssignmentStrategy Copy() => new RoleAssignmentStrategy
        {
            Name = Name,
            Preset = Preset,
            Mode = Mode,
            Scale = Scale?.Copy(),
        };

        /// Value equality excluding Name: import uses this to flag which
        /// same-named strategies actually change.
        public bool SameValuesAs(RoleAssignmentStrategy other)
        {
            if (other == null || Mode != other.Mode) return false;
            if (Scale == null) return other.Scale == null;
            return other.Scale != null && Scale.SameValuesAs(other.Scale);
        }

        /// The Never strategy: no numerics, assigns no one.
        public static RoleAssignmentStrategy Never(string name) =>
            new RoleAssignmentStrategy
            {
                Name = name,
                Preset = true,
                Mode = ScaleMode.Never,
                Scale = null,
            };

        /// Resolves a persisted mode token, falling back to the pre-mode
        /// encoding: an all-zero-max scale was Never, anything else Skilled.
        public static ScaleMode ParseMode(string token, HolderScale legacyScale)
        {
            if (!string.IsNullOrEmpty(token)
                && int.TryParse(token.Trim(), out int value)
                && value >= 0 && value <= (int)ScaleMode.Never)
                return (ScaleMode)value;
            return legacyScale != null && legacyScale.AllZeroMax
                ? ScaleMode.Never : ScaleMode.Skilled;
        }

        /// Builds a strategy from decoded rows and a (possibly absent) mode
        /// token, dropping the numerics for Never.
        public static RoleAssignmentStrategy FromRows(
            string name, bool preset, string modeToken, HolderScale scale)
        {
            ScaleMode mode = ParseMode(modeToken, scale);
            if (mode == ScaleMode.Never)
                return new RoleAssignmentStrategy
                {
                    Name = name, Preset = preset,
                    Mode = ScaleMode.Never, Scale = null,
                };
            scale?.Normalize();
            return new RoleAssignmentStrategy
            {
                Name = name, Preset = preset, Mode = mode, Scale = scale,
            };
        }

        /// The Unskilled strategy: every capable pawn is assigned; a single
        /// high-priority champion is designated once the colony reaches the
        /// 10-12 band. Max stays uncapped so all capable pawns fill in.
        public static RoleAssignmentStrategy Unskilled(string name)
        {
            var scale = new HolderScale();
            for (int band = 3; band < HolderScale.Bands; band++)
                scale.RequiredTotals[band] = 1;
            scale.Normalize();
            return new RoleAssignmentStrategy
            {
                Name = name,
                Preset = true,
                Mode = ScaleMode.Unskilled,
                Scale = scale,
            };
        }
    }
}
