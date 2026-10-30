using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.UI;

namespace ChangesPreviewRegression;

public sealed class Probe : GameComponent
{
    const string LongName = "鉱山建築研究医療調理農業運搬清掃工芸製作専門担当役割確認表示";
    int frames, stage;
    Dialog_ChangesPreview preview;
    Role role;
    object observed;
    float initialWidth;
    string FilePath(string name) => Path.Combine(GenFilePaths.SaveDataFolderPath, "changes-preview-" + name);
    public Probe(Game game) { }
    void Record(string message) { File.AppendAllText(FilePath("results.txt"), message + Environment.NewLine); Log.Message("[ChangesPreviewRegression] " + message); }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    object Snapshot => AccessTools.Field(typeof(Dialog_ChangesPreview), "renderSnapshot").GetValue(preview);
    float Width => (float)AccessTools.Field(typeof(Dialog_ChangesPreview), "renderWidth").GetValue(preview);
    public override void GameComponentUpdate()
    {
        if (stage == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        if (++frames < 35) return;
        try
        {
            if (stage == 0)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                foreach (var window in Find.WindowStack.Windows.ToList()) window.Close(false);
                RoleCommands.SetAutoOptimize(false);
                var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                Find.MainTabsRoot.SetCurrentTab(button);
                var tab = (ColonistsTabView)AccessTools.Field(typeof(MainTabWindow_WorkRoles), "colonistsTab").GetValue(button.TabWindow);
                var listed = (IReadOnlyList<Pawn>)AccessTools.Method(typeof(ColonistsTabView), "ListedPawns").Invoke(tab, null);
                Require(LongName.Length == 30, "Fixture must use the legal 30-character name limit");
                RoleCommands.CreateRole(LongName);
                role = RoleStore.Current.roles.Single(r => r.label == LongName);
                RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, "CleanFilth"));
                RoleCommands.SetRoleRecommendationExcluded(role.id, true);
                RoleCommands.SetRoleBlocker(role.id, true);
                RoleCommands.SetRoleActiveHours(role.id, 1);
                RoleCommands.ToggleRoleLocation(role.id, LocationRules.Settlements);
                RoleCommands.AssignRole(listed[0], role.id, 0);
                tab.ShowFixPreview();
                preview = Find.WindowStack.Windows.OfType<Dialog_ChangesPreview>().Single();
                initialWidth = preview.windowRect.width;
                Record("WR30 native Fix My Colony preview: pawn=" + listed[0].LabelShort + " legal name=" + LongName + " scale=" + Prefs.UIScale);
                stage = 1; frames = 0;
            }
            else if (stage == 1)
            {
                Validate("marked", true);
                observed = Snapshot;
                RoleCommands.SetRoleBlocker(role.id, true);
                File.WriteAllText(FilePath("stage.txt"), "CAPTURE_MARKED");
                stage = 2; frames = 0;
            }
            else if (stage == 2)
            {
                Require(ReferenceEquals(observed, Snapshot), "No-op and steady reads must retain the render snapshot");
                if (!File.Exists(FilePath("captured.txt"))) return;
                preview.windowRect.width = initialWidth + 240f;
                stage = 3; frames = 0;
            }
            else if (stage == 3)
            {
                Require(!ReferenceEquals(observed, Snapshot), "Resizing must update chip geometry");
                Validate("wide", false);
                preview.windowRect.width = initialWidth;
                RoleCommands.SetRoleBlocker(role.id, false);
                RoleCommands.SetRoleActiveHours(role.id, FileRole.AllHours);
                RoleCommands.ToggleRoleLocation(role.id, LocationRules.Settlements);
                stage = 4; frames = 0;
            }
            else if (stage == 4)
            {
                Validate("unmarked", false);
                Record("PASS: bounded marked chip, intact tooltip label, unchanged normal widths, resize and paused marker transitions, steady/no-op identity");
                File.WriteAllText(FilePath("stage.txt"), "SUCCESS");
                stage = 99;
            }
        }
        catch (Exception error) { Record("FAILED " + error); File.WriteAllText(FilePath("stage.txt"), "FAILED " + error); stage = 99; }
    }

    void Validate(string label, bool expectFade)
    {
        Require(Snapshot != null, "Preview did not publish its render data");
        var entries = (Array)AccessTools.Field(Snapshot.GetType(), "entries").GetValue(Snapshot);
        bool found = false;
        for (int i = 0; i < entries.Length; i++)
        {
            var entry = entries.GetValue(i);
            var chips = (Array)AccessTools.Field(entry.GetType(), "chips").GetValue(entry);
            foreach (var chip in chips)
            {
                var type = chip.GetType();
                Rect rect = (Rect)AccessTools.Property(type, "Rect").GetValue(chip);
                Require(rect.x >= 26f && rect.xMax <= Width + 0.01f, "A chip extends beyond the visible row");
                var data = AccessTools.Property(type, "RenderData").GetValue(chip);
                string name = (string)AccessTools.Property(data.GetType(), "Label").GetValue(data);
                if (name != LongName) continue;
                found = true;
                float fade = (float)AccessTools.Property(type, "FadeLabelWidth").GetValue(chip);
                Require((fade > 0f) == expectFade, label + ": unexpected label fading");
                if (!expectFade) Require(Math.Abs(rect.width - RoleChipUI.WidthFor(role, false)) < 0.01f, "Fitting role must retain its original width");
                string tip = (string)AccessTools.Property(type, "Tip").GetValue(chip);
                Require(tip != null && tip.Contains(LongName), "Full localized role name must remain readable in the tooltip");
                Record(label + " right=" + rect.xMax + " viewport=" + Width + " fade=" + fade + " full-name-tooltip=True");
                if (expectFade)
                {
                    // This is the first pawn and first preview row. Window margins
                    // and cached banner geometry locate the real control for scoped input.
                    float info = (float)AccessTools.Field(typeof(Dialog_ChangesPreview), "infoHeight").GetValue(preview);
                    float warning = (float)AccessTools.Field(typeof(Dialog_ChangesPreview), "warningHeight").GetValue(preview);
                    bool hasInfo = AccessTools.Field(typeof(Dialog_ChangesPreview), "infoFactory").GetValue(preview) != null;
                    float top = 38f + 26f + (hasInfo ? info + 8f + (warning > 0 ? warning + 4f : 0) : 0);
                    Require(i == 0, "Hover fixture must remain the first visible pawn");
                    float x = (preview.windowRect.x + 18f + rect.center.x) * Prefs.UIScale;
                    float y = (preview.windowRect.y + 18f + top + rect.center.y) * Prefs.UIScale;
                    File.WriteAllText(FilePath("hover.txt"), ((int)x) + "," + ((int)y));
                }
            }
        }
        Require(found, "Long role missing from actual recommendation preview");
    }
}
