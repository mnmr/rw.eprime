using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles
{
    public class WorkRolesMod : Mod
    {
        public const string HarmonyId = "mnmr.workroles";

        public WorkRolesMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            // Grow the bill dialog's worker-selection section to fit the role
            // restriction button (the value doubles as the section's sentinel —
            // see Patch_ListingStandard_BeginSection).
            AccessTools.StaticFieldRefAccess<int>(typeof(Dialog_BillConfig), "WorkerSelectionSubdialogHeight")
                = Patches.Patch_ListingStandard_BeginSection.WorkerSectionHeight;
            Log.Message("[WorkRoles] loaded");
        }
    }
}
