using System.Collections.Generic;

namespace Implanner.Core
{
    /// Stable per-plan goal identities used by delivery latches, reservations
    /// and owned bills. Keys survive save/load and plan edits that do not
    /// recreate the goal (child ids are never reused). The composer and the
    /// parsers live together: the wire format is a save-file contract, and
    /// every consumer must interpret it identically.
    public static class GoalKeys
    {
        /// One selected implant slot.
        public static string ImplantSlot(int goalId, int ordinal) =>
            "i" + goalId + ":" + ordinal;

        /// Parses "i[goalId]:[ordinal]"; false for any other shape.
        public static bool TryParseImplantSlot(
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
        /// a latch or reservation key refers to.
        public static bool Contains(IReadOnlyList<ImplantGoal> goals, string key)
        {
            if (!TryParseImplantSlot(key, out int goalId, out int ordinal))
                return false;
            for (int i = 0; i < goals.Count; i++)
            {
                if (goals[i].Id != goalId) continue;
                IReadOnlyList<int> ordinals = goals[i].SlotOrdinals;
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
            if (!TryParseImplantSlot(key, out int goalId, out ordinal))
                return false;
            for (int i = 0; i < goals.Count; i++)
                if (goals[i].Id == goalId)
                {
                    goal = goals[i];
                    return true;
                }
            return false;
        }
    }
}
