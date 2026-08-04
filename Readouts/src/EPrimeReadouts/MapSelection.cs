using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts
{
    /// Selects, on one map, the things behind a clicked readout slot. The
    /// candidate passes mirror GameCounts.AccumulateMap (stored stacks, then
    /// scattered haulables) narrowed by the same storage-only/hide-forbidden
    /// options, so the selection matches the displayed count. Selection is
    /// per-player presentation state — vanilla multiplayer does not sync it —
    /// so no command or revision is involved. Runs only from a discrete click
    /// event, never from a steady render pass.
    internal static class MapSelection
    {
        // Scratch buffers reused across clicks; cleared before and after use.
        // Click-path only, so single-threaded main-thread access is guaranteed.
        private static readonly HashSet<string> memberSet = new HashSet<string>();
        private static readonly List<Thing> candidates = new List<Thing>();

        public static void SelectMembers(
            Map map,
            IReadOnlyList<string> members,
            bool storageOnly,
            bool hideForbidden,
            bool additive,
            bool jumpCamera)
        {
            if (map == null || members == null || members.Count == 0) return;
            var selector = Find.Selector;
            if (selector == null) return;

            memberSet.Clear();
            for (int i = 0; i < members.Count; i++) memberSet.Add(members[i]);
            candidates.Clear();
            GatherStored(map, hideForbidden);
            if (!storageOnly) GatherScattered(map, hideForbidden);
            memberSet.Clear();

            // Shift-click toggles: when every matching thing is already
            // selected, the click deselects them all instead — and the camera
            // stays put.
            if (additive && AllSelected(selector))
            {
                for (int i = 0; i < candidates.Count; i++)
                    selector.Deselect(candidates[i]);
                candidates.Clear();
                return;
            }
            if (!additive) selector.ClearSelection();

            bool selectedAny = false;
            Thing jumpTarget = null;
            float jumpDistSq = float.MaxValue;
            IntVec3 cameraCell = Find.CameraDriver != null
                ? Find.CameraDriver.MapPosition : map.Center;
            for (int i = 0; i < candidates.Count; i++)
            {
                Thing thing = candidates[i];
                if (selector.IsSelected(thing)) continue;
                // Vanilla caps the selection list at 200; further Select calls
                // would be silently ignored, so stop offering them.
                if (selector.NumSelected >= 200) break;
                // One audible confirmation for the batch, not one per stack.
                selector.Select(thing, playSound: !selectedAny);
                selectedAny = true;
                float distSq = (thing.PositionHeld - cameraCell).LengthHorizontalSquared;
                if (distSq < jumpDistSq)
                {
                    jumpDistSq = distSq;
                    jumpTarget = thing;
                }
            }
            candidates.Clear();

            if (jumpCamera && jumpTarget != null)
                CameraJumper.TryJump(jumpTarget.PositionHeld, map);
        }

        private static bool AllSelected(Selector selector)
        {
            if (candidates.Count == 0) return false;
            for (int i = 0; i < candidates.Count; i++)
                if (!selector.IsSelected(candidates[i])) return false;
            return true;
        }

        /// Stored pass: haul destinations, matching the counted criteria.
        private static void GatherStored(Map map, bool hideForbidden)
        {
            var groups = map.haulDestinationManager.AllGroupsListForReading;
            for (int i = 0; i < groups.Count; i++)
            {
                foreach (Thing held in groups[i].HeldThings)
                {
                    var inner = held.GetInnerIfMinified();
                    if (!memberSet.Contains(inner.def.defName)) continue;
                    if (inner.IsNotFresh()) continue;
                    if (inner.SpawnedOrAnyParentSpawned
                        && inner.PositionHeld.Fogged(inner.MapHeld)) continue;
                    if (hideForbidden && held.IsForbidden(Faction.OfPlayer)) continue;
                    candidates.Add(held);
                }
            }
        }

        /// Scattered pass: spawned haulables outside any storage; only offered
        /// when the count basis includes them.
        private static void GatherScattered(Map map, bool hideForbidden)
        {
            var things = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing.IsInAnyStorage()) continue;
                var inner = thing.GetInnerIfMinified();
                if (!memberSet.Contains(inner.def.defName)) continue;
                if (inner.IsNotFresh()) continue;
                if (thing.Position.Fogged(map)) continue;
                if (hideForbidden && thing.IsForbidden(Faction.OfPlayer)) continue;
                candidates.Add(thing);
            }
        }
    }
}
