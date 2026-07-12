using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    [DefOf]
    public static class WR_KeyBindingDefOf
    {
        public static KeyBindingDef WR_PrevColonist;
        public static KeyBindingDef WR_NextColonist;
        public static KeyBindingDef WR_FirstColonist;
        public static KeyBindingDef WR_LastColonist;
        public static KeyBindingDef WR_PrevPage;
        public static KeyBindingDef WR_NextPage;

        static WR_KeyBindingDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(WR_KeyBindingDefOf));
    }

    /// While the WorkRoles window is open, its navigation keys take precedence:
    /// every other binding sharing a physical key is stashed and set to None,
    /// then restored on close. Rebinding is required because polled consumers
    /// (camera dolly reads IsDown off Input) never see consumed GUI events.
    /// In-memory only — nothing is written to the key prefs file.
    internal static class KeyOverride
    {
        private struct Stash
        {
            public KeyBindingData data;
            public bool slotA;
            public KeyCode code;
        }

        private static readonly List<Stash> stashed = new List<Stash>();
        private static bool active;

        private static IEnumerable<KeyBindingDef> OurDefs()
        {
            yield return WR_KeyBindingDefOf.WR_PrevColonist;
            yield return WR_KeyBindingDefOf.WR_NextColonist;
            yield return WR_KeyBindingDefOf.WR_FirstColonist;
            yield return WR_KeyBindingDefOf.WR_LastColonist;
            yield return WR_KeyBindingDefOf.WR_PrevPage;
            yield return WR_KeyBindingDefOf.WR_NextPage;
        }

        internal static void Apply()
        {
            if (active) return;
            active = true;

            var ours = new HashSet<KeyBindingDef>();
            var ourCodes = new HashSet<KeyCode>();
            foreach (var def in OurDefs())
            {
                if (def == null) continue;
                ours.Add(def);
                var a = KeyPrefs.KeyPrefsData.GetBoundKeyCode(def, KeyPrefs.BindingSlot.A);
                var b = KeyPrefs.KeyPrefsData.GetBoundKeyCode(def, KeyPrefs.BindingSlot.B);
                if (a != KeyCode.None) ourCodes.Add(a);
                if (b != KeyCode.None) ourCodes.Add(b);
            }

            foreach (var kv in KeyPrefs.KeyPrefsData.keyPrefs)
            {
                if (ours.Contains(kv.Key)) continue;
                var data = kv.Value;
                if (data.keyBindingA != KeyCode.None && ourCodes.Contains(data.keyBindingA))
                {
                    stashed.Add(new Stash { data = data, slotA = true, code = data.keyBindingA });
                    data.keyBindingA = KeyCode.None;
                }
                if (data.keyBindingB != KeyCode.None && ourCodes.Contains(data.keyBindingB))
                {
                    stashed.Add(new Stash { data = data, slotA = false, code = data.keyBindingB });
                    data.keyBindingB = KeyCode.None;
                }
            }
        }

        internal static void Restore()
        {
            if (!active) return;
            active = false;
            foreach (var s in stashed)
            {
                if (s.slotA) s.data.keyBindingA = s.code;
                else s.data.keyBindingB = s.code;
            }
            stashed.Clear();
        }
    }
}
