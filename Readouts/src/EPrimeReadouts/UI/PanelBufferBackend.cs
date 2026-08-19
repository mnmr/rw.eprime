using System.Collections.Generic;
using EPrimeReadouts.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Owns the color-space boundary between premultiplied composition targets
    /// and straight-alpha published textures. The backend is unavailable unless
    /// the exact runtime shader path passes a literal pixel round-trip probe.
    internal sealed class PanelBufferBackend
    {
        private static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);

        internal static readonly PanelBufferBackend Shared =
            new PanelBufferBackend();

        private Material? spriteMaterial;
        private Mesh? fontMesh;
        private Texture2D? reader;
        private bool initializationAttempted;
        private bool available;

        internal bool IsAvailable => available;

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
                if (!ValidateRoundTrip())
                    return Disable("premultiplied pixel round-trip failed");

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

        internal void Publish(RenderTexture working, Texture2D destination)
        {
            EnsureReader(working.width, working.height);
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = working;
            try
            {
                reader!.ReadPixels(
                    new Rect(0f, 0f, working.width, working.height),
                    0, 0, recalculateMipMaps: false);
                reader.Apply(updateMipmaps: false,
                    makeNoLongerReadable: false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Color32[] pixels = reader!.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                PixelRgba straight = Rgba32Math.Unpremultiply(
                    pixel.r, pixel.g, pixel.b, pixel.a);
                pixels[i] = new Color32(
                    straight.R, straight.G, straight.B, straight.A);
            }
            destination.SetPixels32(pixels);
            destination.Apply(updateMipmaps: false,
                makeNoLongerReadable: false);
        }

        /// GUI font atlases carry coverage in alpha and black RGB. RimWorld's
        /// font shader emits the requested vertex color, but its straight-alpha
        /// blend squares destination alpha on a transparent target. RGB is
        /// nevertheless the correct premultiplied result; every buffered
        /// content tint has a red channel of one, so red recovers coverage.
        internal void PublishFont(
            RenderTexture working, Texture2D destination)
        {
            EnsureReader(working.width, working.height);
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = working;
            try
            {
                reader!.ReadPixels(
                    new Rect(0f, 0f, working.width, working.height),
                    0, 0, recalculateMipMaps: false);
                reader.Apply(updateMipmaps: false,
                    makeNoLongerReadable: false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            Color32[] pixels = reader!.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                byte coverage = pixel.r;
                PixelRgba straight = Rgba32Math.Unpremultiply(
                    pixel.r, pixel.g, pixel.b, coverage);
                pixels[i] = new Color32(
                    straight.R, straight.G,
                    straight.B, straight.A);
            }
            destination.SetPixels32(pixels);
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

        internal void CompositeOver(
            Texture2D destination,
            Texture2D source,
            int sourceX,
            int sourceY,
            int destinationX,
            int destinationY,
            int width,
            int height)
        {
            if (sourceX < 0)
            {
                destinationX -= sourceX;
                width += sourceX;
                sourceX = 0;
            }
            if (sourceY < 0)
            {
                destinationY -= sourceY;
                height += sourceY;
                sourceY = 0;
            }
            if (destinationX < 0)
            {
                sourceX -= destinationX;
                width += destinationX;
                destinationX = 0;
            }
            if (destinationY < 0)
            {
                sourceY -= destinationY;
                height += destinationY;
                destinationY = 0;
            }
            width = Mathf.Min(width,
                source.width - sourceX,
                destination.width - destinationX);
            height = Mathf.Min(height,
                source.height - sourceY,
                destination.height - destinationY);
            if (width <= 0 || height <= 0) return;

            Color32[] sourcePixels = source.GetPixels32();
            Color32[] destinationPixels = destination.GetPixels32();
            for (int y = 0; y < height; y++)
            {
                int sourceIndex = (sourceY + y) * source.width + sourceX;
                int destinationIndex =
                    (destinationY + y) * destination.width + destinationX;
                for (int x = 0; x < width; x++)
                {
                    Color32 sourcePixel = sourcePixels[sourceIndex + x];
                    if (sourcePixel.a == 0) continue;
                    Color32 destinationPixel =
                        destinationPixels[destinationIndex + x];
                    PixelRgba result = Rgba32Math.SourceOver(
                        new PixelRgba(
                            sourcePixel.r, sourcePixel.g,
                            sourcePixel.b, sourcePixel.a),
                        new PixelRgba(
                            destinationPixel.r, destinationPixel.g,
                            destinationPixel.b, destinationPixel.a));
                    destinationPixels[destinationIndex + x] = new Color32(
                        result.R, result.G, result.B, result.A);
                }
            }
            destination.SetPixels32(destinationPixels);
            destination.Apply(false, false);
        }

        internal void DrawQuadsToActive(
            IList<Vector3> vertices,
            IList<Vector2> uvs,
            IList<Color32> colors,
            Texture texture)
            => DrawQuadsToActive(
                vertices, uvs, colors, texture, spriteMaterial);

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
                    "Buffered font target is unavailable.");
            if (fontMesh == null)
            {
                fontMesh = new Mesh
                {
                    name = "EPrimeReadouts.PanelGlyphMesh",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            fontMesh.Clear();
            fontMesh.SetVertices(vertices);
            fontMesh.SetUVs(0, uvs);
            fontMesh.SetColors(colors);
            fontMesh.SetTriangles(triangles, 0, calculateBounds: false);

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
                        fontMesh, Matrix4x4.identity, fontMaterial);
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

        internal static void Clear(RenderTexture target)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = target;
            try
            {
                GL.Clear(clearDepth: true, clearColor: true, Color.clear);
            }
            finally
            {
                RenderTexture.active = previous;
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
            ReleaseTexture(reader);
            reader = null;
            ReleaseTexture(fontMesh);
            fontMesh = null;
            ReleaseTexture(spriteMaterial);
            spriteMaterial = null;
        }

        private bool ValidateRoundTrip()
        {
            var source = new Texture2D(
                1, 1, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };
            RenderTexture? working = null;
            Texture2D? published = null;
            RenderTexture? final = null;
            try
            {
                var sourcePixel = new PixelRgba(200, 100, 50, 128);
                source.SetPixel(0, 0, new Color32(
                    sourcePixel.R, sourcePixel.G,
                    sourcePixel.B, sourcePixel.A));
                source.Apply(false, false);

                working = CreateWorkingSurface(1, 1);
                DrawProbe(working, source, Color.clear);
                PixelRgba observedPremultiplied = ReadPixel(working);
                PixelRgba expectedPremultiplied = Rgba32Math.Premultiply(
                    sourcePixel.R, sourcePixel.G,
                    sourcePixel.B, sourcePixel.A);
                if (!Near(observedPremultiplied, expectedPremultiplied, 2))
                    return false;

                published = CreatePublishedTexture(1, 1);
                Publish(working, published);
                PixelRgba observedStraight = ReadPixel(published);
                PixelRgba expectedStraight = Rgba32Math.Unpremultiply(
                    expectedPremultiplied.R, expectedPremultiplied.G,
                    expectedPremultiplied.B, expectedPremultiplied.A);
                if (!Near(observedStraight, expectedStraight, 2))
                    return false;

                var destination = new PixelRgba(20, 40, 80, 255);
                final = CreateWorkingSurface(1, 1);
                DrawProbe(final, published, new Color32(
                    destination.R, destination.G,
                    destination.B, destination.A));
                PixelRgba observedFinal = ReadPixel(final);
                PixelRgba expectedFinal = Rgba32Math.SourceOver(
                    expectedStraight, destination);
                return Near(observedFinal, expectedFinal, 2);
            }
            finally
            {
                ReleaseTexture(source);
                ReleaseTexture(working);
                ReleaseTexture(published);
                ReleaseTexture(final);
            }
        }

        private void DrawProbe(
            RenderTexture target, Texture texture, Color clear)
        {
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix(0f, 1f, 1f, 0f);
                GL.Clear(clearDepth: true, clearColor: true, clear);
                DrawToActive(new Rect(0f, 0f, 1f, 1f),
                    texture, Color.white);
            }
            finally
            {
                GL.PopMatrix();
                RenderTexture.active = previous;
            }
        }

        private PixelRgba ReadPixel(RenderTexture texture)
        {
            EnsureReader(1, 1);
            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = texture;
            try
            {
                reader!.ReadPixels(
                    new Rect(0f, 0f, 1f, 1f), 0, 0, false);
                reader.Apply(false, false);
                Color32 pixel = reader.GetPixel(0, 0);
                return new PixelRgba(pixel.r, pixel.g, pixel.b, pixel.a);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static PixelRgba ReadPixel(Texture2D texture)
        {
            Color32 pixel = texture.GetPixel(0, 0);
            return new PixelRgba(pixel.r, pixel.g, pixel.b, pixel.a);
        }

        private void EnsureReader(int width, int height)
        {
            if (reader != null
                && reader.width == width && reader.height == height) return;
            ReleaseTexture(reader);
            reader = new Texture2D(
                width, height, TextureFormat.RGBA32, false, true)
            {
                name = "EPrimeReadouts.PanelReadback",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private bool Disable(string reason)
        {
            available = false;
            Log.Warning("[Readouts] Buffered renderer disabled: " + reason);
            ReleaseTexture(reader);
            reader = null;
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
