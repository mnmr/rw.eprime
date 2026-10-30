using System.Collections.Generic;
using HarmonyLib;
using Multiplayer.API;
using RimShared.Common;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Detached input for one native construction placement. Multiplayer binds
    /// every field explicitly; its existing precept worker supplies stable IDs.
    public sealed class CopyPlacementValues
    {
        public int mapId;
        public string buildingDefName = string.Empty;
        public string? stuffDefName;
        public string? styleDefName;
        public Precept_Building? sourcePrecept;
        public int rotation;
        public bool styleOverridden;
        public bool hasGlowerColor;
        public int glowerR, glowerG, glowerB, glowerA;
        public IntVec3[] cells = System.Array.Empty<IntVec3>();
        public bool multiCell;
        public int minSkill, targetQuality;
        public bool inspired, specialist, autoBest;
    }

    internal static class CopyPlacement
    {
        private sealed class Settings
        {
            internal readonly int MapId, MinSkill, TargetQuality;
            internal readonly bool Inspired, Specialist, AutoBest;

            internal Settings(int mapId, PlanPresentationSnapshot plan)
            {
                MapId = mapId;
                MinSkill = plan.MinSkill;
                TargetQuality = plan.MinQuality;
                Inspired = plan.RequireInspired;
                Specialist = plan.RequireSpecialist;
                AutoBest = plan.AutoBest;
            }
        }

        // Owner: local store (weak). Key: initiating designator identity (weak).
        // Value: immutable primitive copy settings and map ID. Dependencies:
        // source settings at the copy click. Refresh: explicit input only.
        // Equality: reuse until another copy click. Teardown: deselection, map
        // removal and game disposal clear the table; owner changes reject it.
        // This is local input state, never consulted during synchronized replay.
        private static readonly OwnerScopedTransferTable<Designator_Build, QualityJobsStore, Settings> pending =
            new OwnerScopedTransferTable<Designator_Build, QualityJobsStore, Settings>();

        private static readonly AccessTools.FieldRef<Designator_Place, Rot4> Rotation =
            AccessTools.FieldRefAccess<Designator_Place, Rot4>("placingRot");

        // Command-local scope only: set identically on every client from the
        // payload, released in finally. No selection or pending input is read.
        private static CopyPlacementValues? applying;
        private static Map? applyingMap;

        internal static void Stage(Designator_Build designator, QualityJobsStore store,
            PlanPresentationSnapshot plan)
            => pending.Set(designator, store, new Settings(designator.Map.uniqueID, plan));

        internal static void ClearInput() => pending.Clear();

        internal static bool TryIssue(Designator_Build designator, IEnumerable<IntVec3> cells,
            bool multiCell)
        {
            // Native MP replay may run while this client has another copy tool
            // selected. Only the issuing interface may turn input into a command.
            if (applying != null || (MP.IsInMultiplayer && !MP.InInterface)) return false;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || !pending.TryGet(designator, store, out Settings? settings)
                || settings == null || designator.Map.uniqueID != settings.MapId
                || designator.PlacingDef is not ThingDef building) return false;

            var positions = new List<IntVec3>();
            foreach (IntVec3 cell in cells) positions.Add(cell);
            if (positions.Count == 0) return false;
            ColorInt? color = designator.glowerColorOverride;
            var values = new CopyPlacementValues
            {
                mapId = settings.MapId,
                buildingDefName = building.defName,
                stuffDefName = designator.StuffDef?.defName,
                styleDefName = designator.styleDef?.defName,
                sourcePrecept = designator.sourcePrecept,
                rotation = Rotation(designator).AsInt,
                styleOverridden = designator.styleOverridden,
                hasGlowerColor = color.HasValue,
                glowerR = color?.r ?? 0, glowerG = color?.g ?? 0,
                glowerB = color?.b ?? 0, glowerA = color?.a ?? 0,
                cells = positions.ToArray(), multiCell = multiCell,
                minSkill = settings.MinSkill, targetQuality = settings.TargetQuality,
                inspired = settings.Inspired, specialist = settings.Specialist,
                autoBest = settings.AutoBest,
            };
            Commands.PlaceCopiedConstruction(values);
            return true;
        }

        internal static void Place(CopyPlacementValues values)
        {
            Map? map = Find.CurrentMap;
            if (map == null || map.uniqueID != values.mapId
                || QualityJobsStore.Active == null || values.cells.Length == 0) return;
            ThingDef? building = DefDatabase<ThingDef>.GetNamedSilentFail(values.buildingDefName);
            ThingDef? stuff = values.stuffDefName == null ? null
                : DefDatabase<ThingDef>.GetNamedSilentFail(values.stuffDefName);
            if (building == null || building.blueprintDef == null
                || !building.HasComp(typeof(CompQuality))
                || (building.MadeFromStuff && stuff == null)) return;
            var designator = new Designator_Build(building)
            {
                sourcePrecept = values.sourcePrecept,
                styleDef = values.styleDefName == null ? null
                    : DefDatabase<ThingStyleDef>.GetNamedSilentFail(values.styleDefName),
                styleOverridden = values.styleOverridden,
                glowerColorOverride = values.hasGlowerColor
                    ? new ColorInt(values.glowerR, values.glowerG, values.glowerB, values.glowerA)
                    : (ColorInt?)null,
            };
            designator.SetStuffDef(stuff);
            Rotation(designator) = new Rot4(values.rotation);
            CopyPlacementValues? previous = applying;
            Map? previousMap = applyingMap;
            try
            {
                applying = values;
                applyingMap = map;
                // Preserve vanilla validation, replacement, style, color,
                // place-worker callbacks and multi-cell finalization.
                if (values.multiCell) designator.DesignateMultiCell(values.cells);
                else if (designator.CanDesignateCell(values.cells[0]).Accepted)
                    designator.DesignateSingleCell(values.cells[0]);
            }
            finally
            {
                applying = previous;
                applyingMap = previousMap;
            }
        }

        internal static bool TryApply(QualityJobsStore store, Blueprint_Build blueprint)
        {
            CopyPlacementValues? values = applying;
            if (values == null || blueprint.Map != applyingMap
                || blueprint.def.entityDefToBuild.defName != values.buildingDefName) return false;
            for (int i = 0; i < values.cells.Length; i++)
            {
                if (blueprint.Position != values.cells[i]) continue;
                store.ApplyPlanSettings(blueprint.thingIDNumber, values.minSkill,
                    values.inspired, values.specialist, values.targetQuality, values.autoBest);
                return true;
            }
            return false;
        }
    }
}
