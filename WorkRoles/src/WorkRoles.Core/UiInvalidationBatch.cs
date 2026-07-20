using System;

namespace WorkRoles.Core
{
    /// <summary>Coalesces any number of UI invalidation requests into one action.</summary>
    public sealed class UiInvalidationBatch : IDisposable
    {
        private readonly Action invalidate;
        private bool requested;
        private bool disposed;

        public UiInvalidationBatch(Action invalidate)
        {
            this.invalidate = invalidate ?? throw new ArgumentNullException(nameof(invalidate));
        }

        public void Request()
        {
            if (disposed) throw new ObjectDisposedException(nameof(UiInvalidationBatch));
            requested = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (requested) invalidate();
        }
    }
}
