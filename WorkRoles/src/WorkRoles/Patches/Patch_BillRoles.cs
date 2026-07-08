using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.Patches
{
    /// Enforces per-bill role restrictions at vanilla's single bill-worker gate
    /// (WorkGiver_DoBill consults PawnAllowedToStartAnew for every bill).
    [HarmonyPatch(typeof(Bill), nameof(Bill.PawnAllowedToStartAnew))]
    public static class Patch_Bill_PawnAllowedToStartAnew
    {
        public static void Postfix(Bill __instance, Pawn p, ref bool __result)
        {
            if (__result && !BillRoles.Allowed(__instance, p))
                __result = false;
        }
    }

    /// Bill copy-paste carries the role restriction to the clone.
    [HarmonyPatch(typeof(Bill), nameof(Bill.Clone))]
    public static class Patch_Bill_Clone
    {
        public static void Postfix(Bill __instance, Bill __result)
        {
            var store = RoleStore.Current;
            if (store != null && __result != null && store.billRoles.TryGetValue(__instance, out int roleId))
                store.billRoles[__result] = roleId;
        }
    }

    /// Role options live INSIDE vanilla's worker dropdown ("Anybody / colonists /
    /// Role: X"): picking a role clears the pawn restriction, picking any vanilla
    /// option clears the role — the two restrictions are mutually exclusive.
    [HarmonyPatch(typeof(Dialog_BillConfig), "GeneratePawnRestrictionOptions")]
    public static class Patch_DialogBillConfig_GeneratePawnRestrictionOptions
    {
        public static void Postfix(ref IEnumerable<Widgets.DropdownMenuElement<Pawn>> __result,
            Bill_Production ___bill)
            => __result = WithRoleOptions(__result, ___bill);

        private static IEnumerable<Widgets.DropdownMenuElement<Pawn>> WithRoleOptions(
            IEnumerable<Widgets.DropdownMenuElement<Pawn>> options, Bill_Production bill)
        {
            // Roles slot in after the "Anybody"-style options, before the colonist
            // list (colonist entries are the ones carrying a pawn payload).
            bool rolesEmitted = false;
            foreach (var element in options)
            {
                if (!rolesEmitted && element.payload != null)
                {
                    foreach (var roleElement in RoleOptions(bill)) yield return roleElement;
                    rolesEmitted = true;
                }
                // Any vanilla worker selection drops the role restriction.
                var vanillaAction = element.option.action;
                element.option.action = () =>
                {
                    RoleCommands.SetBillRole(bill, -1);
                    vanillaAction?.Invoke();
                };
                yield return element;
            }
            if (!rolesEmitted)
                foreach (var roleElement in RoleOptions(bill)) yield return roleElement;
        }

        private static IEnumerable<Widgets.DropdownMenuElement<Pawn>> RoleOptions(Bill_Production bill)
        {
            foreach (var role in BillRoles.EligibleRoles(bill))
            {
                int roleId = role.id;
                yield return new Widgets.DropdownMenuElement<Pawn>
                {
                    option = new FloatMenuOption("WR_BillRoleOption".Translate(role.label), () =>
                    {
                        bill.SetAnyPawnRestriction();
                        RoleCommands.SetBillRole(bill, roleId);
                    }),
                    payload = null
                };
            }
        }
    }

    /// The dropdown's button label is built inline in DoWindowContents, so an
    /// active role restriction would still read "Anybody". The worker section's
    /// height constant is bumped to a sentinel at startup (see WorkRolesMod); a
    /// BeginSection postfix recognizes it and records the section, and an
    /// EndSection prefix — still inside the section's GUI group, after vanilla
    /// drew its content — overdraws the dropdown button with the role's label.
    [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
    public static class Patch_DialogBillConfig_DoWindowContents
    {
        /// The bill whose dialog is currently drawing (null outside DoWindowContents).
        internal static Bill_Production CurrentBill;

        public static void Prefix(Bill_Production ___bill) => CurrentBill = ___bill;

        public static void Finalizer() => CurrentBill = null;
    }

    /// The worker section grew by the sentinel delta; without matching window
    /// height, Listing_Standard would push the section into an off-rect overflow
    /// column (Listing.NewColumnIfNeeded), hiding the whole panel.
    [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.InitialSize), MethodType.Getter)]
    public static class Patch_DialogBillConfig_InitialSize
    {
        public static void Postfix(ref Vector2 __result)
            => __result.y += Patch_ListingStandard_BeginSection.WorkerSectionHeight - 96f;
    }

    [HarmonyPatch(typeof(Listing_Standard), nameof(Listing_Standard.BeginSection))]
    public static class Patch_ListingStandard_BeginSection
    {
        /// Sentinel worker-section height; WorkerSelectionSubdialogHeight is set to
        /// this at startup (vanilla value 96 + a sliver so the sentinel is unique).
        public const int WorkerSectionHeight = 101;

        internal static Listing_Standard WorkerSection;

        public static void Postfix(float height, Listing_Standard __result)
        {
            if (Patch_DialogBillConfig_DoWindowContents.CurrentBill != null
                && (int)height == WorkerSectionHeight)
                WorkerSection = __result;
        }
    }

    [HarmonyPatch(typeof(Listing_Standard), nameof(Listing_Standard.EndSection))]
    public static class Patch_ListingStandard_EndSection
    {
        public static void Prefix(Listing_Standard listing)
        {
            if (listing == null || listing != Patch_ListingStandard_BeginSection.WorkerSection) return;
            Patch_ListingStandard_BeginSection.WorkerSection = null;

            var bill = Patch_DialogBillConfig_DoWindowContents.CurrentBill;
            if (bill == null || bill.PawnRestriction != null) return;
            var role = BillRoles.RestrictionFor(bill);
            if (role == null) return;

            // Local coordinates: the section's GUI group is still open and the
            // worker dropdown was its first 30f row. Overdraw its button; the
            // click was already handled by the dropdown underneath this frame.
            var rect = new Rect(0f, 0f, listing.ColumnWidth, 30f);
            Widgets.ButtonText(rect, "WR_BillRoleOption".Translate(role.label));
            TooltipHandler.TipRegion(rect, "WR_BillWorkerRoleTip".Translate());
        }
    }
}
