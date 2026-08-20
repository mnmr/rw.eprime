using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// <summary>Window-owned renderer for a detached readout snapshot.</summary>
    internal sealed class ReadoutsPreviewView
    {
        private static float RowH =>
            EprStyle.TinyTextMetrics.MinHeight(22f);
        private const float HeaderH = 26f;
        private const float BottomPad = 8f;

        // Cache contract:
        // Owner: one import/export dialog window.
        // Key: detached snapshot identity.
        // Value: immutable parallel arrays of translated preview rows.
        // Dependencies: snapshot identity and UiVersion.Current (language plus
        // resolved Tiny-font line metrics).
        // Refresh policy: immediate when either dependency changes.
        // Equality policy: unchanged dependencies preserve the arrays by identity.
        // Teardown: Reset is called by the owning dialog during PreClose.
        private ReadoutSnapshot? lastSnapshot;
        private int lastLanguageVersion = -1;
        private int lastUiVersion = -1;
        private string[]? lines;
        private bool[]? isHeader;
        private float contentHeight;

        internal void DrawListing(Rect outRect, ReadoutSnapshot snapshot, ref Vector2 scroll)
        {
            EnsureLines(snapshot);

            var viewRect = new Rect(0f, 0f,
                outRect.width - GenUI.ScrollBarWidth, contentHeight);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            try
            {
                float y = 0f;
                for (int i = 0; i < lines!.Length; i++) // built by EnsureLines above
                {
                    float height = isHeader![i] ? HeaderH : RowH;
                    if (y + height > scroll.y - RowH
                        && y < scroll.y + outRect.height + RowH)
                    {
                        if (isHeader[i])
                            DrawMiniHeader(lines[i], 0f, y, viewRect.width);
                        else
                            DrawRow(lines[i], 0f, y, viewRect.width);
                    }
                    y += height;
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private void EnsureLines(ReadoutSnapshot snapshot)
        {
            UiVersion.ObserveCurrentMetrics();
            int languageVersion = UiVersion.LanguageCurrent;
            if (ReferenceEquals(lastSnapshot, snapshot)
                && lastLanguageVersion == languageVersion
                && lastUiVersion == UiVersion.Current
                && lines != null)
                return;

            lastSnapshot = snapshot;
            lastLanguageVersion = languageVersion;
            lastUiVersion = UiVersion.Current;

            var builtLines = new List<string>();
            var builtHeaders = new List<bool>();
            int poolCount = snapshot?.Pools.Count ?? 0;
            int groupCount = snapshot?.Groups.Count ?? 0;

            builtLines.Add(UiText.Get("EPR.PreviewPoolsHeader"));
            builtHeaders.Add(true);
            if (poolCount == 0)
            {
                builtLines.Add(UiText.Get("EPR.PreviewNone"));
                builtHeaders.Add(false);
            }
            else
            {
                for (int i = 0; i < snapshot!.Pools.Count; i++) // poolCount > 0 implies a snapshot
                {
                    ReadoutSnapshot.Pool pool = snapshot.Pools[i];
                    builtLines.Add("EPR.PreviewPoolRow".Translate(
                        pool.Name, pool.Members.Count));
                    builtHeaders.Add(false);
                }
            }

            builtLines.Add(UiText.Get("EPR.PreviewGroupsHeader"));
            builtHeaders.Add(true);
            if (groupCount == 0)
            {
                builtLines.Add(UiText.Get("EPR.PreviewNone"));
                builtHeaders.Add(false);
            }
            else
            {
                for (int i = 0; i < snapshot!.Groups.Count; i++) // groupCount > 0 implies a snapshot
                {
                    ReadoutSnapshot.Group group = snapshot.Groups[i];
                    int slots = 0;
                    for (int tier = 0; tier < group.Tiers.Count; tier++)
                        slots += group.Tiers[tier].Count;
                    builtLines.Add("EPR.PreviewGroupRow".Translate(
                        group.Name, group.Tiers.Count, slots));
                    builtHeaders.Add(false);
                }
            }

            lines = builtLines.ToArray();
            isHeader = builtHeaders.ToArray();
            float total = 0f;
            for (int i = 0; i < isHeader.Length; i++)
                total += isHeader[i] ? HeaderH : RowH;
            contentHeight = total + BottomPad;
        }

        private static void DrawMiniHeader(string text, float x, float y, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            Text.Font = GameFont.Small;
            GUI.color = EprStyle.HeaderText;
            Widgets.Label(new Rect(x, y, width, HeaderH - 4f), text);
            GUI.color = EprStyle.HeaderRule;
            WrText.LineHorizontal(x, y + HeaderH - 4f, width);
            GUI.color = Color.white;
        }

        private static void DrawRow(string text, float x, float y, float width)
        {
            if (Event.current.type != EventType.Repaint) return;
            Text.Font = GameFont.Tiny;
            GUI.color = EprStyle.CaptionText;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x + 8f, y, width - 8f, RowH), text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        internal void Reset()
        {
            lastSnapshot = null;
            lastLanguageVersion = -1;
            lastUiVersion = -1;
            lines = null;
            isHeader = null;
            contentHeight = 0f;
        }
    }
}
