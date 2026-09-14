using System.Threading;

namespace EPrimeReadouts.Core
{
    /// Exactly one caller may release the owned GPU target after teardown is
    /// requested and any outstanding asynchronous readback has completed.
    public sealed class PanelReadbackLifetime
    {
        private const int Pending = 1;
        private const int Released = 2;
        private int state;

        public bool IsPending => (Volatile.Read(ref state) & Pending) != 0;
        public bool IsReleased => (Volatile.Read(ref state) & Released) != 0;
        public bool TryBegin() => Interlocked.CompareExchange(ref state, Pending, 0) == 0;

        public bool Complete()
        {
            int observed;
            do
            {
                observed = Volatile.Read(ref state);
                if ((observed & Pending) == 0) return false;
            } while (Interlocked.CompareExchange(ref state,
                observed & ~Pending, observed) != observed);
            return (observed & Released) != 0;
        }

        public bool RequestRelease()
        {
            int observed;
            do
            {
                observed = Volatile.Read(ref state);
                if ((observed & Released) != 0) return false;
            } while (Interlocked.CompareExchange(ref state,
                observed | Released, observed) != observed);
            return (observed & Pending) == 0;
        }
    }
}
