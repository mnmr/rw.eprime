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
        public static PawnFixTarget BuildFixTarget(ColonyView colony, MechanicalWorkerView worker)
        {
            var ids = new List<int>(Recommend(colony, worker, preservePrimary: true));
            var roles = new Dictionary<int, RoleView>();
            foreach (RoleView role in colony.Roles) roles[role.Id] = role;
            foreach (AssignmentView assignment in worker.Facts.Existing)
                if (roles.TryGetValue(assignment.RoleId, out RoleView role)
                    && (assignment.Pinned || role.HasRules || role.Blocker)
                    && !ids.Contains(assignment.RoleId)) ids.Add(assignment.RoleId);
            int[] target = Ordering.PreserveProtectedOrder(roles, worker.Facts.Existing, ids);
            bool changed = target.Length != worker.Facts.Existing.Count;
            for (int i = 0; i < target.Length && !changed; i++)
                changed = target[i] != worker.Facts.Existing[i].RoleId;
            return new PawnFixTarget(-1, target, changed);
        }

        public static int[] Recommend(ColonyView colony, MechanicalWorkerView worker)
            => Recommend(colony, worker, preservePrimary: false);

        private static int[] Recommend(ColonyView colony, MechanicalWorkerView worker, bool preservePrimary)
        {
            var result = new List<int>();
            var seenWork = new HashSet<string>();
            // Applying a combined human/robot preview must retain the accepted
            // duty that supplied its human coverage. Exclusions veto new
            // suggestions; changing this explicit primary remains a user edit.
            RoleView? accepted = preservePrimary ? PrimaryDuty(colony, worker) : null;
            if (accepted != null)
            {
                result.Add(accepted.Id);
                foreach (string giver in accepted.Coverage)
                    if (worker.CommittedGivers.Contains(giver)) seenWork.Add(giver);
            }
            // Native effective skills, without passions, XP, or training paths.
            var capabilityColony = new ColonyView { Roles = colony.Roles, Paths = colony.Paths };
            capabilityColony.Pawns.Add(worker.Facts);
            var facts = new EngineContext(capabilityColony);
            var order = new List<int>(worker.ActiveRoleIds);
            foreach (int id in colony.OrderTemplate) if (!order.Contains(id)) order.Add(id);
            foreach (int id in order)
            {
                if (accepted?.Id == id) continue;
                RoleView? role = facts.RoleOf(id);
                if (role == null || !Eligible(role) || facts.IsRecommendationExcluded(role)
                    || !facts.MeetsCapabilityRequirement(0, role)
                    || !PathActivation.TargetBandContains(facts, 0, role)) continue;
                bool useful = false;
                bool complete = true;
                foreach (string giver in role.Coverage)
                {
                    if (!worker.ExecutableGivers.Contains(giver)) { complete = false; continue; }
                    if (!seenWork.Contains(giver)) useful = true;
                }
                if (!useful || !complete && role.UsesSkills && !role.PartialCapability) continue;
                result.Add(role.Id);
                foreach (string giver in role.Coverage)
                    if (worker.ExecutableGivers.Contains(giver)) seenWork.Add(giver);
            }
            return result.ToArray();
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
            if (role.Coverage.Count == 0 || role.HasRules || role.Blocker) return false;
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
            && role.MemberRoleIds == null && role.Coverage.Count > 0
            && (!role.AutoAssign || role.TemplateDefName == "WS_Grunt");
    }
}
