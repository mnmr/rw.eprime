namespace WorkRoles.Core
{
    /// <summary>
    /// Owns one view's pawn-list revision. Map observation is part of stamp
    /// creation so no consumer can store a pre-transition cache key.
    /// </summary>
    public sealed class PawnListRevisionTracker
    {
        private bool hasObservedMap;
        private int observedMapId;

        public int Revision { get; private set; }

        public void Invalidate()
        {
            checked { Revision++; }
        }

        public ScopeCacheStamp Stamp(int uiRevision, int mapId)
        {
            if (!hasObservedMap)
            {
                hasObservedMap = true;
                observedMapId = mapId;
            }
            else if (observedMapId != mapId)
            {
                observedMapId = mapId;
                checked { Revision++; }
            }

            return new ScopeCacheStamp(uiRevision, Revision);
        }
    }
}
