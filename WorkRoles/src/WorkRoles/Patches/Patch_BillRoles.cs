using System;
using System.Collections.Generic;
using HarmonyLib;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.Patches
{
    /// Guards the private bill-dialog surface used to add role choices and
    /// repaint the selected label. Runtime bill enforcement is independent.
    internal static class BillDialogCompatibility
    {
        private static bool initialized;

        internal static bool WorkerUiAvailable { get; private set; }

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            try
            {
                var height = AccessTools.Field(
                    typeof(Dialog_BillConfig), "WorkerSelectionSubdialogHeight");
                var options = AccessTools.Method(
                    typeof(Dialog_BillConfig), "GeneratePawnRestrictionOptions");
                if (height == null || !height.IsStatic
                    || height.FieldType != typeof(int)
                    || options == null || options.IsStatic
                    || options.GetParameters().Length != 0
                    || !typeof(IEnumerable<Widgets.DropdownMenuElement<Pawn>>)
                        .IsAssignableFrom(options.ReturnType))
                    throw new MissingMemberException(
                        "Dialog_BillConfig worker-selection internals have an unexpected shape");

                height.SetValue(null,
                    Patch_ListingStandard_BeginSection.WorkerSectionHeight);
                WorkerUiAvailable = true;
            }
            catch (Exception exception)
            {
                WorkerUiAvailable = false;
                Log.Warning("[WorkRoles] Bill role selection UI disabled; "
                    + "runtime bill restrictions remain active: " + exception.Message);
            }
        }
    }

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

    /// Surgery bills have no config dialog, so their health-tab row carries the
    /// worker choice ("Any worker / Role: X") itself. Vanilla's label wraps in
    /// the narrow operations column and its second line reaches into the row's
    /// 29..53 band, so the row grows by vanilla's own per-bill status line
    /// (StatusString non-empty + StatusLineMinHeight, as Bill_Production does
    /// for "Paused"). Vanilla centres its info button in the area below the
    /// first line (Bill.DoConfigInterface: yMin += 29, then the centre); the
    /// worker button takes the same centre line to its left, and its own label
    /// is truncated to the button width. The surgery text is never altered.
    [HarmonyPatch(typeof(Bill), "StatusString", MethodType.Getter)]
    public static class Patch_Bill_StatusString
    {
        public static void Postfix(Bill __instance, ref string __result)
        {
            if (__instance is Bill_Medical && RoleStore.Current != null && __result.NullOrEmpty())
                __result = " "; // non-empty so the band exists; draws nothing visible
        }
    }

    [HarmonyPatch(typeof(Bill), "StatusLineMinHeight", MethodType.Getter)]
    public static class Patch_Bill_StatusLineMinHeight
    {
        public static void Postfix(Bill __instance, ref float __result)
        {
            if (__instance is Bill_Medical && RoleStore.Current != null
                && __result < Patch_Bill_DoConfigInterface.BandHeight)
                __result = Patch_Bill_DoConfigInterface.BandHeight;
        }
    }

    /// Draws the worker button on the info button's centre line. The rect
    /// argument is read only for its extents (vanilla shifts its yMin by 29f
    /// while drawing); the centre is recomputed from vanilla's constant.
    [HarmonyPatch(typeof(Bill), "DoConfigInterface")]
    public static class Patch_Bill_DoConfigInterface
    {
        internal const float BandHeight = 24f;
        /// Vanilla: the config area starts 29f down; the info button is 24f
        /// square and ends 12f before the right edge; 4f gap before it.
        private const float ConfigTop = 29f, LabelLeft = 28f, InfoButtonReserve = 40f;
        /// ButtonText's own horizontal text padding.
        private const float ButtonTextPadding = 10f;

        // Cache contract. Owner: process. Key: (font, button label, button
        // width in whole pixels). Value: the truncated button label string
        // (immutable). Dependencies: UiVersion.Current (UI scale / tiny-font
        // preference) via the stamp; language changes alter the key text and
        // also clear through WindowDataLifecycle.ReleaseShared. Refresh:
        // immediate on stamp mismatch. Equality: strings; identity irrelevant.
        // Teardown: ReleaseShared (window close, world unload, language change).
        private static readonly Dictionary<(GameFont, string, int), string> buttonLabels
            = new Dictionary<(GameFont, string, int), string>();
        private static int stamp = -1;

        public static void Postfix(Bill __instance, Rect rect)
        {
            if (!(__instance is Bill_Medical bill) || RoleStore.Current == null) return;
            float centerY = (ConfigTop + rect.yMax) / 2f;
            var buttonRect = new Rect(LabelLeft, centerY - BandHeight / 2f,
                rect.xMax - LabelLeft - InfoButtonReserve, BandHeight);
            var role = BillRoles.RestrictionFor(bill);
            string label = role == null
                ? "AnyWorker".Translate()
                : "WR_BillRoleOption".Translate(role.label);
            if (Widgets.ButtonText(buttonRect, ButtonLabel(label, buttonRect.width - ButtonTextPadding)))
                Find.WindowStack.Add(new FloatMenu(WorkerOptions(bill)));
            WrTips.Key("WR_SurgeryWorkerRoleTip").Region(buttonRect);
        }

        private static string ButtonLabel(string label, float width)
        {
            if (stamp != UiVersion.Current)
            {
                buttonLabels.Clear();
                stamp = UiVersion.Current;
            }
            var key = (Text.Font, label, (int)width);
            if (!buttonLabels.TryGetValue(key, out string result))
            {
                // Same drift margin as WrText.FitWidth so a fractional UI scale
                // cannot wrap a label that measured as fitting.
                result = WrText.FitWidth(label) <= width ? label : label.Truncate((width - 2f) / 1.02f);
                buttonLabels[key] = result;
            }
            return result;
        }

        internal static void Clear()
        {
            buttonLabels.Clear();
            stamp = -1;
        }

        /// Built on click only: "Any worker" clears the restriction; eligible roles set it.
        private static List<FloatMenuOption> WorkerOptions(Bill bill)
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("AnyWorker".Translate(), () => RoleCommands.SetBillRole(bill, -1))
            };
            foreach (var role in BillRoles.EligibleRoles(bill))
            {
                int roleId = role.id;
                options.Add(new FloatMenuOption("WR_BillRoleOption".Translate(role.label),
                    () => RoleCommands.SetBillRole(bill, roleId)));
            }
            return options;
        }
    }

    /// Bill copy-paste carries the role restriction to the clone.
    [HarmonyPatch(typeof(Bill), nameof(Bill.Clone))]
    public static class Patch_Bill_Clone
    {
        public static void Postfix(Bill __instance, Bill __result)
        {
            BillRoleTransfer.PropagateClone(__instance, __result, RoleStore.Current);
        }
    }

    /// Multiplayer 1.6 exposes BillStack.AddBill's bill parameter through the
    /// normal IExposable pipeline. Scribe only the detached transient marker;
    /// attached bills have already consumed it, so ordinary saves omit the field.
    [HarmonyPatch(typeof(Bill), nameof(Bill.ExposeData))]
    public static class Patch_Bill_ExposeData
    {
        public static void Postfix(Bill __instance)
        {
            var store = RoleStore.Current;
            int roleId = BillRoleTransfer.RoleIdForScribe(__instance, store);
            Scribe_Values.Look(ref roleId, "workRoles_billRoleId", -1);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                BillRoleTransfer.RestoreFromScribe(__instance, store, roleId);
        }
    }

    /// AddBill is synchronized by Multiplayer 1.6 with parameter 0 exposed.
    /// After vanilla/other patches attach this exact bill, consume the one-shot
    /// marker and promote its role through the centralized store setter.
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill), typeof(Bill))]
    public static class Patch_BillStack_AddBill
    {
        public static void Postfix(BillStack __instance, Bill bill)
        {
            var store = RoleStore.Current;
            if (store == null || bill == null
                || !RoleStore.BillStackContainsReference(__instance, bill)) return;
            if (!BillRoleTransfer.TryConsume(bill, store, out int roleId)) return;
            store.SetBillRole(bill, roleId);
        }
    }

    /// Audited RimWorld 1.6 lifecycle: Delete(Bill) sets Bill.deleted, removes
    /// the exact reference, then notifies the giver. Postfix avoids dropping a
    /// mapping when another patch suppressed or reversed the deletion.
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.Delete), typeof(Bill))]
    public static class Patch_BillStack_Delete
    {
        public static void Postfix(BillStack __instance, Bill bill)
        {
            if (bill == null) return;
            if (bill.deleted || !RoleStore.BillStackContainsReference(__instance, bill))
                RoleStore.Current?.RemoveBillRole(bill);
        }
    }

    /// Audited RimWorld 1.6 lifecycle: Clear() directly clears the private list;
    /// it does not call Delete or mark bills deleted. Capture only mapped refs,
    /// then remove those actually absent after the original and other patches.
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.Clear))]
    public static class Patch_BillStack_Clear
    {
        public static void Prefix(BillStack __instance, out List<Bill>? __state)
        {
            __state = RoleStore.Current?.CaptureBillRolesForStack(__instance);
        }

        public static void Postfix(BillStack __instance, List<Bill>? __state)
        {
            RoleStore.Current?.RemoveCapturedBillRolesMissingFromStack(__instance, __state!); // callee null-checks
        }
    }

    /// Audited RimWorld 1.6 lifecycle: RemoveIncompletableBills() removes list
    /// entries and notifies the giver directly, bypassing Delete and Clear. The
    /// state list is allocated only when a mapped bill is currently removable.
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.RemoveIncompletableBills))]
    public static class Patch_BillStack_RemoveIncompletableBills
    {
        public static void Prefix(BillStack __instance, out List<Bill>? __state)
        {
            __state = RoleStore.Current?.CaptureBillRolesForStack(
                __instance, onlyIncompletable: true);
        }

        public static void Postfix(BillStack __instance, List<Bill>? __state)
        {
            RoleStore.Current?.RemoveCapturedBillRolesMissingFromStack(__instance, __state!); // callee null-checks
        }
    }

    /// Bill.DeletedOrDereferenced becomes true when its Thing giver is destroyed,
    /// but reading that property is not a lifecycle event and is null-unsafe in
    /// 1.6. Preserve the stack before destruction and clean it only after success.
    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy), typeof(DestroyMode))]
    public static class Patch_Thing_DestroyBillRoles
    {
        public static void Prefix(Thing __instance, out BillStack? __state)
        {
            __state = (__instance as IBillGiver)?.BillStack;
        }

        public static void Postfix(Thing __instance, BillStack? __state)
        {
            if (__instance != null && __instance.Destroyed)
                RoleStore.Current?.RemoveBillRolesForStack(__state!); // callee null-checks
        }
    }

    /// Role options live INSIDE vanilla's worker dropdown ("Anybody / colonists /
    /// Role: X"): picking a role clears the pawn restriction, picking any vanilla
    /// option clears the role — the two restrictions are mutually exclusive.
    [HarmonyPatch(typeof(Dialog_BillConfig), "GeneratePawnRestrictionOptions")]
    public static class Patch_DialogBillConfig_GeneratePawnRestrictionOptions
    {
        public static bool Prepare() =>
            BillDialogCompatibility.WorkerUiAvailable;

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
                    payload = null! // role entries carry no pawn payload
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
        internal static Bill_Production? CurrentBill;

        public static bool Prepare() =>
            BillDialogCompatibility.WorkerUiAvailable;

        public static void Prefix(Bill_Production ___bill) => CurrentBill = ___bill;

        public static void Finalizer()
        {
            CurrentBill = null;
            Patch_ListingStandard_BeginSection.WorkerSection = null;
        }

        internal static void ReleaseForTeardown()
        {
            CurrentBill = null;
            Patch_ListingStandard_BeginSection.WorkerSection = null;
        }
    }

    /// The worker section grew by the sentinel delta; without matching window
    /// height, Listing_Standard would push the section into an off-rect overflow
    /// column (Listing.NewColumnIfNeeded), hiding the whole panel.
    [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.InitialSize), MethodType.Getter)]
    public static class Patch_DialogBillConfig_InitialSize
    {
        public static bool Prepare() =>
            BillDialogCompatibility.WorkerUiAvailable;

        public static void Postfix(ref Vector2 __result)
            => __result.y += Patch_ListingStandard_BeginSection.WorkerSectionHeight - 96f;
    }

    [HarmonyPatch(typeof(Listing_Standard), nameof(Listing_Standard.BeginSection))]
    public static class Patch_ListingStandard_BeginSection
    {
        /// Sentinel worker-section height; WorkerSelectionSubdialogHeight is set to
        /// this at startup (vanilla value 96 + a sliver so the sentinel is unique).
        public const int WorkerSectionHeight = 101;

        internal static Listing_Standard? WorkerSection;

        public static bool Prepare() =>
            BillDialogCompatibility.WorkerUiAvailable;

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
        public static bool Prepare() =>
            BillDialogCompatibility.WorkerUiAvailable;

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
            WrTips.Key("WR_BillWorkerRoleTip").Region(rect);
        }
    }
}
