using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Store-owned explicit mutation domains. Current remains a compatibility
    /// stamp; consumers must prefer exact domains or per-identity stamps.
    public static class UiVersion
    {
        private static ModelRevisions? Revisions => RoleStore.Current?.Revisions;
        public static int Current => Revisions?.Current ?? 0;
        public static int Catalog => Read(ModelDomain.Catalog);
        public static int RolePresentation => Read(ModelDomain.RolePresentation);
        public static int RoleWork => Read(ModelDomain.RoleWork);
        public static int RoleHistory => Read(ModelDomain.RoleHistory);
        public static int RoleCapability => Read(ModelDomain.RoleCapability);
        public static int RoleActivation => Read(ModelDomain.RoleActivation);
        public static int RoleRules => Read(ModelDomain.RoleRules);
        public static int RoleRecommendation => Read(ModelDomain.RoleRecommendation);
        public static int Assignments => Read(ModelDomain.Assignments);
        public static int AssignmentPresentation => Read(ModelDomain.AssignmentPresentation);
        public static int Roster => Read(ModelDomain.Roster);
        public static int RuntimeRules => Read(ModelDomain.RuntimeRules);
        public static int Preferences => Read(ModelDomain.Preferences);
        public static int RecommendationOrder => Read(ModelDomain.RecommendationOrder);
        public static int PresentationForRole(int id) => ReadRole(id, ModelDomain.RolePresentation);
        public static int WorkForRole(int id) => ReadRole(id, ModelDomain.RoleWork);
        public static int HistoryForRole(int id) => ReadRole(id, ModelDomain.RoleHistory);
        public static int CapabilityForRole(int id) => ReadRole(id, ModelDomain.RoleCapability);
        public static int ActivationForRole(int id) => ReadRole(id, ModelDomain.RoleActivation);
        public static int RulesForRole(int id) => ReadRole(id, ModelDomain.RoleRules);
        public static int RecommendationForRole(int id) => ReadRole(id, ModelDomain.RoleRecommendation);
        public static int PresentationForPawn(Pawn pawn) => PresentationForPawn(pawn.thingIDNumber);
        public static int PresentationForPawn(int id) => ReadPawn(id, ModelDomain.AssignmentPresentation);
        public static int AssignmentsForPawn(Pawn pawn) => AssignmentsForPawn(pawn.thingIDNumber);
        public static int AssignmentsForPawn(int id) => ReadPawn(id, ModelDomain.Assignments);
        public static int RuntimeForPawn(Pawn pawn) => RuntimeForPawn(pawn.thingIDNumber);
        public static int RuntimeForPawn(int id) => ReadPawn(id, ModelDomain.RuntimeRules);
        private static int Read(ModelDomain domain) => Revisions?.For(domain) ?? 0;
        private static int ReadRole(int id, ModelDomain domain) => Revisions?.ForRole(id, domain) ?? 0;
        private static int ReadPawn(int id, ModelDomain domain) => Revisions?.ForPawn(id, domain) ?? 0;
        internal static void Changed(ModelDomain domains) => Revisions?.Changed(domains);
        internal static void AggregateChanged(ModelDomain domains) => Revisions?.AggregateChanged(domains);
        internal static void RoleChanged(int id, ModelDomain domains) => Revisions?.RoleChanged(id, domains);
        internal static void PawnChanged(Pawn pawn, bool planChanged) => Revisions?.PawnChanged(pawn.thingIDNumber,
            ModelDomain.AssignmentPresentation | (planChanged ? ModelDomain.Assignments : ModelDomain.None));
        internal static void RuntimeChanged(Pawn pawn) => Revisions?.PawnChanged(pawn.thingIDNumber, ModelDomain.RuntimeRules);
        internal static void RosterChanged() => Changed(ModelDomain.Roster);
        // Compatibility for full lifecycle resets only; ordinary commands name domains.
        public static void Bump() => Changed(ModelDomain.All);
    }
}
