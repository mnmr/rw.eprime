using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Detached native execution facts. Mechanical workers never join Pawns,
    /// the human demand denominator, and never receive learning credit.
    public sealed class MechanicalWorkerView
    {
        public int Id;
        public PawnView Facts = new PawnView();
        public HashSet<string> ExecutableGivers = new HashSet<string>();
        public HashSet<string> CommittedGivers = new HashSet<string>();
        public HashSet<string> PartialServiceGivers = new HashSet<string>();
        public bool ProvidesService;
        /// Active assignments in actual execution order. Only the first
        /// eligible duty earns an independent staffing credit.
        public List<int> ActiveRoleIds = new List<int>();

        public bool ContentEquals(MechanicalWorkerView other) =>
            RecommendationInputEquality.MechanicalWorker(this, other);
    }

    public static class MechanicalDutyPlan
    {
        private readonly struct Candidate
        {
            internal Candidate(RoleView role, int executable, long position, int catalogIndex, int preference)
            { Role = role; Executable = executable; Position = position; CatalogIndex = catalogIndex; Preference = preference; }
            internal readonly RoleView Role;
            internal readonly int Executable;
            internal readonly long Position;
            internal readonly int CatalogIndex;
            internal readonly int Preference;
            internal int Unsupported => Role.Coverage.Count - Executable;
        }

        private static readonly Comparison<Candidate> DefaultOrder = (a, b) =>
        {
            int position = a.Position.CompareTo(b.Position);
            return position != 0 ? position : a.CatalogIndex.CompareTo(b.CatalogIndex);
        };
        private static readonly Comparison<Candidate> SelectionOrder = (a, b) =>
        {
            int preference = a.Preference.CompareTo(b.Preference);
            return preference != 0 ? preference : DefaultOrder(a, b);
        };

        public static PawnFixTarget BuildFixTarget(ColonyView colony, MechanicalWorkerView worker,
            Dictionary<int, RoleRecommendationExplanation>? explanations = null,
            Dictionary<int, SignalBucket>? suitability = null)
        {
            EngineContext facts = CapabilityFacts(colony, worker);
            var ids = new List<int>();
            if (worker.Facts.Incapacitated)
            {
                // Temporary incapacity changes staffing, not the player's
                // standing duties. Only explicit exclusions remove them;
                // protected assignments are merged back below as for humans.
                foreach (AssignmentView assignment in worker.Facts.Existing)
                    if (facts.RoleOf(assignment.RoleId) is RoleView role
                        && !facts.IsRecommendationExcluded(role)) ids.Add(assignment.RoleId);
            }
            else ids.AddRange(Recommend(colony, worker, facts, preservePrimary: true));
            var roles = new Dictionary<int, RoleView>();
            foreach (RoleView role in colony.Roles) roles[role.Id] = role;
            foreach (AssignmentView assignment in worker.Facts.Existing)
                if (roles.TryGetValue(assignment.RoleId, out RoleView role)
                    && (assignment.Pinned
                        || !facts.IsRecommendationExcluded(role) && (role.HasRules || role.Blocker))
                    && !ids.Contains(assignment.RoleId)) ids.Add(assignment.RoleId);
            int[] target = Ordering.PreserveProtectedOrder(roles, worker.Facts.Existing, ids);
            bool changed = target.Length != worker.Facts.Existing.Count;
            for (int i = 0; i < target.Length && !changed; i++)
                changed = target[i] != worker.Facts.Existing[i].RoleId;
            Dictionary<int, SignalBucket>? verdicts = suitability != null || explanations != null
                ? RoleSuitability.VerdictsForPawn(facts, 0) : null;
            if (suitability != null)
            {
                suitability.Clear();
                foreach (var pair in verdicts!)
                    suitability.Add(pair.Key, pair.Value);
            }
            if (explanations != null)
                ExplainTarget(colony, worker, facts, target, verdicts!, explanations);
            return new PawnFixTarget(-1, target, changed);
        }

        public static int[] Recommend(ColonyView colony, MechanicalWorkerView worker)
            => Recommend(colony, worker, CapabilityFacts(colony, worker), preservePrimary: false);

        private static EngineContext CapabilityFacts(ColonyView colony, MechanicalWorkerView worker)
        {
            // Native effective skills, without human demand, learning or XP.
            var capabilityColony = new ColonyView { Roles = colony.Roles, Paths = colony.Paths };
            capabilityColony.Pawns.Add(worker.Facts);
            return new EngineContext(capabilityColony);
        }

        private static int[] Recommend(ColonyView colony, MechanicalWorkerView worker,
            EngineContext facts, bool preservePrimary)
        {
            var result = new List<int>();
            var seenWork = new HashSet<string>();
            List<Candidate> candidates = Candidates(colony, worker, facts);
            // Applying a combined human/robot preview must retain the accepted
            // primary work that supplied human coverage. An equivalent native
            // duty may replace its label, but never its executable work or pin.
            RoleView? accepted = preservePrimary ? PrimaryDuty(colony, worker) : null;
            if (accepted != null && !MeetsEquipmentRequirement(worker, accepted)
                && !IsProtected(worker, accepted)) accepted = null;
            if (accepted != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i].Role.Id == accepted.Id)
                    { accepted = BestFit(candidates[i], candidates, worker); break; }
                result.Add(accepted.Id);
                foreach (string giver in accepted.Coverage)
                    if (worker.CommittedGivers.Contains(giver)) seenWork.Add(giver);
            }
            foreach (Candidate candidate in candidates)
            {
                RoleView role = BestFit(candidate, candidates, worker);
                bool useful = false;
                foreach (string giver in role.Coverage)
                {
                    if (!worker.ExecutableGivers.Contains(giver)) continue;
                    if (!seenWork.Contains(giver)) useful = true;
                }
                if (!useful) continue;
                if (!result.Contains(role.Id)) result.Add(role.Id);
                foreach (string giver in role.Coverage)
                    if (worker.ExecutableGivers.Contains(giver)) seenWork.Add(giver);
            }
            // The template prefers broad duties during selection; newly found
            // fallbacks still belong at their normal priority when first assigned.
            // Existing workers keep their accepted execution order.
            if (worker.ActiveRoleIds.Count == 0)
            {
                var ordered = new List<Candidate>();
                foreach (Candidate candidate in candidates)
                    if (result.Contains(candidate.Role.Id)) ordered.Add(candidate);
                ordered.Sort(DefaultOrder);
                result.Clear();
                foreach (Candidate candidate in ordered) result.Add(candidate.Role.Id);
            }
            return result.ToArray();
        }

        private static List<Candidate> Candidates(ColonyView colony,
            MechanicalWorkerView worker, EngineContext facts)
        {
            var all = new List<Candidate>(colony.Roles.Count);
            IReadOnlyDictionary<int, long> positions = Ordering.BasePositions(colony.Roles, colony.OrderTemplate);
            for (int i = 0; i < colony.Roles.Count; i++)
            {
                RoleView role = colony.Roles[i];
                // Training bands gate human trainees. A mechanical worker's
                // skills are fixed and it never trains, so the band would only
                // demote it below work it performs natively; CanCover still
                // applies the band before it may replace human staffing.
                if (!Eligible(role) || facts.IsRecommendationExcluded(role)
                    || !facts.MeetsCapabilityRequirement(0, role)
                    || !MeetsEquipmentRequirement(worker, role)) continue;
                int executable = 0;
                foreach (string giver in role.Coverage)
                    if (worker.ExecutableGivers.Contains(giver)) executable++;
                // Capability policy governs required work types/skills. A native
                // miner need not support drilling too; only its actual givers run.
                // Full replacement of human staffing remains stricter in CanCover.
                if (executable > 0) all.Add(new Candidate(role, executable, positions[role.Id], i,
                    Preference(role.Id, worker.ActiveRoleIds, colony.OrderTemplate)));
            }
            all.Sort(SelectionOrder);
            return all;
        }

        private static int Preference(int roleId, IReadOnlyList<int> active, IReadOnlyList<int> template)
        {
            for (int i = 0; i < active.Count; i++) if (active[i] == roleId) return i;
            for (int i = 0; i < template.Count; i++) if (template[i] == roleId) return active.Count + i;
            return int.MaxValue;
        }

        private static RoleView BestFit(Candidate original, List<Candidate> candidates,
            MechanicalWorkerView worker)
        {
            if (original.Unsupported == 0 || IsProtected(worker, original.Role)) return original.Role;
            Candidate best = original;
            foreach (Candidate candidate in candidates)
            {
                if (candidate.Unsupported >= best.Unsupported
                    || candidate.Executable != original.Executable) continue;
                bool equal = true;
                foreach (string giver in original.Role.Coverage)
                    if (worker.ExecutableGivers.Contains(giver) && !candidate.Role.Coverage.Contains(giver))
                    { equal = false; break; }
                if (equal) best = candidate;
            }
            return best.Role;
        }

        private static bool IsProtected(MechanicalWorkerView worker, RoleView role)
        {
            if (role.HasRules || role.Blocker) return true;
            foreach (AssignmentView assignment in worker.Facts.Existing)
                if (assignment.RoleId == role.Id && assignment.Pinned) return true;
            return false;
        }

        private static bool MeetsEquipmentRequirement(MechanicalWorkerView worker, RoleView role)
            => !role.Hunting || worker.Facts.HasRangedWeapon;

        private static void ExplainTarget(ColonyView colony, MechanicalWorkerView worker,
            EngineContext facts, int[] target, Dictionary<int, SignalBucket> suitability,
            Dictionary<int, RoleRecommendationExplanation> explanations)
        {
            explanations.Clear();
            var selected = new HashSet<int>(target);
            var relevant = new HashSet<int>(target);
            foreach (AssignmentView assignment in worker.Facts.Existing) relevant.Add(assignment.RoleId);
            RoleView? primary = PrimaryDuty(colony, worker);
            foreach (int id in relevant)
            {
                RoleView? role = facts.RoleOf(id);
                if (role == null) continue;
                SignalBucket bucket = facts.BestSignal(0, role, out string skill, out _);
                var required = new List<string>();
                foreach (RoleSkillFact fact in facts.RequiredSkills(role)) required.Add(fact.SkillDefName);
                var explanation = new RoleRecommendationExplanation
                {
                    RoleId = id,
                    Recommended = selected.Contains(id),
                    SignalBucket = suitability[id],
                    BaseSignalBucket = bucket,
                    SignalSkillDefName = skill,
                    SignalSkillLevel = facts.SkillLevel(0, skill),
                    RequiredSkills = required.ToArray(),
                };
                if (explanation.Recommended)
                {
                    bool protectedAssignment = role.HasRules || role.Blocker;
                    foreach (AssignmentView assignment in worker.Facts.Existing)
                        if (assignment.RoleId == id && assignment.Pinned) protectedAssignment = true;
                    explanation.SpecialPickReason = worker.Facts.Incapacitated
                        ? SpecialPickReason.MechanicalIncapacitated
                        : protectedAssignment ? SpecialPickReason.Protected
                        : primary?.Id == id ? SpecialPickReason.MechanicalPrimary
                        : SpecialPickReason.MechanicalWork;
                }
                else if (!facts.MeetsCapabilityRequirement(0, role))
                    explanation.RejectReason = PickRejectReason.Incapable;
                else if (!MeetsEquipmentRequirement(worker, role))
                    explanation.RejectReason = PickRejectReason.HunterRequirementsNotMet;
                else if (Eligible(role) && !facts.IsRecommendationExcluded(role))
                {
                    bool any = false;
                    foreach (string giver in role.Coverage)
                    {
                        if (worker.ExecutableGivers.Contains(giver)) any = true;
                    }
                    explanation.RejectReason = !any ? PickRejectReason.Incapable : PickRejectReason.Covered;
                }
                explanations.Add(id, explanation);
            }
        }

        internal static Dictionary<int, int> BuildCoverage(ColonyView colony)
        {
            var result = new Dictionary<int, int>();
            foreach (MechanicalWorkerView worker in colony.MechanicalWorkers)
            {
                if (!worker.ProvidesService) continue;
                RoleView? primary = PrimaryDuty(colony, worker);
                // An active conditional duty occupies this worker too; do not
                // skip it and incorrectly credit a fallback as full staffing.
                if (primary == null || primary.HasRules) continue;
                var capabilityColony = new ColonyView { Roles = colony.Roles, Paths = colony.Paths };
                capabilityColony.Pawns.Add(worker.Facts);
                var facts = new EngineContext(capabilityColony);
                foreach (RoleView role in colony.Roles)
                    if (CanCover(worker, primary, role, facts))
                    {
                        result.TryGetValue(role.Id, out int previous);
                        result[role.Id] = previous + 1;
                    }
            }
            return result;
        }

        private static RoleView? PrimaryDuty(ColonyView colony, MechanicalWorkerView worker)
        {
            foreach (int id in worker.ActiveRoleIds)
                foreach (RoleView role in colony.Roles)
                    if (role.Id == id && !role.Blocker)
                    {
                        foreach (string giver in role.Coverage)
                            if (worker.CommittedGivers.Contains(giver)) return role;
                        break;
                    }
            return null;
        }

        private static bool CanCover(MechanicalWorkerView worker, RoleView primary, RoleView role, EngineContext facts)
        {
            if (role.Coverage.Count == 0 || role.HasRules || role.Blocker
                || !MeetsEquipmentRequirement(worker, role)) return false;
            bool full = true;
            foreach (string giver in role.Coverage)
                if (!primary.Coverage.Contains(giver) || !worker.ExecutableGivers.Contains(giver)
                    || !worker.CommittedGivers.Contains(giver)
                    || worker.PartialServiceGivers.Contains(giver))
                { full = false; break; }
            if (!full) return false;
            foreach (RoleWorkCapabilitySpec capability in role.WorkSpec.Capabilities)
                foreach (RoleWorkGiverSpec giver in capability.Givers)
                {
                    if (worker.Facts.WorkEvidence.Blocks(giver.WorkGiverDefName)) return false;
                    foreach (RoleWorkContentSpec content in giver.Contents)
                    {
                        if (worker.Facts.WorkEvidence.Blocks(giver.WorkGiverDefName, content.DefName)) return false;
                        foreach (RoleContentGate gate in content.Gates)
                            if (!worker.Facts.SkillLevels.TryGetValue(gate.SkillDefName, out int level)
                                || level < gate.MinimumLevel) return false;
                    }
                }
            // Current native skill must qualify for the covered service;
            // a low-tier robot cannot replace a high-skill path target.
            return facts.MeetsCapabilityRequirement(0, role)
                && PathActivation.TargetBandContains(facts, 0, role);
        }

        private static bool Eligible(RoleView role) => role.Enabled && role.Available
            && !role.ExcludedFromRecommendations && !role.Blocker && !role.HasRules
            && role.MemberRoleIds == null && role.Coverage.Count > 0;
    }
}
