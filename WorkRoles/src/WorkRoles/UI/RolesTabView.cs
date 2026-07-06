using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    public class RolesTabView
    {
        private Vector2 listScroll;
        private Vector2 entriesScroll;
        private Vector2 treeScroll;
        private int selectedRoleId = -1;
        private string filter = "";
        private readonly HashSet<string> expanded = new HashSet<string>();
        private int reorderableGroupId = -1;
        private int entriesReorderableGroupId = -1;

        private const float ListWidth = 260f;
        private const float RowHeight = 28f;
        private const float IconButton = 20f;

        // Change 7: cache of disambiguated display names per WorkGiverDef.
        // Built once (defs don't change mid-game), invalidated by Reset().
        private static Dictionary<WorkGiverDef, string> _giverDisplayCache;

        // Change 4: Tailwind v3 colour palette — 19 families × 3 shades (800, 700, 600; neutral and 500 removed).
        // Laid out as 3 rows (800 top, 600 bottom) × 19 columns.
        private static readonly Color[] Swatches;

        private static Color Hex(string h)
        {
            int r = System.Convert.ToInt32(h.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(h.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(h.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f);
        }

        static RolesTabView()
        {
            // 19 families (neutral dropped), each with (800, 700, 600) shades.
            // family-major array; we draw as shade-major rows (row 0=all 800s, row 1=all 700s, row 2=all 600s).
            var families = new string[,]
            {
                // family        800       700       600
                /* slate    */ { "1E293B", "334155", "475569" },
                /* stone    */ { "292524", "44403C", "57534E" },
                /* red      */ { "991B1B", "B91C1C", "DC2626" },
                /* orange   */ { "9A3412", "C2410C", "EA580C" },
                /* amber    */ { "92400E", "B45309", "D97706" },
                /* yellow   */ { "854D0E", "A16207", "CA8A04" },
                /* lime     */ { "3F6212", "4D7C0F", "65A30D" },
                /* green    */ { "166534", "15803D", "16A34A" },
                /* emerald  */ { "065F46", "047857", "059669" },
                /* teal     */ { "115E59", "0F766E", "0D9488" },
                /* cyan     */ { "155E75", "0E7490", "0891B2" },
                /* sky      */ { "075985", "0369A1", "0284C7" },
                /* blue     */ { "1E40AF", "1D4ED8", "2563EB" },
                /* indigo   */ { "3730A3", "4338CA", "4F46E5" },
                /* violet   */ { "5B21B6", "6D28D9", "7C3AED" },
                /* purple   */ { "6B21A8", "7E22CE", "9333EA" },
                /* fuchsia  */ { "86198F", "A21CAF", "C026D3" },
                /* pink     */ { "9D174D", "BE185D", "DB2777" },
                /* rose     */ { "9F1239", "BE123C", "E11D48" },
            };

            int numFamilies = families.GetLength(0); // 19
            int numShades   = families.GetLength(1); // 3

            // shade-major order: swatches[shade * numFamilies + family]
            Swatches = new Color[numShades * numFamilies];
            for (int shade = 0; shade < numShades; shade++)
                for (int family = 0; family < numFamilies; family++)
                    Swatches[shade * numFamilies + family] = Hex(families[family, shade]);
        }

        public void Reset()
        {
            listScroll = entriesScroll = treeScroll = Vector2.zero;
            filter = "";
            selectedRoleId = -1;
            _giverDisplayCache = null; // force rebuild on next use
        }

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            if (selectedRoleId == -1 && store.roles.Count > 0)
                selectedRoleId = store.roles[0].id;

            var listRect = new Rect(rect.x, rect.y, ListWidth, rect.height);
            var editorRect = new Rect(rect.x + ListWidth + 12f, rect.y, rect.width - ListWidth - 12f, rect.height);
            DrawRoleList(listRect, store);

            // Separator with light grey
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            Widgets.DrawLineVertical(rect.x + ListWidth + 6f, rect.y, rect.height);
            GUI.color = Color.white;

            var selected = store.RoleById(selectedRoleId);
            if (selected != null) DrawEditor(editorRect, store, selected);
            else Widgets.Label(editorRect, "Select or create a role.");
        }

        // ----- Left: role list + management buttons -----

        private void DrawRoleList(Rect rect, RoleStore store)
        {
            float buttonsHeight = 34f;
            var scrollRect = new Rect(rect.x, rect.y, rect.width, rect.height - buttonsHeight - 6f);
            float contentHeight = store.roles.Count * RowHeight;

            if (Event.current.type == EventType.Repaint)
            {
                reorderableGroupId = ReorderableWidget.NewGroup(
                    (from, to) => {
                        if (to > from) to--;
                        RoleCommands.MoveRoleInCatalog(from, to);
                    },
                    ReorderableDirection.Vertical,
                    scrollRect);
            }

            Widgets.BeginScrollView(scrollRect, ref listScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));
            for (int i = 0; i < store.roles.Count; i++)
            {
                var role = store.roles[i];
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);

                bool dragging = ReorderableWidget.Reorderable(reorderableGroupId, row, useRightButton: false, highlightDragged: true);

                if (role.id == selectedRoleId) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row) && !dragging) Widgets.DrawHighlight(row);

                if (Mouse.IsOver(row))
                    TooltipHandler.TipRegion(row,
                        $"{role.entries.Count} entries: {string.Join(", ", role.entries.Take(4).Select(e => e.DefName))}{(role.entries.Count > 4 ? ", …" : "")}");

                var swatch = new Rect(Mathf.Round(row.x) + 6f, Mathf.Round(row.y) + 6f, 16f, 16f);
                Widgets.DrawBoxSolid(swatch, role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor);
                GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
                Widgets.DrawBox(swatch.ExpandedBy(1f));
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.MiddleLeft;
                if (!role.enabled) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(new Rect(swatch.xMax + 6f, row.y, row.width - swatch.width - 8f, RowHeight),
                    role.enabled ? role.label : role.label + " (off)");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (!dragging && Widgets.ButtonInvisible(new Rect(row.x, row.y, row.width, row.height)))
                    selectedRoleId = role.id;
            }
            Widgets.EndScrollView();

            // Three buttons — New / Copy / Delete
            float bw = (rect.width - 8f) / 3f;
            float by = rect.yMax - buttonsHeight + 4f;
            if (Widgets.ButtonText(new Rect(rect.x, by, bw, 30f), "New"))
                selectedRoleId = RoleCommands.CreateRole("New role")?.id ?? selectedRoleId;

            if (Widgets.ButtonText(new Rect(rect.x + bw + 4f, by, bw, 30f), "Copy"))
            {
                var toCopy = RoleStore.Current.RoleById(selectedRoleId);
                if (toCopy != null)
                {
                    string suggestedName = toCopy.label + " copy";
                    Find.WindowStack.Add(new Dialog_RenameRole("Copy Role", suggestedName, enteredName =>
                    {
                        var newRole = RoleCommands.DuplicateRole(selectedRoleId, enteredName);
                        if (newRole != null) selectedRoleId = newRole.id;
                    }));
                }
            }

            if (Widgets.ButtonText(new Rect(rect.x + (bw + 4f) * 2f, by, bw, 30f), "Delete"))
            {
                var role = RoleStore.Current.RoleById(selectedRoleId);
                if (role != null)
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        $"Delete role '{role.label}'? It will be removed from all colonists.",
                        () => RoleCommands.DeleteRole(role.id), destructive: true));
            }
        }

        // ----- Right: editor for the selected role -----

        private void DrawEditor(Rect rect, RoleStore store, Role role)
        {
            // Change 4: Tailwind palette — 19 cols × 3 rows (800, 700, 600; neutral and 500 removed), swatch 18×18, 2px gap
            const float SwatchSize = 18f;
            const float SwatchGap = 2f;
            const int SwatchCols = 19;
            const int SwatchRows = 3;

            // Split top box into LEFT (name + pencil, assigned-to) and RIGHT (swatches).
            // Height fits whichever half is taller: the swatch grid or the three text rows.
            const float TopBoxPadding = 8f;
            const float TitleH = 30f;
            const float AssignedRowH = 22f;
            const float NamesRowH = 22f;
            float swatchGridH = (SwatchSize + SwatchGap) * SwatchRows - SwatchGap;
            float leftContentH = TitleH + AssignedRowH + NamesRowH;
            float TopBoxHeight = Mathf.Max(swatchGridH, leftContentH) + TopBoxPadding * 2f;

            var topBox = new Rect(rect.x, rect.y, rect.width, TopBoxHeight);
            Widgets.DrawBoxSolidWithOutline(topBox, new Color(0.08f, 0.08f, 0.08f, 0.9f), new Color(1f, 1f, 1f, 0.15f));

            // Swatch grid width
            float swatchGridW = SwatchCols * (SwatchSize + SwatchGap) - SwatchGap;

            // RIGHT half: swatch grid, right-aligned inside box
            float swatchStartX = topBox.xMax - TopBoxPadding - swatchGridW;
            float swatchStartY = topBox.y + TopBoxPadding;
            for (int i = 0; i < Swatches.Length; i++)
            {
                int col = i % SwatchCols;
                int row = i / SwatchCols;
                var swatchRect = new Rect(
                    swatchStartX + col * (SwatchSize + SwatchGap),
                    swatchStartY + row * (SwatchSize + SwatchGap),
                    SwatchSize, SwatchSize);
                Widgets.DrawBoxSolid(swatchRect, Swatches[i]);
                if (role.hasCustomColor && role.color.IndistinguishableFrom(Swatches[i]))
                    Widgets.DrawBox(swatchRect.ExpandedBy(2f));
                if (Widgets.ButtonInvisible(swatchRect))
                    RoleCommands.SetRoleColor(role.id, Swatches[i]);
            }

            // LEFT half: three rows — name+pencil, "Assigned to", colonist names
            // The name's container is 50% of the framed box's full width.
            float leftContainerW = topBox.width / 2f;
            float leftX = topBox.x + TopBoxPadding;
            // Usable width within the left container (inset from left padding, right edge = box centre)
            float leftW = leftContainerW - TopBoxPadding;

            // Row 1: role name top-aligned at box.y + padding, UpperLeft anchor
            float rowsStartY = topBox.y + TopBoxPadding;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(leftX, rowsStartY, leftW - 26f - 8f, TitleH), role.label);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Pencil icon: 26×26, right-aligned inside the left container (just left of box centre),
            // vertically aligned with the title line.
            const float PencilSize = 26f;
            float pencilX = leftContainerW + topBox.x - PencilSize; // right edge of left container
            float pencilY = rowsStartY + (TitleH - PencilSize) / 2f;
            if (Widgets.ButtonImage(new Rect(pencilX, pencilY, PencilSize, PencilSize), TexButton.Rename))
                Find.WindowStack.Add(new Dialog_RenameRole(role));

            // Row 2: small grey "Assigned to" label (no colon)
            float row2Y = rowsStartY + TitleH;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(leftX, row2Y, leftW, AssignedRowH), "Assigned to");
            GUI.color = Color.white;

            // Row 3: colonist names, ordered by position in their assignment list
            float row3Y = row2Y + AssignedRowH;
            DrawAssignedPawnNames(new Rect(leftX, row3Y, leftW, NamesRowH), role, store);

            // BOTTOM: split vertically — left = job tree, right = entries table
            float bottomY = topBox.yMax + 6f;
            float bottomH = rect.yMax - bottomY;
            float halfW = (rect.width - 6f) / 2f;

            var treeRect    = new Rect(rect.x, bottomY, halfW, bottomH);
            var entriesRect = new Rect(rect.x + halfW + 6f, bottomY, halfW, bottomH);

            // Light grey vertical separator
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            Widgets.DrawLineVertical(rect.x + halfW + 3f, bottomY, bottomH);
            GUI.color = Color.white;

            DrawJobTree(treeRect, role);
            DrawEntries(entriesRect, role);
        }

        // ----- Assigned pawn names row -----

        private static void DrawAssignedPawnNames(Rect rect, Role role, RoleStore store)
        {
            // Collect all colonists and their position (1-based index of this role in their assignment list)
            var allPawns = ColonistsTabView.ListedPawns();
            var pawnPositions = new List<(Pawn pawn, int position)>();
            foreach (var pawn in allPawns)
            {
                if (!store.pawnSets.TryGetValue(pawn, out var set)) continue;
                int idx = set.assignments.FindIndex(a => a.roleId == role.id);
                if (idx < 0) continue;
                pawnPositions.Add((pawn, idx + 1)); // 1-based position = priority
            }

            // Sort ascending by position so highest-priority pawns appear first
            pawnPositions.Sort((a, b) => a.position.CompareTo(b.position));

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (pawnPositions.Count == 0)
            {
                GUI.color = new Color(0.5f, 0.5f, 0.5f);
                Widgets.Label(rect, "Nobody");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Color SepColor = new Color(0.55f, 0.55f, 0.55f);
            const string Sep = ", ";
            float sepW = Text.CalcSize(Sep).x;
            // Reserve enough width so "+99 others" always fits at the right edge.
            const float OverflowReserve = 70f;

            float x = rect.x;
            int remaining = 0;

            for (int i = 0; i < pawnPositions.Count; i++)
            {
                var (pawn, _) = pawnPositions[i];
                string name = pawn.LabelShortCap;
                float nameW = Text.CalcSize(name).x;
                bool hasNext = i < pawnPositions.Count - 1;

                // Determine how much space remains after this name (sep + overflow reserve if more names follow).
                float needed = nameW + (hasNext ? sepW + OverflowReserve : 0f);
                if (x + needed > rect.xMax && i > 0)
                {
                    // No room — count remaining (including this one)
                    remaining = pawnPositions.Count - i;
                    break;
                }
                // Even the very first name doesn't fit: show overflow immediately.
                if (i == 0 && x + nameW + (hasNext ? OverflowReserve : 0f) > rect.xMax && hasNext)
                {
                    remaining = pawnPositions.Count;
                    break;
                }

                // Draw name in default label colour (no greyscale ramp).
                Widgets.Label(new Rect(x, rect.y, nameW, rect.height), name);
                x += nameW;

                if (hasNext && x + sepW <= rect.xMax)
                {
                    // Draw comma separator in default grey
                    GUI.color = SepColor;
                    Widgets.Label(new Rect(x, rect.y, sepW, rect.height), Sep);
                    GUI.color = Color.white;
                    x += sepW;
                }
            }

            if (remaining > 0)
            {
                string moreText = $"+{remaining} others";
                GUI.color = SepColor;
                Widgets.Label(new Rect(x, rect.y, rect.xMax - x, rect.height), moreText);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ----- Change 5: entries as two-column table with drag reorder + up/down buttons -----

        private void DrawEntries(Rect rect, Role role)
        {
            // "Selected Jobs (ordered)" via WrText.HeaderLabel, 28f row
            WrText.HeaderLabel(new Rect(rect.x + 8f, rect.y, rect.width - 8f, 28f), "Selected Jobs (ordered)");

            // Column headers — 24f height so descenders aren't clipped
            float headerY = rect.y + 28f + 4f;
            float removeW = (IconButton + 4f) * 3f; // room for up + down + [x]
            float typeW = (rect.width - 8f - removeW - 8f) * 0.45f;
            float jobW  = (rect.width - 8f - removeW - 8f) * 0.55f;

            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f + 4f, headerY, typeW, 24f), "Type");
            Widgets.Label(new Rect(rect.x + 8f + 4f + typeW, headerY, jobW, 24f), "Job");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            var scrollRect = new Rect(rect.x + 8f, headerY + 24f, rect.width - 8f, rect.height - 28f - 4f - 24f);
            float contentHeight = role.entries.Count * RowHeight;

            // Drag-reorder group for entries
            if (Event.current.type == EventType.Repaint)
            {
                int capturedRoleId = role.id;
                entriesReorderableGroupId = ReorderableWidget.NewGroup(
                    (from, to) => {
                        if (to > from) to--;
                        RoleCommands.MoveEntry(capturedRoleId, from, to);
                    },
                    ReorderableDirection.Vertical,
                    scrollRect);
            }

            Widgets.BeginScrollView(scrollRect, ref entriesScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));

            for (int i = 0; i < role.entries.Count; i++)
            {
                var entry = role.entries[i];
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);

                bool dragging = ReorderableWidget.Reorderable(entriesReorderableGroupId, row, useRightButton: false, highlightDragged: true);

                if (Mouse.IsOver(row) && !dragging) Widgets.DrawHighlight(row);

                string typeLabel, jobLabel;
                bool missing = false;
                GetEntryLabels(entry, out typeLabel, out jobLabel, out missing);

                Text.Anchor = TextAnchor.MiddleLeft;
                if (missing) GUI.color = new Color(1f, 0.4f, 0.4f, 0.8f);

                // Type column
                Widgets.Label(new Rect(row.x + 4f, row.y, typeW, RowHeight), typeLabel);

                // Job column
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    Widgets.Label(new Rect(row.x + 4f + typeW, row.y, jobW, RowHeight), "All jobs");
                }
                else
                {
                    Widgets.Label(new Rect(row.x + 4f + typeW, row.y, jobW, RowHeight), jobLabel);
                }

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (missing)
                    TooltipHandler.TipRegion(row, entry.DefName + " is not present with the current mod list (inactive until its mod returns).");

                // up/down buttons + [x] remove
                float btnY = row.y + (RowHeight - IconButton) / 2f;
                float removeX = row.xMax - IconButton - 2f;
                float downX   = removeX - IconButton - 2f;
                float upX     = downX - IconButton - 2f;

                int capturedI = i;
                int capturedRoleId = role.id;

                if (i > 0 && Widgets.ButtonImage(new Rect(upX, btnY, IconButton, IconButton), TexButton.ReorderUp))
                    RoleCommands.MoveEntry(capturedRoleId, capturedI, capturedI - 1);
                if (i < role.entries.Count - 1 && Widgets.ButtonImage(new Rect(downX, btnY, IconButton, IconButton), TexButton.ReorderDown))
                    RoleCommands.MoveEntry(capturedRoleId, capturedI, capturedI + 1);
                if (Widgets.ButtonImage(new Rect(removeX, btnY, IconButton, IconButton), TexButton.Delete))
                    RoleCommands.RemoveEntry(capturedRoleId, capturedI);
            }
            Widgets.EndScrollView();
        }

        // Display label helper
        private static void GetEntryLabels(JobEntry entry, out string typeLabel, out string jobLabel, out bool missing)
        {
            missing = false;
            if (entry.Kind == JobEntryKind.WorkType)
            {
                var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                if (def != null)
                {
                    typeLabel = (def.gerundLabel ?? def.labelShort ?? def.defName).CapitalizeFirst();
                    jobLabel = "All jobs";
                    return;
                }
            }
            else
            {
                var def = DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName);
                if (def != null)
                {
                    typeLabel = def.workType != null
                        ? (def.workType.gerundLabel ?? def.workType.labelShort ?? def.workType.defName).CapitalizeFirst()
                        : "?";
                    jobLabel = GetGiverDisplayName(def);
                    return;
                }
            }
            // Missing def
            missing = true;
            typeLabel = entry.DefName;
            jobLabel = "";
        }

        // ----- Change 3: job-tree pane -----

        private void DrawJobTree(Rect rect, Role role)
        {
            // Change 5: "Available Jobs" (capital J) via WrText.HeaderLabel
            // Search label + field right-aligned on the same row
            const float SearchW = 110f;
            const float SearchLabelW = 46f;
            const float SearchH = 24f;
            float headerW = rect.width - SearchLabelW - SearchW - 8f;

            WrText.HeaderLabel(new Rect(rect.x + 4f, rect.y, headerW - 4f, 28f), "Available Jobs");

            // "Search" label immediately left of field; group shifted 4f left from right edge
            const float SearchRightPad = 4f;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.xMax - SearchLabelW - SearchW - 4f - SearchRightPad, rect.y + (28f - SearchH) / 2f, SearchLabelW, SearchH), "Search");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            filter = Widgets.TextField(new Rect(rect.xMax - SearchW - SearchRightPad, rect.y + (28f - SearchH) / 2f, SearchW, SearchH), filter);

            float treeTopY = rect.y + 28f + 4f;
            var scrollRect = new Rect(rect.x, treeTopY, rect.width, rect.height - 28f - 4f);
            bool filtering = !filter.NullOrEmpty();

            var nodes = new List<(WorkTypeDef type, WorkGiverDef giver)>();
            foreach (var type in DefDatabase<WorkTypeDef>.AllDefsListForReading
                .OrderByDescending(t => t.naturalPriority))
            {
                var givers = type.workGiversByPriority;
                string typeDisplayName = (type.gerundLabel ?? type.labelShort ?? type.defName).CapitalizeFirst();
                bool typeMatches = !filtering || Matches(typeDisplayName);

                var matchingGivers = filtering
                    ? givers.Where(g => Matches(GetGiverDisplayName(g))).ToList()
                    : givers.ToList();

                if (filtering && !typeMatches && matchingGivers.Count == 0) continue;

                nodes.Add((type, null));
                if (filtering || expanded.Contains(type.defName))
                    foreach (var giver in (filtering && !typeMatches) ? matchingGivers : givers.ToList())
                        nodes.Add((type, giver));
            }

            Widgets.BeginScrollView(scrollRect, ref treeScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, nodes.Count * RowHeight));

            for (int i = 0; i < nodes.Count; i++)
            {
                var (type, giver) = nodes[i];
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                Text.Anchor = TextAnchor.MiddleLeft;

                if (giver == null)
                {
                    // Work-type header row
                    string typeDisplayName = (type.gerundLabel ?? type.labelShort ?? type.defName).CapitalizeFirst();
                    int giverCount = type.workGiversByPriority.Count;
                    string countHint = $" ({giverCount})";

                    bool isExpanded = filtering || expanded.Contains(type.defName);
                    if (Widgets.ButtonImage(new Rect(row.x + 2f, row.y + 4f, IconButton, IconButton),
                        isExpanded ? TexButton.Collapse : TexButton.Reveal))
                    {
                        if (!expanded.Add(type.defName)) expanded.Remove(type.defName);
                    }

                    var checkboxRect = new Rect(row.x + 26f, row.y + (row.height - 24f) / 2f, 24f, 24f);
                    var currentState = GetWorkTypeState(role, type);
                    var newState = Widgets.CheckboxMulti(checkboxRect, currentState);
                    if (newState != currentState)
                        ApplyWorkTypeState(role, type, newState);

                    Widgets.Label(new Rect(row.x + 54f, row.y, row.width - 54f, RowHeight), typeDisplayName + countHint);
                }
                else
                {
                    // Job giver child row
                    string giverName = GetGiverDisplayName(giver);

                    var checkboxRect = new Rect(row.x + 42f, row.y + (row.height - 24f) / 2f, 24f, 24f);
                    var currentState = GetGiverState(role, type, giver);
                    var newState = Widgets.CheckboxMulti(checkboxRect, currentState);
                    if (newState != currentState)
                        ApplyGiverState(role, type, giver, newState);

                    Widgets.Label(new Rect(row.x + 70f, row.y, row.width - 70f, RowHeight), giverName);
                }
                Text.Anchor = TextAnchor.UpperLeft;
            }
            Widgets.EndScrollView();

            bool Matches(string label) =>
                label != null && label.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ----- Change 7: disambiguated display names for WorkGiverDefs -----

        private static Dictionary<WorkGiverDef, string> BuildGiverDisplayCache()
        {
            var result = new Dictionary<WorkGiverDef, string>();
            foreach (var type in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                var givers = type.workGiversByPriority;
                // Build base label → list of givers
                var byLabel = new Dictionary<string, List<WorkGiverDef>>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var g in givers)
                {
                    string baseName = (g.label ?? g.defName).CapitalizeFirst();
                    if (!byLabel.TryGetValue(baseName, out var list))
                        byLabel[baseName] = list = new List<WorkGiverDef>();
                    list.Add(g);
                }
                foreach (var g in givers)
                {
                    string baseName = (g.label ?? g.defName).CapitalizeFirst();
                    byLabel.TryGetValue(baseName, out var siblings);
                    if (siblings != null && siblings.Count > 1)
                    {
                        // Collision: append " (emergency)" to the one with emergency == true.
                        // If both or neither are emergency, no suffix (just use base name).
                        int emergencyCount = siblings.Count(s => s.emergency);
                        if (emergencyCount == 1 && g.emergency)
                            result[g] = baseName + " (emergency)";
                        else
                            result[g] = baseName;
                    }
                    else
                    {
                        result[g] = baseName;
                    }
                }
            }
            return result;
        }

        private static string GetGiverDisplayName(WorkGiverDef g)
        {
            if (_giverDisplayCache == null)
                _giverDisplayCache = BuildGiverDisplayCache();
            if (_giverDisplayCache.TryGetValue(g, out var name))
                return name;
            return (g.label ?? g.defName).CapitalizeFirst();
        }

        // ----- Tri-state helpers -----

        private static MultiCheckboxState GetWorkTypeState(Role role, WorkTypeDef type)
        {
            bool hasTypeEntry = role.entries.Any(e => e.Kind == JobEntryKind.WorkType && e.DefName == type.defName);
            if (hasTypeEntry) return MultiCheckboxState.On;

            var givers = type.workGiversByPriority;
            if (givers.Count == 0) return MultiCheckboxState.Off;

            int giverMatches = givers.Count(g => role.entries.Any(e => e.Kind == JobEntryKind.WorkGiver && e.DefName == g.defName));
            if (giverMatches == 0) return MultiCheckboxState.Off;
            if (giverMatches == givers.Count) return MultiCheckboxState.On;
            return MultiCheckboxState.Partial;
        }

        private static MultiCheckboxState GetGiverState(Role role, WorkTypeDef type, WorkGiverDef giver)
        {
            bool hasTypeEntry = role.entries.Any(e => e.Kind == JobEntryKind.WorkType && e.DefName == type.defName);
            if (hasTypeEntry) return MultiCheckboxState.On;
            bool hasGiverEntry = role.entries.Any(e => e.Kind == JobEntryKind.WorkGiver && e.DefName == giver.defName);
            return hasGiverEntry ? MultiCheckboxState.On : MultiCheckboxState.Off;
        }

        private static void ApplyWorkTypeState(Role role, WorkTypeDef type, MultiCheckboxState newState)
        {
            if (newState == MultiCheckboxState.On)
            {
                var toRemove = new List<int>();
                for (int j = 0; j < role.entries.Count; j++)
                {
                    var e = role.entries[j];
                    if (e.Kind == JobEntryKind.WorkGiver
                        && type.workGiversByPriority.Any(g => g.defName == e.DefName))
                        toRemove.Add(j);
                }
                for (int k = toRemove.Count - 1; k >= 0; k--)
                    RoleCommands.RemoveEntry(role.id, toRemove[k]);
                if (!role.entries.Any(e => e.Kind == JobEntryKind.WorkType && e.DefName == type.defName))
                    RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkType, type.defName));
            }
            else // Off
            {
                var toRemove = new List<int>();
                for (int j = 0; j < role.entries.Count; j++)
                {
                    var e = role.entries[j];
                    if (e.Kind == JobEntryKind.WorkType && e.DefName == type.defName)
                        toRemove.Add(j);
                    else if (e.Kind == JobEntryKind.WorkGiver
                             && type.workGiversByPriority.Any(g => g.defName == e.DefName))
                        toRemove.Add(j);
                }
                for (int k = toRemove.Count - 1; k >= 0; k--)
                    RoleCommands.RemoveEntry(role.id, toRemove[k]);
            }
        }

        private static void ApplyGiverState(Role role, WorkTypeDef type, WorkGiverDef giver, MultiCheckboxState newState)
        {
            if (newState == MultiCheckboxState.On)
            {
                if (!role.entries.Any(e => e.Kind == JobEntryKind.WorkGiver && e.DefName == giver.defName))
                    RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName));

                // Collapse to WorkType entry if all givers are now individually present
                var allGivers = type.workGiversByPriority;
                bool allPresent = allGivers.Count > 0
                    && allGivers.All(g => role.entries.Any(e => e.Kind == JobEntryKind.WorkGiver && e.DefName == g.defName));

                if (allPresent)
                {
                    int insertAt = int.MaxValue;
                    var toRemove = new List<int>();
                    for (int j = 0; j < role.entries.Count; j++)
                    {
                        var e = role.entries[j];
                        if (e.Kind == JobEntryKind.WorkGiver
                            && allGivers.Any(g => g.defName == e.DefName))
                        {
                            toRemove.Add(j);
                            if (j < insertAt) insertAt = j;
                        }
                    }
                    for (int k = toRemove.Count - 1; k >= 0; k--)
                        RoleCommands.RemoveEntry(role.id, toRemove[k]);
                    if (insertAt == int.MaxValue) insertAt = role.entries.Count;
                    RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkType, type.defName), insertAt);
                }
            }
            else // Off
            {
                int giverIdx = role.entries.FindIndex(e => e.Kind == JobEntryKind.WorkGiver && e.DefName == giver.defName);
                if (giverIdx >= 0)
                {
                    RoleCommands.RemoveEntry(role.id, giverIdx);
                    return;
                }

                // Covered by a parent WorkType entry: remove it, re-add all OTHER givers
                int typeIdx = role.entries.FindIndex(e => e.Kind == JobEntryKind.WorkType && e.DefName == type.defName);
                if (typeIdx >= 0)
                {
                    RoleCommands.RemoveEntry(role.id, typeIdx);
                    int insertAt = typeIdx;
                    var otherGivers = type.workGiversByPriority.Where(g => g.defName != giver.defName).ToList();
                    for (int k = otherGivers.Count - 1; k >= 0; k--)
                        RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, otherGivers[k].defName), insertAt);
                }
            }
        }
    }
}
