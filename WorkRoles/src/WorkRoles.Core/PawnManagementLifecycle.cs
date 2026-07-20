using System;

namespace WorkRoles.Core
{
    /// <summary>Orders the state changes required when a pawn stops being managed.</summary>
    public static class PawnManagementLifecycle
    {
        public static void Unmanage(
            bool hasVanillaWorkSettings,
            Action mirrorFallback,
            Action removeManagedState,
            Action notifyVanilla,
            Action invalidateUi)
        {
            // Without work settings there is no vanilla authority or priorities map
            // to restore. Removing managed state is still required so a pawn cannot
            // remain shadow-managed merely because its vanilla component is absent.
            if (hasVanillaWorkSettings)
                mirrorFallback();
            removeManagedState();
            if (hasVanillaWorkSettings)
                notifyVanilla();
            invalidateUi();
        }
    }
}
