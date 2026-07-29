using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// The holder-scale editor: two captioned numeric rows over a band-label
    /// row. The totals row edits recommended assignments (trainees included);
    /// the minimums row edits the direct-assignment floor, with the trainee
    /// share the implicit difference. Click +1 (wraps at the cap), right-click
    /// -1, drag across bands to copy the pressed band's value. Max is not
    /// editable here (uncapped in practice). Presets fork on first edit; every
    /// gesture commits once, on release. Reset restores the values captured
    /// when the role or scale was selected.
    internal static class ScaleEditorUI
    {
        private const float PickerRowH = 26f;
        private const float BandRowH = 20f;
        private const float CaptionH = 16f;
        private const float BandLabelRowH = 16f;

        /// Editor height for this role: path targets get the totals row,
        /// their training roles a help paragraph in its place.
        internal static float HeightFor(Role role, RoleStore store, float width)
        {
            float h = PickerRowH + 4f + CaptionH + BandRowH + 2f + BandLabelRowH;
            if (store == null) return h;
            if (IsPathTarget(store, role.id))
                return h + CaptionH + BandRowH + 2f;
            var target = ControllingTarget(store, role.id);
            return target == null ? h
                : h + HelpHeight(TrainingHelp(target, role), width);
        }

        private static string TrainingHelp(Role target, Role role) =>
            "WR_ScaleTrainingHelp".Translate(target.label, role.label).ToString();

        private static float HelpHeight(string text, float width)
        {
            Text.Font = GameFont.Tiny;
            float h = Text.CalcHeight(text, width - 8f);
            Text.Font = GameFont.Small;
            return h + 4f;
        }

        /// True when some training path lists the role at its highest
        /// min-skill band (ties count): trainees may substitute for it, so
        /// the trainee-inclusive totals row applies.
        internal static bool IsPathTarget(RoleStore store, int roleId)
        {
            foreach (var path in store.trainingPaths)
            {
                int at = path.roleIds.IndexOf(roleId);
                if (at < 0 || at >= path.bandMins.Count) continue;
                int highest = int.MinValue;
                foreach (int min in path.bandMins)
                    highest = Mathf.Max(highest, min);
                if (path.bandMins[at] >= highest) return true;
            }
            return false;
        }

        /// The target (highest min band) of the first path containing the
        /// role, when the role is never itself a target; null otherwise.
        internal static Role ControllingTarget(RoleStore store, int roleId)
        {
            if (IsPathTarget(store, roleId)) return null;
            foreach (var path in store.trainingPaths)
            {
                int at = path.roleIds.IndexOf(roleId);
                if (at < 0 || at >= path.bandMins.Count) continue;
                int highest = int.MinValue;
                foreach (int min in path.bandMins)
                    highest = Mathf.Max(highest, min);
                for (int i = 0; i < path.bandMins.Count; i++)
                    if (path.bandMins[i] == highest)
                    {
                        var target = store.RoleById(path.roleIds[i]);
                        if (target != null) return target;
                    }
            }
            return null;
        }

        private const int MaxDirectPick = 8;
        private const int MaxTotalPick = 30;
        private const int SeriesDirectRow = 0, SeriesTotalRow = 1;

        private static readonly Color AxisColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color CellPanel = new Color(1f, 1f, 1f, 0.06f);

        // One gesture at a time: a working copy previews live, commits on
        // release. dragDirect freezes each band's direct minimum so total
        // edits recompute the trainee slice instead of dragging it along.
        private static int dragRoleId = -1;
        private static int dragSeries = -1;
        private static int dragPickValue;
        private static int dragOriginBand = -1;
        private static int dragButton;
        private static bool dragMoved;
        private static int[] dragDirect;
        private static HolderScale dragScale;
        private static string dragSourceName;

        // Reset baseline: the values on display when the user last picked this
        // role or scale. Fork commits rename the edited scale mid-session;
        // expectedName lets the baseline follow the fork instead of resetting.
        private static int baselineRoleId = -1;
        private static string baselineName;
        private static HolderScale baseline;
        private static string expectedName;
        // The shown scale is a this-session fork of a preset: Save As renames
        // it instead of splitting off yet another scale.
        private static bool baselineForked;

        internal static void ReleaseState()
        {
            dragRoleId = -1;
            dragSeries = -1;
            dragScale = null;
            dragDirect = null;
            dragSourceName = null;
            baselineRoleId = -1;
            baselineName = null;
            baseline = null;
            expectedName = null;
            baselineForked = false;
        }

        internal static void Draw(Rect rect, Role role, RoleStore store)
        {
            HolderScale stored = store.ScaleFor(role) ?? store.ScaleByName("Never");
            if (stored == null) return; // pre-seed frame
            UpdateBaseline(role, stored);
            HolderScale shown = dragRoleId == role.id && dragScale != null
                ? dragScale : stored;

            DrawPickerRow(new Rect(rect.x, rect.y, rect.width, PickerRowH),
                role, store, stored);
            float y = rect.y + PickerRowH + 4f;

            if (IsPathTarget(store, role.id))
            {
                DrawCaption(new Rect(rect.x, y, rect.width, CaptionH),
                    "WR_ScaleTotalsCaption".Translate());
                y += CaptionH;
                DrawValueRow(new Rect(rect.x, y, rect.width, BandRowH),
                    role, shown, stored, totalsRow: true);
                y += BandRowH + 2f;
            }
            else if (ControllingTarget(store, role.id) is Role target)
            {
                string help = TrainingHelp(target, role);
                float helpH = HelpHeight(help, rect.width);
                Text.Font = GameFont.Tiny;
                GUI.color = WrStyle.CaptionText;
                Widgets.Label(new Rect(rect.x + 4f, y, rect.width - 8f, helpH - 4f), help);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += helpH;
            }

            DrawCaption(new Rect(rect.x, y, rect.width, CaptionH),
                "WR_ScaleMinsCaption".Translate(role.label));
            y += CaptionH;
            DrawValueRow(new Rect(rect.x, y, rect.width, BandRowH),
                role, shown, stored, totalsRow: false);
            y += BandRowH + 2f;

            DrawBandLabels(new Rect(rect.x, y, rect.width, BandLabelRowH));
        }

        /// Recaptures the pre-edit values whenever the user switches role or
        /// picks another scale; our own fork commits only carry the name over.
        private static void UpdateBaseline(Role role, HolderScale stored)
        {
            if (baselineRoleId == role.id
                && string.Equals(stored.Name, baselineName,
                    System.StringComparison.OrdinalIgnoreCase)) return;
            if (baselineRoleId == role.id && expectedName != null
                && string.Equals(stored.Name, expectedName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                baselineName = stored.Name;
                return;
            }
            baselineRoleId = role.id;
            baselineName = stored.Name;
            baseline = stored.Copy();
            expectedName = null;
            baselineForked = false;
        }

        private static int DirectOf(HolderScale scale, int band) =>
            Mathf.Max(0, scale.Min[band] - scale.Train[band]);

        /// Raising direct past the total grows the total with it; otherwise
        /// the trainee share absorbs the difference.
        private static void SetDirect(HolderScale scale, int band, int direct)
        {
            direct = Mathf.Clamp(direct, 0, MaxDirectPick);
            if (direct > scale.Min[band]) scale.Min[band] = direct;
            scale.Train[band] = scale.Min[band] - direct;
        }

        /// The totals row sets the TOTAL; each band's direct minimum stays
        /// frozen so the trainee share absorbs the change.
        private static void SetTotal(HolderScale scale, int band, int total)
        {
            scale.Min[band] = Mathf.Clamp(total, 0, MaxTotalPick);
            scale.Train[band] = Mathf.Max(0, scale.Min[band] - dragDirect[band]);
        }

        // ----- Shared band-column geometry (integer spacing everywhere) -----

        /// Integer column width and centered integer start so every gap
        /// between columns is exactly equal.
        private static int ColW(float width) => (int)(width / HolderScale.Bands);

        private static float StartX(Rect area)
        {
            int colW = ColW(area.width);
            return Mathf.Round(area.x
                + (area.width - colW * HolderScale.Bands) / 2f);
        }

        private static string BandLabel(int band) =>
            band == HolderScale.Bands - 1
                ? (band * HolderScale.BandSize + 1) + "+"
                : (band * HolderScale.BandSize + 1) + "-"
                    + (band + 1) * HolderScale.BandSize;

        // ----- Picker row (scale selection) -----

        private static void DrawPickerRow(Rect rect, Role role, RoleStore store,
            HolderScale scale)
        {
            const float PickW = 150f;
            const float AddW = 90f;
            const float ResetW = 70f;
            var pickRect = new Rect(rect.x, rect.y, PickW, rect.height - 2f);
            if (Widgets.ButtonText(pickRect, scale.Name.Truncate(PickW - 20f)))
            {
                var options = new List<FloatMenuOption>();
                foreach (var candidate in store.holderScales
                             .OrderBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase))
                {
                    string captured = candidate.Name;
                    var option = new FloatMenuOption(candidate.Name,
                        () => RoleCommands.SetRoleScale(role.id, captured));
                    if (!candidate.Preset)
                    {
                        // Same dismiss icon as role chips (ChipUI remove X).
                        option.extraPartWidth = 24f;
                        option.extraPartOnGUI = part =>
                        {
                            var iconRect = new Rect(part.x + 4f,
                                part.y + (part.height - 16f) / 2f, 16f, 16f);
                            if (Widgets.ButtonImage(iconRect, TexButton.Delete))
                            {
                                RequestDelete(captured);
                                return true;
                            }
                            return false;
                        };
                    }
                    options.Add(option);
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            float x = pickRect.xMax + 4f;
            if (!scale.Preset)
            {
                var renameRect = new Rect(x, rect.y + (rect.height - 18f) / 2f, 18f, 18f);
                TooltipHandler.TipRegion(renameRect, "WR_ScaleRenameTip".Translate());
                if (Widgets.ButtonImage(renameRect, TexButton.Rename))
                {
                    string oldName = scale.Name;
                    Find.WindowStack.Add(new Dialog_RenameRole(
                        "WR_ScaleRenameTitle".Translate(),
                        name => RoleCommands.RenameScale(oldName, name),
                        oldName));
                }
                x = renameRect.xMax + 6f;
            }

            bool dirty = baseline != null && !MatchesBaseline(scale);
            var addRect = new Rect(rect.xMax - AddW, rect.y, AddW, rect.height - 2f);
            var resetRect = new Rect(addRect.x - 4f - ResetW, rect.y,
                ResetW, rect.height - 2f);

            string usedBy = UsedBySummary(store, scale.Name);
            if (usedBy != null)
            {
                float labelEnd = (dirty ? resetRect.x : addRect.x) - 8f;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = WrStyle.DimText;
                Widgets.Label(new Rect(x, rect.y, labelEnd - x, rect.height),
                    usedBy);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            if (dirty)
            {
                TooltipHandler.TipRegion(resetRect, "WR_ScaleResetTip".Translate());
                if (Widgets.ButtonText(resetRect, "WR_ScaleReset".Translate()))
                    CommitValues(role, scale, baseline);
            }

            if (Widgets.ButtonText(addRect,
                    (dirty ? "WR_ScaleSaveAs" : "WR_AddNew").Translate()))
            {
                if (dirty) OpenSaveAsDialog(role, scale);
                else OpenAddNewDialog(role, scale);
            }
        }

        /// Clean state: a plain copy of the current scale under a new name,
        /// with the role pointed at it.
        private static void OpenAddNewDialog(Role role, HolderScale scale)
        {
            string sourceName = scale.Name;
            int roleId = role.id;
            Find.WindowStack.Add(new Dialog_RenameRole(
                "WR_ScaleNewTitle".Translate(), name =>
                {
                    var store = RoleStore.Current;
                    name = name?.Trim();
                    if (store == null || name.NullOrEmpty()
                        || store.ScaleByName(name) != null) return;
                    RoleCommands.CommitScaleEdit(new ScaleEdit
                    {
                        roleId = roleId,
                        sourceName = sourceName,
                        targetName = name,
                    });
                }));
        }

        /// Dirty state: the session's edits land under the new name (with the
        /// role pointed at it) and the edited scale reverts to its pre-edit
        /// values. A preset fork just renames — the preset never changed.
        private static void OpenSaveAsDialog(Role role, HolderScale scale)
        {
            string sourceName = scale.Name;
            bool forked = baselineForked;
            var edited = scale.Copy();
            var original = baseline.Copy();
            int roleId = role.id;
            Find.WindowStack.Add(new Dialog_RenameRole(
                "WR_ScaleSaveAsTitle".Translate(), name =>
                {
                    var store = RoleStore.Current;
                    name = name?.Trim();
                    if (store == null || name.NullOrEmpty()
                        || store.ScaleByName(name) != null) return;
                    if (forked)
                        RoleCommands.RenameScale(sourceName, name);
                    else
                    {
                        RoleCommands.CommitScaleEdit(new ScaleEdit
                        {
                            roleId = roleId,
                            sourceName = sourceName,
                            targetName = name,
                            min = HolderScaleCodec.EncodeRow(edited.Min),
                            train = HolderScaleCodec.EncodeRow(edited.Train),
                        });
                        RoleCommands.CommitScaleEdit(new ScaleEdit
                        {
                            sourceName = sourceName,
                            targetName = sourceName,
                            min = HolderScaleCodec.EncodeRow(original.Min),
                            train = HolderScaleCodec.EncodeRow(original.Train),
                        });
                    }
                    baselineRoleId = -1; // recapture clean from the new scale
                }));
        }

        private static bool MatchesBaseline(HolderScale stored)
        {
            for (int i = 0; i < HolderScale.Bands; i++)
                if (stored.Min[i] != baseline.Min[i]
                    || stored.Train[i] != baseline.Train[i])
                    return false;
            return true;
        }

        /// Dropdown delete: unused scales go immediately; referenced ones
        /// confirm first, naming the roles that will fall back to Never.
        private static void RequestDelete(string name)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            string usedBy = UsedBySummary(store, name);
            if (usedBy == null)
            {
                RoleCommands.DeleteScale(name);
                return;
            }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "WR_ScaleDeleteConfirm".Translate(name, usedBy),
                () => RoleCommands.DeleteScale(name), destructive: true));
        }

        /// "Used by Doctor, Medic, Nurse +2 more" — first three names, dim.
        private static string UsedBySummary(RoleStore store, string scaleName)
        {
            List<string> names = null;
            int count = 0;
            foreach (var role in store.roles)
            {
                if (!string.Equals(role.holderScaleName, scaleName,
                        System.StringComparison.OrdinalIgnoreCase)) continue;
                count++;
                if (count <= 3) (names ??= new List<string>()).Add(role.label);
            }
            if (names == null) return null;
            string text = names.ToCommaList();
            if (count > 3) text += " " + "WR_TipMore".Translate(count - 3).ToString();
            return "WR_ScaleUsedBy".Translate(text).ToString();
        }

        // ----- Captions and band labels -----

        private static void DrawCaption(Rect rect, string text)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = WrStyle.CaptionText;
            Widgets.Label(new Rect(rect.x + 4f, rect.y, rect.width - 8f, rect.height), text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        private static void DrawBandLabels(Rect rect)
        {
            int colW = ColW(rect.width);
            float startX = StartX(rect);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = AxisColor;
            for (int band = 0; band < HolderScale.Bands; band++)
                Widgets.Label(new Rect(startX + band * colW, rect.y,
                    colW, rect.height), BandLabel(band));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }

        // ----- Band value rows (totals and direct minimums) -----

        private static void DrawValueRow(Rect rect, Role role,
            HolderScale shown, HolderScale stored, bool totalsRow)
        {
            int colW = ColW(rect.width);
            float startX = StartX(rect);
            var e = Event.current;
            int series = totalsRow ? SeriesTotalRow : SeriesDirectRow;
            int wrapAt = totalsRow ? MaxTotalPick : MaxDirectPick;
            bool picking = dragRoleId == role.id && dragSeries == series
                && dragScale != null;

            for (int band = 0; band < HolderScale.Bands; band++)
            {
                var cell = new Rect(startX + band * colW + 4f, rect.y,
                    colW - 8f, rect.height);
                Widgets.DrawBoxSolid(cell, CellPanel);
                Widgets.DrawHighlightIfMouseover(cell);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(cell, totalsRow
                    ? shown.Min[band].ToStringCached()
                    : DirectOf(shown, band).ToStringCached());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                if (Mouse.IsOver(cell))
                    TooltipHandler.TipRegion(cell, totalsRow
                        ? "WR_ScaleTotalTip".Translate()
                        : "WR_ScaleDirectTip".Translate());

                if (e.type == EventType.MouseDown && cell.Contains(e.mousePosition))
                {
                    // Nothing changes on press: a plain click increments on
                    // release, a drag-across copies the ORIGIN's value as-is.
                    BeginGesture(role, stored, series);
                    dragOriginBand = band;
                    dragButton = e.button;
                    dragMoved = false;
                    dragPickValue = totalsRow
                        ? dragScale.Min[band] : DirectOf(dragScale, band);
                    e.Use();
                }
                else if (picking && e.type == EventType.MouseDrag
                    && cell.Contains(e.mousePosition) && band != dragOriginBand)
                {
                    dragMoved = true;
                    ApplyRowValue(band, totalsRow);
                    e.Use();
                }
            }

            if (picking && (e.type == EventType.MouseUp || !Input.GetMouseButton(0)
                && !Input.GetMouseButton(1)))
            {
                if (!dragMoved)
                {
                    dragPickValue = dragButton == 1
                        ? Mathf.Max(0, dragPickValue - 1)
                        : dragPickValue >= wrapAt ? 0 : dragPickValue + 1;
                    ApplyRowValue(dragOriginBand, totalsRow);
                }
                Commit(role);
                EndGesture();
                if (e.type == EventType.MouseUp) e.Use();
            }
        }

        private static void ApplyRowValue(int band, bool totalsRow)
        {
            if (totalsRow) SetTotal(dragScale, band, dragPickValue);
            else SetDirect(dragScale, band, dragPickValue);
            dragScale.Normalize();
        }

        private static void BeginGesture(Role role, HolderScale stored, int series)
        {
            dragRoleId = role.id;
            dragSeries = series;
            dragScale = stored.Copy();
            dragSourceName = stored.Name;
            dragDirect = new int[HolderScale.Bands];
            for (int band = 0; band < HolderScale.Bands; band++)
                dragDirect[band] = DirectOf(stored, band);
        }

        private static void EndGesture()
        {
            dragRoleId = -1;
            dragSeries = -1;
            dragOriginBand = -1;
            dragMoved = false;
            dragScale = null;
            dragDirect = null;
            dragSourceName = null;
        }

        /// Presets fork into "<name> N" (unique); user scales commit in place.
        private static void Commit(Role role)
        {
            if (dragScale == null) return;
            var store = RoleStore.Current;
            var source = store?.ScaleByName(dragSourceName);
            if (store == null || source != null && source.SameValuesAs(dragScale))
                return;
            CommitValues(role, source, dragScale);
        }

        private static void CommitValues(Role role, HolderScale source,
            HolderScale values)
        {
            var store = RoleStore.Current;
            if (store == null || values == null) return;
            string sourceName = source?.Name;
            string targetName = source != null && source.Preset
                ? CatalogNameRules.Unique(sourceName, store.holderScales, c => c.Name)
                : sourceName;
            if (targetName.NullOrEmpty()) return;
            expectedName = targetName;
            if (source != null && source.Preset) baselineForked = true;
            // Max is deliberately not sent: the editor never touches it, so
            // the target keeps its stored row (uncapped for new scales).
            RoleCommands.CommitScaleEdit(new ScaleEdit
            {
                roleId = role.id,
                sourceName = sourceName,
                targetName = targetName,
                min = HolderScaleCodec.EncodeRow(values.Min),
                train = HolderScaleCodec.EncodeRow(values.Train),
            });
        }
    }
}
