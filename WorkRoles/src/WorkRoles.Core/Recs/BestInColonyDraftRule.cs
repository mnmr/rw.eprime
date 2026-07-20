using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Rule 5: under-covered wanted roles fill the absolute minHolders floor
    /// with the colony's best remaining pawns. The floor ignores band gating
    /// (bands gate interest, not need); Awful pawns are never eligible.
    /// Ranking: in-band first, then bucket descending, matched skill level,
    /// fewer candidate roles (spread), pawn index.
    public sealed class BestInColonyDraftRule : RecRule
    {
        public override string Id => "draft";
        public override RuleKind Kind => RuleKind.Colony;
        public override bool Relevant(EngineContext context) => context.Want.Count > 0;

        public override void Apply(EngineContext context)
        {
            var positions = context.BasePositions();
            foreach (var role in context.Colony.Roles
                         .Where(r => context.Want.ContainsKey(r.Id))
                         .OrderBy(r => positions[r.Id]).ThenBy(r => r.Id))
            {
                if (SkippedForCoverer(context, role)) continue;
                int want = context.Want[role.Id];
                int holders = context.AllocatedHoldersOf(role.Id);
                int openSlots = System.Math.Max(0, want - holders);

                var eligible = new List<(int pawn, SignalBucket bucket, string skill, int level, bool inBand)>();
                for (int i = 0; i < context.Colony.Pawns.Count; i++)
                {
                    if (context.CoversRole(i, role)) continue;
                    if (!context.FullyCapable(i, role)) continue;
                    if (role.Hunting && !context.Colony.Pawns[i].HasRangedWeapon) continue;
                    var bucket = context.BestSignal(i, role, out string skill, out _);
                    if (bucket == SignalBucket.Awful) continue;
                    eligible.Add((i, bucket, skill, context.SkillLevel(i, skill),
                        context.PassesBands(i, role)));
                }
                var ranked = eligible
                    .OrderByDescending(e => e.inBand)
                    .ThenByDescending(e => e.bucket)
                    .ThenByDescending(e => e.level)
                    .ThenBy(e => context.Candidates[e.pawn].Count)
                    .ThenBy(e => e.pawn)
                    .ToList();
                for (int rank = 0; rank < ranked.Count; rank++)
                {
                    var candidate = ranked[rank];
                    context.DraftRankings[candidate.pawn][role.Id] = new DraftRanking
                    {
                        Rank = rank + 1,
                        EligibleCount = ranked.Count,
                        OpenSlots = openSlots,
                        SkillDefName = candidate.skill,
                        SkillLevel = candidate.level,
                    };
                }

                foreach (var pick in ranked)
                {
                    if (holders >= want) break;
                    context.AddCandidate(pick.pawn, role.Id, new Reason
                    {
                        RuleId = Id, Bucket = pick.bucket,
                        SkillDefName = pick.skill, TowardRoleId = -1,
                    }, pick.bucket);
                    holders++;
                }
            }
        }

        /// Covered roles leave dealing to their coverer — unless needed
        /// (MinHolders >= 1) or a path member (their own low-band audience).
        private static bool SkippedForCoverer(EngineContext context, RoleView role)
        {
            if (role.MinHolders >= 1) return false;
            if (context.Colony.Paths.Any(p => p.RoleIds.Contains(role.Id))) return false;
            return context.AllocatedHoldersOf(role.Id) >= context.Want[role.Id];
        }
    }
}
