using System.Collections.Generic;

namespace RimShared.Common
{
    /// Plain color value for palette planning; Core stays Unity-free.
    public readonly struct Rgba
    {
        public readonly float R, G, B, A;

        public Rgba(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// Swatch-slot convention: alpha below 0.5 marks an empty slot.
        public bool Defined => A >= 0.5f;

        /// Verse's IndistinguishableFrom: equal 32-bit quantization, or a
        /// channel-delta sum under 0.005.
        public bool Matches(Rgba other)
        {
            if (Byte(R) == Byte(other.R) && Byte(G) == Byte(other.G)
                && Byte(B) == Byte(other.B) && Byte(A) == Byte(other.A))
                return true;
            return Abs(R - other.R) + Abs(G - other.G)
                + Abs(B - other.B) + Abs(A - other.A) < 0.005f;
        }

        private static int Byte(float channel) =>
            (int)((channel < 0f ? 0f : channel > 1f ? 1f : channel) * 255f + 0.5f);

        private static float Abs(float value) => value < 0f ? -value : value;
    }

    /// One custom-swatch pick resolved into slot and role mutations.
    public sealed class SwatchPickPlan
    {
        /// The pick collapsed into an existing palette color: empty the slot.
        public bool ClearSlot;
        /// The pick defines or redefines the slot.
        public bool SetSlot;
        /// Canonical color for the slot and every recolor: the matched palette
        /// color when the pick collapsed, otherwise the pick itself.
        public Rgba Applied;
        /// Apply Applied to the edited role (empty-slot define flow).
        public bool RecolorEditedRole;
        /// Roles whose color was the slot's previous color: they follow the
        /// redefined slot so their color never drops out of the palette.
        public readonly List<int> RecolorRoleIds = new List<int>();
    }

    /// A global palette-coverage sweep resolved into slot and role mutations.
    public sealed class PaletteCoveragePlan
    {
        /// Orphan role colors gaining a custom slot, in claim order.
        public readonly List<(int slot, Rgba color)> DefineSlots =
            new List<(int, Rgba)>();
        /// Roles snapped to the nearest palette color: slot capacity ran out.
        public readonly List<(int roleId, Rgba color)> SnapRoles =
            new List<(int, Rgba)>();

        public bool IsEmpty => DefineSlots.Count == 0 && SnapRoles.Count == 0;
    }

    /// Closes the palette invariant globally: every custom role color must
    /// exist in the palette (standard swatch or defined custom slot). Orphans
    /// (imports, hand-edited files) claim free slots in role order; past
    /// capacity, the role snaps to the nearest palette color by squared RGB
    /// distance, alpha ignored (the def-color seeding rule).
    public static class PaletteCoverageEnforcer
    {
        public static PaletteCoveragePlan Plan(
            IReadOnlyList<Rgba> standardSwatches,
            IReadOnlyList<Rgba> customSwatches,
            int maxCustomSwatches,
            IReadOnlyList<(int id, Rgba color)> customColoredRoles)
        {
            var plan = new PaletteCoveragePlan();
            var slots = new List<Rgba>(customSwatches);
            for (int r = 0; r < customColoredRoles.Count; r++)
            {
                Rgba color = customColoredRoles[r].color;
                if (Covered(color, standardSwatches, slots)) continue;
                int free = FreeSlot(slots, maxCustomSwatches);
                if (free >= 0)
                {
                    while (slots.Count <= free) slots.Add(default);
                    slots[free] = color;
                    plan.DefineSlots.Add((free, color));
                }
                else
                    plan.SnapRoles.Add((customColoredRoles[r].id,
                        Nearest(color, standardSwatches, slots)));
            }
            return plan;
        }

        private static bool Covered(Rgba color,
            IReadOnlyList<Rgba> standard, List<Rgba> slots)
        {
            for (int i = 0; i < standard.Count; i++)
                if (color.Matches(standard[i])) return true;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Defined && color.Matches(slots[i])) return true;
            return false;
        }

        private static int FreeSlot(List<Rgba> slots, int maxSlots)
        {
            for (int i = 0; i < maxSlots; i++)
                if (i >= slots.Count || !slots[i].Defined) return i;
            return -1;
        }

        private static Rgba Nearest(Rgba color,
            IReadOnlyList<Rgba> standard, List<Rgba> slots)
        {
            Rgba best = default;
            float bestDist = float.MaxValue;
            for (int i = 0; i < standard.Count; i++)
                Consider(standard[i], color, ref best, ref bestDist);
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].Defined)
                    Consider(slots[i], color, ref best, ref bestDist);
            return best;
        }

        private static void Consider(Rgba candidate, Rgba color,
            ref Rgba best, ref float bestDist)
        {
            float dr = candidate.R - color.R;
            float dg = candidate.G - color.G;
            float db = candidate.B - color.B;
            float dist = dr * dr + dg * dg + db * db;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }
    }

    /// Resolves a color picked for a custom swatch slot. Invariant: applying
    /// the plan leaves every role the pick touched with a color present in
    /// the palette (a standard swatch or a defined custom slot), so the role
    /// editor always highlights the role's color.
    public static class SwatchPickPlanner
    {
        public static SwatchPickPlan Plan(
            Rgba picked,
            int slot,
            IReadOnlyList<Rgba> standardSwatches,
            IReadOnlyList<Rgba> customSwatches,
            bool applyToEditedRole,
            int editedRoleId,
            IReadOnlyList<(int id, Rgba color)> customColoredRoles)
        {
            var plan = new SwatchPickPlan { Applied = picked };

            // The slot being defined never matches against itself, or
            // re-accepting its current color would collapse the slot.
            bool matched = false;
            for (int i = 0; i < standardSwatches.Count && !matched; i++)
                if (picked.Matches(standardSwatches[i]))
                {
                    plan.Applied = standardSwatches[i];
                    matched = true;
                }
            for (int i = 0; i < customSwatches.Count && !matched; i++)
                if (i != slot && customSwatches[i].Defined
                    && picked.Matches(customSwatches[i]))
                {
                    plan.Applied = customSwatches[i];
                    matched = true;
                }

            Rgba oldColor = slot >= 0 && slot < customSwatches.Count
                ? customSwatches[slot] : default;
            if (matched)
                // No duplicate slots: an already-defined slot empties; an
                // empty slot simply stays a "+" picker.
                plan.ClearSlot = oldColor.Defined;
            else
                plan.SetSlot = true;

            // Redefining a shared swatch recolors every role painted with it
            // (mirrors palette import), keeping their palette highlight.
            if (oldColor.Defined && !oldColor.Matches(plan.Applied))
                for (int i = 0; i < customColoredRoles.Count; i++)
                    if (customColoredRoles[i].color.Matches(oldColor))
                        plan.RecolorRoleIds.Add(customColoredRoles[i].id);

            plan.RecolorEditedRole = applyToEditedRole
                && !plan.RecolorRoleIds.Contains(editedRoleId);
            return plan;
        }
    }
}
