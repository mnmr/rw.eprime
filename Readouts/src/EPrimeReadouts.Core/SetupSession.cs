namespace EPrimeReadouts.Core
{
    /// Local permission to update the player's default, scoped to one loaded game.
    /// No load callback, timer or view interaction can grant that permission.
    public sealed class SetupSession
    {
        public bool Checked { get; private set; }
        public bool CanUpdate { get; private set; }
        public bool Dirty { get; private set; }
        public bool CheckOnOpen(bool autoUpdate, bool hasDefault, bool matches)
        {
            if (Checked) return false;
            Checked = true;
            CanUpdate = autoUpdate && hasDefault && matches;
            return autoUpdate && hasDefault && !matches;
        }
        public void Choose(bool useThisSetup) { Checked = true; CanUpdate = useThisSetup; Dirty = useThisSetup; }
        public void Changed() { if (CanUpdate) Dirty = true; }
        public void Written() { Dirty = false; }
        public void Disable() { CanUpdate = false; Dirty = false; }
    }
}
