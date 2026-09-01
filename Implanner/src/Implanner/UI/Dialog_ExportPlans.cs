using Implanner.Core;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// Export dialog: summary line, framed read-only listing of the plans
    /// that will be exported, Copy to Clipboard beside the title, and a save
    /// row with a location picker (mod data folder, Desktop, user home or a
    /// custom directory) plus file name. Chrome pattern mirrors
    /// EPrimeReadouts' Dialog_ExportReadouts.
    public class Dialog_ExportPlans : Dialog_PlanPickerBase
    {
        // Cache contract:
        // Owner: one export window.
        // Key: ImplannerStore identity plus PlansVersion.
        // Value: detached immutable PlanRows projection and its serialized XML.
        // Dependencies: the Plans domain only; other store domains are unrelated.
        // Refresh policy: immediate in WindowUpdate, never in OnGUI.
        // Equality policy: an unchanged domain revision preserves rows/XML identity.
        // Teardown: PreClose releases rows, XML and derived text.
        private PlanRows? rows;
        private ImplannerStore? snapshotStore;
        private int snapshotPlansVersion = -1;
        private string? xml;          // export XML, rebuilt alongside the rows

        // Cache contract:
        // Owner: one export window.
        // Key: rows identity plus UiVersion.LanguageCurrent.
        // Value: translated summary line and per-plan implant-count captions.
        // Dependencies: rows snapshot identity and the language revision only.
        // Refresh policy: immediate re-translate behind the gate in EnsureText.
        // Equality policy: unchanged rows and language preserve the strings.
        // Teardown: PreClose releases the derived text with the rows.
        private PlanRows? textRows;
        private int textLanguageVersion = -1;
        private string? summaryText;
        private string[] captions = System.Array.Empty<string>();

        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(560f, 560f);

        public Dialog_ExportPlans()
        {
            RebuildSnapshot();
            RefreshResolvedPathCache();
        }

        private void RebuildSnapshot()
        {
            var store = ImplannerStore.Current;
            if (store == null) return;
            snapshotStore = store;
            snapshotPlansVersion = store.PlansVersion;
            rows = PlanRows.Capture(store.Model.Plans);
            xml = PlansXml.Export(store.Model.Plans, ModRequirements.PackageIdOf);
        }

        public override void PreClose()
        {
            rows = null;
            snapshotStore = null;
            textRows = null;
            xml = null;
            summaryText = null;
            captions = System.Array.Empty<string>();
            base.PreClose();
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            RefreshResolvedPathCache();
            var store = ImplannerStore.Current;
            if (store == null)
            {
                snapshotStore = null;
                rows = null;
                xml = null;
                snapshotPlansVersion = -1;
                return;
            }
            if (!ReferenceEquals(store, snapshotStore)
                || store.PlansVersion != snapshotPlansVersion)
                RebuildSnapshot();
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
                UiVersion.ObserveCurrentMetrics();
                PlanIoLabels.Ensure();
                EnsureText();

                float bodyTop = DrawTitle(inRect, PlanIoLabels.ExportTitle);

                // Copy to Clipboard lives top-right, beside the title: it acts
                // on the previewed plans, not on the save controls below.
                var copyRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, FooterH);
                if (Widgets.ButtonText(copyRect, PlanIoLabels.CopyClipboard,
                        active: xml != null)
                    && xml != null)
                {
                    GUIUtility.systemCopyBuffer = xml;
                }

                // ── Summary line ────────────────────────────────────────────
                float summaryH = Mathf.Max(18f, TinyText.LineHeight);
                GUI.color = PlannerStyle.CaptionText;
                TinyText.Label(new Rect(inRect.x, bodyTop, inRect.width, summaryH),
                    summaryText!);
                GUI.color = Color.white;
                bodyTop += summaryH + 2f;

                // Bottom-up layout: Cancel/Save row, optional custom-dir row,
                // then the location + file name row.
                float btnY = FooterY(inRect);
                float customRowY = btnY - FooterGap - (location == Location.Custom ? RowH : 0f);
                float locRowY = customRowY - RowH;

                // ── Framed listing fills the middle region ──────────────────
                var frameRect = new Rect(inRect.x, bodyTop, inRect.width,
                    locRowY - 6f - bodyTop);
                var listRect = DrawFrame(frameRect);
                if (rows != null)
                    DrawPlanListing(listRect, rows, captions, ref scroll);

                DrawLocationRows(inRect, locRowY, customRowY);

                string? path = CachedResolvedPath(out _, out _);

                // Bottom row: Cancel escapes left, Save commits right.
                var cancelRect = new Rect(inRect.x, btnY, ButtonW, FooterH);
                var saveRect = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, FooterH);
                if (Widgets.ButtonText(cancelRect, PlanIoLabels.Cancel))
                    Close();
                if (Widgets.ButtonText(saveRect, PlanIoLabels.Save,
                        active: path != null && xml != null)
                    && path != null && xml != null)
                {
                    if (PlansFiles.TryWrite(path, xml, out string? writeError))
                    {
                        Messages.Message("IMP_SavedTo".Translate(path),
                            MessageTypeDefOf.TaskCompletion, historical: false);
                        Close();
                    }
                    else
                    {
                        Messages.Message("IMP_SaveFailed".Translate(writeError),
                            MessageTypeDefOf.RejectInput, historical: false);
                    }
                }
            }
        }

        /// Rebuilds the translated summary and per-row captions only when the
        /// rows snapshot or language revision moved; steady repaints reuse the
        /// cached strings without translating or formatting.
        private void EnsureText()
        {
            if (ReferenceEquals(textRows, rows)
                && textLanguageVersion == UiVersion.LanguageCurrent
                && summaryText != null)
                return;
            int planCount = rows?.PlanCount ?? 0;
            int implantTotal = rows?.ImplantTotal ?? 0;
            summaryText = "IMP_ExportSummary".Translate(planCount, implantTotal);
            captions = new string[planCount];
            for (int i = 0; i < planCount; i++)
                captions[i] = "IMP_PlanGoalCount".Translate(rows!.ImplantCounts[i]);
            textRows = rows;
            textLanguageVersion = UiVersion.LanguageCurrent;
        }
    }
}
