using System;

namespace WorkRoles.Core
{
    /// Processes one simulation context's pending work and explicit time event.
    /// Delegates are owned/cached by the context; this coordinator allocates none.
    public static class RuntimeWorkBoundary
    {
        public static void Process(bool hourChanged, Action hourUpdates, Action reconcile)
        {
            if (hourChanged) hourUpdates();
            reconcile();
        }
    }
}
