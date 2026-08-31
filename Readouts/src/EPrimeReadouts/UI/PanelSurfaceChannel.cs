using UnityEngine;
using UnityEngine.Rendering;

namespace EPrimeReadouts.UI
{
    internal enum SurfacePublishState
    {
        Idle,
        Pending,
        Ready,
        Failed,
    }

    internal enum SurfaceEnsureResult
    {
        /// The surface cannot be built (unsupported icon, font, or size).
        Failed,
        /// The published front already matches the requested revision.
        Unchanged,
        /// A publish for the requested revision is in flight or ready and
        /// waits for promotion.
        InFlight,
    }

    /// One buffered surface's GPU plumbing: the working render target the
    /// surface draws into, the front texture presentation reads, and the back
    /// texture an in-flight publish fills. A publish reads the working target
    /// back asynchronously (synchronously on platforms without async
    /// readback), so the front keeps presenting untouched until the owner
    /// promotes the completed back — presentation never sees a partial
    /// publish, and each texture keeps its own dimensions until promotion.
    internal sealed class PanelSurfaceChannel
    {
        private readonly PanelBufferBackend backend;
        /// Glyph surfaces recover coverage from the red channel because the
        /// font shader leaves squared alpha; see
        /// PanelBufferBackend.PublishFromReadback.
        private readonly bool coverageFromRed;
        private RenderTexture? working;
        private Texture2D? front;
        private Texture2D? back;
        private AsyncGPUReadbackRequest request;
        private bool publishPending;
        private bool backReady;

        internal PanelSurfaceChannel(
            PanelBufferBackend backend, bool coverageFromRed = false)
        {
            this.backend = backend;
            this.coverageFromRed = coverageFromRed;
        }

        internal Texture2D? Front => front;
        internal int FrontWidth => front != null ? front.width : 0;
        internal int FrontHeight => front != null ? front.height : 0;
        internal bool HasWorkInFlight => publishPending || backReady;

        /// The render target for the next build, recreated only when the
        /// requested pixel size changes and re-created in place after a
        /// device reset dropped it.
        internal RenderTexture? EnsureWorking(int pixelWidth, int pixelHeight)
        {
            if (working != null
                && working.width == pixelWidth
                && working.height == pixelHeight)
            {
                if (!working.IsCreated()) working.Create();
                return working;
            }
            PanelBufferBackend.ReleaseTexture(working);
            working = backend.CreateWorkingSurface(pixelWidth, pixelHeight);
            return working;
        }

        /// Starts publishing the working target's pixels. On async platforms
        /// this issues a readback request and returns immediately; otherwise
        /// the back texture is filled synchronously and sits ready for
        /// promotion.
        internal void RequestPublish()
        {
            if (working == null) return;
            EnsureBack(working.width, working.height);
            if (back == null) return;
            if (PanelBufferBackend.AsyncReadbackSupported)
            {
                request = AsyncGPUReadback.Request(
                    working, 0, TextureFormat.RGBA32);
                publishPending = true;
                backReady = false;
            }
            else
            {
                PanelBufferBackend.PublishSync(working, back, coverageFromRed);
                publishPending = false;
                backReady = true;
            }
        }

        /// Polls the in-flight publish. Failed is transient (typically a
        /// device reset invalidated the working target); the owner may retry
        /// with a fresh build.
        internal SurfacePublishState Pump()
        {
            if (backReady) return SurfacePublishState.Ready;
            if (!publishPending) return SurfacePublishState.Idle;
            if (!request.done) return SurfacePublishState.Pending;
            publishPending = false;
            if (request.hasError || back == null)
                return SurfacePublishState.Failed;
            backend.PublishFromReadback(request, back, coverageFromRed);
            backReady = true;
            return SurfacePublishState.Ready;
        }

        /// Atomically exposes the completed back as the new front. Returns
        /// false when no completed publish is waiting (the surface was
        /// unchanged in this build).
        internal bool Promote()
        {
            if (!backReady) return false;
            Texture2D? previous = front;
            front = back;
            back = previous;
            backReady = false;
            return true;
        }

        /// Drops any in-flight or completed-but-unpromoted publish.
        internal void Abandon()
        {
            publishPending = false;
            backReady = false;
        }

        internal void Release()
        {
            PanelBufferBackend.ReleaseTexture(working);
            PanelBufferBackend.ReleaseTexture(front);
            PanelBufferBackend.ReleaseTexture(back);
            working = null;
            front = null;
            back = null;
            publishPending = false;
            backReady = false;
        }

        private void EnsureBack(int pixelWidth, int pixelHeight)
        {
            if (back != null
                && back.width == pixelWidth
                && back.height == pixelHeight)
                return;
            PanelBufferBackend.ReleaseTexture(back);
            back = backend.CreatePublishedTexture(
                pixelWidth, pixelHeight, FilterMode.Point);
        }
    }
}
