using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// Game-agnostic role values at the player-duplication boundary. Identity and
    /// label belong to the caller; this type owns which source values survive and
    /// which mutable collections must become independently owned.
    /// </summary>
    public sealed class RoleCopyValues<TColor> where TColor : struct
    {
        public bool Enabled { get; set; } = true;
        public bool HasCustomColor { get; set; }
        public TColor Color { get; set; }
        public string IconPath { get; set; }
        public string TemplateDefName { get; set; }
        public string TemplateVersion { get; set; }
        public uint TemplateHash { get; set; }
        public bool AutoAssign { get; set; }
        public bool Blocker { get; set; }
        public RoleHolderMode HolderMode { get; set; }
        public bool HolderRangeSet { get; set; }
        public int MinHolders { get; set; }
        public int MaxHolders { get; set; } = RoleHolderRange.Uncapped;
        public int TrainingWaivers { get; set; }
        public int GroupId { get; set; }
        public int ActiveHours { get; set; }
        public List<string> LocationTokens { get; set; } = new List<string>();
        public List<JobEntry> Entries { get; set; } = new List<JobEntry>();
        public Dictionary<string, List<string>> WorkTypeSnapshots { get; set; } =
            new Dictionary<string, List<string>>();

        /// <summary>
        /// Produces a player-owned duplicate. Template identity and auto-assignment
        /// deliberately do not propagate; all other semantic values do.
        /// </summary>
        public RoleCopyValues<TColor> ForPlayerDuplicate()
        {
            var snapshots = new Dictionary<string, List<string>>(
                WorkTypeSnapshots.Count, WorkTypeSnapshots.Comparer);
            foreach (KeyValuePair<string, List<string>> snapshot in WorkTypeSnapshots)
                snapshots.Add(snapshot.Key, new List<string>(snapshot.Value));

            return new RoleCopyValues<TColor>
            {
                Enabled = Enabled,
                HasCustomColor = HasCustomColor,
                Color = Color,
                IconPath = IconPath,
                TemplateDefName = null,
                TemplateVersion = null,
                TemplateHash = 0u,
                AutoAssign = false,
                Blocker = Blocker,
                HolderMode = HolderMode,
                HolderRangeSet = HolderRangeSet,
                MinHolders = MinHolders,
                MaxHolders = MaxHolders,
                TrainingWaivers = TrainingWaivers,
                GroupId = GroupId,
                ActiveHours = ActiveHours,
                LocationTokens = new List<string>(LocationTokens),
                Entries = new List<JobEntry>(Entries),
                WorkTypeSnapshots = snapshots,
            };
        }
    }
}
