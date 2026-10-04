using RimShared.GameLib;
using RimShared.UiLib;

namespace WorkRoles.UI
{
    /// Shared projections created only for WorkRoles windows. Keeping the owner
    /// list here makes ordinary window close and world teardown release exactly
    /// the same static data.
    internal static class WindowDataLifecycle
    {
        internal static void ReleaseShared()
        {
            ColonyGroupsDataSource.ReleaseSnapshot();
            RolesListState.ReleaseSectionsSnapshot();
            GroupSources.ReleaseWindowData();
            RoleIconPresentationCatalog.ReleaseForTeardown();
            WorkJobLabels.InvalidateLanguageCaches();
            ColonistsTabView.InvalidateSharedLanguageCaches();
            MapClassifications.ReleaseSnapshot();
            WrText.Reset();
            Patches.Patch_Bill_DoConfigInterface.Clear();
            WrToast.Clear();
        }
    }
}
