using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Stable goal-slot identities used by reservations and owned bills.
    /// A goal's identity is natural — the owning plan plus the implant kind
    /// (one goal per kind per plan by construction) — so keys cannot
    /// collide, need no allocation, and survive removing and re-adding the
    /// same pick. The composer and the parsers live together: the wire
    /// format is a save-file contract, and every consumer must interpret it
    /// identically.
    public static class GoalKeys
    {
        /// One selected implant slot: "p[planId]:[defName]:[ordinal]".
        public static string ImplantSlot(ImplantGoal goal, int ordinal) =>
            ImplantSlot(goal.PlanId, goal.ImplantDefName, ordinal);

        public static string ImplantSlot(
            int planId, string implantDefName, int ordinal) =>
            "p" + planId + ":" + implantDefName + ":" + ordinal;

        /// Goal-level token (no slot) for deterministic ordering and
        /// grouping of whole goals; never persisted.
        public static string GoalToken(ImplantGoal goal) =>
            "p" + goal.PlanId + ":" + goal.ImplantDefName;

        /// Parses "p[planId]:[defName]:[ordinal]"; false for any other
        /// shape, including the retired legacy format (see
        /// TryParseLegacyImplantSlot). Def names cannot contain ':'
        /// (RimWorld validates def names), so the first and last separator
        /// bound the def name.
        public static bool TryParseImplantSlot(string key,
            out int planId, out string implantDefName, out int ordinal)
        {
            planId = 0;
            implantDefName = "";
            ordinal = -1;
            if (key == null || key.Length < 6 || key[0] != 'p') return false;
            int first = key.IndexOf(':');
            int last = key.LastIndexOf(':');
            if (first < 2 || last <= first + 1 || last >= key.Length - 1)
                return false;
            if (!int.TryParse(key.Substring(1, first - 1), out planId))
                return false;
            if (!int.TryParse(key.Substring(last + 1), out ordinal))
                return false;
            implantDefName = key.Substring(first + 1, last - first - 1);
            return true;
        }

        /// Parses the retired "i[goalId]:[ordinal]" format so loading can
        /// migrate keys persisted before goals carried natural identities.
        public static bool TryParseLegacyImplantSlot(
            string key, out int goalId, out int ordinal)
        {
            goalId = 0;
            ordinal = -1;
            if (key == null || key.Length < 4 || key[0] != 'i') return false;
            int separator = key.IndexOf(':');
            if (separator < 2) return false;
            if (!int.TryParse(key.Substring(1, separator - 1), out goalId))
                return false;
            return int.TryParse(key.Substring(separator + 1), out ordinal);
        }

        /// Whether the effective goal list still contains the exact goal slot
        /// a reservation key refers to.
        public static bool Contains(IReadOnlyList<ImplantGoal> goals, string key)
        {
            if (!TryParseImplantSlot(key,
                    out int planId, out string defName, out int ordinal))
                return false;
            for (int i = 0; i < goals.Count; i++)
            {
                ImplantGoal goal = goals[i];
                if (goal.PlanId != planId
                    || !string.Equals(goal.ImplantDefName, defName,
                        StringComparison.Ordinal))
                    continue;
                IReadOnlyList<int> ordinals = goal.SlotOrdinals;
                for (int j = 0; j < ordinals.Count; j++)
                    if (ordinals[j] == ordinal)
                        return true;
                return false;
            }
            return false;
        }

        /// Resolves the goal a key refers to in the effective goal list. The
        /// ordinal is parsed but not required to be selected — callers that
        /// need selection membership use Contains.
        public static bool TryResolveImplantSlot(IReadOnlyList<ImplantGoal> goals,
            string key, out ImplantGoal goal, out int ordinal)
        {
            goal = null!;
            if (!TryParseImplantSlot(key,
                    out int planId, out string defName, out ordinal))
                return false;
            for (int i = 0; i < goals.Count; i++)
                if (goals[i].PlanId == planId
                    && string.Equals(goals[i].ImplantDefName, defName,
                        StringComparison.Ordinal))
                {
                    goal = goals[i];
                    return true;
                }
            return false;
        }
    }
}
