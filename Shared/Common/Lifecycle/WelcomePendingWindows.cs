using System.Collections.Generic;

namespace RimShared.Common
{
    /// <summary>Closed welcome instances waiting for space in one save.</summary>
    public sealed class WelcomePendingWindows<T> where T : class
    {
        // Owner: caller's process/assembly, scoped to one save identity. Key:
        // save id and window reference. Value: closed instances, never live UI
        // resources. Dependencies: save identity and explicit enqueue/take.
        // Refresh: lifecycle events only. Equality: duplicate references are
        // ignored. Teardown: Clear or observing a different save drops all refs;
        // Stop also rejects callbacks until Start admits the next game.
        private readonly List<T> windows = new List<T>();
        private string? owner;
        public int Count => windows.Count;
        public bool IsStopped { get; private set; }
        public void Stop() { Clear(); IsStopped = true; }
        public void Start() { Clear(); IsStopped = false; }
        public void Add(string save, T window)
        {
            if (IsStopped) return;
            Observe(save);
            for (int i = 0; i < windows.Count; i++)
                if (ReferenceEquals(windows[i], window)) return;
            windows.Add(window);
        }
        public bool TryTake(string save, bool spaceAvailable, out T? window)
        {
            if (IsStopped) { window = null; return false; }
            Observe(save);
            if (!spaceAvailable || windows.Count == 0) { window = null; return false; }
            window = windows[0];
            windows.RemoveAt(0);
            return true;
        }
        public void Clear() { windows.Clear(); owner = null; }
        private void Observe(string save)
        {
            if (owner == save) return;
            windows.Clear();
            owner = save;
        }
    }
}
