using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Left panel: groups listed in display order (matching the main readout),
    /// with drag-to-reorder, enable checkbox, delete, and creation footer.
    public sealed class GroupListView
    {
        private const float RowH = 26f;
        private const float FooterH = 30f;

        private Vector2 scroll;
        private string newName = "";
        private string pendingSelect;

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var store = ReadoutStore.Current;
            var settings = EPrimeReadoutsMod.Settings;

            // Section header with fold toggle
            bool folded = settings.helpGroupsFolded;
            float headerUsed = EprStyle.SectionHeader(rect.x, rect.y, rect.width,
                "EPR.Groups".Translate(), "EPR.HelpGroups".Translate(), ref folded);
            if (folded != settings.helpGroupsFolded)
                EPrimeReadoutsMod.Persist(s => s.helpGroupsFolded = folded);

            var ordered = store.Model.InDisplayOrder();

            if (pendingSelect != null)
            {
                foreach (var g in ordered)
                    if (g.Name == pendingSelect)
                    {
                        owner.selectedGroupId = g.Id;
                        string newKey = store.DepthKey(g.Id);
                        EPrimeReadoutsMod.Persist(s => s.enabledGroups[newKey] = true);
                        ReadoutPanel.BumpView();
                        break;
                    }
                pendingSelect = null;
            }

            var listRect = new Rect(rect.x, rect.y + headerUsed, rect.width,
                rect.height - headerUsed - FooterH);
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, ordered.Count * RowH);
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);

            bool groupDrag = EprDrag.Active && EprDrag.GroupId >= 0;
            var e = Event.current;

            for (int i = 0; i < ordered.Count; i++)
            {
                var group = ordered[i];
                var row = new Rect(0f, i * RowH, viewRect.width, RowH);

                // WorkRoles-style: get a control id per row for drag registration,
                // then ObserveSource inside the scroll clip.
                int controlId = GUIUtility.GetControlID(FocusType.Passive, row);
                EprDrag.ObserveSource(controlId, row);

                if (group.Id == owner.selectedGroupId) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

                string key = store.DepthKey(group.Id);
                bool enabled = settings.enabledGroups.TryGetValue(key, out bool on) ? on : group.DefaultEnabled;
                bool prev = enabled;
                Widgets.Checkbox(new Vector2(row.x + 2f, row.y + 5f), ref enabled, 16f);
                if (enabled != prev)
                {
                    bool localEnabled = enabled;
                    EPrimeReadoutsMod.Persist(s => s.enabledGroups[key] = localEnabled);
                    ReadoutPanel.BumpView();
                }
                var checkRect = new Rect(row.x + 2f, row.y + 5f, 16f, 16f);
                if (Mouse.IsOver(checkRect))
                    TooltipHandler.TipRegion(checkRect, (TaggedString)"EPR.EnableTip".Translate());

                // Name area: right of checkbox, left of delete button
                var nameRect = new Rect(row.x + 24f, row.y, row.width - 48f, RowH);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(nameRect, group.Name);
                Text.Anchor = TextAnchor.UpperLeft;

                // MouseDown on the name area: press registers drag + click=select.
                if (e.type == EventType.MouseDown && e.button == 0
                    && nameRect.Contains(e.mousePosition))
                {
                    int capturedId = group.Id;
                    EprDrag.OnPressGroup(controlId, capturedId,
                        () => owner.selectedGroupId = capturedId);
                    e.Use();
                }

                // While a group drag is active and the mouse is over this row
                // (inside the scroll clip): register insert marker + drop action.
                if (groupDrag && EprDrag.GroupId != group.Id && Mouse.IsOver(row))
                {
                    bool below = e.mousePosition.y - row.y >= row.height / 2f;
                    int target = i + (below ? 1 : 0);
                    int from = DisplayIndex(ordered, EprDrag.GroupId);
                    int to = target > from ? target - 1 : target;
                    if (to != from)
                    {
                        DrawInsertMarker(row, below ? row.yMax : row.y, viewRect.width);
                        int dragId = EprDrag.GroupId;
                        int targetIndex = to;
                        EprDrag.HoverDropAction = () => ReadoutCommands.MoveGroupTo(dragId, targetIndex);
                    }
                }

                // Delete button
                if (Widgets.ButtonText(new Rect(row.xMax - 24f, row.y + 2f, 22f, 22f), "✕"))
                {
                    int id = group.Id;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "EPR.DeleteConfirm".Translate(group.Name),
                        () => ReadoutCommands.DeleteGroup(id), destructive: true));
                }
            }

            Widgets.EndScrollView();

            // Footer (name field + Add) sits directly below the last group row
            // with 8px margin, falling back to bottom-anchored when the list
            // fills the available space.
            float footerY = Mathf.Min(
                listRect.y + ordered.Count * RowH + 8f,
                rect.yMax - FooterH + 4f);
            var footer = new Rect(rect.x, footerY, rect.width, FooterH - 4f);
            newName = Widgets.TextField(
                new Rect(footer.x, footer.y, footer.width - 60f, 24f), newName);
            if (Widgets.ButtonText(new Rect(footer.xMax - 56f, footer.y, 56f, 24f),
                    "EPR.Add".Translate())
                && !newName.NullOrEmpty())
            {
                ReadoutCommands.CreateGroup(newName.Trim());
                pendingSelect = newName.Trim();
                newName = "";
            }
        }

        private static int DisplayIndex(List<Core.ReadoutGroup> ordered, int groupId)
        {
            for (int i = 0; i < ordered.Count; i++)
                if (ordered[i].Id == groupId) return i;
            return -1;
        }

        private static void DrawInsertMarker(Rect row, float markerY, float width)
        {
            Widgets.DrawBoxSolid(new Rect(row.x, markerY - 1f, width, 2f),
                new Color(1f, 1f, 1f, 0.9f));
        }
    }
}
