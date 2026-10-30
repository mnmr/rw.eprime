using System;
using System.IO;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.UI;

namespace WorkRolesMechanicalReview
{
    public static partial class Probe
    {
        private static bool rowPending, inStats, tagPending;
        private static Pawn rowPawn;
        private static GameFont rowEntryFont;
        private static bool rowEntryWrap, statsEntryWrap;
        private static int boxNumber;
        private static Rect referenceTag;
        private static Vector2 LastTagCenter;
        private static readonly System.Collections.Generic.Dictionary<string, string> recordedGeometry = new System.Collections.Generic.Dictionary<string, string>();
        private struct LabelState
        {
            internal bool Restore;
            internal GameFont Font;
            internal bool Wrap;
        }

        private static void InstallVisualReference(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(ColonistsTabView), "DrawColonistCell"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(BeginRow)), postfix: new HarmonyMethod(typeof(Probe), nameof(EndRow)));
            harmony.Patch(AccessTools.Method(typeof(ColonistsTabView), "DrawStatsPanel"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(BeginStats)), postfix: new HarmonyMethod(typeof(Probe), nameof(EndStats)));
            harmony.Patch(AccessTools.Method(typeof(Widgets), "Label", new[] { typeof(Rect), typeof(string) }),
                prefix: new HarmonyMethod(typeof(Probe), nameof(BeforeLabel)), postfix: new HarmonyMethod(typeof(Probe), nameof(AfterLabel)));
            harmony.Patch(AccessTools.Method(typeof(Widgets), "DrawBoxSolidWithOutline"),
                prefix: new HarmonyMethod(typeof(Probe), nameof(BeforeBox)));
        }
        private static void BeginRow(object row)
        {
            rowPawn = (Pawn)Property(row, "Pawn");
            rowPending = humans != null && Array.IndexOf(humans, rowPawn) >= 0;
            rowEntryFont = Text.Font;
            rowEntryWrap = Text.WordWrap;
        }
        private static void EndRow() { rowPending = false; rowPawn = null; }
        private static void BeginStats() { inStats = true; boxNumber = 0; statsEntryWrap = Text.WordWrap; tagPending = false; }
        private static void EndStats() { inStats = false; tagPending = false; }
        private static Pawn Selected => view == null ? null : (Pawn)Property(Field(view, "selection"), "Single");

        private static void BeforeBox(ref Rect rect)
        {
            if (!inStats || Selected == null || humans == null || Array.IndexOf(humans, Selected) < 0) return;
            ++boxNumber;
            if (boxNumber == 2)
            {
                // Exact d47bdc4c BuildChrome + DrawStatsPanel name-tag geometry:
                // Small; clamp(FitWidth(full label)+10,40,PortraitDisplaySize);
                // tag centered on portraitFrameRect.y with PortraitNameH=20.
                GameFont prior = Text.Font;
                Text.Font = GameFont.Small;
                float fit = (float)AccessTools.Method(typeof(RoleStore).Assembly.GetType("RimShared.UiLib.WrText"), "FitWidth").Invoke(null, new object[] { Selected.LabelShortCap.ToString() });
                Text.Font = prior;
                float width = Mathf.Clamp(fit + 10f, 40f, 96f);
                referenceTag = new Rect(rect.x + (rect.width - width) / 2f, rect.y - 10f, width, 20f);
            }
            if (boxNumber != 3) return;
            tagPending = true;
            if (ReferenceDrawing) rect = referenceTag;
            LastTagCenter = UI.GUIToScreenPoint(rect.center) * Prefs.UIScale;
            RecordRectangle("tag:" + Selected.thingIDNumber, rect, "box");
        }
        private static void BeforeLabel(ref Rect rect, ref string label, out LabelState __state)
        {
            __state = default;
            string key = null;
            if (rowPending)
            {
                rowPending = false;
                key = "row:" + rowPawn.thingIDNumber;
                if (ReferenceDrawing)
                {
                    __state = new LabelState { Restore = true, Font = Text.Font, Wrap = Text.WordWrap };
                    // Exact d47bdc4c DrawColonistCell: full row.Label and inherited
                    // Text.Font/WordWrap; MiddleLeft and slave tint are unchanged.
                    label = rowPawn.LabelShortCap;
                    Text.Font = rowEntryFont;
                    Text.WordWrap = rowEntryWrap;
                }
            }
            else if (inStats && tagPending)
            {
                tagPending = false;
                key = "selected:" + Selected.thingIDNumber;
                if (ReferenceDrawing)
                {
                    __state = new LabelState { Restore = true, Font = Text.Font, Wrap = Text.WordWrap };
                    // Exact d47bdc4c selected tag: full label in entire 20px tag,
                    // no inset, inherited wrapping, Small + MiddleCenter.
                    rect = referenceTag;
                    label = Selected.LabelShortCap;
                    Text.Font = GameFont.Small;
                    Text.WordWrap = statsEntryWrap;
                }
            }
            // Deliberate probe-only mismatch proves the screenshot/crop pipeline
            // detects a real shift. This scene must differ; all real parity
            // scenes must match. It does not characterize production behavior.
            if (key != null && Scene == "reference-control" && !ReferenceDrawing) rect.x += 4f;
            if (key != null) RecordRectangle(key, rect, Text.Font + ":" + Text.WordWrap + ":" + label);
        }
        private static void AfterLabel(LabelState __state)
        {
            if (!__state.Restore) return;
            Text.Font = __state.Font;
            Text.WordWrap = __state.Wrap;
        }
        private static void RecordRectangle(string key, Rect rect, string text)
        {
            if (Event.current.type != EventType.Repaint || !started || done) return;
            string prefix = Scene + "\t" + (ReferenceDrawing ? "reference" : "current") + "\t" + key;
            // Verse returns screen UI coordinates, including nested groups.
            // Convert the entire point to physical capture pixels: scaling only
            // the local vector leaves group origins wrong at fractional scales.
            Vector2 a = UI.GUIToScreenPoint(rect.min) * Prefs.UIScale;
            Vector2 b = UI.GUIToScreenPoint(rect.max) * Prefs.UIScale;
            string line = prefix + "\t" + a.x + "\t" + a.y + "\t" + (b.x - a.x) + "\t" + (b.y - a.y)
                + "\t" + text.Replace('\t', ' ').Replace('\n', ' ') + Environment.NewLine;
            recordedGeometry[prefix] = line;
        }
        private static void FlushGeometry()
        {
            // Capture acknowledgements arrive after stable repaint; use the
            // latest coordinates, not an opening frame before window resize.
            File.AppendAllText(Path.Combine(Root, "review-geometry.tsv"), string.Concat(recordedGeometry.Values));
            recordedGeometry.Clear();
        }
    }
}
