using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// Immutable sparkle draw artifacts. Models are built by the owning
    /// QualityJobsStore when plan structure changes; the render patch only
    /// indexes the published arrays and submits draw calls.
    public static class SparkleOverlay
    {
        internal sealed class Model
        {
            internal readonly int ThingId;
            internal readonly IntVec3 Position;
            internal readonly Rot4 Rotation;
            internal readonly int MinX;
            internal readonly int MaxX;
            internal readonly int MinZ;
            internal readonly int MaxZ;
            internal readonly Matrix4x4[] Matrices;
            internal readonly Material[] Materials;

            private Model(int thingId, IntVec3 position, Rot4 rotation, CellRect rect,
                Matrix4x4[] matrices, Material[] materials)
            {
                ThingId = thingId;
                Position = position;
                Rotation = rotation;
                MinX = rect.minX;
                MaxX = rect.maxX;
                MinZ = rect.minZ;
                MaxZ = rect.maxZ;
                Matrices = matrices;
                Materials = materials;
            }

            internal static Model Build(Thing thing, Model? previous)
            {
                IntVec3 position = thing.Position;
                Rot4 rotation = thing.Rotation;
                CellRect rect = thing.OccupiedRect();
                int cellCount = rect.Area;
                if (previous != null
                    && previous.ThingId == thing.thingIDNumber
                    && previous.Position == position
                    && previous.Rotation == rotation
                    && previous.MinX == rect.minX
                    && previous.MaxX == rect.maxX
                    && previous.MinZ == rect.minZ
                    && previous.MaxZ == rect.maxZ)
                    return previous;

                var matrices = new Matrix4x4[cellCount];
                var materials = new Material[cellCount];
                int index = 0;
                for (int z = rect.minZ; z <= rect.maxZ; z++)
                {
                    for (int x = rect.minX; x <= rect.maxX; x++)
                    {
                        var cell = new IntVec3(x, 0, z);
                        int hash = unchecked(thing.thingIDNumber * 31 + index) & 3;
                        materials[index] = QualityJobsTex.SparkleMats[hash];
                        Vector3 worldPosition = cell.ToVector3ShiftedWithAltitude(
                            AltitudeLayer.MetaOverlays);
                        matrices[index] = Matrix4x4.TRS(
                            worldPosition, Quaternion.identity, Vector3.one);
                        index++;
                    }
                }

                return new Model(thing.thingIDNumber, position, rotation, rect,
                    matrices, materials);
            }
        }

        internal sealed class MapSnapshot
        {
            internal static readonly MapSnapshot Empty =
                new MapSnapshot(System.Array.Empty<Model>());

            internal readonly Model[] Models;

            internal MapSnapshot(Model[] models) => Models = models;

            internal bool HasSameModels(List<Model> models)
            {
                if (Models.Length != models.Count) return false;
                for (int i = 0; i < models.Count; i++)
                    if (!ReferenceEquals(Models[i], models[i])) return false;
                return true;
            }

            internal Model? Find(int thingId)
            {
                for (int i = 0; i < Models.Length; i++)
                    if (Models[i].ThingId == thingId) return Models[i];
                return null;
            }
        }

        internal static void Draw(MapSnapshot snapshot)
        {
            Model[] models = snapshot.Models;
            for (int modelIndex = 0; modelIndex < models.Length; modelIndex++)
            {
                Model model = models[modelIndex];
                Material[] materials = model.Materials;
                Matrix4x4[] matrices = model.Matrices;
                for (int i = 0; i < materials.Length; i++)
                    Graphics.DrawMesh(MeshPool.plane10, matrices[i], materials[i], 0);
            }
        }
    }
}
