using Verse;

namespace WorkRoles
{
    /// A player-defined organizational group in the role list. Purely
    /// presentational: never consulted by recommendations, plans or compiled
    /// orders. Id 0 is reserved for the auto-created "Default" group (fixed
    /// label, recreated on demand); like any user group it is swept when empty.
    /// The Conditional Roles section is NOT a group — its membership is derived
    /// (HasRules) and a member's stored group is remembered.
    public class RoleGroup : IExposable
    {
        public const int DefaultId = 0;

        public int id;
        public string label = null!; // assigned at creation or load
        // Explicit provenance for newly seeded groups. Never backfill by label:
        // legacy saves cannot distinguish shipped and player-authored names.
        public string? nameSourceDef;

        // Owner: this RoleGroup; Key: instance identity. Value: immutable display label.
        // Dependencies: saved label, nameSourceDef, group identity, language and definition revisions.
        // Refresh: immediate on a dependency change, never changes saved state.
        // Equality: an equal rebuild retains the prior string reference.
        // Teardown: no external resources; instance-owned fields die with the model.
        private string? displaySource;
        private string? displayNameSourceDef;
        private int displayGroupId = -1;
        private string? displayLabel;
        private int displayLanguage = -1, displayDefinitions = -1;
        internal string DisplayLabel
        {
            get
            {
                int language = LanguageChangeCoordinator.Revision;
                int definitions = DefinitionReloadCoordinator.Revision;
                if (displayLabel == null || displaySource != label || displayGroupId != id
                    || displayNameSourceDef != nameSourceDef
                    || displayLanguage != language || displayDefinitions != definitions)
                {
                    string next = SeededDisplayNames.Group(this);
                    if (!string.Equals(displayLabel, next, System.StringComparison.Ordinal))
                        displayLabel = next;
                    displaySource = label;
                    displayNameSourceDef = nameSourceDef;
                    displayGroupId = id;
                    displayLanguage = language;
                    displayDefinitions = definitions;
                }
                return displayLabel!;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label!, "label");
            Scribe_Values.Look(ref nameSourceDef, "nameSourceDef");
        }
    }
}
