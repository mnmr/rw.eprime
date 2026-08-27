namespace WorkRoles.Core
{
    /// Strike-through pattern for an off role chip: the strike count tells the
    /// player which of the two independent toggles turned the chip off.
    public static class RoleChipStrikes
    {
        public const int None = 0;
        /// Single strike on the chip's midline.
        public const int PawnOff = 1;
        /// Double strike bracketing the midline, so the single strike's slot
        /// stays visually distinct in the middle.
        public const int GlobalOff = 2;
        /// All three lines.
        public const int BothOff = 3;

        /// ForceOn is active regardless of the global toggle, so it never
        /// strikes; its running-despite-global-off story is told by the
        /// forced-on chip marker instead.
        public static int Count(bool globalEnabled, AssignmentState state) =>
            state == AssignmentState.ForceOn ? None
            : (globalEnabled ? None : GlobalOff)
              + (state == AssignmentState.Disabled ? PawnOff : None);
    }
}
