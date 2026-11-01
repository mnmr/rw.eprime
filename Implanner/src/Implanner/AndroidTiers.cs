using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Android Tiers Continued bridge, resolved by name (no assembly
    /// reference; verified against the mod's decompiled source). Androids
    /// are operated on by doctors unless the mod's "androids can only be
    /// healed by crafters" setting is on; then only pawns doing Smithing
    /// work with the "repair androids" toggle (CompSurrogateOwner
    /// .repairAndroids, saved with the pawn) take their operations
    /// (WorkGiver_DoBill_Patch.JobOnThing).
    // Cache contract:
    // Owner: process.
    // Key: none.
    // Value: the android flesh type, the crafters' work type, the
    //   CompSurrogateOwner type with a field accessor for repairAndroids,
    //   and the setting's static field; immutable facts, never results.
    // Dependencies: the loaded assembly and definition sets (static per
    //   session).
    // Refresh policy: resolved once on first query.
    // Equality policy: never changes within a session.
    // Teardown: none needed; process-lifetime immutable facts that retain
    //   no world, pawn or Unity object.
    internal static class AndroidTiers
    {
        private static bool resolved;
        private static FleshTypeDef? androidFlesh;
        private static WorkTypeDef? crafterWork;
        private static Type? surrogateOwner;
        private static AccessTools.FieldRef<object, bool>? repairAndroids;
        private static System.Reflection.FieldInfo? craftersOnlySetting;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            androidFlesh = DefDatabase<FleshTypeDef>.GetNamedSilentFail("AndroidTier");
            if (androidFlesh == null) return;
            crafterWork = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Smithing");
            surrogateOwner = GenTypes.GetTypeInAnyAssembly("MOARANDROIDS.CompSurrogateOwner");
            if (surrogateOwner != null
                && AccessTools.Field(surrogateOwner, "repairAndroids")?.FieldType == typeof(bool))
                repairAndroids = AccessTools.FieldRefAccess<bool>(surrogateOwner, "repairAndroids");
            Type? settings = GenTypes.GetTypeInAnyAssembly("MOARANDROIDS.Settings");
            System.Reflection.FieldInfo? setting = settings != null
                ? AccessTools.Field(settings, "androidsCanOnlyBeHealedByCrafter")
                : null;
            if (setting != null && setting.IsStatic && setting.FieldType == typeof(bool))
                craftersOnlySetting = setting;
        }

        /// Whether Android Tiers is loaded.
        internal static bool Loaded
        {
            get
            {
                Resolve();
                return androidFlesh != null;
            }
        }

        /// Whether the pawn is one of the mod's androids (its flesh type,
        /// the test the mod itself applies).
        internal static bool IsAndroid(Pawn pawn)
        {
            Resolve();
            return androidFlesh != null && pawn.RaceProps.FleshType == androidFlesh;
        }

        /// Whether the pawn takes android operations while crafters operate:
        /// Smithing work active and the "repair androids" toggle on.
        /// Allocation-free; reached from the reconcile pass.
        internal static bool RepairsAndroids(Pawn pawn)
        {
            Resolve();
            if (crafterWork == null || repairAndroids == null || surrogateOwner == null)
                return false;
            if (pawn.workSettings == null || !pawn.workSettings.WorkIsActive(crafterWork))
                return false;
            List<ThingComp> comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
                if (comps[i].GetType() == surrogateOwner)
                    return repairAndroids(comps[i]);
            return false;
        }

        /// The mod's own (local, per-player) setting, or null when it
        /// cannot be read. Automation never reads it: FollowSetting copies
        /// it into the synced PlannerModel.AndroidSurgeryByCrafters.
        internal static bool? CraftersOnlySetting()
        {
            Resolve();
            return craftersOnlySetting?.GetValue(null) is bool value ? value : null;
        }

        /// Whether the mod is Android Tiers' own Mod class, whose settings
        /// window is the only place the setting changes.
        internal static bool IsAndroidTiersMod(Mod mod) =>
            mod.GetType().FullName == "MOARANDROIDS.AndroidTiersPP";

        /// Set when a game loads or starts; the first frame update while
        /// playing follows the setting (changes made from the main menu).
        /// Cleared on world teardown.
        internal static bool FollowPending;

        /// Implanner follows Android Tiers' setting: when it differs from
        /// the synced option, the option is set through the ordinary synced
        /// command, so automation only ever reads synced state. Called on
        /// the first update after a load or new game and after Android Tiers
        /// writes its settings during play (Patch_AndroidTiersSettings).
        /// Players whose settings differ in multiplayer leave the last
        /// write in effect for everyone, as Android Tiers itself does not
        /// support multiplayer. A no-op without Android Tiers or a game.
        internal static void FollowSetting()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            ImplannerStore? store = ImplannerStore.Current;
            bool? setting = CraftersOnlySetting();
            if (store == null || setting == null
                || store.Model.AndroidSurgeryByCrafters == setting.Value)
                return;
            PlannerCommands.SetAndroidSurgeryByCrafters(setting.Value);
        }
    }
}
