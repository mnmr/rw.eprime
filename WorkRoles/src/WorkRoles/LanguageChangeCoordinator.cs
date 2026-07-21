using WorkRoles.Core;
using WorkRoles.Signals;

namespace WorkRoles
{
    /// <summary>
    /// Completes a requested language change after translated play data has
    /// been reinjected, then invalidates every owner in dependency order.
    /// </summary>
    internal static class LanguageChangeCoordinator
    {
        private static readonly DeferredInvalidationRevision deferredRevision =
            new DeferredInvalidationRevision();

        internal static int Revision => deferredRevision.Current;

        internal static void Request()
        {
            deferredRevision.Request();
        }

        internal static void Complete()
        {
            if (!deferredRevision.Complete()) return;
            GameJobCatalog.Instance.InvalidateSessionCache();
            UiVersion.Bump();
            UI.GroupSources.InvalidateLanguageCaches();
            UI.WorkJobLabels.InvalidateLanguageCaches();
            UI.ColonistsTabView.InvalidateSharedLanguageCaches();
            UI.RolesTabView.InvalidateSharedLanguageCaches();
            JobSkillProfiles.InvalidateLanguageCaches();
            JobSkillProfiles.QueueLocalizedFacadeWarm();
            ColonyScope.InvalidateLanguageCaches();
            PawnSignalSnapshotCache.Clear();
            Patches.Patch_ActiveTip_TipRect.Clear();
        }
    }
}
