using HarmonyLib;
using RimShared.UiLib;
using Verse;

namespace QualityJobs
{
    // StaticConstructorOnStartup ensures FieldRefAccess failures surface at game load
    // (during static field initialization) rather than silently deferring to first use mid-play.
    [StaticConstructorOnStartup]
    public static class UftAuthor
    {
        // creatorInt is declared as `private Pawn creatorInt` in UnfinishedThing (nullable-oblivious).
        // We declare the FieldRef with Pawn? so that assigning null is warning-free on our side.
        private static readonly AccessTools.FieldRef<UnfinishedThing, Pawn?> CreatorRef =
            AccessTools.FieldRefAccess<UnfinishedThing, Pawn?>("creatorInt");

        // creatorName is declared as `private string creatorName` in UnfinishedThing.
        // The RimWorld field is nullable-oblivious; string (not string?) matches the runtime type.
        private static readonly AccessTools.FieldRef<UnfinishedThing, string> CreatorNameRef =
            AccessTools.FieldRefAccess<UnfinishedThing, string>("creatorName");

        /// creatorName is scribed sim state written from the gate on every MP
        /// client: it must be language-invariant or clients with different
        /// languages diverge their saves. The inspector resolves this marker
        /// through DisplayName without changing the persisted field.
        private const string ReservedAuthorLabel = "reserved (Quality Jobs)";

        // Owner: process/current language. Key: language revision. Value: one
        // immutable localized reserved-author string. Dependencies: language only.
        // Refresh: immediate at the inspector boundary after revision comparison.
        // Equality: equal rebuilds retain the string reference. Teardown: ResetDisplay
        // from game disposal; no world, item or definition references are retained.
        private static string? reservedDisplay;
        private static int displayLanguage = -1;

        internal static string DisplayName(UnfinishedThing uft)
        {
            string persisted = CreatorNameRef(uft);
            if (CreatorRef(uft) != null || persisted != ReservedAuthorLabel) return persisted;
            UiRevision.ObserveCurrentMetrics();
            if (reservedDisplay == null || displayLanguage != UiRevision.LanguageCurrent)
            {
                string next = "QJ_ReservedAuthor".Translate();
                if (!string.Equals(reservedDisplay, next, System.StringComparison.Ordinal))
                    reservedDisplay = next;
                displayLanguage = UiRevision.LanguageCurrent;
            }
            return reservedDisplay!;
        }

        internal static void ResetDisplay()
        {
            reservedDisplay = null;
            displayLanguage = -1;
        }

        /// Paused lock (spec §5): authorless + a readable label. Vanilla resume
        /// requires Creator == pawn, which null never satisfies.
        public static void Clear(UnfinishedThing uft)
        {
            CreatorRef(uft) = null;
            CreatorNameRef(uft) = ReservedAuthorLabel;
        }

        /// Dispatch/restore: assign a real pawn through the property so
        /// creatorName updates too (UnfinishedThing.cs:32-41).
        public static void Assign(UnfinishedThing uft, Pawn pawn) => uft.Creator = pawn;

        public static Pawn? Get(UnfinishedThing uft) => CreatorRef(uft);

        /// M5: when no owner could be assigned during RestoreAllToVanilla, reset
        /// the reserved label so post-disable saves don't carry the mod label on
        /// authorless items. Only clears when creatorName is exactly our label.
        public static void ClearLabelIfReserved(UnfinishedThing uft)
        {
            if (CreatorNameRef(uft) == ReservedAuthorLabel)
                CreatorNameRef(uft) = "";
        }
    }
}
