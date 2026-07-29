using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Export preview dialog: shows a summary (counts + listing) of what will be
    /// exported, then lets the user copy to clipboard or save to a named file.
    /// Chrome pattern mirrors WorkRoles Dialog_ExportPreview, simplified to our
    /// always-everything semantics.
    public class Dialog_ExportReadouts : Dialog_EprPreviewBase
    {
        // Snapshot frozen at open time; rebuilt if store.Version changes.
        private List<ResourcePool> pools;
        private List<ReadoutGroup> groups;
        private int snapshotVersion = -1;

        private string xml;           // export XML, rebuilt alongside the snapshot
        private Vector2 scroll;

        // Name field for Save-to-file
        private string fileName = "readouts";

        public override Vector2 InitialSize => new Vector2(560f, 520f);

        public Dialog_ExportReadouts()
        {
            RebuildSnapshot();
        }

        private void RebuildSnapshot()
        {
            var store = ReadoutStore.Current;
            if (store == null) return;
            snapshotVersion = store.Version;
            pools  = new List<ResourcePool>(store.Model.Pools);
            groups = store.Model.InDisplayOrder();
            xml    = ReadoutsXml.Export(pools, groups);
            ReadoutsPreviewUI.Invalidate();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var store = ReadoutStore.Current;
            if (store != null && store.Version != snapshotVersion)
                RebuildSnapshot();

            float bodyTop = DrawTitle(inRect, "EPR.ExportPreviewTitle");

            // ── Summary line ────────────────────────────────────────────────
            int poolCount  = pools  != null ? pools.Count  : 0;
            int groupCount = groups != null ? groups.Count : 0;
            Text.Font   = GameFont.Tiny;
            GUI.color   = EprStyle.CaptionText;
            Widgets.Label(new Rect(inRect.x, bodyTop, inRect.width, 18f),
                "EPR.ContentSummary".Translate(poolCount, groupCount));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            bodyTop  += 20f;

            // ── Footer layout (bottom-up) ────────────────────────────────────
            float footerY  = FooterY(inRect);
            // Row above footer: file name label + field
            float nameRowY = footerY - FooterGap - 28f;
            // Row above that: caption
            float captionY = nameRowY - 18f;

            // ── Listing fills the middle region ──────────────────────────────
            var listRect = new Rect(inRect.x, bodyTop, inRect.width, captionY - bodyTop - 4f);
            if (pools != null && groups != null)
                ReadoutsPreviewUI.DrawListing(listRect, pools, groups, ref scroll);

            // ── Save-to-file caption + name field ────────────────────────────
            Text.Font   = GameFont.Tiny;
            GUI.color   = EprStyle.CaptionText;
            Widgets.Label(new Rect(inRect.x, captionY, inRect.width, 18f),
                "EPR.SaveToFileCaption".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Label "Name:" + text field for file name
            const float LabelW = 50f;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, nameRowY, LabelW, 28f),
                "EPR.NameLabel".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            fileName = Widgets.TextField(
                new Rect(inRect.x + LabelW, nameRowY, inRect.width - LabelW, 24f),
                fileName ?? "readouts");

            // ── Footer buttons ────────────────────────────────────────────────
            // [Copy to clipboard]  |  [Save to file…]  |  [Close]
            float closeX  = inRect.xMax - ButtonW;
            float saveX   = closeX - FooterGap - ButtonW;
            float copyX   = saveX  - FooterGap - ButtonW;

            var copyRect  = new Rect(copyX,  footerY, ButtonW, FooterH);
            var saveRect  = new Rect(saveX,  footerY, ButtonW, FooterH);
            var closeRect = new Rect(closeX, footerY, ButtonW, FooterH);

            if (Widgets.ButtonText(copyRect, "EPR.CopyToClipboard".Translate()))
            {
                GUIUtility.systemCopyBuffer = xml ?? "";
                Messages.Message("EPR.CopiedToClipboard".Translate(),
                    MessageTypeDefOf.PositiveEvent, historical: false);
            }

            bool nameOk = !string.IsNullOrWhiteSpace(fileName);
            if (!nameOk)
                TooltipHandler.TipRegion(saveRect, "EPR.NameLabel".Translate());
            if (Widgets.ButtonText(saveRect, "EPR.SaveToFile".Translate(), active: nameOk) && nameOk)
            {
                string path = ReadoutsFiles.PathFor(fileName.Trim());
                if (ReadoutsFiles.TryWrite(path, xml, out string writeError))
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

            if (Widgets.ButtonText(closeRect, "EPR.Cancel".Translate()))
                Close();
        }
    }
}
