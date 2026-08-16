using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Export dialog: framed summary listing of what will be exported, Copy to
    /// Clipboard beside the title, and a save row with a location picker (mod
    /// data folder, Desktop, user home or a custom directory) plus file name.
    /// Chrome pattern mirrors WorkRoles Dialog_ExportPreview.
    public class Dialog_ExportReadouts : Dialog_EprFilePicker
    {
        // Cache contract:
        // Owner: one export window.
        // Key: ReadoutStore identity plus GroupsVersion and PoolsVersion.
        // Value: detached immutable ReadoutSnapshot and its serialized XML.
        // Dependencies: group/pool domains only; thresholds are unrelated.
        // Refresh policy: immediate in WindowUpdate, never in OnGUI.
        // Equality policy: unchanged domain revisions preserve snapshot/XML identity.
        // Teardown: PreClose releases snapshot, XML and preview rows.
        private ReadoutSnapshot? snapshot;
        private ReadoutStore? snapshotStore;
        private int snapshotGroupsVersion = -1;
        private int snapshotPoolsVersion = -1;

        private string? xml;          // export XML, rebuilt alongside the snapshot
        private Vector2 scroll;
        private readonly ReadoutsPreviewView preview = new ReadoutsPreviewView();
        private ReadoutSnapshot? textSnapshot;
        private int textLanguageVersion = -1;
        private string? summaryText;

        public override Vector2 InitialSize => new Vector2(560f, 560f);

        public Dialog_ExportReadouts()
        {
            RebuildSnapshot();
            RefreshResolvedPathCache();
        }

        private void RebuildSnapshot()
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            snapshotStore = store;
            snapshotGroupsVersion = store.GroupsVersion;
            snapshotPoolsVersion = store.PoolsVersion;
            snapshot = ReadoutSnapshot.Capture(
                store.Model.Pools, store.Model.InDisplayOrder());
            xml = snapshot.ToXml(ModRequirements.PackageIdOf);
        }

        public override void PreClose()
        {
            preview.Reset();
            snapshot = null;
            snapshotStore = null;
            textSnapshot = null;
            xml = null;
            base.PreClose();
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            RefreshResolvedPathCache();
            var store = ReadoutStore.Current;
            if (store == null)
            {
                snapshotStore = null;
                snapshot = null;
                xml = null;
                snapshotGroupsVersion = -1;
                snapshotPoolsVersion = -1;
                return;
            }
            if (!ReferenceEquals(store, snapshotStore)
                    || store.GroupsVersion != snapshotGroupsVersion
                    || store.PoolsVersion != snapshotPoolsVersion)
                RebuildSnapshot();
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (new GuiStateScope())
            {
            EnsureText();

            float bodyTop = DrawTitle(inRect, "EPR.ExportPreviewTitle");

            // Copy to Clipboard lives top-right, beside the title: it acts on
            // the previewed config, not on the save controls below.
            var copyRect = new Rect(inRect.xMax - ButtonW, inRect.y, ButtonW, FooterH);
            if (Widgets.ButtonText(copyRect, UiText.Get("EPR.CopyToClipboard")))
            {
                GUIUtility.systemCopyBuffer = xml ?? "";
                Messages.Message("EPR.CopiedToClipboard".Translate(),
                    MessageTypeDefOf.PositiveEvent, historical: false);
            }

            // ── Summary line ────────────────────────────────────────────────
            Text.Font   = GameFont.Tiny;
            GUI.color   = EprStyle.CaptionText;
            Widgets.Label(new Rect(inRect.x, bodyTop, inRect.width, 18f),
                summaryText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            bodyTop  += 20f;

            // Bottom-up layout: Cancel/Save row, optional custom-dir row,
            // location+filename row, caption/Copy Path link row.
            float btnY = FooterY(inRect);
            float customRowY = btnY - FooterGap - (location == Location.Custom ? RowH : 0f);
            float locRowY = customRowY - RowH;
            float captionRowY = locRowY - CaptionRowH;

            // ── Framed listing fills the middle region ──────────────────────
            var frameRect = new Rect(inRect.x, bodyTop, inRect.width, captionRowY - 6f - bodyTop);
            var listRect  = DrawFrame(frameRect);
            if (snapshot != null)
                preview.DrawListing(listRect, snapshot, ref scroll);

            string? path = CachedResolvedPath(out string? problem, out _);

            DrawCaption(new Rect(inRect.x, captionRowY, 200f, CaptionRowH - 2f),
                UiText.Get("EPR.ExportLocationLabel"));

            // Copy Path: a link (no button chrome), right-aligned over the file
            // name it copies. With nothing to copy it CLEARS the clipboard, so a
            // paste can't insert stale content.
            string copyPathLabel = UiText.Get("EPR.CopyPath");
            UiVersion.ObserveCurrentMetrics();
            float linkW = WrText.FitWidth(copyPathLabel) + 6f;
            var linkRect = new Rect(inRect.xMax - linkW, captionRowY, linkW, CaptionRowH - 4f);
            if (problem != null)
                TooltipHandler.TipRegion(linkRect, problem);
            if (Widgets.ButtonText(linkRect, copyPathLabel, drawBackground: false))
            {
                GUIUtility.systemCopyBuffer = path ?? "";
                if (path != null)
                    Messages.Message("EPR.CopiedToClipboard".Translate(),
                        MessageTypeDefOf.PositiveEvent, historical: false);
            }

            DrawLocationRows(inRect, locRowY, customRowY);

            // Bottom row: Cancel escapes on the left, Save commits on the right.
            var cancelRect = new Rect(inRect.x, btnY, ButtonW, FooterH);
            var saveRect   = new Rect(inRect.xMax - ButtonW, btnY, ButtonW, FooterH);
            if (Widgets.ButtonText(cancelRect, UiText.Get("EPR.Cancel")))
                Close();
            if (problem != null)
                TooltipHandler.TipRegion(saveRect, problem);
            if (Widgets.ButtonText(saveRect, UiText.Get("EPR.Save"), active: path != null)
                && path != null)
            {
                if (ReadoutsFiles.TryWrite(path, xml!, out string? writeError)) // xml built with the snapshot
                {
                    Messages.Message("EPR.Exported".Translate(path),
                        MessageTypeDefOf.TaskCompletion, historical: false);
                    Close();
                }
                else
                {
                    Messages.Message(writeError, MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            }
        }

        private void EnsureText()
        {
            UiVersion.ObserveCurrentMetrics();
            if (ReferenceEquals(textSnapshot, snapshot)
                && textLanguageVersion == UiVersion.LanguageCurrent
                && summaryText != null)
                return;
            int pools = snapshot?.Pools.Count ?? 0;
            int groups = snapshot?.Groups.Count ?? 0;
            summaryText = "EPR.ContentSummary".Translate(pools, groups);
            textSnapshot = snapshot;
            textLanguageVersion = UiVersion.LanguageCurrent;
        }
    }
}
