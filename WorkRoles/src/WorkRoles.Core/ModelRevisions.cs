using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    [Flags]
    public enum ModelDomain
    {
        None = 0, Catalog = 1 << 0, RolePresentation = 1 << 1,
        RoleWork = 1 << 2, RoleCapability = 1 << 3, RoleActivation = 1 << 4,
        RoleRules = 1 << 5, RoleRecommendation = 1 << 6, Assignments = 1 << 7,
        AssignmentPresentation = 1 << 8, Roster = 1 << 9, RuntimeRules = 1 << 10,
        Preferences = 1 << 11, RecommendationOrder = 1 << 12, RoleHistory = 1 << 13,
        All = (1 << 14) - 1
    }

    /// Store-owned notification stamps. Keys are stable role/pawn ids, values
    /// are domain sequence stamps, and dependencies are explicit mutation
    /// notifications only. Publication is immediate; no-op callers pass None.
    /// Changed publishes a global floor; AggregateChanged announces collection
    /// changes without expiring keyed consumers. Targeted edits update only
    /// their key and the aggregate. Batches share one sequence across changes.
    /// Reads allocate nothing. Reset drops every keyed entry at store teardown;
    /// ForgetRole/ForgetPawn release removed identities during play.
    public sealed class ModelRevisions
    {
        private const int DomainCount = 14;
        private readonly int[] domains = new int[DomainCount];
        private readonly int[] globalDomains = new int[DomainCount];
        private readonly Dictionary<int, int[]> roles = new Dictionary<int, int[]>();
        private readonly Dictionary<int, int[]> pawns = new Dictionary<int, int[]>();
        private int batchDepth;
        private int batchSequence;
        public int Current { get; private set; }
        /// Event subscribers are store-owned and explicitly released on teardown.
        public event Action<ModelDomain>? Published;
        public int For(ModelDomain domain) => Read(domains, domain);
        public int ForRole(int id, ModelDomain domain) =>
            Math.Max(Read(globalDomains, domain), roles.TryGetValue(id, out int[] stamps) ? Read(stamps, domain) : 0);
        public int ForPawn(int id, ModelDomain domain) =>
            Math.Max(Read(globalDomains, domain), pawns.TryGetValue(id, out int[] stamps) ? Read(stamps, domain) : 0);
        public void Changed(ModelDomain changed) => Publish(changed, null, 0, global: true);
        public void AggregateChanged(ModelDomain changed) => Publish(changed, null, 0);
        public void RoleChanged(int id, ModelDomain changed) => Publish(changed, roles, id);
        public void PawnChanged(int id, ModelDomain changed) => Publish(changed, pawns, id);
        private static int Read(int[] stamps, ModelDomain mask)
        {
            int value = 0;
            for (int i = 0; i < DomainCount; i++)
                if (((int)mask & (1 << i)) != 0 && stamps[i] > value) value = stamps[i];
            return value;
        }
        private void Publish(ModelDomain changed, Dictionary<int, int[]>? keys, int id, bool global = false)
        {
            if (changed == ModelDomain.None) return;
            int sequence;
            if (batchDepth > 0 && batchSequence != 0) sequence = batchSequence;
            else { sequence = ++Current; if (batchDepth > 0) batchSequence = sequence; }
            int[]? keyed = null;
            if (keys != null && !keys.TryGetValue(id, out keyed)) keys[id] = keyed = new int[DomainCount];
            for (int i = 0; i < DomainCount; i++)
                if (((int)changed & (1 << i)) != 0)
                {
                    domains[i] = sequence;
                    if (global) globalDomains[i] = sequence;
                    if (keyed != null) keyed[i] = sequence;
                }
            Published?.Invoke(changed);
        }
        public IDisposable Batch() { batchDepth++; return new BatchScope(this); }
        private sealed class BatchScope : IDisposable
        {
            private ModelRevisions? owner;
            internal BatchScope(ModelRevisions owner) { this.owner = owner; }
            public void Dispose()
            {
                if (owner == null) return;
                if (--owner.batchDepth == 0) owner.batchSequence = 0;
                owner = null;
            }
        }
        public void ForgetRole(int id) => roles.Remove(id);
        public void ForgetPawn(int id) => pawns.Remove(id);
        public void Reset()
        {
            roles.Clear(); pawns.Clear(); Array.Clear(domains, 0, domains.Length);
            Array.Clear(globalDomains, 0, globalDomains.Length);
            Current = batchDepth = batchSequence = 0;
        }
    }
}
