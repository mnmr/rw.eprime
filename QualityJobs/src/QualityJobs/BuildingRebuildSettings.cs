using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs
{
    /// Player-authored settings owned by one construction plan, captured before
    /// the original building despawns. Persist only these fields, not the old
    /// building or its component graph. Released with the plan on completion,
    /// target loss, map removal or mod disable. This is authoritative state,
    /// not a render cache. Unknown mod components retain vanilla rebuild behavior.
    public sealed class BuildingRebuildSettings : IExposable
    {
        private bool hasColor, hasBed;
        private Color color;
        private Color? desiredColor;
        private ColorDef? buildingPaint;
        private BedOwnerType bedOwnerType;
        private bool medical;
        private List<Pawn>? owners;

        internal static BuildingRebuildSettings Capture(Building building)
        {
            var result = new BuildingRebuildSettings();
            result.buildingPaint = building.PaintColorDef;
            CompColorable? paint = building.TryGetComp<CompColorable>();
            if (paint != null)
            {
                result.hasColor = paint.Active;
                result.color = paint.Color;
                result.desiredColor = paint.DesiredColor;
            }
            if (building is Building_Bed bed)
            {
                result.hasBed = true;
                result.medical = bed.Medical;
                result.bedOwnerType = bed.ForOwnerType;
                result.owners = new List<Pawn>(bed.OwnersForReading);
            }
            return result;
        }

        internal void Apply(Building building, bool accepted)
        {
            building.ChangePaint(buildingPaint);
            CompColorable? paint = building.TryGetComp<CompColorable>();
            if (paint != null)
            {
                if (hasColor)
                {
                    // SetColor(white) alone is a no-op for an inactive component
                    // whose internal default is already white. Activate through
                    // the public API before restoring that exact color.
                    if (!paint.Active && color == Color.white) paint.SetColor(Color.clear);
                    paint.SetColor(color);
                }
                else paint.Disable();
                paint.DesiredColor = desiredColor;
            }
            if (!hasBed || !(building is Building_Bed bed)) return;
            bed.ForOwnerType = bedOwnerType;
            bed.Medical = medical;
            if (!accepted || medical || owners == null) return;
            for (int i = 0; i < owners.Count; i++)
            {
                Pawn pawn = owners[i];
                // Do not steal a pawn's new bed while this one was unavailable.
                if (pawn == null || pawn.Dead || pawn.MapHeld != bed.Map
                    || pawn.ownership == null || pawn.ownership.OwnedBed != null
                    || !bed.CompAssignableToPawn.CanAssignTo(pawn).Accepted) continue;
                bed.CompAssignableToPawn.TryAssignPawn(pawn);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref hasColor, "hasColor");
            Scribe_Values.Look(ref color, "color");
            Scribe_Values.Look(ref desiredColor, "desiredColor");
            Scribe_Defs.Look(ref buildingPaint, "buildingPaint");
            Scribe_Values.Look(ref hasBed, "hasBed");
            Scribe_Values.Look(ref bedOwnerType, "bedOwnerType");
            Scribe_Values.Look(ref medical, "medical");
            Scribe_Collections.Look(ref owners, "owners", LookMode.Reference);
        }
    }
}
