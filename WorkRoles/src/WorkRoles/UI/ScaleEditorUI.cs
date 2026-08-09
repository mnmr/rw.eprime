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
        private readonly RoleAssignmentStrategy stored;

        internal ScaleEditorSnapshot(int roleId, string roleLabel,
            RoleAssignmentStrategy stored, bool pathTarget, string trainingHelp,
            float height, string pickerLabel, string usedBy,
            string forkName, List<ScaleOptionSnapshot> options,
            string requiredTotalCaption, string directMinimumCaption,
            string resetLabel,
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
            RequiredTotalCaption = requiredTotalCaption;
            DirectMinimumCaption = directMinimumCaption;
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
        /// Never carries no numerics: the band editor is suppressed for it.
        internal bool HasBands => stored.Scale != null;
        internal int StoredRequiredTotalAt(int band) =>
            stored.Scale?.RequiredTotals[band] ?? 0;
        internal int StoredTrainingWaiversAt(int band) =>
            stored.Scale?.TrainingWaivers[band] ?? 0;
        internal HolderScale CopyStored() =>
            stored.Scale?.Copy() ?? new HolderScale();
        internal bool StoredSameValuesAs(HolderScale other) =>
            (stored.Scale ?? new HolderScale()).SameValuesAs(other);
        internal bool PathTarget { get; }
        internal string TrainingHelp { get; }
        internal float Height { get; }
        internal string PickerLabel { get; }
        internal string UsedBy { get; }
        internal string ForkName { get; }
        internal int OptionCount => options.Count;
        internal ScaleOptionSnapshot OptionAt(int index) => options[index];
        internal string RequiredTotalCaption { get; }
        internal string DirectMinimumCaption { get; }
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
    /// row. The required-total row includes training waivers; the direct-minimum
    /// row edits the direct-assignment floor. Click +1 (wraps at the cap), right-click
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
            RoleAssignmentStrategy source =
                store?.ScaleFor(role) ?? store?.ScaleByName("Never");
            if (source == null) return null;
            RoleAssignmentStrategy stored = source.Copy();
            bool hasBands = source.Scale != null;
            bool pathTarget = hasBands && IsPathTarget(store, role.id);
            Role controlling = !hasBands || pathTarget
                ? null : ControllingTarget(store, role.id);
            string trainingHelp = controlling == null
                ? null : TrainingHelp(controlling, role);
            float height = PickerRowH + 4f;
            if (hasBands)
            {
                height += CaptionH + BandRowH + 2f + BandLabelRowH;
                if (pathTarget) height += CaptionH + BandRowH + 2f;
                else if (trainingHelp != null)
                    height += HelpHeight(trainingHelp, width);
            }

            var options = new List<ScaleOptionSnapshot>();
            foreach (RoleAssignmentStrategy candidate in store.holderScales
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

        /// Editor height for this role: path targets get the required-total row,
        /// their training roles a help paragraph in its place.
        internal static float HeightFor(Role role, RoleStore store, float width)
        {
            float picker = PickerRowH + 4f;
            if (store == null) return picker + CaptionH + BandRowH + 2f
                + BandLabelRowH;
            // Never carries no numerics: only the picker row is shown.
            if ((store.ScaleFor(role) ?? store.ScaleByName("Never"))?.Scale
                == null)
                return picker;
            float h = picker + CaptionH + BandRowH + 2f + BandLabelRowH;
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
        /// the training-waiver-inclusive required-total row applies.
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
        // release. dragDirectMinimums freezes each band's direct minimum so
        // required-total edits recompute training waivers instead of dragging
        // them along.
        private static int dragRoleId = -1;
        private static int dragSeries = -1;
        private static int dragPickValue;
        private static int dragOriginBand = -1;
        private static int dragLastBand = -1;
        private static int dragBumpDir = 1;
        private static int dragButton;
        private static bool dragMoved;
        private static int[] dragDirectMinimums;
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
            dragDirectMinimums = null;
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
            // Never carries no numerics: nothing below the picker to edit.
            if (!model.HasBands) return;

            if (model.PathTarget)
            {
                DrawCaption(new Rect(rect.x, y, rect.width, CaptionH),
                    model.RequiredTotalCaption);
                y += CaptionH;
                DrawValueRow(new Rect(rect.x, y, rect.width, BandRowH),
                    model, shown, requiredTotalRow: true);
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
                model.DirectMinimumCaption);
            y += CaptionH;
            DrawValueRow(new Rect(rect.x, y, rect.width, BandRowH),
                model, shown, requiredTotalRow: false);
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

        private static int DirectMinimumOf(HolderScale scale, int band) =>
            new HolderRequirement(
                scale.RequiredTotals[band], scale.TrainingWaivers[band])
                .DirectMinimum;

        /// Raising the direct minimum past the required total grows that total;
        /// otherwise training waivers absorb the difference.
        private static void SetDirectMinimum(
            HolderScale scale, int band, int directMinimum)
        {
            directMinimum = Mathf.Clamp(directMinimum, 0, MaxDirectPick);
            if (directMinimum > scale.RequiredTotals[band])
                scale.RequiredTotals[band] = directMinimum;
            scale.TrainingWaivers[band] =
                scale.RequiredTotals[band] - directMinimum;
        }

        /// The required-total row sets the total; each band's direct minimum
        /// stays frozen so training waivers absorb the change.
        private static void SetRequiredTotal(
            HolderScale scale, int band, int requiredTotal)
        {
            scale.RequiredTotals[band] = Mathf.Clamp(
                requiredTotal, 0, MaxTotalPick);
            scale.TrainingWaivers[band] = Mathf.Max(
                0, scale.RequiredTotals[band] - dragDirectMinimums[band]);
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
                    CommitValues(model, baseline);
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
                            requiredTotals = HolderScaleCodec.EncodeRow(
                                edited.RequiredTotals),
                            trainingWaivers = HolderScaleCodec.EncodeRow(
                                edited.TrainingWaivers),
                        });
                        RoleCommands.CommitScaleEdit(new ScaleEdit
                        {
                            sourceName = sourceName,
                            targetName = sourceName,
                            requiredTotals = HolderScaleCodec.EncodeRow(
                                original.RequiredTotals),
                            trainingWaivers = HolderScaleCodec.EncodeRow(
                                original.TrainingWaivers),
                        });
                    }
                    baselineRoleId = -1; // recapture clean from the new scale
                }));
        }

        private static bool MatchesBaseline(ScaleEditorSnapshot model)
        {
            for (int i = 0; i < HolderScale.Bands; i++)
                if (model.StoredRequiredTotalAt(i) != baseline.RequiredTotals[i]
                    || model.StoredTrainingWaiversAt(i)
                        != baseline.TrainingWaivers[i])
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

        // ----- Band value rows (required totals and direct minimums) -----

        private static void DrawValueRow(Rect rect, ScaleEditorSnapshot model,
            HolderScale shown, bool requiredTotalRow)
        {
            int colW = ColW(rect.width);
            float startX = StartX(rect);
            var e = Event.current;
            int series = requiredTotalRow ? SeriesTotalRow : SeriesDirectRow;
            int wrapAt = requiredTotalRow ? MaxTotalPick : MaxDirectPick;
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
                Widgets.Label(cell, requiredTotalRow
                    ? (shown != null ? shown.RequiredTotals[band]
                        : model.StoredRequiredTotalAt(band)).ToStringCached()
                    : (shown != null ? DirectMinimumOf(shown, band)
                        : new HolderRequirement(
                            model.StoredRequiredTotalAt(band),
                            model.StoredTrainingWaiversAt(band))
                            .DirectMinimum).ToStringCached());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                if (Mouse.IsOver(cell))
                    (requiredTotalRow ? WrTips.Key("WR_ScaleTotalTip")
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
                    dragPickValue = requiredTotalRow
                        ? dragScale.RequiredTotals[band]
                        : DirectMinimumOf(dragScale, band);
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
                    if (dragButton == 1) ApplyRamp(band, requiredTotalRow);
                    else ApplyRowValue(band, requiredTotalRow);
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
                    ApplyRowValue(dragOriginBand, requiredTotalRow);
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

        private static void ApplyRowValue(int band, bool requiredTotalRow)
        {
            if (requiredTotalRow)
                SetRequiredTotal(dragScale, band, dragPickValue);
            else
                SetDirectMinimum(dragScale, band, dragPickValue);
            dragScale.Normalize();
        }

        /// Right-drag: bands from the origin to the cursor get the origin
        /// value shifted one step per band, rising rightward and falling
        /// leftward; the row clamps saturate the ends. Painting the whole
        /// span heals bands a fast drag skipped over.
        private static void ApplyRamp(int hovered, bool requiredTotalRow)
        {
            int step = hovered >= dragOriginBand ? 1 : -1;
            for (int band = dragOriginBand; ; band += step)
            {
                int value = dragPickValue + (band - dragOriginBand);
                if (requiredTotalRow) SetRequiredTotal(dragScale, band, value);
                else SetDirectMinimum(dragScale, band, value);
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
            dragDirectMinimums = new int[HolderScale.Bands];
            for (int band = 0; band < HolderScale.Bands; band++)
                dragDirectMinimums[band] = DirectMinimumOf(dragScale, band);
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
            dragDirectMinimums = null;
            dragSourceName = null;
        }

        /// Presets fork into "<name> N" (unique); user scales commit in place.
        private static void Commit(ScaleEditorSnapshot model)
        {
            if (dragScale == null) return;
            if (model.StoredSameValuesAs(dragScale))
                return;
            CommitValues(model, dragScale);
        }

        private static void CommitValues(ScaleEditorSnapshot model,
            HolderScale values)
        {
            if (values == null) return;
            string sourceName = model.StoredName;
            string targetName = model.StoredPreset
                ? model.ForkName
                : sourceName;
            if (targetName.NullOrEmpty()) return;
            expectedName = targetName;
            if (model.StoredPreset) baselineForked = true;
            // Max is deliberately not sent: the editor never touches it, so
            // the target keeps its stored row (uncapped for new scales).
            RoleCommands.CommitScaleEdit(new ScaleEdit
            {
                roleId = model.RoleId,
                sourceName = sourceName,
                targetName = targetName,
                requiredTotals = HolderScaleCodec.EncodeRow(values.RequiredTotals),
                trainingWaivers = HolderScaleCodec.EncodeRow(values.TrainingWaivers),
            });
        }
    }
}
