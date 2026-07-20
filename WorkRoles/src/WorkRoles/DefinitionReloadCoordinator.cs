using Verse;
using WorkRoles.Signals;

namespace WorkRoles
{
    /// Main-thread definition lifecycle. Prefix release protects every owner
    /// before a database is mutated; the second synchronous hot-reload event
    /// warms only the invariant profile index after vanilla's reload event and
    /// its ExecuteWhenFinished callbacks have completed.
    internal static class DefinitionReloadCoordinator
    {
        // Cancellation generation for queued hot-reload warms. Definition
        // completion inside that reload must not advance this token.
        private static int generation;
        private static int queuedWarmGeneration = -1;
        private static int definitionRevision;

        /// UI/cache revision for authoritative definition-owner releases.
        internal static int Revision => definitionRevision;

        internal static void ReleaseBeforeReload()
        {
            // Every reload/clear prefix advances the cancellation token. Any
            // later mutation boundary therefore makes an earlier queued warm
            // ineligible without definition completion canceling its own warm.
            unchecked { generation++; }
            queuedWarmGeneration = -1;
            ReleaseOwners();
        }

        private static void ReleaseOwners()
        {
            unchecked { definitionRevision++; }
            JobSkillProfiles.InvalidateDefinitions();
            GameJobCatalog.Instance.InvalidateSessionCache();
            CompiledJobOrders.InvalidateDefinitions();
            VseSignalReflection.InvalidateDefinitions();
            PawnSignalSnapshotCache.Clear();
        }

        /// Late generation completion is authoritative even when no clear/reload
        /// prefix preceded it (startup, language reinjection, generated defs).
        internal static void DefinitionsRegenerated()
        {
            ReleaseOwners();
        }

        /// World teardown invalidates the same definition owners without
        /// duplicating their list at the lifecycle patch site.
        internal static void ReleaseForTeardown()
        {
            ReleaseOwners();
        }

        internal static void QueueHotReloadWarm()
        {
            int token = generation;
            if (queuedWarmGeneration == token) return;
            queuedWarmGeneration = token;
            LongEventHandler.QueueLongEvent(
                () =>
                {
                    if (token != generation) return;
                    queuedWarmGeneration = -1;
                    JobSkillProfiles.WarmDefinitionFacts();
                },
                null,
                doAsynchronously: false,
                exceptionHandler: null,
                showExtraUIInfo: false,
                forceHideUI: true);
        }

        /// World teardown is not a play-data reload, but it must invalidate a
        /// queued hot-reload token before the ordinary owner clears run.
        internal static void CancelPendingWarm()
        {
            unchecked { generation++; }
            queuedWarmGeneration = -1;
        }
    }
}
