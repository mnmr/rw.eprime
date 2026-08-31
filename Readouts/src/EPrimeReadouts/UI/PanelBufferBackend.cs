using System.Collections.Generic;
using EPrimeReadouts.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Owns the color-space boundary between premultiplied composition targets
    /// and straight-alpha published textures. Publication reads the GPU result
    /// back asynchronously (AsyncGPUReadback) so no build ever stalls the
    /// pipeline; platforms without async readback fall back to a synchronous
    /// read into the same buffers. The backend is unavailable unless the exact
    /// runtime shader and publish path pass a literal pixel round-trip probe,
    /// including readback row orientation.
    internal sealed class PanelBufferBackend
    {
        private static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);

        internal static readonly PanelBufferBackend Shared =
            new PanelBufferBackend();

        private Material? spriteMaterial;
        private Mesh? glyphMesh;
        private Texture2D? probeReader;
        private bool initializationAttempted;
        private bool available;
        /// Probe-calibrated: whether async readback rows arrive top-down
        /// relative to the synchronous ReadPixels convention.
        private bool readbackFlipsRows;

        internal bool IsAvailable => available;

        internal static bool AsyncReadbackSupported =>
            SystemInfo.supportsAsyncGPUReadback;

        internal bool TryInitialize()
        {
            if (initializationAttempted) return available;
            initializationAttempted = true;
            try
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    return Disable("Sprites/Default shader was not found");

                spriteMaterial = new Material(shader)
                {
                    name = "EPrimeReadouts.BufferedSprite",
                    color = Color.white,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (!ValidateRoundTrip(out string reason))
                    return Disable(
                        "pixel round-trip probe failed: " + reason);

                available = true;
                return true;
            }
            catch (System.Exception exception)
            {
                return Disable("backend probe threw "
                    + exception.GetType().Name + ": " + exception.Message);
            }
        }

        internal RenderTexture CreateWorkingSurface(int width, int height)
        {
            var texture = new RenderTexture(
                width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = "EPrimeReadouts.PanelWorking",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.Create();
            return texture;
        }

        internal Texture2D CreatePublishedTexture(
            int width, int height,
            FilterMode filterMode = FilterMode.Bilinear)
        {
            var texture = new Texture2D(
                width, height, TextureFormat.RGBA32,
                mipChain: false, linear: true)
            {
                name = "EPrimeReadouts.PanelPublished",
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return texture;
        }

        /// Copies one completed asynchronous readback into the destination
        /// texture's own CPU buffer, unpremultiplying in the same pass. Row
        /// order is calibrated once by the probe against the synchronous
        /// path, because platforms disagree on readback orientation.
        /// coverageFromRed serves the glyph surface: the font shader's
        /// straight-alpha blend squares destination alpha on a transparent
        /// target, but RGB is the correct premultiplied result and every
        /// buffered content tint has a red channel of one, so red recovers
        /// coverage.
        internal void PublishFromReadback(
            AsyncGPUReadbackRequest request,
            Texture2D destination,
            bool coverageFromRed)
        {
            int width = destination.width;
            int height = destination.height;
            NativeArray<Color32> source = request.GetData<Color32>();
            NativeArray<Color32> target =
                destination.GetRawTextureData<Color32>();
            bool flip = readbackFlipsRows;
            for (int row = 0; row < height; row++)
            {
                int sourceRow = (flip ? height - 1 - row : row) * width;
                int targetRow = row * width;
                for (int column = 0; column < width; column++)
                {
                    Color32 pixel = source[sourceRow + column];
                    byte coverage = coverageFromRed ? pixel.r : pixel.a;
                    PixelRgba straight = Rgba32Math.Unpremultiply(
                        pixel.r, pixel.g, pixel.b, coverage);
                    target[targetRow + column] = new Color32(
                        straight.R, straight.G, straight.B, straight.A);
                }
            }
            destination.Apply(updateMipmaps: false,
                makeNoLongerReadable: false);
        }

        /// Synchronous publish for platforms without async readback: reads the
        /// working target directly into the destination's CPU buffer, then
        /// unpremultiplies it in place. No intermediate reader texture exists.
        /// See PublishFromReadback for the coverageFromRed contract.
        internal static void PublishSync(
            RenderTexture working,
            Texture2D destination,
            bool coverageFromRed)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = working;
            try
            {
                destination.ReadPixels(
                    new Rect(0f, 0f, working.width, working.height),
                    0, 0, recalculateMipMaps: false);
            }
            finally
            {
                RenderTexture.active = previous;
            }
            NativeArray<Color32> pixels =
                destination.GetRawTextureData<Color32>();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                byte coverage = coverageFromRed ? pixel.r : pixel.a;
                PixelRgba straight = Rgba32Math.Unpremultiply(
                    pixel.r, pixel.g, pixel.b, coverage);
                pixels[i] = new Color32(
                    straight.R, straight.G, straight.B, straight.A);
            }
            destination.Apply(updateMipmaps: false,
                makeNoLongerReadable: false);
        }

        internal void Present(Texture2D texture, Rect rect, Rect uv)
        {
            if (!available || spriteMaterial == null) return;
            Graphics.DrawTexture(
                rect, texture, uv,
                0, 0, 0, 0, Color.white, spriteMaterial);
        }

        internal void DrawToActive(
            Rect rect, Texture texture, Color color)
            => DrawToActive(rect, texture, FullUv, color);

        internal void DrawToActive(
            Rect rect, Texture texture, Rect uv, Color color)
        {
            if (spriteMaterial == null)
                throw new System.InvalidOperationException(
                    "Buffered sprite material is unavailable.");
            Graphics.DrawTexture(
                rect, texture, uv,
                0, 0, 0, 0, color, spriteMaterial);
        }

        internal void DrawNineSliceToActive(
            Rect rect, Texture texture, RectOffset border, Color color)
        {
            if (spriteMaterial == null)
                throw new System.InvalidOperationException(
                    "Buffered sprite material is unavailable.");
            Graphics.DrawTexture(
                rect, texture, FullUv,
                border.left, border.right, border.top, border.bottom,
                color, spriteMaterial);
        }

        internal void DrawQuadsToActive(
            IList<Vector3> vertices,
            IList<Vector2> uvs,
            IList<Color32> colors,
            Texture texture)
            => DrawQuadsToActive(
                vertices, uvs, colors, texture, spriteMaterial);

        /// Draws the content glyph mesh through the FONT material: GUI font
        /// atlases carry coverage in alpha and black RGB, so the sprite
        /// material (which multiplies by the atlas RGB) renders glyphs black.
        /// RimWorld's font shader emits the requested vertex color and reads
        /// only atlas alpha; its straight-alpha blend leaves squared alpha on
        /// the transparent target, which the coverage-from-red publish
        /// corrects.
        internal void DrawFontQuadsToActive(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<Color32> colors,
            List<int> triangles,
            Material fontMaterial)
        {
            RenderTexture? target = RenderTexture.active;
            if (target == null)
                throw new System.InvalidOperationException(
                    "Buffered glyph target is unavailable.");
            if (glyphMesh == null)
            {
                glyphMesh = new Mesh
                {
                    name = "EPrimeReadouts.PanelGlyphMesh",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            glyphMesh.Clear();
            glyphMesh.SetVertices(vertices);
            glyphMesh.SetUVs(0, uvs);
            glyphMesh.SetColors(colors);
            glyphMesh.SetTriangles(triangles, 0, calculateBounds: false);

            var commands = new CommandBuffer
            {
                name = "EPrimeReadouts.PanelGlyphs",
            };
            try
            {
                commands.SetRenderTarget(target);
                commands.SetViewport(new Rect(
                    0f, 0f, target.width, target.height));
                commands.DisableScissorRect();
                commands.ClearRenderTarget(
                    clearDepth: true,
                    clearColor: true,
                    backgroundColor: Color.clear);
                if (vertices.Count != 0)
                {
                    commands.SetViewProjectionMatrices(
                        Matrix4x4.identity,
                        Matrix4x4.Ortho(
                            0f, target.width,
                            target.height, 0f,
                            -1f, 1f));
                    commands.DrawMesh(
                        glyphMesh, Matrix4x4.identity, fontMaterial);
                }
                Graphics.ExecuteCommandBuffer(commands);
            }
            finally
            {
                commands.Release();
            }
        }

        private static void DrawQuadsToActive(
            IList<Vector3> vertices,
            IList<Vector2> uvs,
            IList<Color32> colors,
            Texture texture,
            Material? material)
        {
            if (material == null)
                throw new System.InvalidOperationException(
                    "Buffered quad material is unavailable.");
            material.SetTexture("_MainTex", texture);
            material.color = Color.white;
            if (!material.SetPass(0))
                throw new System.InvalidOperationException(
                    "Buffered quad material pass is unavailable.");
            GL.Begin(GL.QUADS);
            try
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    GL.Color(colors[i]);
                    Vector2 uv = uvs[i];
                    GL.TexCoord2(uv.x, uv.y);
                    GL.Vertex(vertices[i]);
                }
            }
            finally
            {
                GL.End();
            }
        }

        internal static void ReleaseTexture(Object? texture)
        {
            if (ReferenceEquals(texture, null)) return;
            Object owned = texture;
            // World teardown can enter from the long-event worker. Unity
            // destruction remains on the main-thread completion gate.
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (owned is RenderTexture renderTexture)
                    renderTexture.Release();
                Object.Destroy(owned);
            });
        }

        internal void Release()
        {
            available = false;
            initializationAttempted = false;
            ReleaseTexture(probeReader);
            probeReader = null;
            ReleaseTexture(glyphMesh);
            glyphMesh = null;
            ReleaseTexture(spriteMaterial);
            spriteMaterial = null;
        }

        /// Validates the exact production pipeline on a 1x2 surface with two
        /// distinct pixels so row order is observable. Stage one draws two
        /// straight-alpha pixels through the sprite material and expects
        /// exact premultiplication (accepting either platform row
        /// convention). Stage two publishes synchronously as the reference,
        /// then calibrates the asynchronous readback's row order against it.
        /// Stage three presents the published texture over an opaque
        /// destination and expects exact source-over on both rows.
        private bool ValidateRoundTrip(out string reason)
        {
            var pixelA = new PixelRgba(200, 100, 50, 128);
            var pixelB = new PixelRgba(60, 180, 90, 200);
            var source = new Texture2D(
                1, 2, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };
            RenderTexture? working = null;
            Texture2D? syncPublished = null;
            Texture2D? asyncPublished = null;
            RenderTexture? final = null;
            try
            {
                source.SetPixel(0, 1, new Color32(
                    pixelA.R, pixelA.G, pixelA.B, pixelA.A));
                source.SetPixel(0, 0, new Color32(
                    pixelB.R, pixelB.G, pixelB.B, pixelB.A));
                source.Apply(false, false);

                working = CreateWorkingSurface(1, 2);
                DrawProbe(working, source, Color.clear);
                PixelRgba observedTop = ReadPixel(working, top: true);
                PixelRgba observedBottom = ReadPixel(working, top: false);
                PixelRgba premultipliedA = Rgba32Math.Premultiply(
                    pixelA.R, pixelA.G, pixelA.B, pixelA.A);
                PixelRgba premultipliedB = Rgba32Math.Premultiply(
                    pixelB.R, pixelB.G, pixelB.B, pixelB.A);
                bool upright = Near(observedTop, premultipliedA, 2)
                    && Near(observedBottom, premultipliedB, 2);
                bool inverted = Near(observedTop, premultipliedB, 2)
                    && Near(observedBottom, premultipliedA, 2);
                if (!upright && !inverted)
                {
                    reason = "sprite draw did not premultiply";
                    return false;
                }

                // Straight-alpha expectations follow the observed positions,
                // so later stages are independent of the draw convention.
                PixelRgba straightTop = Rgba32Math.Unpremultiply(
                    observedTop.R, observedTop.G,
                    observedTop.B, observedTop.A);
                PixelRgba straightBottom = Rgba32Math.Unpremultiply(
                    observedBottom.R, observedBottom.G,
                    observedBottom.B, observedBottom.A);

                syncPublished = CreatePublishedTexture(
                    1, 2, FilterMode.Point);
                PublishSync(working, syncPublished, coverageFromRed: false);
                if (!Near(ReadPublished(syncPublished, top: true),
                        straightTop, 2)
                    || !Near(ReadPublished(syncPublished, top: false),
                        straightBottom, 2))
                {
                    reason = "synchronous publish mismatch";
                    return false;
                }

                Texture2D presented = syncPublished;
                if (AsyncReadbackSupported)
                {
                    AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(
                        working, 0, TextureFormat.RGBA32);
                    request.WaitForCompletion();
                    if (request.hasError)
                    {
                        reason = "asynchronous readback errored";
                        return false;
                    }
                    asyncPublished = CreatePublishedTexture(
                        1, 2, FilterMode.Point);
                    readbackFlipsRows = false;
                    PublishFromReadback(
                        request, asyncPublished, coverageFromRed: false);
                    if (!MatchesPublished(
                        asyncPublished, straightTop, straightBottom))
                    {
                        readbackFlipsRows = true;
                        PublishFromReadback(
                            request, asyncPublished, coverageFromRed: false);
                        if (!MatchesPublished(
                            asyncPublished, straightTop, straightBottom))
                        {
                            reason = "asynchronous readback layout mismatch";
                            return false;
                        }
                    }
                    presented = asyncPublished;
                }

                var destination = new PixelRgba(20, 40, 80, 255);
                final = CreateWorkingSurface(1, 2);
                DrawProbe(final, presented, new Color32(
                    destination.R, destination.G,
                    destination.B, destination.A));
                if (!Near(ReadPixel(final, top: true),
                        Rgba32Math.SourceOver(straightTop, destination), 3)
                    || !Near(ReadPixel(final, top: false),
                        Rgba32Math.SourceOver(straightBottom, destination), 3))
                {
                    reason = "present blend mismatch";
                    return false;
                }
                reason = "";
                return true;
            }
            finally
            {
                ReleaseTexture(source);
                ReleaseTexture(working);
                ReleaseTexture(syncPublished);
                ReleaseTexture(asyncPublished);
                ReleaseTexture(final);
            }
        }

        private static bool MatchesPublished(
            Texture2D published, PixelRgba top, PixelRgba bottom) =>
            Near(ReadPublished(published, top: true), top, 2)
            && Near(ReadPublished(published, top: false), bottom, 2);

        private void DrawProbe(
            RenderTexture target, Texture texture, Color clear)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix(0f, 1f, 2f, 0f);
                GL.Clear(clearDepth: true, clearColor: true, clear);
                DrawToActive(new Rect(0f, 0f, 1f, 2f),
                    texture, Color.white);
            }
            finally
            {
                GL.PopMatrix();
                RenderTexture.active = previous;
            }
        }

        private PixelRgba ReadPixel(RenderTexture texture, bool top)
        {
            if (probeReader == null)
            {
                probeReader = new Texture2D(
                    1, 2, TextureFormat.RGBA32, false, true)
                {
                    name = "EPrimeReadouts.PanelProbeReadback",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = texture;
            try
            {
                probeReader.ReadPixels(
                    new Rect(0f, 0f, 1f, 2f), 0, 0, false);
                probeReader.Apply(false, false);
                return ReadPublished(probeReader, top);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static PixelRgba ReadPublished(Texture2D texture, bool top)
        {
            Color32 pixel = texture.GetPixel(0, top ? 1 : 0);
            return new PixelRgba(pixel.r, pixel.g, pixel.b, pixel.a);
        }

        private bool Disable(string reason)
        {
            available = false;
            Log.Warning("[Readouts] Buffered renderer disabled: " + reason);
            ReleaseTexture(probeReader);
            probeReader = null;
            ReleaseTexture(spriteMaterial);
            spriteMaterial = null;
            return false;
        }

        private static bool Near(
            PixelRgba left, PixelRgba right, int tolerance) =>
            Near(left.R, right.R, tolerance)
            && Near(left.G, right.G, tolerance)
            && Near(left.B, right.B, tolerance)
            && Near(left.A, right.A, tolerance);

        private static bool Near(byte left, byte right, int tolerance) =>
            System.Math.Abs(left - right) <= tolerance;
    }
}
