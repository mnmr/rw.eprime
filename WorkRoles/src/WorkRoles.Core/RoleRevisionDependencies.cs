namespace WorkRoles.Core
{
    /// Propagates member changes to the derived domains a composite consumes.
    public static class RoleRevisionDependencies
    {
        public static ModelDomain ForComposite(ModelDomain memberChanges,
            bool compositeBlocker, bool memberBlocker, bool memberBlockerChanged)
        {
            ModelDomain result = memberChanges & ModelDomain.RoleActivation;
            // Ordinary composites omit veto members from coverage/work specs.
            // A blocker composite includes every member as part of its veto.
            // Changing blocker status can change inclusion in either direction.
            if ((memberBlockerChanged && !compositeBlocker)
                || ((memberChanges & ModelDomain.RoleWork) != 0
                    && (compositeBlocker || !memberBlocker)))
                result |= ModelDomain.RoleWork | ModelDomain.RoleCapability;
            return result;
        }
    }
}
