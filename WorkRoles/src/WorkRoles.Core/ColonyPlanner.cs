using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// One pawn as colony planning sees it.
    public class PlanPawn
    {
        public RecPawn Rec = new RecPawn();
        public List<PlannedAssignment> Existing = new List<PlannedAssignment>();
        public bool FireFear;
    }

    public enum PlanReason { Hunter, Essential, Coverage, FireFear }

    public struct PlanGrant
    {
        public int PawnIndex;
        public int RoleId;
        public PlanReason Reason;
        public int EssentialRank; // index into the essentials table; -1 = the doctoring backup
    }

    public class ColonyPlanResult
    {
        public List<List<int>> VirtualSets = new List<List<int>>();
        public List<List<int>> Promoted = new List<List<int>>();  // essential grants, rank order; null when none
        public List<int> HunterTiers = new List<int>();           // -1 = not a hunter
        public List<bool> FireGranted = new List<bool>();
        public List<PlanGrant> Grants = new List<PlanGrant>();
    }

    /// The colony-wide passes of Fix My Colony (per-pawn ordering is
    /// TargetPlanner's job):
    /// 1. Virtual set per pawn: the autoAssign catalog roles (catalog order) plus
    ///    the pawn's assigned rule-carrying roles.
    /// 2. Coverage: every enabled, rule-free, non-auto, skill-associated role is
    ///    dealt to the best eligible pawns (gates pass; ranked by matched skill,
    ///    then passion, then fewest virtual roles). The deal count scales with
    ///    colony size (one unit per 6 colonists): MinHolders -1 (auto) wants one
    ///    unit, N wants N units, 0 is never dealt (interest-driven only).
    ///    Sub-roles are skipped in favor of their coverer unless gated/essential/
    ///    Hunter. Hunter is exempt from top-N: every gun carrier hunts, tiered by
    ///    Shooting (&lt;15 / &lt;19 / 19+), with at least one tier-0 hunter.
    /// 3. Doctoring floor: at least TWO pawns able to tend — two doctors or a
    ///    doctor/medic pair; the backup prefers a Doctor-band passer, then a Medic
    ///    trainee, then the best pawn by Medicine with gates waived.
    /// 4. Fire safety: fire-fearing pawns get the firefighting blocker.
    public static class ColonyPlanner
    {
        public static ColonyPlanResult Compute(
            IReadOnlyList<RecRole> catalog,
            IReadOnlyList<PlanPawn> pawns,
            IReadOnlyDictionary<string, int> skillMaxLevels,
            IReadOnlyDictionary<string, IReadOnlyList<string>> workTypeSkills,
            IReadOnlyDictionary<int, int> essentialRankByRoleId,
            int hunterRoleId, int doctorRoleId, int medicRoleId, int fireBlockerRoleId)
        {
            var byId = catalog.ToDictionary(r => r.Id);
            RecRole RoleOf(int id) => byId.TryGetValue(id, out var role) ? role : null;

            var result = new ColonyPlanResult();
            var essentialGrants = new Dictionary<int, List<int>>(); // pawn index -> role ids
            var hunterTiers = new Dictionary<int, int>();

            // Pass 1: virtual sets.
            var virtualSets = new List<List<int>>();
            foreach (var pawn in pawns)
            {
                var ids = new List<int>();
                foreach (var role in catalog)
                    if (role.AutoAssign) ids.Add(role.Id);
                foreach (var a in pawn.Existing)
                {
                    var role = RoleOf(a.RoleId);
                    if (role != null && role.HasRules && !ids.Contains(role.Id)) ids.Add(role.Id);
                }
                virtualSets.Add(ids);
            }

            void Grant(int pawnIndex, int roleId, PlanReason reason, int essentialRank = int.MinValue)
                => result.Grants.Add(new PlanGrant
                { PawnIndex = pawnIndex, RoleId = roleId, Reason = reason, EssentialRank = essentialRank });

            List<string> RelevantSkills(RecRole role) => role.WorkTypes
                .SelectMany(wt => workTypeSkills.TryGetValue(wt, out var skills)
                    ? skills : (IReadOnlyList<string>)new List<string>())
                .Distinct()
                .ToList();

            bool Capable(PlanPawn pawn, RecRole role) =>
                role.WorkTypes.Any(pawn.Rec.CapableWorkTypes.Contains);

            // Pass 2: coverage.
            int coverage = System.Math.Max(1, (pawns.Count + 5) / 6);
            foreach (var role in catalog)
            {
                if (!role.Enabled || role.HasRules || role.AutoAssign || role.Blocker || role.Managed) continue;
                if (role.MinHolders == 0) continue; // never dealt: interest-driven only
                var relevantSkills = RelevantSkills(role);
                if (relevantSkills.Count == 0) continue; // not skill-associated
                // Sub-roles are not dealt — their coverer is — unless gated
                // (training roles have their own low-skill audience) or resolved as
                // an essential/Hunter (those must be dealt to keep their guarantee).
                if (!role.Gated && !essentialRankByRoleId.ContainsKey(role.Id) && role.Id != hunterRoleId
                    && catalog.Any(o => o.Enabled && !o.HasRules
                        && CoverageMath.MakesRedundant(o.Coverage, o.Id, role.Coverage, role.Id)))
                    continue;

                // Hunter ignores top-N: EVERY pawn with a ranged weapon hunts, but at
                // a skill-dependent position, and at least one hunter is placed
                // before essentials so there's food on the table.
                if (role.Id == hunterRoleId)
                {
                    var shooters = new List<(int index, int level, int passion)>();
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        var pawn = pawns[i];
                        if (!Capable(pawn, role)) continue;
                        if (!pawn.Rec.HasRangedWeapon) continue;
                        int level = pawn.Rec.SkillLevels.TryGetValue("Shooting", out var l) ? l : 0;

                        var ids = virtualSets[i];
                        bool has = ids.Contains(role.Id);
                        if (!has && !ids.Any(id => RoleOf(id) is RecRole covering
                                && CoverageMath.MakesRedundant(covering.Coverage, covering.Id, role.Coverage, role.Id)))
                        {
                            ids.Add(role.Id);
                            has = true;
                        }
                        if (!has) continue;

                        int passion = pawn.Rec.PassionScores.TryGetValue("Shooting", out var p) ? p : 0;
                        shooters.Add((i, level, passion));
                        hunterTiers[i] = level < 15 ? 0 : level < 19 ? 1 : 2;
                        Grant(i, role.Id, PlanReason.Hunter);
                    }
                    if (shooters.Count > 0 && !hunterTiers.ContainsValue(0))
                    {
                        var best = shooters
                            .OrderByDescending(s => s.level)
                            .ThenByDescending(s => s.passion)
                            .First();
                        hunterTiers[best.index] = 0;
                    }
                    continue;
                }

                var eligible = new List<(int index, int level, int passion, int load)>();
                for (int i = 0; i < pawns.Count; i++)
                {
                    var pawn = pawns[i];
                    if (!Capable(pawn, role)) continue;
                    if (!RecommendationEngine.PassesGates(role, pawn.Rec, skillMaxLevels)) continue;
                    int level = 0, passion = 0;
                    foreach (var skill in relevantSkills)
                    {
                        if (pawn.Rec.SkillLevels.TryGetValue(skill, out int l) && l > level) level = l;
                        if (pawn.Rec.PassionScores.TryGetValue(skill, out int p) && p > passion) passion = p;
                    }
                    eligible.Add((i, level, passion, virtualSets[i].Count));
                }

                int want = role.MinHolders > 0 ? role.MinHolders * coverage : coverage;
                int holders = virtualSets.Count(ids => ids.Contains(role.Id));
                foreach (var candidate in eligible
                    .OrderByDescending(t => t.level)
                    .ThenByDescending(t => t.passion)
                    .ThenBy(t => t.load))
                {
                    if (holders >= want) break;
                    var ids = virtualSets[candidate.index];
                    if (ids.Contains(role.Id)) continue;
                    if (ids.Any(id => RoleOf(id) is RecRole covering
                            && CoverageMath.MakesRedundant(covering.Coverage, covering.Id, role.Coverage, role.Id)))
                        continue;
                    ids.Add(role.Id);
                    holders++;
                    if (essentialRankByRoleId.TryGetValue(role.Id, out int rank))
                    {
                        AddEssentialGrant(essentialGrants, candidate.index, role.Id);
                        Grant(candidate.index, role.Id, PlanReason.Essential, rank);
                    }
                    else
                    {
                        Grant(candidate.index, role.Id, PlanReason.Coverage);
                    }
                }
            }

            // Pass 3: doctoring redundancy floor.
            if (doctorRoleId != -1 || medicRoleId != -1)
            {
                var doctorRole = RoleOf(doctorRoleId);
                bool ProvidesDoctoring(int id)
                {
                    if (doctorRoleId != -1 && id == doctorRoleId) return true;
                    if (medicRoleId != -1 && id == medicRoleId) return true;
                    if (doctorRole == null) return false;
                    var covering = RoleOf(id);
                    return covering != null && !covering.Blocker
                        && CoverageMath.CoversOrMatches(covering.Coverage, doctorRole.Coverage);
                }

                int doctoring = virtualSets.Count(ids => ids.Any(ProvidesDoctoring));
                if (doctoring == 1) // 0 means nobody can tend at all; nothing to back up
                {
                    var ranked = new List<(int index, int level, int passion, int load)>();
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        var pawn = pawns[i];
                        if (!pawn.Rec.CapableWorkTypes.Contains("Doctor")) continue;
                        if (virtualSets[i].Any(ProvidesDoctoring)) continue;
                        int level = pawn.Rec.SkillLevels.TryGetValue("Medicine", out var l) ? l : 0;
                        int passion = pawn.Rec.PassionScores.TryGetValue("Medicine", out var p) ? p : 0;
                        ranked.Add((i, level, passion, virtualSets[i].Count));
                    }
                    ranked = ranked
                        .OrderByDescending(t => t.level)
                        .ThenByDescending(t => t.passion)
                        .ThenBy(t => t.load)
                        .ToList();

                    int backup = -1;
                    int backupRoleId = -1;
                    if (doctorRole != null)
                    {
                        foreach (var c in ranked)
                            if (RecommendationEngine.PassesGates(doctorRole, pawns[c.index].Rec, skillMaxLevels))
                            { backup = c.index; break; }
                    }
                    if (backup >= 0)
                        backupRoleId = doctorRoleId;
                    else if (medicRoleId != -1 && RoleOf(medicRoleId) is RecRole medicRole)
                    {
                        foreach (var c in ranked)
                            if (RecommendationEngine.PassesGates(medicRole, pawns[c.index].Rec, skillMaxLevels))
                            { backup = c.index; break; }
                        if (backup < 0 && ranked.Count > 0) backup = ranked[0].index; // gates waived
                        backupRoleId = medicRoleId;
                    }
                    else if (ranked.Count > 0)
                    {
                        backup = ranked[0].index; // no medic-style role: waive Doctor's gate
                        backupRoleId = doctorRoleId;
                    }

                    if (backup >= 0 && backupRoleId != -1)
                    {
                        virtualSets[backup].Add(backupRoleId);
                        AddEssentialGrant(essentialGrants, backup, backupRoleId);
                        Grant(backup, backupRoleId, PlanReason.Essential, -1);
                    }
                }
            }

            // Pass 4: fire safety.
            var fireGranted = new bool[pawns.Count];
            if (fireBlockerRoleId != -1 && RoleOf(fireBlockerRoleId) != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    if (!pawns[i].FireFear) continue;
                    if (!virtualSets[i].Contains(fireBlockerRoleId))
                        virtualSets[i].Add(fireBlockerRoleId);
                    fireGranted[i] = true;
                    Grant(i, fireBlockerRoleId, PlanReason.FireFear);
                }
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                result.VirtualSets.Add(virtualSets[i]);
                result.Promoted.Add(essentialGrants.TryGetValue(i, out var granted)
                    ? granted.OrderBy(id => essentialRankByRoleId.TryGetValue(id, out var rank)
                        ? rank : int.MaxValue).ToList()
                    : null);
                result.HunterTiers.Add(hunterTiers.TryGetValue(i, out var tier) ? tier : -1);
                result.FireGranted.Add(fireGranted[i]);
            }
            return result;
        }

        private static void AddEssentialGrant(Dictionary<int, List<int>> grants, int pawnIndex, int roleId)
        {
            if (!grants.TryGetValue(pawnIndex, out var list))
                grants[pawnIndex] = list = new List<int>();
            list.Add(roleId);
        }
    }
}
