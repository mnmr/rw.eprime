namespace WorkRoles.Core
{
    /// <summary>
    /// Coalesces invalidation requests until the caller reaches a safe
    /// completion point, then advances one observable revision.
    /// </summary>
    public sealed class DeferredInvalidationRevision
    {
        private bool pending;

        public int Current { get; private set; }

        public void Request() => pending = true;

        public bool Complete()
        {
            if (!pending) return false;
            pending = false;
            Current++;
            return true;
        }
    }
}
