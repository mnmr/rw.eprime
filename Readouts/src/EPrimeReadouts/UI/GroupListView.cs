using EPrimeReadouts.Core;
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
        private string? pendingSelect;

        private struct GroupRow
        {
            internal int Id;
            internal string Name;
            internal string DepthKey;
            internal bool Enabled;
        }

        // Cache contract:
        // Owner: this dialog view and one ReadoutStore.
        // Key: store identity plus GroupsVersion.
        // Value: immutable display-ordered group row array.
        // Dependencies: group structure, owning world's depth-key prefix and
        // the exact enabled-group presentation revision.
        // Refresh policy: immediate on store/version change.
        // Equality policy: unchanged dependencies preserve row-array identity.
        // Teardown: view becomes collectible with its dialog; Reset drops rows.
        private ReadoutStore? builtStore;
        private int builtGroupsVersion = -1;
        private int builtPresentationVersion = -1;
        private static int presentationVersion;
        private GroupRow[]? rows;

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var store = ReadoutStore.Current!; // dialog draws only while a world exists
            var settings = EPrimeReadoutsMod.Settings;

            float headerUsed = EprStyle.SectionHeader(
                rect.x, rect.y, rect.width, UiText.Get("EPR.Groups"));

            bool folded = settings.helpGroupsFolded;
            headerUsed += EprStyle.HelpGroup(
                rect.x,
                rect.y + headerUsed,
                rect.width,
                UiText.Get("EPR.Help"),
                UiText.Get("EPR.HelpGroups"),
                ref folded);
            if (folded != settings.helpGroupsFolded)
                EPrimeReadoutsMod.Persist(s => s.helpGroupsFolded = folded);

            EnsureRows(store);

            if (pendingSelect != null)
            {
                for (int i = 0; i < rows!.Length; i++) // built by EnsureRows above
                    if (rows[i].Name == pendingSelect)
                    {
                        owner.SelectGroup(rows[i].Id);
                        string newKey = rows[i].DepthKey;
                        EPrimeReadoutsMod.Persist(s => s.enabledGroups[newKey] = true);
                        rows[i].Enabled = true;
                        builtPresentationVersion = ++presentationVersion;
                        ReadoutPanel.BumpView();
                        break;
                    }
                pendingSelect = null;
            }

            var listRect = new Rect(rect.x, rect.y + headerUsed, rect.width,
                rect.height - headerUsed - FooterH);
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, rows!.Length * RowH); // built by EnsureRows above
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            try
            {

            bool groupDrag = EprDrag.Active && EprDrag.GroupId >= 0;
            var e = Event.current;

            for (int i = 0; i < rows.Length; i++)
            {
                GroupRow group = rows[i];
                var row = new Rect(0f, i * RowH, viewRect.width, RowH);

                // WorkRoles-style: get a control id per row for drag registration,
                // then ObserveSource inside the scroll clip.
                int controlId = GUIUtility.GetControlID(FocusType.Passive, row);
                EprDrag.ObserveSource(controlId, row);

                if (group.Id == owner.selectedGroupId) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

                string key = group.DepthKey;
                bool enabled = group.Enabled;
                bool prev = enabled;
                Widgets.Checkbox(new Vector2(row.x + 2f, row.y + 5f), ref enabled, 16f);
                if (enabled != prev)
                {
                    bool localEnabled = enabled;
                    EPrimeReadoutsMod.Persist(s => s.enabledGroups[key] = localEnabled);
                    rows[i].Enabled = enabled;
                    builtPresentationVersion = ++presentationVersion;
                    ReadoutPanel.BumpView();
                }
                var checkRect = new Rect(row.x + 2f, row.y + 5f, 16f, 16f);
                WrTips.Key("EPR.EnableTip").Region(checkRect);

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
                        () => owner.SelectGroup(capturedId));
                    e.Use();
                }

                // While a group drag is active and the mouse is over this row
                // (inside the scroll clip): register insert marker + drop action.
                if (groupDrag && EprDrag.GroupId != group.Id && Mouse.IsOver(row))
                {
                    bool below = e.mousePosition.y - row.y >= row.height / 2f;
                    int target = i + (below ? 1 : 0);
                    int from = DisplayIndex(rows, EprDrag.GroupId);
                    int to = target > from ? target - 1 : target;
                    if (to != from)
                    {
                        DrawInsertMarker(row, below ? row.yMax : row.y, viewRect.width);
                        EprDrag.SetGroupDrop(EprDrag.GroupId, to);
                    }
                }

                // Delete button
                if (Widgets.ButtonText(new Rect(row.xMax - 24f, row.y + 2f, 22f, 22f), "✕"))
                {
                    int id = group.Id;
                    Find.WindowStack.Add(new Dialog_CompactConfirm(
                        "EPR.DeleteConfirm".Translate(group.Name),
                        () => ReadoutCommands.DeleteGroup(id), destructive: true));
                }
            }
            }
            finally
            {
                Widgets.EndScrollView();
            }

            // Footer (name field + Add) sits directly below the last group row
            // with 8px margin, falling back to bottom-anchored when the list
            // fills the available space.
            float footerY = Mathf.Min(
                listRect.y + rows.Length * RowH + 8f,
                rect.yMax - FooterH + 4f);
            var footer = new Rect(rect.x, footerY, rect.width, FooterH - 4f);
            GUI.SetNextControlName("EPR.NewGroupName");
            newName = Widgets.TextField(
                new Rect(footer.x, footer.y, footer.width - 60f, 24f), newName);
            if (Widgets.ButtonText(new Rect(footer.xMax - 56f, footer.y, 56f, 24f),
                    UiText.Get("EPR.Add"))
                && !newName.NullOrEmpty())
            {
                ReadoutCommands.CreateGroup(newName.Trim());
                pendingSelect = newName.Trim();
                newName = "";
            }
        }

        private static int DisplayIndex(GroupRow[] ordered, int groupId)
        {
            for (int i = 0; i < ordered.Length; i++)
                if (ordered[i].Id == groupId) return i;
            return -1;
        }

        private void EnsureRows(ReadoutStore store)
        {
            if (ReferenceEquals(builtStore, store)
                && builtGroupsVersion == store.GroupsVersion
                && builtPresentationVersion == presentationVersion
                && rows != null)
                return;

            var ordered = store.Model.InDisplayOrder();
            var built = new GroupRow[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
            {
                ReadoutGroup group = ordered[i];
                built[i] = new GroupRow
                {
                    Id = group.Id,
                    Name = group.Name,
                    DepthKey = store.DepthKey(group.Id),
                    Enabled = EPrimeReadoutsMod.Settings.enabledGroups.TryGetValue(
                        store.DepthKey(group.Id), out bool enabled)
                            ? enabled : group.DefaultEnabled,
                };
            }
            builtStore = store;
            builtGroupsVersion = store.GroupsVersion;
            builtPresentationVersion = presentationVersion;
            rows = built;
        }

        internal void Reset()
        {
            builtStore = null;
            builtGroupsVersion = -1;
            builtPresentationVersion = -1;
            rows = null;
            pendingSelect = null;
            newName = "";
            scroll = Vector2.zero;
        }

        internal bool HandleEscape() => DialogInputFocus.TryHandleEscape(
            "EPR.NewGroupName", newName, () => newName = "");

        private static void DrawInsertMarker(Rect row, float markerY, float width)
        {
            Widgets.DrawBoxSolid(new Rect(row.x, markerY - 1f, width, 2f),
                new Color(1f, 1f, 1f, 0.9f));
        }
    }
}
