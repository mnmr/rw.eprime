using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Item quality rules for implants whose items carry a quality (Quality
    /// Bionics Remastered gives bionics one; Vanilla Genetics Expanded's
    /// hybrid implants have their own). Qualities are the game's
    /// QualityCategory values: 0 Awful … 6 Legendary.
    public static class ImplantQuality
    {
        public const int Lowest = 0;
        public const int Highest = 6;
        public const int None = -1;
        public const int AllQualities = (1 << (Highest + 1)) - 1;

        /// QualityCategory names, the save and plan-file contract.
        static readonly string[] Names =
            { "Awful", "Poor", "Normal", "Good", "Excellent", "Masterwork", "Legendary" };

        public static int Clamp(int quality) =>
            quality < Lowest ? Lowest : quality > Highest ? Highest : quality;

        public static string NameOf(int quality) => Names[Clamp(quality)];

        /// Parses a quality name (case-insensitive); false for anything else.
        public static bool TryParse(string? text, out int quality)
        {
            quality = Lowest;
            if (text == null) return false;
            for (int i = 0; i < Names.Length; i++)
                if (string.Equals(Names[i], text.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    quality = i;
                    return true;
                }
            return false;
        }

        /// Tolerance for efficiencies computed along different paths
        /// (multipliers, stage offsets): an implant exactly as good as the
        /// part still counts as not worse.
        const float Epsilon = 0.0001f;

        public static bool AtLeast(float efficiency, float floor) =>
            !float.IsNaN(efficiency) && !float.IsInfinity(efficiency)
            && efficiency >= floor - Epsilon;

        /// Each actual quality must qualify independently: mod settings can
        /// make a higher quality label less efficient than a lower one.
        public static int AcceptableQualities(IReadOnlyList<float> afterInstall,
            float recoverable, int planMinimum)
        {
            int accepted = 0;
            for (int q = Clamp(planMinimum); q <= Highest && q < afterInstall.Count; q++)
                if (AtLeast(afterInstall[q], recoverable)) accepted |= 1 << q;
            return accepted;
        }

        public static bool Accepts(int accepted, int quality) =>
            quality >= Lowest && quality <= Highest && (accepted & (1 << quality)) != 0;

        /// The lowest quality a slot accepts: at or above the plan's minimum
        /// and leaving the part no worse than its supplied recovery floor
        /// (an Awful bionic never replaces a healing arm, but fills a missing one).
        /// afterInstall[q] is the part's efficiency with the implant
        /// installed at quality q (one value per quality, all equal for an
        /// item without quality); current is the recoverable efficiency floor.
        /// None when no quality qualifies. A higher label is not a guarantee;
        /// callers choosing actual items must also check AcceptableQualities.
        public static int MinimumAcceptable(IReadOnlyList<float> afterInstall,
            float current, int planMinimum)
        {
            for (int q = Clamp(planMinimum); q <= Highest && q < afterInstall.Count; q++)
                if (AtLeast(afterInstall[q], current))
                    return q;
            return None;
        }

        /// The item to take from candidates listed in item-id order by
        /// quality: the highest quality at or above minimum (the lowest one
        /// when preferLowest), the oldest item among equals; -1 when none
        /// qualifies.
        public static int Choose(IReadOnlyList<int> qualities, int minimum,
            bool preferLowest, int acceptableQualities = AllQualities)
        {
            if (minimum == None) return -1;
            int pick = -1;
            for (int i = 0; i < qualities.Count; i++)
            {
                int q = qualities[i];
                if (q < minimum || !Accepts(acceptableQualities, q)) continue;
                if (pick < 0
                    || (preferLowest ? q < qualities[pick] : q > qualities[pick]))
                    pick = i;
            }
            return pick;
        }
    }
}
