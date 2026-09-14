using System;
using System.Threading;
using EPrimeReadouts.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace EPrimeReadouts.UI
{
    internal enum TextureHealthResult { Idle, Pending, Healthy, Failed, Stale }

    /// Samples actual published textures through the same material used to
    /// present the panel. This detects erased samples or a broken sprite path,
    /// not corruption elsewhere in a texture or final-screen clipping/occlusion.
    internal sealed class PanelTextureHealth
    {
        private const int SampleCount = 4;
        private readonly PanelBufferBackend backend;
        private readonly Action<AsyncGPUReadbackRequest> completed;
        // Owner: this panel health checker. Key: captured frame-buffer owner
        // and publication version (textures may be recycled across versions).
        // Value: four immutable expected output pixels until completion.
        // Dependencies: published sample metadata, visible title, owned material.
        // Refresh: explicit tick-gated Begin, or bounded recovery verification.
        // Equality: scalar comparisons; no render snapshot is republished.
        // Teardown: Release invalidates the result and defers destruction of the
        // owned 4x1 target until its outstanding GPU read completes. The cached
        // callback also consumes results while the panel is hidden or unloaded.
        private readonly Color32[] expected = new Color32[SampleCount];
        private RenderTexture? target;
        private PanelFrameBuffers? owner;
        private long publicationVersion;
        private readonly PanelReadbackLifetime lifetime = new PanelReadbackLifetime();
        private bool hasResult;
        private bool healthy;

        internal PanelTextureHealth(PanelBufferBackend backend)
        {
            this.backend = backend;
            completed = Complete;
        }

        internal bool Pending => lifetime.IsPending;

        internal bool Begin(PanelFrameBuffers buffers)
        {
            if (!buffers.CanPresent || !PanelBufferBackend.AsyncReadbackSupported
                || !lifetime.TryBegin()) return false;
            bool submitted = false;
            try
            {
                if (target == null) target = backend.CreateWorkingSurface(SampleCount, 1);
                if (!target.IsCreated() && !target.Create()) return false;
                owner = buffers;
                publicationVersion = buffers.PublicationVersion;
                hasResult = false;
                RenderTexture? previous = RenderTexture.active;
                RenderTexture.active = target;
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0f, SampleCount, 1f, 0f);
                    GL.Clear(true, true, Color.clear);
                    for (int i = 0; i < SampleCount; i++)
                    {
                        PanelSurfaceChannel? channel = buffers.HealthChannel(i);
                        expected[i] = channel == null ? default : channel.FrontSample.Expected;
                        if (channel == null) continue; // hidden title
                        Texture2D? front = channel.Front;
                        if (front == null || !backend.Present(front,
                            new Rect(i, 0f, 1f, 1f), channel.FrontSample.Uv)) return false;
                    }
                }
                finally
                {
                    GL.PopMatrix();
                    RenderTexture.active = previous;
                }
                AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, completed);
                submitted = true;
                return true;
            }
            finally
            {
                if (!submitted && lifetime.Complete()) ReleaseTarget();
            }
        }

        internal TextureHealthResult TakeResult(PanelFrameBuffers buffers)
        {
            if (lifetime.IsReleased) return TextureHealthResult.Idle;
            if (lifetime.IsPending) return TextureHealthResult.Pending;
            if (!hasResult) return TextureHealthResult.Idle;
            hasResult = false;
            bool current = ReferenceEquals(owner, buffers)
                && publicationVersion == buffers.PublicationVersion;
            owner = null;
            return !current ? TextureHealthResult.Stale
                : healthy ? TextureHealthResult.Healthy : TextureHealthResult.Failed;
        }

        private void Complete(AsyncGPUReadbackRequest request)
        {
            if (lifetime.Complete()) { owner = null; ReleaseTarget(); return; }
            if (lifetime.IsReleased) return;
            healthy = !request.hasError;
            if (healthy)
            {
                var pixels = request.GetData<Color32>();
                healthy = pixels.Length == SampleCount;
                if (healthy)
                    for (int i = 0; i < SampleCount; i++)
                    {
                        Color32 actual = pixels[i], wanted = expected[i];
                        if (Math.Abs(actual.r - wanted.r) > 2
                            || Math.Abs(actual.g - wanted.g) > 2
                            || Math.Abs(actual.b - wanted.b) > 2
                            || Math.Abs(actual.a - wanted.a) > 2) healthy = false;
                    }
            }
            hasResult = true;
        }

        internal void Release()
        {
            owner = null;
            hasResult = false;
            if (lifetime.RequestRelease()) ReleaseTarget();
        }

        private void ReleaseTarget()
        {
            // World teardown may run on the long-event worker while the
            // main-thread callback completes. Transfer ownership only once;
            // ReleaseTexture marshals actual Unity destruction to that thread.
            PanelBufferBackend.ReleaseTexture(Interlocked.Exchange(ref target, null));
        }
    }
}
