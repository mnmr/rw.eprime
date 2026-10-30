namespace WorkRoles.Core
{
    /// Player permission and dirty state for one exact loaded store. Persistence
    /// consumes Dirty; loading or inspecting a save cannot grant write permission.
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
        public void Choose(bool useCurrent) { Checked = true; CanUpdate = useCurrent; Dirty = useCurrent; }
        public void Changed() { if (CanUpdate) Dirty = true; }
        public void Written() { Dirty = false; }
        public void Disable() { CanUpdate = false; Dirty = false; }
    }
}
