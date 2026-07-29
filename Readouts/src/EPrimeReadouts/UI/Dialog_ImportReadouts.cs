using System;
using System.Collections.Generic;
using System.IO;
using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Import dialog: source stage (file list or clipboard) → preview stage
    /// (parsed listing + warning) → commit. Lives in one window with a stage enum.
    /// Mirrors WorkRoles Dialog_ImportSource + Dialog_ImportPreview pattern but
    /// fits our simpler always-overwrite-everything semantics.
    public class Dialog_ImportReadouts : Dialog_EprPreviewBase
    {
        private enum Stage { Source, Preview }

        private const float RowH       = 28f;
        private const float DeleteW    = 22f;

        // ── Stage ────────────────────────────────────────────────────────────
        private Stage stage = Stage.Source;

        // ── Source stage state ───────────────────────────────────────────────
        private List<(string name, string fullPath, DateTime modified)> files;
        private Vector2 sourceScroll;
        private string clip;
        private bool clipUsable;

        // ── Preview stage state ──────────────────────────────────────────────
        private string pendingXml;
        private List<ResourcePool> previewPools;
        private List<ReadoutGroup> previewGroups;
        private Vector2 previewScroll;

        public override Vector2 InitialSize => new Vector2(540f, 520f);

        public Dialog_ImportReadouts()
        {
            RefreshFiles();
            RefreshClipboard();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            RefreshFiles();
            RefreshClipboard();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private void RefreshFiles()
        {
            files = ReadoutsFiles.ListFiles();
        }

        private void RefreshClipboard()
        {
            clip      = GUIUtility.systemCopyBuffer;
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

            float footerY = FooterY(inRect);

            // [From clipboard] button top-right, mirroring export's Copy button.
            var clipRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, FooterH);
            if (!clipUsable)
                TooltipHandler.TipRegion(clipRect, "EPR.ClipboardEmpty".Translate());
            if (Widgets.ButtonText(clipRect, "EPR.FromClipboard".Translate(), active: clipUsable)
                && clipUsable)
            {
                if (TryEnterPreview(clip)) { /* stage changed */ }
            }

            // Caption above the file list
            Text.Font   = GameFont.Tiny;
            GUI.color   = EprStyle.CaptionText;
            Widgets.Label(new Rect(inRect.x, bodyTop, inRect.width, 18f),
                "EPR.FromFile".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            bodyTop  += 20f;

            // File list
            var listRect = new Rect(inRect.x, bodyTop, inRect.width, footerY - bodyTop - FooterGap);

            if (files.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = EprStyle.CaptionText;
                Widgets.Label(listRect, "EPR.NoFiles".Translate());
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                float totalH  = files.Count * RowH;
                bool needsBar = totalH > listRect.height;
                var viewRect  = new Rect(0f, 0f,
                    listRect.width - (needsBar ? GenUI.ScrollBarWidth : 0f), totalH);

                Widgets.BeginScrollView(listRect, ref sourceScroll, viewRect);

                for (int i = 0; i < files.Count; i++)
                {
                    var (name, fullPath, modified) = files[i];
                    var rowRect = new Rect(0f, i * RowH, viewRect.width, RowH);

                    if (i % 2 == 0)
                        Widgets.DrawBoxSolid(rowRect, new Color(1f, 1f, 1f, 0.03f));
                    Widgets.DrawHighlightIfMouseover(rowRect);

                    // Delete ✕ button (right side, inside the row)
                    var delRect = new Rect(rowRect.xMax - DeleteW - 2f,
                        rowRect.y + (RowH - DeleteW) / 2f, DeleteW, DeleteW);
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
                                RefreshFiles();
                            },
                            destructive: true));
                    }

                    // File name (left)
                    float availW = rowRect.width - DeleteW - 8f;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(rowRect.x + 4f, rowRect.y, availW * 0.60f, RowH), name);

                    // Modified date (right, caption style)
                    Text.Font   = GameFont.Tiny;
                    GUI.color   = EprStyle.CaptionText;
                    float dateW = availW * 0.38f;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(new Rect(rowRect.x + availW * 0.60f, rowRect.y, dateW, RowH),
                        modified.ToString("yyyy-MM-dd HH:mm"));
                    GUI.color   = Color.white;
                    Text.Font   = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;

                    // Row click → read + enter preview
                    if (Widgets.ButtonInvisible(
                        new Rect(rowRect.x, rowRect.y, rowRect.width - DeleteW - 4f, RowH)))
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
            float warnH = Text.CalcHeight("EPR.ImportWarning".Translate(), inRect.width);
            Widgets.Label(new Rect(inRect.x, bodyTop, inRect.width, warnH),
                "EPR.ImportWarning".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            bodyTop  += warnH + 4f;

            // Preview listing
            var listRect = new Rect(inRect.x, bodyTop, inRect.width, footerY - bodyTop - FooterGap);
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
                RefreshFiles();
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
