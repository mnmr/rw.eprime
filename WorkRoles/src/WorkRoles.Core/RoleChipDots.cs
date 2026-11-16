namespace WorkRoles.Core
{
    /// The marker dots of a role chip. A dot is drawn only for a fact that
    /// holds, and each colour has one meaning: purple always on, yellow has
    /// rules, blue pinned. The mask indexes the baked dot atlas.
    public static class RoleChipDots
    {
        public const int AlwaysOn = 1;
        public const int Rules = 2;
        public const int Pinned = 4;
        public const int Combinations = 8;

        public static int Mask(bool alwaysOn, bool rules, bool pinned) =>
            (alwaysOn ? AlwaysOn : 0) | (rules ? Rules : 0)
            | (pinned ? Pinned : 0);

        /// Number of dots drawn for a mask.
        public static int Count(int mask) =>
            (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);
    }
}
