using RimShared.UiLib;
using UnityEngine;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    /// Translated strings for the plan import/export dialogs, resolved once
    /// per language revision so render passes never translate. Read-only
    /// during drawing. (Local to the picker dialogs; the main window keeps
    /// its own PlannerLabels.)
    // Cache contract:
    // Owner: process/current UI presentation.
    // Key: none (single snapshot of all keys).
    // Value: translated strings and the location picker's labels, including
    //   its measured Small-font Enter-path width.
    // Dependencies: UiRevision.LanguageCurrent for the strings;
    //   UiRevision.Current for the Enter-path width.
    // Refresh policy: immediate rebuild on next Ensure() after the language
    //   revision (strings) or UI revision (width) moves.
    // Equality policy: unchanged revisions return the same strings and width.
    // Teardown: none needed (bounded static strings; the stamp gates handle
    //   refreshes for the process lifetime).
    internal static class PlanIoLabels
    {
        private static int stamp = -1;
        private static int metricStamp = -1;

        internal static readonly ExportPickerLabels Picker = new ExportPickerLabels();

        internal static string ExportTitle = "";
        internal static string ImportTitle = "";
        internal static string CopyClipboard = "";
        internal static string FromClipboard = "";
        internal static string Save = "";
        internal static string Cancel = "";
        internal static string Back = "";
        internal static string Import = "";
        internal static string ImportAddNote = "";
        internal static string NoFiles = "";

        internal static void Ensure()
        {
            if (stamp != UiRevision.LanguageCurrent)
            {
                stamp = UiRevision.LanguageCurrent;
                ExportTitle = "IMP_ExportPlansTitle".Translate();
                ImportTitle = "IMP_ImportPlansTitle".Translate();
                CopyClipboard = "IMP_CopyClipboard".Translate();
                FromClipboard = "IMP_FromClipboard".Translate();
                Save = "IMP_Save".Translate();
                Cancel = "CancelButton".Translate();
                Back = "Back".Translate();
                Import = "IMP_Import".Translate();
                ImportAddNote = "IMP_ImportAddNote".Translate();
                NoFiles = "IMP_NoFiles".Translate();
                Picker.GameData = "IMP_LocGameData".Translate();
                Picker.Desktop = "IMP_LocDesktop".Translate();
                Picker.UserHome = "IMP_LocUserHome".Translate();
                Picker.Custom = "IMP_LocCustom".Translate();
                Picker.EnterPath = "IMP_EnterPath".Translate();
                Picker.BadFileName = "IMP_BadFileName".Translate();
                Picker.BadDirectory = "IMP_BadDirectory".Translate();
            }
            if (metricStamp != UiRevision.Current)
            {
                metricStamp = UiRevision.Current;
                GameFont previousFont = Text.Font;
                try
                {
                    Text.Font = GameFont.Small;
                    Picker.EnterPathWidth = WrText.FitWidth(Picker.EnterPath) + 6f;
                }
                finally
                {
                    Text.Font = previousFont;
                }
            }
        }
    }

    /// Shared chrome and location/file plumbing for the plan export and
    /// import dialogs: title strip, body/footer geometry, and the shared
    /// location picker (mod data folder under the game's save data root,
    /// Desktop, user home or a custom directory, plus a file name field).
    /// Adapted from EPrimeReadouts' Dialog_EprPreviewBase plus
    /// Dialog_EprFilePicker.
    public abstract class Dialog_PlanPickerBase : Window
    {
        protected const float TitleH = 38f;
        protected const float FooterH = 32f;
        protected const float FooterGap = 8f;
        protected const float ButtonW = 140f;
        protected const float RowH = 30f;

        private static readonly ExportFolder Exports = new ExportFolder("Implanner");

        private protected readonly ExportLocationPicker picker =
            new ExportLocationPicker(Exports, "Plans.xml");

        protected Dialog_PlanPickerBase()
        {
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;
            draggable = true;
            forcePause = false;
            closeOnAccept = false;
        }

        // ── Chrome ───────────────────────────────────────────────────────────

        /// Draws the (pre-translated) title and returns the Y just below it.
        protected static float DrawTitle(Rect inRect, string title)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleH), title);
            Text.Font = GameFont.Small;
            return inRect.y + TitleH;
        }

        /// Footer Y: top of the footer button row.
        protected static float FooterY(Rect inRect) => inRect.yMax - FooterH;

        /// Inset panel behind list content; returns the inner content rect.
        /// Device-pixel-snapped frame in the shared panel palette — the
        /// vanilla outline helper bleeds past the fill at fractional scales.
        protected static Rect DrawFrame(Rect rect)
        {
            PixelBox.SolidWithOutline(rect,
                SegmentedControl.PanelBackground,
                SegmentedControl.PanelOutline);
            return rect.ContractedBy(6f);
        }

        /// Location dropdown (+ file name field for export-style dialogs), and
        /// the Enter-path row while Custom is picked. PlanIoLabels.Ensure()
        /// must have run this pass.
        protected void DrawLocationRows(Rect inRect, float locRowY, float customRowY,
            bool includeNameField = true) =>
            picker.DrawLocationRows(inRect, locRowY, customRowY, PlanIoLabels.Picker,
                includeNameField);

        // ── Shared plan preview listing ──────────────────────────────────────

        /// Detached, immutable projection of a plan list for the preview
        /// listings: names plus physical implant counts (each selected slot is
        /// one implant). Snapshot ownership — every field is copied out of the
        /// source plans at capture, so later model mutations can't reach it.
        protected sealed class PlanRows
        {
            public readonly string[] Names;
            public readonly int[] ImplantCounts;
            public readonly int ImplantTotal;

            private PlanRows(string[] names, int[] implantCounts, int implantTotal)
            {
                Names = names;
                ImplantCounts = implantCounts;
                ImplantTotal = implantTotal;
            }

            public int PlanCount => Names.Length;

            public static PlanRows Capture(System.Collections.Generic.IReadOnlyList<Plan> plans)
            {
                var names = new string[plans.Count];
                var counts = new int[plans.Count];
                int total = 0;
                for (int i = 0; i < plans.Count; i++)
                {
                    Plan plan = plans[i];
                    names[i] = plan.Name;
                    int count = 0;
                    for (int g = 0; g < plan.Implants.Count; g++)
                        count += plan.Implants[g].SlotOrdinals.Count;
                    counts[i] = count;
                    total += count;
                }
                return new PlanRows(names, counts, total);
            }
        }

        protected static float PreviewRowH => Mathf.Max(24f, TinyText.LineHeight + 4f);

        private static readonly Color RowStripe = new Color(1f, 1f, 1f, 0.03f);

        /// Simple read-only listing: one row per plan, name left (Small),
        /// pre-built implant-count caption right (Tiny). Bounded indexed
        /// iteration over already-built strings; no model access.
        protected static void DrawPlanListing(
            Rect listRect, PlanRows rows, string[] captions, ref Vector2 scroll)
        {
            if (listRect.height <= 0f || rows.PlanCount == 0) return;

            float rowH = PreviewRowH;
            float totalH = rows.PlanCount * rowH;
            bool needsBar = totalH > listRect.height;
            var viewRect = new Rect(0f, 0f,
                listRect.width - (needsBar ? GenUI.ScrollBarWidth : 0f), totalH);

            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            try
            {
                for (int i = 0; i < rows.PlanCount; i++)
                {
                    var rowRect = new Rect(0f, i * rowH, viewRect.width, rowH);
                    if (i % 2 == 0)
                        Widgets.DrawBoxSolid(rowRect, RowStripe);

                    float captionW = rowRect.width * 0.35f;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y,
                        rowRect.width - captionW - 12f, rowH), rows.Names[i]);

                    GUI.color = PlannerStyle.CaptionText;
                    Text.Anchor = TextAnchor.MiddleRight;
                    TinyText.Label(new Rect(rowRect.xMax - captionW - 4f, rowRect.y,
                        captionW, rowH), captions[i]);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }
}
