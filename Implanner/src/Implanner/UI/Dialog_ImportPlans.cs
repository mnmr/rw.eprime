using System;
using System.Collections.Generic;
using System.IO;
using Implanner.Core;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    /// Import dialog: source stage (location picker + newest-first file list,
    /// or clipboard) → preview stage (parsed listing + additive note) →
    /// commit. Lives in one window with a stage enum. The location picker
    /// mirrors the export dialog's, so anything saved there can be loaded
    /// from here. Imported plans are ADDED to the existing list — never
    /// replacing it; duplicate names get numbered by the import command.
    /// Adapted from EPrimeReadouts' Dialog_ImportReadouts.
    public class Dialog_ImportPlans : Dialog_PlanPickerBase
    {
        private enum Stage { Source, Preview }

        private static float FileRowH => Mathf.Max(28f, TinyText.LineHeight);
        private const float DeleteW = 22f;

        private static readonly Color NoteColor = new Color(1f, 0.75f, 0.35f);

        // ── Stage ────────────────────────────────────────────────────────────
        private Stage stage = Stage.Source;

        // ── Source stage state ───────────────────────────────────────────────
        // Cache contract:
        // Owner: one import window.
        // Key: resolved directory string.
        // Value: file entries with preformatted immutable display metadata.
        // Dependencies: explicit directory changes/deletion refresh requests.
        // Refresh policy: WindowUpdate only, never OnGUI.
        // Equality policy: unchanged directory preserves list/entry identities.
        // Teardown: PreClose releases entries, XML and preview rows.
        private List<PlansFiles.Entry>? files;
        private string? listedDir;  // directory the current file list came from
        private Vector2 sourceScroll;
        private string? clip;
        private bool clipUsable;

        // ── Preview stage state ──────────────────────────────────────────────
        private string? pendingXml;
        private PlanRows? pendingRows;
        private Vector2 previewScroll;

        // Cache contract:
        // Owner: one import window.
        // Key: pending rows identity plus UiVersion.LanguageCurrent.
        // Value: translated summary/note strings and per-plan captions.
        // Dependencies: pending rows identity and the language revision only.
        // Refresh policy: immediate re-translate behind the gate in
        //   EnsurePreviewText.
        // Equality policy: unchanged rows and language preserve the strings.
        // Teardown: PreClose releases the derived text with the rows.
        private PlanRows? textRows;
        private int textLanguageVersion = -1;
        private string? previewSummary;
        private string[] previewCaptions = Array.Empty<string>();

        // Cache contract:
        // Owner: process/current UI presentation (shared measurement cache).
        // Key: note text, effective (Tiny) font and wrap width — via the
        //   shared RimShared.Common.TextHeightCache.
        // Value: Tiny-font wrapped note height.
        // Dependencies: key plus UiVersion.Current (scale/font/language
        //   metrics) as the revision.
        // Refresh policy: immediate re-measure on UI revision change.
        // Equality policy: unchanged keys return the cached float.
        // Teardown: bounded key set (one note string per language); the
        //   revision gate handles refreshes.
        private static readonly RimShared.Common.TextHeightCache noteHeights =
            new RimShared.Common.TextHeightCache();

        /// Static delegate: measurement never captures. Word wrap is ambient
        /// GUI state, so the wrapped measurement forces it on.
        private static readonly Func<(string Text, float Width), float> MeasureNote =
            static key =>
            {
                using (GuiStateScope.Capture())
                {
                    Text.WordWrap = true;
                    return TinyText.CalcHeight(key.Text, key.Width);
                }
            };

        private static float NoteHeight(string text, float width) =>
            noteHeights.Get(text, (int)GameFont.Tiny, width,
                UiVersion.Current, (text, width), MeasureNote);

        public override Vector2 InitialSize => new Vector2(560f, 560f);

        public override void PreOpen()
        {
            base.PreOpen();
            files = null;   // force a fresh directory listing
            RefreshClipboard();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// Directory listing for the picked location, refreshed by WindowUpdate
        /// when the directory changed or an explicit action invalidated it.
        private void EnsureFiles()
        {
            string dir = ResolvedDir();
            if (files != null && string.Equals(dir, listedDir, StringComparison.Ordinal))
                return;
            listedDir = dir;
            files = PlansFiles.ListFiles(dir);
            sourceScroll = Vector2.zero;
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (stage == Stage.Source) EnsureFiles();
        }

        private void RefreshClipboard()
        {
            clip = GUIUtility.systemCopyBuffer;
            clipUsable = !string.IsNullOrEmpty(clip) && clip.Contains("<ImplannerPlans");
        }

        /// Attempts to parse xml; on success enters the Preview stage.
        /// On failure shows an error message and stays on the Source stage.
        private bool TryEnterPreview(string xml)
        {
            if (!PlansXml.TryImport(xml, out List<Plan> parsed, out string? parseError,
                ModRequirements.IsModActive))
            {
                Messages.Message("IMP_ImportInvalid".Translate(parseError),
                    MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            pendingXml = xml;
            pendingRows = PlanRows.Capture(parsed);
            previewScroll = Vector2.zero;
            stage = Stage.Preview;
            return true;
        }

        public override void PreClose()
        {
            files = null;
            pendingXml = null;
            pendingRows = null;
            textRows = null;
            previewSummary = null;
            previewCaptions = Array.Empty<string>();
            clip = null;
            base.PreClose();
        }

        // ── DoWindowContents ─────────────────────────────────────────────────

        public override void DoWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
                UiVersion.ObserveCurrentMetrics();
                PlanIoLabels.Ensure();

                if (Event.current.type == EventType.MouseDown)
                    RefreshClipboard();

                if (stage == Stage.Source)
                    DrawSource(inRect);
                else
                    DrawPreview(inRect);
            }
        }

        // ── Source stage ─────────────────────────────────────────────────────

        private void DrawSource(Rect inRect)
        {
            float bodyTop = DrawTitle(inRect, PlanIoLabels.ImportTitle);

            // [From clipboard] top-right, mirroring export's Copy button.
            var clipRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, FooterH);
            if (Widgets.ButtonText(clipRect, PlanIoLabels.FromClipboard, active: clipUsable)
                && clipUsable)
            {
                TryEnterPreview(clip!); // clipUsable implies non-empty clip
            }

            // Location picker (no name field — a file is picked from the list).
            float locRowY = bodyTop;
            float customRowY = locRowY + RowH;
            DrawLocationRows(inRect, locRowY, customRowY, includeNameField: false);
            bodyTop += RowH + (location == Location.Custom ? RowH : 0f);

            float footerY = FooterY(inRect);

            // ── Framed file list ─────────────────────────────────────────────
            var frameRect = new Rect(inRect.x, bodyTop, inRect.width,
                footerY - FooterGap - bodyTop);
            var listRect = DrawFrame(frameRect);
            if (listRect.height > 0f)
            {
                if (files == null || files.Count == 0)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = PlannerStyle.CaptionText;
                    Widgets.Label(listRect, PlanIoLabels.NoFiles);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                }
                else
                {
                    DrawFileList(listRect);
                }
            }

            // Cancel
            if (Widgets.ButtonText(new Rect(inRect.xMax - ButtonW, footerY, ButtonW, FooterH),
                PlanIoLabels.Cancel))
                Close();
        }

        private void DrawFileList(Rect listRect)
        {
            List<PlansFiles.Entry> entries = files!; // caller checked
            float rowH = FileRowH;
            float totalH = entries.Count * rowH;
            bool needsBar = totalH > listRect.height;
            var viewRect = new Rect(0f, 0f,
                listRect.width - (needsBar ? GenUI.ScrollBarWidth : 0f), totalH);

            Widgets.BeginScrollView(listRect, ref sourceScroll, viewRect);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    PlansFiles.Entry file = entries[i];
                    var rowRect = new Rect(0f, i * rowH, viewRect.width, rowH);

                    if (i % 2 == 0)
                        Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
                    Widgets.DrawHighlightIfMouseover(rowRect);

                    // Delete X button (right side, inside the row)
                    var delRect = new Rect(rowRect.xMax - DeleteW - 2f,
                        rowRect.y + (rowH - DeleteW) / 2f, DeleteW, DeleteW);
                    if (Widgets.ButtonImage(delRect, TexButton.CloseXSmall))
                    {
                        string capturedPath = file.FullPath;
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "IMP_DeleteFileConfirm".Translate(file.Name),
                            () =>
                            {
                                try { File.Delete(capturedPath); }
                                catch (Exception ex)
                                {
                                    Messages.Message(ex.Message,
                                        MessageTypeDefOf.RejectInput, historical: false);
                                }
                                files = null;   // force re-list next update
                            },
                            destructive: true));
                    }

                    // File name (left)
                    float availW = rowRect.width - DeleteW - 8f;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y, availW * 0.60f, rowH),
                        file.Name);

                    // Modified date (right, caption style)
                    GUI.color = PlannerStyle.CaptionText;
                    Text.Anchor = TextAnchor.MiddleRight;
                    TinyText.Label(new Rect(rowRect.x + availW * 0.60f, rowRect.y,
                        availW * 0.38f, rowH), file.ModifiedText);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;

                    // Row click → read + enter preview
                    if (Widgets.ButtonInvisible(
                        new Rect(rowRect.x, rowRect.y, rowRect.width - DeleteW - 4f, rowH)))
                    {
                        if (!PlansFiles.TryRead(file.FullPath, out string? xml, out string? readError))
                        {
                            Messages.Message("IMP_ReadFailed".Translate(readError),
                                MessageTypeDefOf.RejectInput, historical: false);
                        }
                        else
                        {
                            TryEnterPreview(xml!); // set on the true path
                        }
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        // ── Preview stage ────────────────────────────────────────────────────

        private void DrawPreview(Rect inRect)
        {
            float bodyTop = DrawTitle(inRect, PlanIoLabels.ImportTitle);
            float footerY = FooterY(inRect);
            EnsurePreviewText();

            // Summary line
            float summaryH = Mathf.Max(18f, TinyText.LineHeight);
            GUI.color = PlannerStyle.CaptionText;
            TinyText.Label(new Rect(inRect.x, bodyTop, inRect.width, summaryH),
                previewSummary!);
            GUI.color = Color.white;
            bodyTop += summaryH + 2f;

            // Additive-import note: plans are ADDED to the existing list,
            // never replacing it; duplicate names get numbered.
            string note = PlanIoLabels.ImportAddNote;
            float noteH = NoteHeight(note, inRect.width);
            GUI.color = NoteColor;
            Text.WordWrap = true;
            TinyText.Label(new Rect(inRect.x, bodyTop, inRect.width, noteH), note);
            GUI.color = Color.white;
            bodyTop += noteH + 4f;

            // ── Framed preview listing ───────────────────────────────────────
            var frameRect = new Rect(inRect.x, bodyTop, inRect.width,
                footerY - bodyTop - FooterGap);
            var listRect = DrawFrame(frameRect);
            if (pendingRows != null)
                DrawPlanListing(listRect, pendingRows, previewCaptions, ref previewScroll);

            // Footer: [Cancel]  [Back]  [Import]
            float importX = inRect.xMax - ButtonW;
            float backX = importX - FooterGap - ButtonW;
            float cancelX = backX - FooterGap - ButtonW;

            if (Widgets.ButtonText(new Rect(cancelX, footerY, ButtonW, FooterH),
                PlanIoLabels.Cancel))
                Close();

            if (Widgets.ButtonText(new Rect(backX, footerY, ButtonW, FooterH),
                PlanIoLabels.Back))
            {
                stage = Stage.Source;
                files = null;   // re-list on return
                pendingXml = null;
                pendingRows = null;
            }

            if (Widgets.ButtonText(new Rect(importX, footerY, ButtonW, FooterH),
                PlanIoLabels.Import))
            {
                PlannerCommands.ImportPlans(pendingXml!); // set when entering the preview stage
                Close();
            }
        }

        /// Rebuilds the translated summary and per-row captions only when the
        /// pending rows or language revision moved; steady repaints reuse the
        /// cached strings without translating or formatting.
        private void EnsurePreviewText()
        {
            if (ReferenceEquals(textRows, pendingRows)
                && textLanguageVersion == UiVersion.LanguageCurrent
                && previewSummary != null)
                return;
            int planCount = pendingRows?.PlanCount ?? 0;
            int implantTotal = pendingRows?.ImplantTotal ?? 0;
            previewSummary = "IMP_ExportSummary".Translate(planCount, implantTotal);
            previewCaptions = new string[planCount];
            for (int i = 0; i < planCount; i++)
                previewCaptions[i] = "IMP_PlanGoalCount".Translate(pendingRows!.ImplantCounts[i]);
            textRows = pendingRows;
            textLanguageVersion = UiVersion.LanguageCurrent;
        }
    }
}
