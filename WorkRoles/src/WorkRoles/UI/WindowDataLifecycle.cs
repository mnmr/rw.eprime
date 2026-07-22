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
            WorkJobLabels.InvalidateLanguageCaches();
            ColonistsTabView.InvalidateSharedLanguageCaches();
            ColonyScope.ReleaseSnapshot();
            WrText.ClearFitWidthCache();
            WrToast.Clear();
        }
    }
}
