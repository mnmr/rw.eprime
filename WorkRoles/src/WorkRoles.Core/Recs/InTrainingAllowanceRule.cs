using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Rule 7: before the draft, up to InTrainingAllowance of a needed role's
    /// floor slots may be reserved for a lower-band partner from a containing
    /// path — but ONLY the slots that would otherwise be filled by a below-band
    /// direct holder (substituting a trainee for an available in-band holder is
    /// pointless). Existing partner candidates consume the reservation first;
    /// the rest is drafted into the most advanced partner. Draft fills the
    /// remainder of the floor directly.
    public sealed class InTrainingAllowanceRule : RecRule
    {
        public override string Id => "allowance";
        public override RuleKind Kind => RuleKind.Colony;

        public override bool Relevant(EngineContext context)
            => context.Colony.Paths.Count > 0
            && context.Colony.Roles.Any(r => r.InTrainingAllowance > 0 && r.MinHolders >= 1);

        public override void Apply(EngineContext context)
        {
            var positions = Ordering.BasePositions(context.Colony.Roles, context.Colony.OrderTemplate);
            foreach (var role in context.Colony.Roles
                         .Where(r => r.MinHolders >= 1 && r.InTrainingAllowance > 0
                             && !context.Vetoed.Contains(r.Id))
                         .OrderBy(r => positions[r.Id]).ThenBy(r => r.Id))
            {
                if (!context.Want.TryGetValue(role.Id, out int want)) continue;
                int missing = want - context.HoldersOf(role.Id);
                if (missing <= 0) continue;

                // Lower-band partners across every containing path, most
                // advanced (highest band min) first.
                var partners = new List<(RoleView partner, int bandMin)>();
                foreach (var path in context.Colony.Paths)
                {
                    int entry = path.RoleIds.IndexOf(role.Id);
                    if (entry < 0) continue;
                    foreach (int i in PathMath.LowerBandEntries(path, entry))
                        if (context.RoleOf(path.RoleIds[i]) is RoleView partner
                            && !context.Vetoed.Contains(partner.Id)
                            && partners.All(p => p.partner.Id != partner.Id))
                            partners.Add((partner, path.BandMins[i]));
                }
                if (partners.Count == 0) continue;
                partners.Sort((a, b) => b.bandMin != a.bandMin
                    ? b.bandMin.CompareTo(a.bandMin)
                    : a.partner.Id.CompareTo(b.partner.Id));

                // Trainees only cover slots no in-band direct holder can fill:
                // reserve = min(allowance, floor slots left below-band).
                int inBandSupply = 0;
                for (int i = 0; i < context.Colony.Pawns.Count; i++)
                    if (!context.CoversRole(i, role) && context.Capable(i, role)
                        && context.PassesBands(i, role)
                        && context.BestSignal(i, role, out _, out _) != SignalBucket.Awful)
                        inBandSupply++;
                int budget = System.Math.Min(role.InTrainingAllowance,
                    System.Math.Max(0, missing - inBandSupply));
                if (budget <= 0) continue;

                // Existing trainees fill the reservation without new grants.
                var trainees = new HashSet<int>();
                for (int i = 0; i < context.Colony.Pawns.Count; i++)
                    if (!context.CoversRole(i, role)
                        && partners.Any(p => context.Candidates[i].ContainsKey(p.partner.Id)))
                        trainees.Add(i);
                budget -= System.Math.Min(budget, trainees.Count);

                foreach (var (partner, _) in partners)
                {
                    if (budget <= 0) break;
                    var eligible = new List<(int pawn, SignalBucket bucket, int level)>();
                    for (int i = 0; i < context.Colony.Pawns.Count; i++)
                    {
                        if (trainees.Contains(i) || context.CoversRole(i, role)) continue;
                        if (context.Candidates[i].ContainsKey(partner.Id)) continue;
                        if (!context.Capable(i, partner) || !context.PassesBands(i, partner)) continue;
                        var bucket = context.BestSignal(i, partner, out _, out _);
                        if (bucket == SignalBucket.Awful) continue;
                        eligible.Add((i, bucket, context.SkillLevel(i, partner.PrimarySkill)));
                    }
                    foreach (var pick in eligible
                                 .OrderByDescending(e => e.bucket)
                                 .ThenByDescending(e => e.level)
                                 .ThenBy(e => context.Candidates[e.pawn].Count)
                                 .ThenBy(e => e.pawn))
                    {
                        if (budget <= 0) break;
                        context.AddCandidate(pick.pawn, partner.Id, new Reason
                        {
                            RuleId = Id, Bucket = pick.bucket,
                            SkillDefName = partner.PrimarySkill, TowardRoleId = role.Id,
                        }, pick.bucket);
                        trainees.Add(pick.pawn);
                        budget--;
                    }
                }
            }
        }
    }
}
