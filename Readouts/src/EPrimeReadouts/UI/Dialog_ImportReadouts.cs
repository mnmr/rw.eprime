using System;
using System.Collections.Generic;
using System.IO;
using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Import dialog: source stage (location picker + file list, or clipboard)
    /// → preview stage (parsed listing + warning) → commit. Lives in one window
    /// with a stage enum. The location picker mirrors the export dialog's, so
    /// anything saved there can be loaded from here.
    public class Dialog_ImportReadouts : Dialog_EprFilePicker
    {
        private enum Stage { Source, Preview }

        private const float FileRowH = 28f;
        private const float DeleteW  = 22f;

        // ── Stage ────────────────────────────────────────────────────────────
        private Stage stage = Stage.Source;

        // ── Source stage state ───────────────────────────────────────────────
        private List<(string name, string fullPath, DateTime modified)> files;
        private string listedDir;   // directory the current file list came from
        private Vector2 sourceScroll;
        private string clip;
        private bool clipUsable;

        // ── Preview stage state ──────────────────────────────────────────────
        private string pendingXml;
        private List<ResourcePool> previewPools;
        private List<ReadoutGroup> previewGroups;
        private Vector2 previewScroll;

        public override Vector2 InitialSize => new Vector2(560f, 560f);

        public override void PreOpen()
        {
            base.PreOpen();
            files = null;   // force a fresh directory listing
            RefreshClipboard();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// Directory listing for the picked location, refreshed on user
        /// interaction when the resolved directory changed — never on idle
        /// repaints (Desktop/UserHome resolution and Directory.GetFiles are
        /// syscalls, several times per frame).
        private void EnsureFiles()
        {
            var e = Event.current;
            bool interact = e != null
                && (e.type == EventType.MouseDown || e.type == EventType.KeyDown);
            if (files != null && !interact) return;
            string dir = ResolvedDir();
            if (files != null && string.Equals(dir, listedDir, StringComparison.Ordinal))
                return;
            listedDir = dir;
            files = ReadoutsFiles.ListFiles(dir);
            sourceScroll = Vector2.zero;
        }

        private void RefreshClipboard()
        {
            clip       = GUIUtility.systemCopyBuffer;
            clipUsable = !string.IsNullOrEmpty(clip) && clip.Contains("<Readouts");
        }

        /// Attempts to parse xml; on success enters Preview stage.
        /// On failure shows an error message and stays on Source stage.
        private bool TryEnterPreview(string xml)
        {
            if (!ReadoutsXml.TryImport(xml, out var parsedPools, out var parsedGroups,
                out string parseError))
            {
                Messages.Message(
                    "EPR.ImportParseFailed".Translate(parseError),
                    MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }
            pendingXml    = xml;
            previewPools  = parsedPools;
            previewGroups = parsedGroups;
            ReadoutsPreviewUI.Invalidate();
            stage = Stage.Preview;
            return true;
        }

        // ── DoWindowContents ─────────────────────────────────────────────────

        public override void DoWindowContents(Rect inRect)
        {
            if (Event.current.type == EventType.MouseDown)
                RefreshClipboard();

            if (stage == Stage.Source)
                DrawSource(inRect);
            else
                DrawPreview(inRect);
        }

        // ── Source stage ─────────────────────────────────────────────────────

        private void DrawSource(Rect inRect)
        {
            float bodyTop = DrawTitle(inRect, "EPR.ImportTitle");

            // [From clipboard] top-right, mirroring export's Copy button.
            var clipRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, FooterH);
            if (!clipUsable)
                TooltipHandler.TipRegion(clipRect, "EPR.ClipboardEmpty".Translate());
            if (Widgets.ButtonText(clipRect, "EPR.FromClipboard".Translate(), active: clipUsable)
                && clipUsable)
            {
                TryEnterPreview(clip);
            }

            // Location picker (no name field — a file is picked from the list).
            DrawCaption(new Rect(inRect.x, bodyTop, 200f, CaptionRowH - 2f),
                "EPR.ImportLocationLabel".Translate());
            bodyTop += CaptionRowH;
            float locRowY = bodyTop;
            float customRowY = locRowY + RowH;
            DrawLocationRows(inRect, locRowY, customRowY, includeNameField: false);
            bodyTop += RowH + (location == Location.Custom ? RowH : 0f);

            float footerY = FooterY(inRect);

            // ── Framed file list ─────────────────────────────────────────────
            var frameRect = new Rect(inRect.x, bodyTop, inRect.width,
                footerY - FooterGap - bodyTop);
            var listRect = DrawFrame(frameRect);
            if (listRect.height <= 0f) return;

            EnsureFiles();

            if (files == null || files.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = EprStyle.CaptionText;
                Widgets.Label(listRect, "EPR.NoFiles".Translate());
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                float totalH  = files.Count * FileRowH;
                bool needsBar = totalH > listRect.height;
                var viewRect  = new Rect(0f, 0f,
                    listRect.width - (needsBar ? GenUI.ScrollBarWidth : 0f), totalH);

                Widgets.BeginScrollView(listRect, ref sourceScroll, viewRect);

                for (int i = 0; i < files.Count; i++)
                {
                    var (name, fullPath, modified) = files[i];
                    var rowRect = new Rect(0f, i * FileRowH, viewRect.width, FileRowH);

                    if (i % 2 == 0)
                        Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
                    Widgets.DrawHighlightIfMouseover(rowRect);

                    // Delete ✕ button (right side, inside the row)
                    var delRect = new Rect(rowRect.xMax - DeleteW - 2f,
                        rowRect.y + (FileRowH - DeleteW) / 2f, DeleteW, DeleteW);
                    if (Widgets.ButtonImage(delRect, TexButton.CloseXSmall))
                    {
                        string capturedPath = fullPath;
                        string capturedName = name;
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "EPR.DeleteFileConfirm".Translate(capturedName),
                            () =>
                            {
                                try { File.Delete(capturedPath); }
                                catch (Exception ex)
                                {
                                    Messages.Message(ex.Message,
                                        MessageTypeDefOf.RejectInput, historical: false);
                                }
                                files = null;   // force re-list next frame
                            },
                            destructive: true));
                    }

                    // File name (left)
                    float availW = rowRect.width - DeleteW - 8f;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y, availW * 0.60f, FileRowH), name);

                    // Modified date (right, caption style)
                    Text.Font   = GameFont.Tiny;
                    GUI.color   = EprStyle.CaptionText;
                    float dateW = availW * 0.38f;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(new Rect(rowRect.x + availW * 0.60f, rowRect.y, dateW, FileRowH),
                        modified.ToString("yyyy-MM-dd HH:mm"));
                    GUI.color   = Color.white;
                    Text.Font   = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;

                    // Row click → read + enter preview
                    if (Widgets.ButtonInvisible(
                        new Rect(rowRect.x, rowRect.y, rowRect.width - DeleteW - 4f, FileRowH)))
                    {
                        if (!ReadoutsFiles.TryRead(fullPath, out string xml, out string readError))
                        {
                            Messages.Message(readError, MessageTypeDefOf.RejectInput, historical: false);
                        }
                        else
                        {
                            TryEnterPreview(xml);
                        }
                    }
                }

                Widgets.EndScrollView();
            }

            // Cancel
            if (Widgets.ButtonText(new Rect(inRect.xMax - ButtonW, footerY, ButtonW, FooterH),
                "EPR.Cancel".Translate()))
                Close();
        }

        // ── Preview stage ────────────────────────────────────────────────────

        private void DrawPreview(Rect inRect)
        {
            float bodyTop = DrawTitle(inRect, "EPR.ImportTitle");
            float footerY = FooterY(inRect);

            // Summary line
            int poolCount  = previewPools  != null ? previewPools.Count  : 0;
            int groupCount = previewGroups != null ? previewGroups.Count : 0;
            Text.Font   = GameFont.Tiny;
            GUI.color   = EprStyle.CaptionText;
            Widgets.Label(new Rect(inRect.x, bodyTop, inRect.width, 18f),
                "EPR.ContentSummary".Translate(poolCount, groupCount));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            bodyTop  += 20f;

            // Warning line
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 0.75f, 0.35f);   // warm warning tint
            string warning = "EPR.ImportWarning".Translate();
            float warnH = EprStyle.CaptionHeight(warning, inRect.width);
            Widgets.Label(new Rect(inRect.x, bodyTop, inRect.width, warnH), warning);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            bodyTop  += warnH + 4f;

            // ── Framed preview listing ───────────────────────────────────────
            var frameRect = new Rect(inRect.x, bodyTop, inRect.width,
                footerY - bodyTop - FooterGap);
            var listRect = DrawFrame(frameRect);
            if (previewPools != null && previewGroups != null)
                ReadoutsPreviewUI.DrawListing(listRect, previewPools, previewGroups, ref previewScroll);

            // Footer: [Import]  [Back]  [Cancel]
            float importX = inRect.xMax - ButtonW;
            float backX   = importX - FooterGap - ButtonW;
            float cancelX = backX   - FooterGap - ButtonW;

            if (Widgets.ButtonText(new Rect(cancelX, footerY, ButtonW, FooterH),
                "EPR.Cancel".Translate()))
                Close();

            if (Widgets.ButtonText(new Rect(backX, footerY, ButtonW, FooterH),
                "EPR.Back".Translate()))
            {
                stage = Stage.Source;
                files = null;   // re-list on return
                ReadoutsPreviewUI.Invalidate();
            }

            if (Widgets.ButtonText(new Rect(importX, footerY, ButtonW, FooterH),
                "EPR.Import".Translate()))
            {
                ReadoutCommands.ImportAll(pendingXml);
                Messages.Message("EPR.Imported".Translate(),
                    MessageTypeDefOf.TaskCompletion, historical: false);
                Close();
            }
        }
    }
}
