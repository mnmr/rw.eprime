using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// Per-frame sparkle overlay drawn above Blueprint_Build and Frame cells that
    /// have an active quality construction plan.
    ///
    /// Cache contract (Model):
    ///   Owner:       process (ConditionalWeakTable — entries are GC'd when their
    ///                Thing key is collected, so no world-change leak)
    ///   Key:         Thing reference (weak — the CWT holds it weakly)
    ///   Value:       Model — immutable-after-build draw data (position, rotation,
    ///                matrices[], mats[]); a positional mismatch triggers a rebuild
    ///   Dependencies: thing.Position, thing.Rotation, thing.OccupiedRect footprint
    ///   Refresh:     rebuilt lazily when Position or Rotation mismatch on access
    ///                (blueprints and frames do not normally move; the guard is a
    ///                safety net for edge cases such as install/reinstall)
    ///   Equality:    n/a — model is not compared; mismatch always rebuilds
    ///   Teardown:    CWT/GC — entries die with their Thing key automatically;
    ///                no explicit teardown required
    public static class SparkleOverlay
    {
        private sealed class Model
        {
            public IntVec3 position;
            public Rot4 rotation;
            public Matrix4x4[] matrices = System.Array.Empty<Matrix4x4>();
            public Material[] mats = System.Array.Empty<Material>();
        }

        // ConditionalWeakTable uses weak keys: when a Thing is GC-collected its
        // entry disappears automatically. No manual teardown needed.
        private static readonly ConditionalWeakTable<Thing, Model> models =
            new ConditionalWeakTable<Thing, Model>();

        /// Draws the sparkle overlay for the given thing (a Blueprint_Build or Frame).
        /// Called from the draw patch postfix — must be allocation-free after the
        /// first build, and must not traverse authoritative models, allocate, or log.
        public static void Draw(Thing thing)
        {
            if (!models.TryGetValue(thing, out Model? model))
            {
                model = new Model();
                models.Add(thing, model);
                Build(thing, model);
            }
            else if (model.position != thing.Position || model.rotation != thing.Rotation)
            {
                Build(thing, model);
            }

            // Allocation-free draw loop: indexed array access, no LINQ, no delegates.
            Material[] mats = model.mats;
            Matrix4x4[] matrices = model.matrices;
            int count = mats.Length;
            for (int i = 0; i < count; i++)
            {
                Graphics.DrawMesh(MeshPool.plane10, matrices[i], mats[i], 0);
            }
        }

        /// Builds (or rebuilds) the draw model for the given thing.
        /// Called only on cache miss or position/rotation change — not per-frame.
        private static void Build(Thing thing, Model model)
        {
            model.position = thing.Position;
            model.rotation = thing.Rotation;

            // Compute cell count without allocating: iterate minX..maxX x minZ..maxZ.
            // OccupiedRect() verified as extension on Thing (GenAdj.cs:572).
            CellRect rect = thing.OccupiedRect();
            int cellCount = rect.Area;

            if (model.matrices.Length != cellCount)
                model.matrices = new Matrix4x4[cellCount];
            if (model.mats.Length != cellCount)
                model.mats = new Material[cellCount];

            int idx = 0;
            // Iterate using struct fields — no IEnumerable boxing, no allocation.
            // AltitudeLayer.MetaOverlays verified in Decompiled\Verse\AltitudeLayer.cs:44.
            // ToVector3ShiftedWithAltitude(AltitudeLayer) verified at IntVec3.cs:163.
            for (int z = rect.minZ; z <= rect.maxZ; z++)
            {
                for (int x = rect.minX; x <= rect.maxX; x++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);

                    // Deterministic, stable tile selection: no Rand, no per-frame
                    // randomness. Hash combines thingIDNumber with cell index.
                    int hash = unchecked(thing.thingIDNumber * 31 + idx) & 3;
                    model.mats[idx] = QualityJobsTex.SparkleMats[hash];

                    // Matrix mirrors Frame.cs:484 pattern (Vector3.up * 0.03f offset).
                    Vector3 worldPos = cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
                    model.matrices[idx] = Matrix4x4.TRS(worldPos, Quaternion.identity, Vector3.one);

                    idx++;
                }
            }
        }
    }
}
