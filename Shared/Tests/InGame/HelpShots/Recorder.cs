using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

// Help image capture, shared by every mod's capture probe (test-only source,
// compiled into each probe assembly; never into a shipped project).
// A probe walks a list of scenes and asks the driving script for a full-frame
// capture of each. The recorder (Harmony hooks on the game's and the mod's
// draw calls) notes the pixel rectangle of every named element drawn in the
// frame, and each capture gets a manifest line with those regions.
// Shared/tools/automation/help-shots/crop-help.py cuts the help images from
// the frames by region name, so the images follow the layout.
namespace RimShared.Tests.HelpShots
{
    /// Named pixel rectangles of the elements drawn in a frame.
    public static class Recorder
    {
        public struct Region { public string Id; public Rect Px; }

        private static List<Region> building = new List<Region>();
        private static List<Region> complete = new List<Region>();
        private static readonly Dictionary<string, int> seen = new Dictionary<string, int>();
        private static int frame = -1;

        /// The last fully drawn frame's regions.
        public static List<Region> Last => complete;

        /// "id" is an exact region; "id@container" is the first region of
        /// that name (any ordinal) whose centre lies inside the container
        /// region, e.g. "label:Shooting@panel".
        public static Rect? Find(string spec)
        {
            int at = spec.IndexOf('@'); // containers nest: "a@b@c"
            if (at <= 0)
            {
                for (int i = 0; i < complete.Count; i++)
                    if (complete[i].Id == spec) return complete[i].Px;
                return null;
            }
            string id = spec.Substring(0, at);
            Rect? box = Find(spec.Substring(at + 1));
            if (box == null) return null;
            for (int i = 0; i < complete.Count; i++)
                if ((complete[i].Id == id || complete[i].Id.StartsWith(id + "#"))
                    && box.Value.Contains(complete[i].Px.center))
                    return complete[i].Px;
            return null;
        }

        /// guiRect is in the current GUI group's units; screen = it is
        /// already in whole-screen UI units (window rectangles).
        public static void Add(string id, Rect guiRect, bool screen = false)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (Time.frameCount != frame)
            {
                // The previous frame is complete once the next one draws.
                List<Region> done = building;
                building = complete;
                building.Clear();
                complete = done;
                seen.Clear();
                frame = Time.frameCount;
            }
            Vector2 origin = screen ? guiRect.position
                : GUIUtility.GUIToScreenPoint(guiRect.position);
            float scale = Prefs.UIScale;
            seen.TryGetValue(id, out int count);
            seen[id] = count + 1;
            building.Add(new Region
            {
                // Repeats in one frame get an ordinal: "label:Core#2".
                Id = count == 0 ? id : id + "#" + (count + 1),
                Px = new Rect(origin.x * scale, origin.y * scale,
                    guiRect.width * scale, guiRect.height * scale),
            });
        }

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var result = new StringBuilder(text.Length);
            bool tag = false;
            foreach (char c in text)
            {
                if (c == '<') tag = true;
                else if (c == '>') tag = false;
                else if (!tag && c != '\n' && c != '\r' && c != '"' && c != '\\') result.Append(c);
                if (result.Length >= 48) break;
            }
            return result.ToString().Trim();
        }
    }
}
