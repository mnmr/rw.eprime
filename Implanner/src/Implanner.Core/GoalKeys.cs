using System;
using System.Collections.Generic;
using System.Globalization;

namespace Implanner.Core
{
    /// The owner of a retired per-goal id: the FINAL id of the plan the goal
    /// belongs to plus the implant kind. Built by the load path only after
    /// PlannerModel.NormalizeLoadedIds has run, because a re-idded duplicate
    /// plan changes the plan id its legacy keys must migrate onto.
    public readonly struct LegacyGoalRef
    {
        public LegacyGoalRef(int planId, string implantDefName)
        {
            PlanId = planId;
            ImplantDefName = implantDefName;
        }

        public int PlanId { get; }
        public string ImplantDefName { get; }
    }

    /// Stable goal-slot identities used by reservations and owned bills.
    /// A goal's identity is natural — the owning plan plus the implant kind
    /// (one goal per kind per plan by construction) — so keys cannot
    /// collide, need no allocation, and survive removing and re-adding the
    /// same pick. The composer and the parsers live together: the wire
    /// format is a save-file contract, and every consumer must interpret it
    /// identically. Numbers are unsigned decimal digits in the invariant
    /// culture on both sides.
    public static class GoalKeys
    {
        /// One selected implant slot: "p[planId]:[defName]:[ordinal]".
        public static string ImplantSlot(ImplantGoal goal, int ordinal) =>
            ImplantSlot(goal.PlanId, goal.ImplantDefName, ordinal);

        public static string ImplantSlot(
            int planId, string implantDefName, int ordinal) =>
            "p" + planId.ToString(CultureInfo.InvariantCulture)
                + ":" + implantDefName
                + ":" + ordinal.ToString(CultureInfo.InvariantCulture);

        /// Goal-level token (no slot) for deterministic ordering and
        /// grouping of whole goals; never persisted.
        public static string GoalToken(ImplantGoal goal) =>
            "p" + goal.PlanId.ToString(CultureInfo.InvariantCulture)
                + ":" + goal.ImplantDefName;

        /// Parses "p[planId]:[defName]:[ordinal]"; false for any other
        /// shape, including the retired legacy format (see
        /// TryParseLegacyImplantSlot). Def names cannot contain ':'
        /// (RimWorld validates def names), so the first and last separator
        /// bound the def name.
        public static bool TryParseImplantSlot(string key,
            out int planId, out string implantDefName, out int ordinal)
        {
            implantDefName = "";
            if (!TryParseSegments(key, out planId, out int defStart,
                    out int defLength, out ordinal))
                return false;
            implantDefName = key.Substring(defStart, defLength);
            return true;
        }

        /// The shared natural-key parser: numbers by digit loop, the def
        /// name as a segment (start, length) so matchers never allocate.
        static bool TryParseSegments(string key, out int planId,
            out int defStart, out int defLength, out int ordinal)
        {
            planId = 0;
            defStart = 0;
            defLength = 0;
            ordinal = -1;
            if (key == null || key.Length < 6 || key[0] != 'p') return false;
            int first = key.IndexOf(':');
            int last = key.LastIndexOf(':');
            if (first < 2 || last <= first + 1 || last >= key.Length - 1)
                return false;
            if (!TryParseDigits(key, 1, first, out planId)) return false;
            if (!TryParseDigits(key, last + 1, key.Length, out ordinal)) return false;
            defStart = first + 1;
            defLength = last - first - 1;
            return true;
        }

        /// Unsigned decimal digits in key[start, end); no sign, whitespace,
        /// or overflow.
        static bool TryParseDigits(string key, int start, int end, out int value)
        {
            value = 0;
            if (start >= end) return false;
            for (int i = start; i < end; i++)
            {
                int digit = key[i] - '0';
                if (digit < 0 || digit > 9) return false;
                if (value > (int.MaxValue - digit) / 10) return false;
                value = value * 10 + digit;
            }
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
            if (!int.TryParse(key.Substring(1, separator - 1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out goalId))
                return false;
            return int.TryParse(key.Substring(separator + 1), NumberStyles.None,
                CultureInfo.InvariantCulture, out ordinal);
        }

        /// Rewrites a persisted key to the natural format: a retired
        /// "i[goalId]:[ordinal]" key becomes "p[planId]:[defName]:[ordinal]"
        /// through the legacy goal map; a natural key passes through
        /// unchanged (same instance); a legacy key whose goal id is unmapped
        /// yields null (the goal no longer exists, so the entry is dropped).
        /// The map must carry FINAL plan ids (built after
        /// PlannerModel.NormalizeLoadedIds).
        public static string? MigrateLegacy(string key,
            IReadOnlyDictionary<int, LegacyGoalRef>? legacyGoals)
        {
            if (!TryParseLegacyImplantSlot(key, out int goalId, out int ordinal))
                return key;
            if (legacyGoals == null
                || !legacyGoals.TryGetValue(goalId, out LegacyGoalRef owner))
                return null;
            return ImplantSlot(owner.PlanId, owner.ImplantDefName, ordinal);
        }

        /// Whether the effective goal list still contains the exact goal slot
        /// a reservation key refers to. Allocation-free (tick path).
        public static bool Contains(IReadOnlyList<ImplantGoal> goals, string key)
        {
            if (!TryParseSegments(key, out int planId, out int defStart,
                    out int defLength, out int ordinal))
                return false;
            for (int i = 0; i < goals.Count; i++)
            {
                ImplantGoal goal = goals[i];
                if (goal.PlanId != planId
                    || !DefNameMatches(key, defStart, defLength, goal.ImplantDefName))
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
        /// need selection membership use Contains. Allocation-free.
        public static bool TryResolveImplantSlot(IReadOnlyList<ImplantGoal> goals,
            string key, out ImplantGoal goal, out int ordinal)
        {
            goal = null!;
            if (!TryParseSegments(key, out int planId, out int defStart,
                    out int defLength, out ordinal))
                return false;
            for (int i = 0; i < goals.Count; i++)
                if (goals[i].PlanId == planId
                    && DefNameMatches(key, defStart, defLength, goals[i].ImplantDefName))
                {
                    goal = goals[i];
                    return true;
                }
            return false;
        }

        static bool DefNameMatches(string key, int defStart, int defLength, string defName) =>
            defName.Length == defLength
            && string.CompareOrdinal(key, defStart, defName, 0, defLength) == 0;
    }
}
