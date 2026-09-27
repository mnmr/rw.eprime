using HarmonyLib;
using RimWorld;
using Verse;

namespace Implanner.Patches
{
    /// An Implanner operation installs the item reserved for it, not
    /// whichever matching item lies nearest: the implant is a fixed
    /// ingredient of its surgery, which Bill.IsFixedOrAllowedIngredient
    /// accepts before the bill's own ingredient filter is consulted, so a
    /// quality or item choice cannot be expressed through the filter.
    /// For a bill bound in SurgeryBindings, every other implant item
    /// (isTechHediff) is refused; medicine and everything else is left to
    /// the game. Reads only the published map; mutates nothing. The doctor's
    /// ingredient search runs in the synchronized tick, and the map derives
    /// from synced model state, so every multiplayer client agrees.
    [HarmonyPatch(typeof(Bill), nameof(Bill.IsFixedOrAllowedIngredient),
        new[] { typeof(Thing) })]
    public static class Patch_BoundSurgeryIngredient
    {
        private static readonly AccessTools.FieldRef<Bill, int> LoadId =
            AccessTools.FieldRefAccess<Bill, int>("loadID");

        public static void Postfix(Bill __instance, Thing thing, ref bool __result)
        {
            if (!__result || !SurgeryBindings.Any || !thing.def.isTechHediff
                || !(__instance is Bill_Medical))
                return;
            if (SurgeryBindings.TryGetItem(LoadId(__instance), out int itemId)
                && thing.thingIDNumber != itemId)
                __result = false;
        }
    }
}
