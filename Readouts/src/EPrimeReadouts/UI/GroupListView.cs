using EPrimeReadouts.Core;
using RimShared.UiLib;
using RimWorld;
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
        // Group just created from the footer: its id once known, else its
        // (unique) name until the synced create lands.
        private int pendingSelectId = -1;
        private string? pendingSelectName;

        // Owner: group-list view. Key: UiRevision.Current. Value: measured Small
        // Add-button bounds. Dependencies: translated Add caption and font
        // metrics. Refresh: immediate after revision changes. Equality: stable
        // revision reuses scalars. Teardown: Reset invalidates the stamp.
        private int footerMetricStamp = -1;
        private float addWidth, addHeight;

        private void EnsureFooterMetrics()
        {
            if (footerMetricStamp == UiRevision.Current) return;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                addWidth = Mathf.Max(56f, WrText.FitWidth(UiText.Get("EPR.Add")) + 20f);
                addHeight = Mathf.Max(24f, Mathf.Ceil(Text.LineHeight) + 2f);
            }
            footerMetricStamp = UiRevision.Current;
        }

        private struct GroupRow
        {
            internal int Id;
            internal string Name;
            internal string OriginalName;
            internal string DepthKey;
            internal bool Enabled;
        }

        // Cache contract:
        // Owner: this dialog view and one ReadoutStore.
        // Key: store identity plus GroupsVersion.
        // Value: immutable display-ordered group row array.
        // Dependencies: group structure, owning world's depth-key prefix and
        // the exact enabled-group presentation revision and language revision.
        // Refresh policy: immediate on store/version change.
        // Equality policy: unchanged dependencies and equal rows preserve row-array identity.
        // Teardown: view becomes collectible with its dialog; Reset drops rows.
        private ReadoutStore? builtStore;
        private int builtGroupsVersion = -1;
        private int builtPresentationVersion = -1;
        private int builtLanguageVersion = -1;
        private static int presentationVersion;
        private GroupRow[]? rows;

        public void Draw(Rect rect, Dialog_ReadoutConfig owner)
        {
            var store = ReadoutStore.Current!; // dialog draws only while a world exists
            var settings = EPrimeReadoutsMod.Settings;

            float headerUsed = EprStyle.SectionHeader(
                rect.x, rect.y, rect.width, UiText.Get("EPR.Groups"));

            bool folded = settings.helpGroupsFolded;
            headerUsed += HelpFoldout.Draw(
                rect.x,
                rect.y + headerUsed,
                rect.width,
                UiText.Get("EPR.Help"),
                UiText.Get("EPR.HelpGroups"),
                ref folded,
                EprStyle.HelpMetrics,
                UiRevision.Current);
            if (folded != settings.helpGroupsFolded)
                EPrimeReadoutsMod.Persist(s => s.helpGroupsFolded = folded);

            EnsureRows(store);
            EnsureFooterMetrics();

            if (pendingSelectId < 0 && pendingSelectName != null)
            {
                for (int i = 0; i < rows!.Length; i++) // built by EnsureRows above
                    if (ReadoutGroupNames.Comparer.Equals(rows[i].OriginalName, pendingSelectName))
                    {
                        pendingSelectId = rows[i].Id;
                        pendingSelectName = null;
                        break;
                    }
            }
            if (pendingSelectId >= 0)
            {
                for (int i = 0; i < rows!.Length; i++)
                    if (rows[i].Id == pendingSelectId)
                    {
                        owner.SelectGroup(rows[i].Id);
                        string newKey = rows[i].DepthKey;
                        EPrimeReadoutsMod.Persist(s => s.enabledGroups[newKey] = true);
                        rows[i].Enabled = true;
                        builtPresentationVersion = ++presentationVersion;
                        ReadoutPanel.BumpView();
                        break;
                    }
                pendingSelectId = -1;
            }

            var listRect = new Rect(rect.x, rect.y + headerUsed, rect.width,
                rect.height - headerUsed - Mathf.Max(FooterH, addHeight + 4f));
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
                    Find.WindowStack.Add(new CompactConfirmDialog(
                        "EPR.DeleteConfirm".Translate(group.Name),
                        () => ReadoutCommands.DeleteGroup(id),
                        UiText.Get("OK"), UiText.Get("CancelButton"),
                        UiRevision.Current, destructive: true));
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
                rect.yMax - addHeight);
            var footer = new Rect(rect.x, footerY, rect.width, addHeight);
            GUI.SetNextControlName("EPR.NewGroupName");
            newName = Widgets.TextField(
                new Rect(footer.x, footer.y, footer.width - addWidth - 4f, addHeight), newName);
            if (Widgets.ButtonText(new Rect(footer.xMax - addWidth, footer.y, addWidth, addHeight),
                    UiText.Get("EPR.Add")))
                TryCreate(store);
        }

        private void TryCreate(ReadoutStore store)
        {
            string name = ReadoutGroupNames.Normalize(newName);
            if (name.Length == 0) return;
            string? problem = GroupNameProblem(name, -1);
            if (problem != null)
            {
                Messages.Message(problem, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            ReadoutCommands.CreateGroup(name);
            ReadoutGroup? created = store.Model.GroupByName(name);
            if (created != null) pendingSelectId = created.Id;
            else pendingSelectName = name;
            newName = "";
        }

        internal static string? GroupNameProblem(string name, int exceptGroupId)
        {
            ReadoutStore? store = ReadoutStore.Current;
            if (store == null || store.Model.CanUseGroupName(name, exceptGroupId))
                return null;
            return UiText.Get("EPR.GroupNameTaken");
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
                && builtLanguageVersion == UiRevision.LanguageCurrent
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
                    Name = SeededReadoutNames.Name(group),
                    OriginalName = group.Name,
                    DepthKey = store.DepthKey(group.Id),
                    Enabled = EPrimeReadoutsMod.Settings.enabledGroups.TryGetValue(
                        store.DepthKey(group.Id), out bool enabled)
                            ? enabled : group.DefaultEnabled,
                };
            }
            builtStore = store;
            builtGroupsVersion = store.GroupsVersion;
            builtPresentationVersion = presentationVersion;
            builtLanguageVersion = UiRevision.LanguageCurrent;
            bool equal = rows != null && rows.Length == built.Length;
            for (int i = 0; equal && i < built.Length; i++)
                equal = rows![i].Id == built[i].Id && rows[i].Name == built[i].Name
                    && rows[i].OriginalName == built[i].OriginalName
                    && rows[i].DepthKey == built[i].DepthKey && rows[i].Enabled == built[i].Enabled;
            if (!equal) rows = built;
        }

        internal void Reset()
        {
            footerMetricStamp = -1;
            builtStore = null;
            builtGroupsVersion = -1;
            builtPresentationVersion = -1;
            builtLanguageVersion = -1;
            rows = null;
            pendingSelectId = -1;
            pendingSelectName = null;
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
