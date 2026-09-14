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
        // Owner: this channel's in-flight publication. Key/dependencies: the
        // working pixels and the builder's visible-geometry expectation for
        // this build only. Value: immutable expectation until Pump completes.
        // Refresh: RequestPublish; equality: no separate cached artifact.
        // Teardown: Abandon/Release discard all pending publication state.
        private bool requiresCoverage;
        private bool publishFailed;
        // Owner/key: this channel's front/back publication. Value: immutable
        // UV and expected premultiplied pixel. Dependencies: exact converted
        // pixels and physical dimensions. Refresh: existing publish conversion;
        // equality: promoted with its texture, never independently refreshed.
        // Teardown: Release clears samples along with the owned textures.
        private PanelTextureSample frontSample;
        private PanelTextureSample backSample;

        internal PanelSurfaceChannel(
            PanelBufferBackend backend, bool coverageFromRed = false)
        {
            this.backend = backend;
            this.coverageFromRed = coverageFromRed;
        }

        internal Texture2D? Front => front;
        internal PanelTextureSample FrontSample => frontSample;
        internal int FrontWidth => front != null ? front.width : 0;
        internal int FrontHeight => front != null ? front.height : 0;
        internal bool HasWorkInFlight => publishPending || backReady;

        // IsCreated must be observed before the revision/build gate. A
        // suspend/device reset does not change any CPU model revision, and
        // checking only in EnsureWorking leaves an idle cache stale forever.
        // ReferenceEquals distinguishes an unused channel from a destroyed
        // Unity object, which compares equal to null but also needs recovery.
        internal bool HasLostTarget => !ReferenceEquals(working, null)
            && (working == null || !working.IsCreated());

        /// Called for every channel after any target signals device loss.
        /// Published textures deliberately retain readable CPU pixels: upload
        /// those exact pixels again instead of rerasterizing an unchanged
        /// surface. The working target is always redrawn before its next
        /// publish, so newly created (undefined) pixels are never presented.
        internal bool RestoreAfterTargetLoss()
        {
            if (working == null && !ReferenceEquals(working, null)) return false;
            if (working != null && !working.IsCreated() && !working.Create())
                return false;
            if (front != null)
                front.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return true;
        }

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
        internal void RequestPublish(bool requiresCoverage)
        {
            this.requiresCoverage = requiresCoverage;
            publishPending = false;
            backReady = false;
            publishFailed = true;
            if (working == null) return;
            EnsureBack(working.width, working.height);
            if (back == null) return;
            if (PanelBufferBackend.AsyncReadbackSupported)
            {
                request = AsyncGPUReadback.Request(
                    working, 0, TextureFormat.RGBA32);
                publishPending = true;
                publishFailed = false;
            }
            else
            {
                backReady = PanelBufferBackend.PublishSync(
                    working, back, coverageFromRed, requiresCoverage, out backSample);
                publishFailed = !backReady;
            }
        }

        /// Polls the in-flight publish. Failed is transient (typically a
        /// device reset invalidated the working target); the owner may retry
        /// with a fresh build.
        internal SurfacePublishState Pump()
        {
            if (publishFailed) return SurfacePublishState.Failed;
            if (backReady) return SurfacePublishState.Ready;
            if (!publishPending) return SurfacePublishState.Idle;
            if (!request.done) return SurfacePublishState.Pending;
            publishPending = false;
            if (request.hasError || back == null)
                return SurfacePublishState.Failed;
            if (!backend.PublishFromReadback(
                    request, back, coverageFromRed, requiresCoverage, out backSample))
                return SurfacePublishState.Failed;
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
            frontSample = backSample;
            backReady = false;
            return true;
        }

        /// Drops any in-flight or completed-but-unpromoted publish.
        internal void Abandon()
        {
            publishPending = false;
            backReady = false;
            publishFailed = false;
            requiresCoverage = false;
        }

        internal void Release()
        {
            PanelBufferBackend.ReleaseTexture(working);
            PanelBufferBackend.ReleaseTexture(front);
            PanelBufferBackend.ReleaseTexture(back);
            working = null;
            front = null;
            back = null;
            frontSample = default;
            backSample = default;
            Abandon();
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
