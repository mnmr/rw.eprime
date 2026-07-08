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
        private int entriesReorderableGroupId = -1;

        private const float ListWidth = 260f;
        private const float RowHeight = 28f;
        private const float IconButton = 20f;

        // Rules section (auto-role checkbox + active-hours grid + location dropdown).
        // Integer-pixel grid geometry: fixed cell width, even gaps.
        private const int HourCellW = 16;
        private const int HourCellH = 20;
        private const int HourCellGap = 2;
        private const int HourGridW = 24 * (HourCellW + HourCellGap) - HourCellGap;
        private const int HourLabelH = 18;
        private const float AutoRowH = 24f;
        private const float RulesSectionH = HourLabelH + 2f + HourCellH;
        // Vanilla-schedule look: paint a color over a grey base.
        private static readonly Color HourActiveColor = Hex("0E7490"); // Tailwind cyan-700
        private static readonly Color HourInactiveColor = new Color(0.35f, 0.35f, 0.35f);

        // Hour-grid paint state: accumulate locally while the button is held and
        // commit ONE SetRoleActiveHours on release (avoids SyncMethod spam in MP).
        private bool paintingHours;
        private bool hourPaintValue;
        private int pendingHoursMask;
        private int paintRoleId = -1;

        // A role is auto iff it has rules; this transient set only reveals the rule
        // inputs for roles that don't have any yet (never scribed, never synced).
        private readonly HashSet<int> rulesRevealed = new HashSet<int>();

        // Change 7: cache of disambiguated display names per WorkGiverDef.
        // Built once (defs don't change mid-game), invalidated by Reset().
        private static Dictionary<WorkGiverDef, string> _giverDisplayCache;

        // Change 4: Tailwind v3 colour palette — 19 families × 3 shades (800, 700, 600; neutral and 500 removed).
        // Laid out as 3 rows (800 top, 600 bottom) × 19 columns.
        internal static readonly Color[] Swatches;
        private static readonly string[] SwatchNames;

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

            var familyNames = new[]
            {
                "Slate", "Stone", "Red", "Orange", "Amber", "Yellow", "Lime", "Green", "Emerald",
                "Teal", "Cyan", "Sky", "Blue", "Indigo", "Violet", "Purple", "Fuchsia", "Pink", "Rose",
            };
            var shadeNames = new[] { "800", "700", "600" };

            int numFamilies = families.GetLength(0); // 19
            int numShades   = families.GetLength(1); // 3

            // shade-major order: swatches[shade * numFamilies + family]
            Swatches = new Color[numShades * numFamilies];
            SwatchNames = new string[numShades * numFamilies];
            for (int shade = 0; shade < numShades; shade++)
                for (int family = 0; family < numFamilies; family++)
                {
                    Swatches[shade * numFamilies + family] = Hex(families[family, shade]);
                    SwatchNames[shade * numFamilies + family] = familyNames[family] + " " + shadeNames[shade];
                }
        }

        /// Content-driven height for window sizing: the role list on the left and
        /// the editor's collapsed job tree on the right are the tall pieces.
        public static float DesiredHeight()
        {
            var store = RoleStore.Current;
            if (store == null) return 684f;
            float chrome = 120f; // tabs, margins, editor gaps
            float list = store.roles.Count * RowHeight + 40f;
            int visibleTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Count(wt => wt.visible);
            float editor = 190f + 32f + visibleTypes * 26f; // top box + tree header + collapsed roots
            return chrome + Mathf.Max(list, editor);
        }

        public void Reset()
        {
            listScroll = entriesScroll = treeScroll = Vector2.zero;
            filter = "";
            selectedRoleId = -1;
            paintingHours = false;
            paintRoleId = -1;
            rulesRevealed.Clear();
            _giverDisplayCache = null; // force rebuild on next use
        }

        public void Draw(Rect rect)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            RoleDrag.Update();
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
            else Widgets.Label(editorRect, "WR_SelectOrCreateRole".Translate());

            DrawDragGhost(store);
            RoleDrag.ResolveMouseUp();
        }

        // ----- Left: role list + management buttons -----

        /// The role list is a two-level tree: roles strictly covered by another role
        /// nest under their tightest coverer (fewest entries; ties keep catalog order),
        /// resolved up to a root so the display never exceeds two levels. Roots keep
        /// catalog order; each root's children sort by where their entry block starts
        /// inside the root's entry list (ties keep catalog order), so child display
        /// order IS the parent's entry order. Rows carry their displayed root so drag
        /// targeting can resolve parent/sibling relationships.
        internal static (List<Role> roots, List<(Role role, Role parent)> rows) BuildRoleTree(RoleStore store)
        {
            var roles = store.roles;
            // Tightest coverer per role (null = root).
            var parent = new Dictionary<Role, Role>();
            foreach (var role in roles)
            {
                Role best = null;
                foreach (var other in roles)
                    if (other.Covers(role) && (best == null || other.entries.Count < best.entries.Count))
                        best = other;
                parent[role] = best;
            }
            Role RootOf(Role role)
            {
                while (parent[role] != null) role = parent[role];
                return role;
            }

            var roots = roles.Where(r => parent[r] == null).ToList();
            var rows = new List<(Role role, Role parent)>(roles.Count);
            foreach (var root in roots)
            {
                rows.Add((root, null));
                // The root covers every displayed descendant, so every child entry
                // has a position in the root's entry list.
                foreach (var child in roles
                    .Where(r => parent[r] != null && RootOf(r) == root)
                    .OrderBy(r => RoleCommands.BlockStart(root.entries, r)))
                    rows.Add((child, root));
            }
            return (roots, rows);
        }

        private void DrawRoleList(Rect rect, RoleStore store)
        {
            float buttonsHeight = 34f;
            var scrollRect = new Rect(rect.x, rect.y, rect.width, rect.height - buttonsHeight - 6f);
            float contentHeight = store.roles.Count * RowHeight;

            var (roots, rows) = BuildRoleTree(store);

            // Dragged-role context, resolved against this frame's tree.
            Role dragged = null;
            Role draggedParent = null;
            if (RoleDrag.Active)
                foreach (var (role, parent) in rows)
                    if (role.id == RoleDrag.RoleId)
                    {
                        dragged = role;
                        draggedParent = parent;
                        break;
                    }

            Widgets.BeginScrollView(scrollRect, ref listScroll,
                new Rect(0f, 0f, scrollRect.width - 16f, contentHeight));
            for (int i = 0; i < rows.Count; i++)
            {
                var (role, parentRole) = rows[i];
                bool isChild = parentRole != null;
                var row = new Rect(0f, i * RowHeight, scrollRect.width - 16f, RowHeight);
                float indent = isChild ? 18f : 0f;

                if (role.id == selectedRoleId) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row) && !RoleDrag.Active) Widgets.DrawHighlight(row);

                if (Mouse.IsOver(row))
                    TooltipHandler.TipRegion(row,
                        $"{role.entries.Count} entries: {string.Join(", ", role.entries.Take(4).Select(e => e.DefName))}{(role.entries.Count > 4 ? ", …" : "")}");

                var swatch = new Rect(Mathf.Round(row.x) + 6f + indent, Mathf.Round(row.y) + 6f, 16f, 16f);
                Widgets.DrawBoxSolid(swatch, role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor);
                GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
                Widgets.DrawBox(swatch.ExpandedBy(1f));
                GUI.color = Color.white;

                string rowLabel = role.enabled ? role.label : "WR_RoleLabelOff".Translate(role.label).ToString();
                var labelRect = new Rect(swatch.xMax + 6f, row.y, row.width - swatch.width - 8f - indent, RowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                if (!role.enabled) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(labelRect, rowLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (role.HasRules)
                {
                    var markerRect = new Rect(labelRect.x + Text.CalcSize(rowLabel).x + 6f,
                        row.y + (RowHeight - 16f) / 2f, 16f, 16f);
                    if (markerRect.xMax <= labelRect.xMax)
                    {
                        var markerColor = RoleChipUI.RuleMarkerColor;
                        if (!role.enabled) markerColor.a *= 0.5f;
                        GUI.color = markerColor;
                        GUI.DrawTexture(markerRect, TexButton.AutoRebuild);
                        GUI.color = Color.white;
                    }
                }

                // Press registers a potential drag + click callback; a release inside
                // the 6px threshold selects (resolved centrally in ResolveMouseUp).
                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                {
                    int capturedId = role.id;
                    RoleDrag.OnPress(capturedId, null, () => selectedRoleId = capturedId);
                    e.Use();
                }

                if (dragged != null && Mouse.IsOver(row))
                    RegisterRowDrop(store, roots, rows, i, row, dragged, draggedParent);
            }
            Widgets.EndScrollView();

            // Three buttons — New / Copy / Delete
            float bw = (rect.width - 8f) / 3f;
            float by = rect.yMax - buttonsHeight + 4f;
            if (Widgets.ButtonText(new Rect(rect.x, by, bw, 30f), "WR_New".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RenameRole("WR_NewRoleTitle".Translate(), null, enteredName =>
                {
                    var newRole = RoleCommands.CreateRole(enteredName);
                    if (newRole != null) selectedRoleId = newRole.id;
                }));
            }

            if (Widgets.ButtonText(new Rect(rect.x + bw + 4f, by, bw, 30f), "WR_Copy".Translate()))
            {
                var toCopy = RoleStore.Current.RoleById(selectedRoleId);
                if (toCopy != null)
                {
                    Find.WindowStack.Add(new Dialog_RenameRole("WR_CopyRoleTitle".Translate(), toCopy.label, enteredName =>
                    {
                        var newRole = RoleCommands.DuplicateRole(selectedRoleId, enteredName);
                        if (newRole != null) selectedRoleId = newRole.id;
                    }));
                }
            }

            var deleteRect = new Rect(rect.x + (bw + 4f) * 2f, by, bw, 30f);
            if (Widgets.ButtonText(deleteRect, "WR_Delete".Translate()))
            {
                var role = RoleStore.Current.RoleById(selectedRoleId);
                if (role != null)
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "WR_DeleteConfirm".Translate(role.label),
                        () => RoleCommands.DeleteRole(role.id), destructive: true));
            }
        }

        // ----- Role-tree drag & drop -----

        /// Drop-zone targeting for the hovered row while a role drag is active.
        /// Dragging a root: the middle 50% of another root row nests into it; the
        /// top/bottom 25% edge zones insert into the catalog before/after that root's
        /// GROUP (root row + its children); a child row nests at that position in the
        /// parent's entry list (top half = before it, bottom half = after it).
        /// Dragging a child: siblings reorder the parent's entries (top/bottom half),
        /// and the bottom half of the parent row moves it to first position. Root-row
        /// edge zones un-nest it — its exact entries leave the parent and it re-roots
        /// at that catalog gap — unless the child is purely semantic (none of its
        /// entries appear exactly in the parent: intrinsically covered, cannot leave).
        /// Everything else — including the dragged row and its own children — blocks.
        private void RegisterRowDrop(RoleStore store, List<Role> roots,
            List<(Role role, Role parent)> rows, int i, Rect row, Role dragged, Role draggedParent)
        {
            var (role, parentRole) = rows[i];

            if (role == dragged || parentRole == dragged)
            {
                RoleDrag.HoverBlocked = true;
                Widgets.DrawBoxSolid(row, new Color(0.8f, 0.2f, 0.2f, 0.12f));
                return;
            }

            float my = Event.current.mousePosition.y - row.y;
            Role NextSibling() => i + 1 < rows.Count && rows[i + 1].parent == parentRole ? rows[i + 1].role : null;

            if (draggedParent == null) // dragging a root
            {
                if (parentRole == null) // over another root row
                {
                    if (my < row.height * 0.25f)
                    {
                        RegisterCatalogInsert(store, roots, dragged, role, row, row.y);
                    }
                    else if (my > row.height * 0.75f)
                    {
                        int groupEnd = i;
                        while (groupEnd + 1 < rows.Count && rows[groupEnd + 1].parent != null) groupEnd++;
                        int nextRootIdx = roots.IndexOf(role) + 1;
                        Role nextRoot = nextRootIdx < roots.Count ? roots[nextRootIdx] : null;
                        RegisterCatalogInsert(store, roots, dragged, nextRoot, row, (groupEnd + 1) * RowHeight);
                    }
                    else
                    {
                        Widgets.DrawHighlight(row);
                        int pid = role.id, cid = dragged.id;
                        RoleDrag.HoverDropAction = () => RoleCommands.NestRoleInto(pid, cid, -1);
                    }
                }
                else // over a child row: nest into its parent at this position
                {
                    int beforeId;
                    float markerY;
                    if (my < row.height / 2f) { beforeId = role.id; markerY = row.y; }
                    else { beforeId = NextSibling()?.id ?? -1; markerY = row.yMax; }
                    DrawInsertMarker(row, markerY);
                    int pid = parentRole.id, cid = dragged.id, captured = beforeId;
                    RoleDrag.HoverDropAction = () => RoleCommands.NestRoleInto(pid, cid, captured);
                }
            }
            else // dragging a child of draggedParent
            {
                // A child with no exact entries in its parent is intrinsically
                // covered: it cannot be un-nested, so root gaps stay blocked.
                bool canUnnest = dragged.entries.Any(e => draggedParent.entries.Contains(e));

                if (parentRole == draggedParent) // sibling: reorder the parent's entries
                {
                    int beforeId;
                    float markerY;
                    if (my < row.height / 2f) { beforeId = role.id; markerY = row.y; }
                    else { beforeId = NextSibling()?.id ?? -1; markerY = row.yMax; }
                    // Same-position drops show the marker but register no command.
                    DrawInsertMarker(row, markerY);
                    if (MoveChildIsNoOp(rows, draggedParent, dragged, beforeId)) return;
                    int pid = draggedParent.id, cid = dragged.id, captured = beforeId;
                    RoleDrag.HoverDropAction = () => RoleCommands.MoveChildBefore(pid, cid, captured);
                }
                else if (role == draggedParent)
                {
                    if (canUnnest && my < row.height * 0.25f)
                    {
                        // Gap before the parent's own group: un-nest, re-root just above it.
                        RegisterChildUnnest(store, dragged, draggedParent, role, row, row.y);
                    }
                    else if (my >= row.height / 2f)
                    {
                        // Bottom half of the parent row: move to first position (a marker-only
                        // no-op when the dragged child is already first).
                        Role first = rows.First(t => t.parent == draggedParent).role;
                        DrawInsertMarker(row, row.yMax);
                        if (first == dragged) return;
                        int pid = draggedParent.id, cid = dragged.id, beforeId = first.id;
                        RoleDrag.HoverDropAction = () => RoleCommands.MoveChildBefore(pid, cid, beforeId);
                    }
                    else
                    {
                        RoleDrag.HoverBlocked = true;
                        Widgets.DrawBoxSolid(row, new Color(0.8f, 0.2f, 0.2f, 0.12f));
                    }
                }
                else if (parentRole == null && canUnnest
                    && (my < row.height * 0.25f || my > row.height * 0.75f))
                {
                    // Root-row edge zones: un-nest into the catalog gap before/after
                    // that root's group.
                    if (my < row.height * 0.25f)
                    {
                        RegisterChildUnnest(store, dragged, draggedParent, role, row, row.y);
                    }
                    else
                    {
                        int groupEnd = i;
                        while (groupEnd + 1 < rows.Count && rows[groupEnd + 1].parent != null) groupEnd++;
                        int nextRootIdx = roots.IndexOf(role) + 1;
                        Role nextRoot = nextRootIdx < roots.Count ? roots[nextRootIdx] : null;
                        RegisterChildUnnest(store, dragged, draggedParent, nextRoot, row, (groupEnd + 1) * RowHeight);
                    }
                }
                else
                {
                    RoleDrag.HoverBlocked = true;
                    Widgets.DrawBoxSolid(row, new Color(0.8f, 0.2f, 0.2f, 0.12f));
                }
            }
        }

        /// Root-gap drop while dragging a child: un-nests it from its parent, then
        /// moves it in the catalog so it re-roots at that gap (insertBeforeRoot ==
        /// null = after the last root's group). The un-nest always changes the parent
        /// (purely semantic children are blocked upstream); the catalog move is
        /// skipped when it would reproduce the current order.
        private static void RegisterChildUnnest(RoleStore store, Role dragged, Role draggedParent,
            Role insertBeforeRoot, Rect row, float markerY)
        {
            DrawInsertMarker(row, markerY);
            int catFrom = store.roles.IndexOf(dragged);
            if (catFrom < 0) return;
            int catTo;
            if (insertBeforeRoot == null)
            {
                catTo = store.roles.Count - 1;
            }
            else
            {
                catTo = store.roles.IndexOf(insertBeforeRoot);
                if (catTo < 0) return;
                if (catFrom < catTo) catTo--;
            }
            int pid = draggedParent.id, cid = dragged.id, from = catFrom, to = catTo;
            RoleDrag.HoverDropAction = () =>
            {
                RoleCommands.UnnestRole(pid, cid);
                if (from != to) RoleCommands.MoveRoleInCatalog(from, to);
            };
        }

        /// Root edge-zone drop: insert the dragged root before insertBeforeRoot in the
        /// catalog (null = after the last root). After the pre-removal adjustment, the
        /// moved root lands at the catalog slot of the root now occupying the target
        /// position (before it when moving up, after it when moving down — matching
        /// MoveRoleInCatalog's remove-then-insert semantics). Same-position drops show
        /// the marker but register no command.
        private static void RegisterCatalogInsert(RoleStore store, List<Role> roots,
            Role dragged, Role insertBeforeRoot, Rect row, float markerY)
        {
            DrawInsertMarker(row, markerY);
            int from = roots.IndexOf(dragged);
            int insertPos = insertBeforeRoot != null ? roots.IndexOf(insertBeforeRoot) : roots.Count;
            if (from < 0 || insertPos < 0) return;
            int to = insertPos > from ? insertPos - 1 : insertPos;
            if (to == from) return;
            int catFrom = store.roles.IndexOf(dragged);
            int catTo = store.roles.IndexOf(roots[to]);
            RoleDrag.HoverDropAction = () => RoleCommands.MoveRoleInCatalog(catFrom, catTo);
        }

        /// A sibling drop that recreates the current display order (before itself,
        /// before its own next sibling, or append while already last) is a no-op.
        private static bool MoveChildIsNoOp(List<(Role role, Role parent)> rows, Role parent, Role child, int beforeId)
        {
            var siblings = rows.Where(t => t.parent == parent).Select(t => t.role).ToList();
            int cur = siblings.IndexOf(child);
            if (cur < 0) return false;
            if (beforeId == child.id) return true;
            if (beforeId == -1) return cur == siblings.Count - 1;
            return cur + 1 < siblings.Count && siblings[cur + 1].id == beforeId;
        }

        /// 2px horizontal insertion marker across the row width at the given boundary.
        private static void DrawInsertMarker(Rect row, float y)
            => Widgets.DrawBoxSolid(new Rect(row.x, y - 1f, row.width, 2f), new Color(1f, 1f, 1f, 0.9f));

        /// Floating drag ghost: the row's swatch square + label following the cursor,
        /// red-tinted while over a blocked target.
        private static void DrawDragGhost(RoleStore store)
        {
            if (!RoleDrag.Active) return;
            var role = store.RoleById(RoleDrag.RoleId);
            if (role == null) return;
            var mouse = Event.current.mousePosition;
            Color tint = RoleDrag.HoverBlocked
                ? new Color(1f, 0.3f, 0.3f, 0.7f)
                : new Color(1f, 1f, 1f, 0.7f);

            Text.Font = GameFont.Small;
            float labelW = Text.CalcSize(role.label).x + 4f;
            GUI.color = tint;
            Widgets.DrawBoxSolid(new Rect(mouse.x + 10f, mouse.y + 6f, 16f, 16f),
                role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(mouse.x + 32f, mouse.y + 2f, labelW, 24f), role.label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
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
            const float RulesRowGap = 6f;
            // +1 row: player-defined custom swatch slots under the Tailwind grid.
            float swatchGridH = (SwatchSize + SwatchGap) * (SwatchRows + 1) - SwatchGap;
            float leftContentH = TitleH + AssignedRowH + NamesRowH;
            bool rulesShown = role.HasRules || rulesRevealed.Contains(role.id);
            float rulesH = AutoRowH + (rulesShown ? RulesRowGap + RulesSectionH : 0f);
            float TopBoxHeight = Mathf.Max(swatchGridH, leftContentH) + RulesRowGap + rulesH + TopBoxPadding * 2f;

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
                TooltipHandler.TipRegion(swatchRect, SwatchNames[i]);
                if (Widgets.ButtonInvisible(swatchRect))
                    RoleCommands.SetRoleColor(role.id, Swatches[i]);
            }

            // Custom row: player-defined slots. Empty slot = pick a color (applies
            // it too); filled = click to apply, right-click to redefine.
            var custom = store.customSwatches;
            float customY = swatchStartY + SwatchRows * (SwatchSize + SwatchGap);
            for (int c = 0; c < SwatchCols; c++)
            {
                var slotRect = new Rect(swatchStartX + c * (SwatchSize + SwatchGap), customY, SwatchSize, SwatchSize);
                var slotColor = c < custom.Count ? custom[c] : UnityEngine.Color.clear;
                bool empty = slotColor.a < 0.5f;
                int capturedSlot = c;
                int capturedRoleId = role.id;

                void OpenPicker(bool applyToRole)
                {
                    Find.WindowStack.Add(new Dialog_RoleColorPicker(
                        role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor,
                        picked =>
                        {
                            RoleCommands.SetCustomSwatch(capturedSlot, picked);
                            if (applyToRole) RoleCommands.SetRoleColor(capturedRoleId, picked);
                        }));
                }

                if (empty)
                {
                    Widgets.DrawBoxSolid(slotRect, new Color(0.14f, 0.14f, 0.14f));
                    GUI.color = new Color(1f, 1f, 1f, 0.35f);
                    Widgets.DrawBox(slotRect);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(slotRect, "+");
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(slotRect, "WR_CustomSwatchEmpty".Translate());
                    if (Widgets.ButtonInvisible(slotRect))
                        OpenPicker(applyToRole: true);
                }
                else
                {
                    Widgets.DrawBoxSolid(slotRect, slotColor);
                    if (role.hasCustomColor && role.color.IndistinguishableFrom(slotColor))
                        Widgets.DrawBox(slotRect.ExpandedBy(2f));
                    TooltipHandler.TipRegion(slotRect, "WR_CustomSwatchTip".Translate());
                    if (Widgets.ButtonInvisible(slotRect))
                        RoleCommands.SetRoleColor(role.id, slotColor);
                    var e = Event.current;
                    if (e.type == EventType.MouseDown && e.button == 1 && slotRect.Contains(e.mousePosition))
                    {
                        e.Use();
                        OpenPicker(applyToRole: false);
                    }
                }
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
            Widgets.Label(new Rect(leftX, row2Y, leftW, AssignedRowH), "WR_AssignedTo".Translate());
            GUI.color = Color.white;

            // Row 3: colonist names, ordered by position in their assignment list
            float row3Y = row2Y + AssignedRowH;
            DrawAssignedPawnNames(new Rect(leftX, row3Y, leftW, NamesRowH), role, store);

            // Row 4 (full box width): auto-role opt-in; rule inputs only while it's on.
            float autoY = topBox.y + TopBoxPadding + Mathf.Max(swatchGridH, leftContentH) + RulesRowGap;
            DrawAutoRoleRow(new Rect(leftX, autoY, topBox.width - TopBoxPadding * 2f, AutoRowH), role, rulesShown);
            if (rulesShown)
                DrawRulesSection(new Rect(leftX, autoY + AutoRowH + RulesRowGap,
                    topBox.width - TopBoxPadding * 2f, RulesSectionH), role);

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

        // ----- Rules section: auto-role opt-in, active-hours grid, location dropdown -----

        private void DrawAutoRoleRow(Rect rect, Role role, bool shown)
        {
            Text.Font = GameFont.Small;
            string label = "WR_AutoRole".Translate();
            var boxRect = new Rect(rect.x, rect.y, Mathf.Min(Text.CalcSize(label).x + 34f, rect.width), rect.height);
            TooltipHandler.TipRegion(boxRect, "WR_AutoRoleTip".Translate());
            bool wanted = shown;
            Widgets.CheckboxLabeled(boxRect, label, ref wanted);

            // Auto-assign to newcomers: player-settable on any role.
            string assignLabel = "WR_AutoAssign".Translate();
            var assignRect = new Rect(boxRect.xMax + 24f, rect.y,
                Mathf.Min(Text.CalcSize(assignLabel).x + 34f, rect.xMax - boxRect.xMax - 24f), rect.height);
            TooltipHandler.TipRegion(assignRect, "WR_AutoAssignTip".Translate());
            bool autoAssign = role.autoAssign;
            Widgets.CheckboxLabeled(assignRect, assignLabel, ref autoAssign);
            if (autoAssign != role.autoAssign)
                RoleCommands.SetRoleAutoAssign(role.id, autoAssign);

            if (wanted == shown) return;

            if (wanted)
            {
                rulesRevealed.Add(role.id);
            }
            else if (role.HasRules)
            {
                // The checkbox derives from HasRules, so unchecking means clearing the rules.
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "WR_ClearRulesConfirm".Translate(role.label),
                    () =>
                    {
                        RoleCommands.ClearRoleRules(role.id);
                        rulesRevealed.Remove(role.id);
                    },
                    destructive: true));
            }
            else
            {
                rulesRevealed.Remove(role.id);
            }
        }

        private void DrawRulesSection(Rect rect, Role role)
        {
            // Selecting another role mid-paint abandons the pending edit.
            if (paintingHours && paintRoleId != role.id)
                paintingHours = false;

            int shownMask = paintingHours ? pendingHoursMask : role.activeHours;
            bool mouseHeld = Input.GetMouseButton(0);
            int x0 = Mathf.RoundToInt(rect.x);
            int labelsY = Mathf.RoundToInt(rect.y);
            int cellsY = labelsY + HourLabelH + 2;

            // Hour headers: one per cell, Tiny and bottom-anchored (vanilla schedule style).
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            for (int h = 0; h < 24; h++)
                Widgets.Label(new Rect(x0 + h * (HourCellW + HourCellGap), labelsY, HourCellW, HourLabelH), h.ToString());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            var gridRect = new Rect(x0, cellsY, HourGridW, HourCellH);
            if (Mouse.IsOver(gridRect))
                TooltipHandler.TipRegion(gridRect, "WR_ActiveHours".Translate());

            for (int h = 0; h < 24; h++)
            {
                var cell = new Rect(x0 + h * (HourCellW + HourCellGap), cellsY, HourCellW, HourCellH);
                bool active = (shownMask & (1 << h)) != 0;
                Widgets.DrawBoxSolid(cell, active ? HourActiveColor : HourInactiveColor);

                if (!Mouse.IsOver(cell)) continue;
                Widgets.DrawBox(cell, 2);

                var e = Event.current;
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    // Start painting: target value = inverse of the pressed cell
                    // (simplified vanilla timetable pattern).
                    paintingHours = true;
                    paintRoleId = role.id;
                    pendingHoursMask = role.activeHours;
                    hourPaintValue = !active;
                    ApplyHourPaint(h);
                    e.Use();
                }
                else if (paintingHours && mouseHeld)
                {
                    ApplyHourPaint(h);
                }
            }

            // Commit ONE synced command on release, and only when something changed.
            if (paintingHours && !mouseHeld)
            {
                paintingHours = false;
                if (pendingHoursMask != role.activeHours)
                    RoleCommands.SetRoleActiveHours(role.id, pendingHoursMask);
            }

            // Location dropdown right of the grid.
            const float LocBtnW = 110f;
            float btnX = gridRect.xMax + 16f;
            if (Widgets.ButtonText(new Rect(btnX, cellsY + (HourCellH - 24f) / 2f, LocBtnW, 24f),
                    LocationLabel(role.location)))
            {
                int roleId = role.id;
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>
                {
                    new FloatMenuOption("WR_LocationAny".Translate(), () => RoleCommands.SetRoleLocation(roleId, RoleLocation.Any)),
                    new FloatMenuOption("WR_LocationHome".Translate(), () => RoleCommands.SetRoleLocation(roleId, RoleLocation.HomeOnly)),
                    new FloatMenuOption("WR_LocationAway".Translate(), () => RoleCommands.SetRoleLocation(roleId, RoleLocation.AwayOnly)),
                }));
            }

            // Legend in the header row above the slots, right-aligned in the section.
            const float LegendGap = 12f;
            Text.Font = GameFont.Small;
            string activeLabel = "WR_HoursActive".Translate();
            string inactiveLabel = "WR_HoursInactive".Translate();
            float legendW = LegendEntryWidth(activeLabel) + LegendGap + LegendEntryWidth(inactiveLabel);
            float legendX = rect.xMax - legendW;
            if (legendX >= gridRect.xMax + 8f)
            {
                legendX = DrawLegendEntry(legendX, labelsY, HourActiveColor, activeLabel);
                DrawLegendEntry(legendX + LegendGap, labelsY, HourInactiveColor, inactiveLabel);
            }
        }

        private const float LegendSwatch = 12f;

        private static float LegendEntryWidth(string label)
        {
            Text.Font = GameFont.Small;
            return LegendSwatch + 4f + Text.CalcSize(label).x;
        }

        private static float DrawLegendEntry(float x, float y, Color color, string label)
        {
            Text.Font = GameFont.Small;
            var size = Text.CalcSize(label);
            Widgets.DrawBoxSolid(new Rect(x, y + (HourLabelH - LegendSwatch) / 2f, LegendSwatch, LegendSwatch), color);
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(new Rect(x + LegendSwatch + 4f, y + (HourLabelH - size.y) / 2f, size.x + 2f, size.y), label);
            GUI.color = Color.white;
            return x + LegendSwatch + 4f + size.x;
        }

        private void ApplyHourPaint(int hour)
        {
            if (hourPaintValue) pendingHoursMask |= 1 << hour;
            else pendingHoursMask &= ~(1 << hour);
        }

        private static string LocationLabel(RoleLocation location)
        {
            switch (location)
            {
                case RoleLocation.HomeOnly: return "WR_LocationHome".Translate();
                case RoleLocation.AwayOnly: return "WR_LocationAway".Translate();
                default: return "WR_LocationAny".Translate();
            }
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
                Widgets.Label(rect, "WR_Nobody".Translate());
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
                string moreText = "WR_PlusOthers".Translate(remaining);
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
            WrText.HeaderLabel(new Rect(rect.x + 8f, rect.y, rect.width - 8f, 28f), "WR_SelectedJobs".Translate());

            // Column headers — 24f height so descenders aren't clipped
            float headerY = rect.y + 28f + 4f;
            float removeW = (IconButton + 4f) * 3f; // room for up + down + [x]
            float typeW = (rect.width - 8f - removeW - 8f) * 0.45f;
            float jobW  = (rect.width - 8f - removeW - 8f) * 0.55f;

            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x + 8f + 4f, headerY, typeW, 24f), "WR_TypeColumn".Translate());
            Widgets.Label(new Rect(rect.x + 8f + 4f + typeW, headerY, jobW, 24f), "WR_JobColumn".Translate());
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
                    Widgets.Label(new Rect(row.x + 4f + typeW, row.y, jobW, RowHeight), "WR_AllJobs".Translate());
                }
                else
                {
                    Widgets.Label(new Rect(row.x + 4f + typeW, row.y, jobW, RowHeight), jobLabel);
                }

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                if (missing)
                    TooltipHandler.TipRegion(row, "WR_MissingDef".Translate(entry.DefName));

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
                    jobLabel = "WR_AllJobs".Translate();
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

            WrText.HeaderLabel(new Rect(rect.x + 4f, rect.y, headerW - 4f, 28f), "WR_AvailableJobs".Translate());

            // "Search" label immediately left of field; group shifted 4f left from right edge
            const float SearchRightPad = 4f;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(rect.xMax - SearchLabelW - SearchW - 4f - SearchRightPad, rect.y + (28f - SearchH) / 2f, SearchLabelW, SearchH), "WR_Search".Translate());
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
                            result[g] = baseName + "WR_EmergencySuffix".Translate();
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
