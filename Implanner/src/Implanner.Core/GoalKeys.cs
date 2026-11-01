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
    /// collide and survive removing and re-adding the same pick. The slot
    /// is its PartAddress key. The composer and the parsers live together:
    /// the wire format is a save-file contract, and every consumer must
    /// interpret it identically. Numbers are unsigned decimal digits in the
    /// invariant culture on both sides.
    public static class GoalKeys
    {
        /// One selected implant slot: "p[planId]:[defName]:[Part#n]".
        public static string ImplantSlot(ImplantGoal goal, PartAddress slot) =>
            ImplantSlot(goal.PlanId, goal.ImplantDefName, slot);

        public static string ImplantSlot(
            int planId, string implantDefName, PartAddress slot) =>
            "p" + planId.ToString(CultureInfo.InvariantCulture)
                + ":" + implantDefName
                + ":" + slot.Key;

        /// An implant a part-wiping surgery pushed out, to go back on the
        /// same part: "r[partIndex]:[defName]". Never parses as a plan slot,
        /// so plan-goal code passes over it.
        public static string Reinstall(string implantDefName, int partIndex) =>
            "r" + partIndex.ToString(CultureInfo.InvariantCulture) + ":" + implantDefName;

        /// The removal half of a quality upgrade (QualityRebalance): the
        /// installed implant comes out so a better item of the same kind can
        /// go in, "u[partIndex]:[defName]". The install half is an ordinary
        /// reinstall record under Reinstall(defName, partIndex).
        public static string Upgrade(string implantDefName, int partIndex) =>
            "u" + partIndex.ToString(CultureInfo.InvariantCulture) + ":" + implantDefName;

        public static bool IsUpgrade(string key) =>
            key != null && key.Length > 0 && key[0] == 'u';

        /// Goal-level token (no slot) for deterministic ordering and
        /// grouping of whole goals; never persisted.
        public static string GoalToken(ImplantGoal goal) =>
            "p" + goal.PlanId.ToString(CultureInfo.InvariantCulture)
                + ":" + goal.ImplantDefName;

        /// Parses "p[planId]:[defName]:[Part#n]"; false for any other shape,
        /// including the retired formats (see MigrateLegacy). Def names
        /// cannot contain ':' (RimWorld validates def names), so the first
        /// and last separator bound the def name.
        public static bool TryParseImplantSlot(string key,
            out int planId, out string implantDefName, out PartAddress slot)
        {
            implantDefName = "";
            slot = default;
            if (!TryParseSegments(key, out planId, out int defStart,
                    out int defLength, out int slotStart))
                return false;
            if (!PartAddress.TryParse(key, slotStart, key.Length, out slot))
                return false;
            implantDefName = key.Substring(defStart, defLength);
            return true;
        }

        /// The shared natural-key parser: the plan id by digit loop, the def
        /// name and slot as segments so matchers never allocate. The slot
        /// segment must contain the address separator.
        static bool TryParseSegments(string key, out int planId,
            out int defStart, out int defLength, out int slotStart)
        {
            planId = 0;
            defStart = 0;
            defLength = 0;
            slotStart = 0;
            if (key == null || key.Length < 6 || key[0] != 'p') return false;
            int first = key.IndexOf(':');
            int last = key.LastIndexOf(':');
            if (first < 2 || last <= first + 1 || last >= key.Length - 1)
                return false;
            if (!TryParseDigits(key, 1, first, out planId)) return false;
            if (key.IndexOf(PartAddress.Separator, last + 1) < 0) return false;
            defStart = first + 1;
            defLength = last - first - 1;
            slotStart = last + 1;
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

        /// Parses the retired ordinal form "p[planId]:[defName]:[ordinal]"
        /// written before slots were part addresses.
        public static bool TryParseLegacyOrdinalSlot(string key,
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
            if (!TryParseDigits(key, 1, first, out planId)) return false;
            if (!TryParseDigits(key, last + 1, key.Length, out ordinal)) return false;
            implantDefName = key.Substring(first + 1, last - first - 1);
            return true;
        }

        /// Rewrites a persisted key to the current format. A retired
        /// "i[goalId]:[ordinal]" key resolves its goal through the legacy
        /// goal map (FINAL plan ids, built after
        /// PlannerModel.NormalizeLoadedIds); a retired
        /// "p[planId]:[defName]:[ordinal]" key keeps its goal. Either
        /// ordinal becomes a part address through legacySlots (game data:
        /// the addresses the ordinal denoted, one per body kind); a key names
        /// one slot, so it takes the first. Should the key's pawn have the
        /// ordinal on another body kind, the reconciler retires the entry as
        /// stale and pursues the migrated goal afresh. A current key passes
        /// through unchanged (same instance). Null when the key's goal or
        /// slot no longer resolves: the entry is dropped.
        public static string? MigrateLegacy(string key,
            IReadOnlyDictionary<int, LegacyGoalRef>? legacyGoals,
            Func<string, int, IReadOnlyList<PartAddress>>? legacySlots)
        {
            int planId;
            string defName;
            int ordinal;
            if (TryParseLegacyImplantSlot(key, out int goalId, out ordinal))
            {
                if (legacyGoals == null
                    || !legacyGoals.TryGetValue(goalId, out LegacyGoalRef owner))
                    return null;
                planId = owner.PlanId;
                defName = owner.ImplantDefName;
            }
            else if (!TryParseLegacyOrdinalSlot(key, out planId, out defName, out ordinal))
            {
                return key;
            }
            IReadOnlyList<PartAddress>? slots = legacySlots?.Invoke(defName, ordinal);
            return slots != null && slots.Count > 0 && !slots[0].IsLegacy
                ? ImplantSlot(planId, defName, slots[0])
                : null;
        }

        /// Whether the effective goal list still contains the exact goal slot
        /// a reservation key refers to. Allocation-free (tick path).
        public static bool Contains(IReadOnlyList<ImplantGoal> goals, string key)
        {
            if (!TryParseSegments(key, out int planId, out int defStart,
                    out int defLength, out int slotStart))
                return false;
            ImplantGoal? goal = FindGoal(goals, key, planId, defStart, defLength);
            return goal != null && SelectedIndex(goal, key, slotStart) >= 0;
        }

        /// Resolves the goal a key refers to in the effective goal list. The
        /// slot is parsed but not required to be selected — callers that
        /// need selection membership use Contains. Allocation-free when the
        /// slot is selected (the slot instance comes from the goal); an
        /// unselected slot is parsed from the key.
        public static bool TryResolveImplantSlot(IReadOnlyList<ImplantGoal> goals,
            string key, out ImplantGoal goal, out PartAddress slot)
        {
            goal = null!;
            slot = default;
            if (!TryParseSegments(key, out int planId, out int defStart,
                    out int defLength, out int slotStart))
                return false;
            ImplantGoal? found = FindGoal(goals, key, planId, defStart, defLength);
            if (found == null) return false;
            int selected = SelectedIndex(found, key, slotStart);
            if (selected >= 0)
                slot = found.Slots[selected];
            else if (!PartAddress.TryParse(key, slotStart, key.Length, out slot))
                return false;
            goal = found;
            return true;
        }

        static ImplantGoal? FindGoal(IReadOnlyList<ImplantGoal> goals, string key,
            int planId, int defStart, int defLength)
        {
            for (int i = 0; i < goals.Count; i++)
                if (goals[i].PlanId == planId
                    && DefNameMatches(key, defStart, defLength, goals[i].ImplantDefName))
                    return goals[i];
            return null;
        }

        static int SelectedIndex(ImplantGoal goal, string key, int slotStart)
        {
            IReadOnlyList<PartAddress> slots = goal.Slots;
            for (int j = 0; j < slots.Count; j++)
                if (slots[j].KeyEquals(key, slotStart, key.Length - slotStart))
                    return j;
            return -1;
        }

        static bool DefNameMatches(string key, int defStart, int defLength, string defName) =>
            defName.Length == defLength
            && string.CompareOrdinal(key, defStart, defName, 0, defLength) == 0;
    }
}
