using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    internal sealed class ScaleEditorSnapshot
    {
        private readonly List<ScaleOptionSnapshot> options;
        private readonly HolderScale stored;

        internal ScaleEditorSnapshot(int roleId, string roleLabel,
            HolderScale stored, bool pathTarget, string trainingHelp,
            float height, string pickerLabel, string usedBy,
            string forkName, List<ScaleOptionSnapshot> options,
            string totalsCaption, string minsCaption, string resetLabel,
            string addLabel, string saveAsLabel, string renameTitle,
            string newTitle, string saveAsTitle)
        {
            RoleId = roleId;
            RoleLabel = roleLabel;
            this.stored = stored;
            PathTarget = pathTarget;
            TrainingHelp = trainingHelp;
            Height = height;
            PickerLabel = pickerLabel;
            UsedBy = usedBy;
            ForkName = forkName;
            this.options = options;
            TotalsCaption = totalsCaption;
            MinsCaption = minsCaption;
            ResetLabel = resetLabel;
            AddLabel = addLabel;
            SaveAsLabel = saveAsLabel;
            RenameTitle = renameTitle;
            NewTitle = newTitle;
            SaveAsTitle = saveAsTitle;
        }

        internal int RoleId { get; }
        internal string RoleLabel { get; }
        internal string StoredName => stored.Name;
        internal bool StoredPreset => stored.Preset;
        internal int StoredMinAt(int band) => stored.Min[band];
        internal int StoredTrainAt(int band) => stored.Train[band];
        internal HolderScale CopyStored() => stored.Copy();
        internal bool StoredSameValuesAs(HolderScale other) =>
            stored.SameValuesAs(other);
        internal bool PathTarget { get; }
        internal string TrainingHelp { get; }
        internal float Height { get; }
        internal string PickerLabel { get; }
        internal string UsedBy { get; }
        internal string ForkName { get; }
        internal int OptionCount => options.Count;
        internal ScaleOptionSnapshot OptionAt(int index) => options[index];
        internal string TotalsCaption { get; }
        internal string MinsCaption { get; }
        internal string ResetLabel { get; }
        internal string AddLabel { get; }
        internal string SaveAsLabel { get; }
        internal string RenameTitle { get; }
        internal string NewTitle { get; }
        internal string SaveAsTitle { get; }
    }

    internal readonly struct ScaleOptionSnapshot
    {
        internal ScaleOptionSnapshot(string name, bool preset,
            string deleteConfirmation)
        {
            Name = name;
            Preset = preset;
            DeleteConfirmation = deleteConfirmation;
        }

        internal string Name { get; }
        internal bool Preset { get; }
        internal string DeleteConfirmation { get; }
    }

    /// The holder-scale editor: two captioned numeric rows over a band-label
    /// row. The totals row edits recommended assignments (trainees included);
    /// the minimums row edits the direct-assignment floor, with the trainee
    /// share the implicit difference. Click +1 (wraps at the cap), right-click
    /// -1. Left-drag copies the pressed band's value across bands; a
    /// right-click mid-drag nudges that value (+1 after moving right, -1
    /// after moving left). Right-drag ramps one step per band, rising
    /// rightward, falling leftward. Max is not
    /// editable here (uncapped in practice). Presets fork on first edit; every
    /// gesture commits once, on release. Reset restores the values captured
    /// when the role or scale was selected.
    internal static class ScaleEditorUI
    {
        private const float PickerRowH = 26f;
        private const float BandRowH = 20f;
        private const float CaptionH = 16f;
        private const float BandLabelRowH = 16f;

        internal static ScaleEditorSnapshot BuildSnapshot(RoleStore store,
            Role role, float width)
        {
            HolderScale source = store?.ScaleFor(role) ?? store?.ScaleByName("Never");
            if (source == null) return null;
            HolderScale stored = source.Copy();
            bool pathTarget = IsPathTarget(store, role.id);
            Role controlling = pathTarget ? null : ControllingTarget(store, role.id);
            string trainingHelp = controlling == null
                ? null : TrainingHelp(controlling, role);
            float height = PickerRowH + 4f + CaptionH + BandRowH + 2f
                + BandLabelRowH;
            if (pathTarget) height += CaptionH + BandRowH + 2f;
            else if (trainingHelp != null) height += HelpHeight(trainingHelp, width);

            var options = new List<ScaleOptionSnapshot>();
            foreach (HolderScale candidate in store.holderScales
                .OrderBy(item => item.Name, System.StringComparer.OrdinalIgnoreCase))
            {
                string usedBy = UsedBySummary(store, candidate.Name);
                options.Add(new ScaleOptionSnapshot(candidate.Name,
                    candidate.Preset, usedBy == null ? null
                        : "WR_ScaleDeleteConfirm".Translate(
                            candidate.Name, usedBy).ToString()));
            }
            string forkName = source.Preset
                ? CatalogNameRules.Unique(source.Name, store.holderScales,
                    candidate => candidate.Name)
                : source.Name;
            const float PickW = 150f;
            return new ScaleEditorSnapshot(role.id, role.label, stored,
                pathTarget, trainingHelp, height,
                source.Name.Truncate(PickW - 20f),
                UsedBySummary(store, source.Name), forkName, options,
                "WR_ScaleTotalsCaption".Translate().ToString(),
                "WR_ScaleMinsCaption".Translate(role.label).ToString(),
                "WR_ScaleReset".Translate().ToString(),
                "WR_AddNew".Translate().ToString(),
                "WR_ScaleSaveAs".Translate().ToString(),
                "WR_ScaleRenameTitle".Translate().ToString(),
                "WR_ScaleNewTitle".Translate().ToString(),
                "WR_ScaleSaveAsTitle".Translate().ToString());
        }

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
        private static int dragLastBand = -1;
        private static int dragBumpDir = 1;
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

        internal static void Draw(Rect rect, ScaleEditorSnapshot model)
        {
            if (model == null) return;
            UpdateBaseline(model);
            HolderScale shown = dragRoleId == model.RoleId && dragScale != null
                ? dragScale : null;

            DrawPickerRow(new Rect(rect.x, rect.y, rect.width, PickerRowH),
                model);
            float y = rect.y + PickerRowH + 4f;

            if (model.PathTarget)
            {
                DrawCaption(new Rect(rect.x, y, rect.width, CaptionH),
                    model.TotalsCaption);
                y += CaptionH;
                DrawValueRow(new Rect(rect.x, y, rect.width, BandRowH),
                    model, shown, totalsRow: true);
                y += BandRowH + 2f;
            }
            else if (model.TrainingHelp != null)
            {
                string help = model.TrainingHelp;
                float helpH = HelpHeight(help, rect.width);
                Text.Font = GameFont.Tiny;
                GUI.color = WrStyle.CaptionText;
                Widgets.Label(new Rect(rect.x + 4f, y, rect.width - 8f, helpH - 4f), help);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += helpH;
            }

            DrawCaption(new Rect(rect.x, y, rect.width, CaptionH),
                model.MinsCaption);
            y += CaptionH;
            DrawValueRow(new Rect(rect.x, y, rect.width, BandRowH),
                model, shown, totalsRow: false);
            y += BandRowH + 2f;

            DrawBandLabels(new Rect(rect.x, y, rect.width, BandLabelRowH));
        }

        /// Recaptures the pre-edit values whenever the user switches role or
        /// picks another scale; our own fork commits only carry the name over.
        private static void UpdateBaseline(ScaleEditorSnapshot model)
        {
            if (baselineRoleId == model.RoleId
                && string.Equals(model.StoredName, baselineName,
                    System.StringComparison.OrdinalIgnoreCase)) return;
            if (baselineRoleId == model.RoleId && expectedName != null
                && string.Equals(model.StoredName, expectedName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                baselineName = model.StoredName;
                return;
            }
            baselineRoleId = model.RoleId;
            baselineName = model.StoredName;
            baseline = model.CopyStored();
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

        private static void DrawPickerRow(Rect rect, ScaleEditorSnapshot model)
        {
            const float PickW = 150f;
            const float AddW = 90f;
            const float ResetW = 70f;
            var pickRect = new Rect(rect.x, rect.y, PickW, rect.height - 2f);
            if (Widgets.ButtonText(pickRect, model.PickerLabel))
            {
                var options = new List<FloatMenuOption>();
                for (int i = 0; i < model.OptionCount; i++)
                {
                    ScaleOptionSnapshot candidate = model.OptionAt(i);
                    string captured = candidate.Name;
                    var option = new FloatMenuOption(candidate.Name,
                        () => RoleCommands.SetRoleScale(model.RoleId, captured));
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
                                RequestDelete(candidate);
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
            if (!model.StoredPreset)
            {
                var renameRect = new Rect(x, rect.y + (rect.height - 18f) / 2f, 18f, 18f);
                WrTips.Key("WR_ScaleRenameTip").Region(renameRect);
                if (Widgets.ButtonImage(renameRect, TexButton.Rename))
                {
                    string oldName = model.StoredName;
                    Find.WindowStack.Add(new Dialog_RenameRole(
                        model.RenameTitle,
                        name => RoleCommands.RenameScale(oldName, name),
                        oldName));
                }
                x = renameRect.xMax + 6f;
            }

            bool dirty = baseline != null && !MatchesBaseline(model);
            var addRect = new Rect(rect.xMax - AddW, rect.y, AddW, rect.height - 2f);
            var resetRect = new Rect(addRect.x - 4f - ResetW, rect.y,
                ResetW, rect.height - 2f);

            string usedBy = model.UsedBy;
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
                WrTips.Key("WR_ScaleResetTip").Region(resetRect);
                if (Widgets.ButtonText(resetRect, model.ResetLabel))
                    CommitValues(model, model.CopyStored(), baseline);
            }

            if (Widgets.ButtonText(addRect,
                    dirty ? model.SaveAsLabel : model.AddLabel))
            {
                if (dirty) OpenSaveAsDialog(model);
                else OpenAddNewDialog(model);
            }
        }

        /// Clean state: a plain copy of the current scale under a new name,
        /// with the role pointed at it.
        private static void OpenAddNewDialog(ScaleEditorSnapshot model)
        {
            string sourceName = model.StoredName;
            int roleId = model.RoleId;
            Find.WindowStack.Add(new Dialog_RenameRole(
                model.NewTitle, name =>
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
        private static void OpenSaveAsDialog(ScaleEditorSnapshot model)
        {
            string sourceName = model.StoredName;
            bool forked = baselineForked;
            var edited = model.CopyStored();
            var original = baseline.Copy();
            int roleId = model.RoleId;
            Find.WindowStack.Add(new Dialog_RenameRole(
                model.SaveAsTitle, name =>
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

        private static bool MatchesBaseline(ScaleEditorSnapshot model)
        {
            for (int i = 0; i < HolderScale.Bands; i++)
                if (model.StoredMinAt(i) != baseline.Min[i]
                    || model.StoredTrainAt(i) != baseline.Train[i])
                    return false;
            return true;
        }

        /// Dropdown delete: unused scales go immediately; referenced ones
        /// confirm first, naming the roles that will fall back to Never.
        private static void RequestDelete(ScaleOptionSnapshot option)
        {
            if (option.DeleteConfirmation == null)
            {
                RoleCommands.DeleteScale(option.Name);
                return;
            }
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                option.DeleteConfirmation,
                () => RoleCommands.DeleteScale(option.Name), destructive: true));
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

        private static void DrawValueRow(Rect rect, ScaleEditorSnapshot model,
            HolderScale shown, bool totalsRow)
        {
            int colW = ColW(rect.width);
            float startX = StartX(rect);
            var e = Event.current;
            int series = totalsRow ? SeriesTotalRow : SeriesDirectRow;
            int wrapAt = totalsRow ? MaxTotalPick : MaxDirectPick;
            bool picking = dragRoleId == model.RoleId && dragSeries == series
                && dragScale != null;

            // A press of the other button mid-gesture nudges the carried
            // value: +1 after the last band crossing moved right, -1 after
            // moving left. The cap follows the active series, not this row.
            if (dragRoleId == model.RoleId && dragScale != null
                && e.type == EventType.MouseDown && e.button != dragButton)
            {
                int cap = dragSeries == SeriesTotalRow
                    ? MaxTotalPick : MaxDirectPick;
                dragPickValue = Mathf.Clamp(dragPickValue + dragBumpDir, 0, cap);
                dragMoved = true;
                e.Use();
            }

            for (int band = 0; band < HolderScale.Bands; band++)
            {
                var cell = new Rect(startX + band * colW + 4f, rect.y,
                    colW - 8f, rect.height);
                Widgets.DrawBoxSolid(cell, CellPanel);
                Widgets.DrawHighlightIfMouseover(cell);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(cell, totalsRow
                    ? (shown != null ? shown.Min[band]
                        : model.StoredMinAt(band)).ToStringCached()
                    : (shown != null ? DirectOf(shown, band)
                        : Mathf.Max(0, model.StoredMinAt(band)
                            - model.StoredTrainAt(band))).ToStringCached());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                if (Mouse.IsOver(cell))
                    (totalsRow ? WrTips.Key("WR_ScaleTotalTip")
                        : WrTips.Key("WR_ScaleDirectTip")).Region(cell);

                if (e.type == EventType.MouseDown && cell.Contains(e.mousePosition)
                    && dragScale == null)
                {
                    // Nothing changes on press: a plain click increments on
                    // release, a drag-across copies the ORIGIN's value as-is.
                    BeginGesture(model, series);
                    dragOriginBand = band;
                    dragLastBand = band;
                    dragBumpDir = 1;
                    dragButton = e.button;
                    dragMoved = false;
                    dragPickValue = totalsRow
                        ? dragScale.Min[band] : DirectOf(dragScale, band);
                    e.Use();
                }
                // Position polling, not MouseDrag events: drag passes are
                // filtered out before tab content draws (WrEvent), and both
                // paints are idempotent functions of origin and cursor. The
                // origin cell is painted too so nudges land while hovering it.
                else if (picking && cell.Contains(e.mousePosition))
                {
                    if (band != dragLastBand)
                    {
                        dragBumpDir = band > dragLastBand ? 1 : -1;
                        dragLastBand = band;
                    }
                    if (band != dragOriginBand) dragMoved = true;
                    if (dragButton == 1) ApplyRamp(band, totalsRow);
                    else ApplyRowValue(band, totalsRow);
                }
            }

            if (picking && (e.type == EventType.MouseUp && e.button == dragButton
                || !Input.GetMouseButton(dragButton)))
            {
                if (!dragMoved)
                {
                    dragPickValue = dragButton == 1
                        ? Mathf.Max(0, dragPickValue - 1)
                        : dragPickValue >= wrapAt ? 0 : dragPickValue + 1;
                    ApplyRowValue(dragOriginBand, totalsRow);
                }
                Commit(model);
                EndGesture();
                if (e.type == EventType.MouseUp) e.Use();
            }
            // The other button must not restart or end the gesture mid-drag.
            else if (picking && (e.type == EventType.MouseDown
                || e.type == EventType.MouseUp))
            {
                e.Use();
            }
        }

        private static void ApplyRowValue(int band, bool totalsRow)
        {
            if (totalsRow) SetTotal(dragScale, band, dragPickValue);
            else SetDirect(dragScale, band, dragPickValue);
            dragScale.Normalize();
        }

        /// Right-drag: bands from the origin to the cursor get the origin
        /// value shifted one step per band, rising rightward and falling
        /// leftward; the row clamps saturate the ends. Painting the whole
        /// span heals bands a fast drag skipped over.
        private static void ApplyRamp(int hovered, bool totalsRow)
        {
            int step = hovered >= dragOriginBand ? 1 : -1;
            for (int band = dragOriginBand; ; band += step)
            {
                int value = dragPickValue + (band - dragOriginBand);
                if (totalsRow) SetTotal(dragScale, band, value);
                else SetDirect(dragScale, band, value);
                if (band == hovered) break;
            }
            dragScale.Normalize();
        }

        private static void BeginGesture(ScaleEditorSnapshot model, int series)
        {
            dragRoleId = model.RoleId;
            dragSeries = series;
            dragScale = model.CopyStored();
            dragSourceName = model.StoredName;
            dragDirect = new int[HolderScale.Bands];
            for (int band = 0; band < HolderScale.Bands; band++)
                dragDirect[band] = DirectOf(dragScale, band);
        }

        private static void EndGesture()
        {
            dragRoleId = -1;
            dragSeries = -1;
            dragOriginBand = -1;
            dragLastBand = -1;
            dragBumpDir = 1;
            dragMoved = false;
            dragScale = null;
            dragDirect = null;
            dragSourceName = null;
        }

        /// Presets fork into "<name> N" (unique); user scales commit in place.
        private static void Commit(ScaleEditorSnapshot model)
        {
            if (dragScale == null) return;
            if (model.StoredSameValuesAs(dragScale))
                return;
            HolderScale source = model.CopyStored();
            CommitValues(model, source, dragScale);
        }

        private static void CommitValues(ScaleEditorSnapshot model,
            HolderScale source,
            HolderScale values)
        {
            if (values == null) return;
            string sourceName = source?.Name;
            string targetName = source != null && source.Preset
                ? model.ForkName
                : sourceName;
            if (targetName.NullOrEmpty()) return;
            expectedName = targetName;
            if (source != null && source.Preset) baselineForked = true;
            // Max is deliberately not sent: the editor never touches it, so
            // the target keeps its stored row (uncapped for new scales).
            RoleCommands.CommitScaleEdit(new ScaleEdit
            {
                roleId = model.RoleId,
                sourceName = sourceName,
                targetName = targetName,
                min = HolderScaleCodec.EncodeRow(values.Min),
                train = HolderScaleCodec.EncodeRow(values.Train),
            });
        }
    }
}
