using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Native IMGUI text editing with a passive control ID. Unity's public
    /// GUI.TextField API always registers a keyboard-focusable control, which
    /// makes an always-visible field part of Tab traversal. DoTextField still
    /// assigns keyboard focus directly on mouse-down, so a passive ID keeps
    /// normal click-to-edit behavior without accepting focus from Tab.
    internal static class MouseFocusTextField
    {
        private static readonly GUIContent Content = new GUIContent();
        private static bool initialized;
        private static bool passiveControlAvailable;

        internal static void Initialize(Harmony harmony)
        {
            if (initialized) return;
            initialized = true;
            try
            {
                MethodInfo original = AccessTools.DeclaredMethod(typeof(GUI),
                    "DoTextField", new Type[]
                    {
                        typeof(Rect), typeof(int), typeof(GUIContent),
                        typeof(bool), typeof(int), typeof(GUIStyle)
                    });
                MethodInfo standin = AccessTools.DeclaredMethod(
                    typeof(MouseFocusTextField), nameof(DoTextField));
                harmony.CreateReversePatcher(original,
                    new HarmonyMethod(standin)).Patch();
                passiveControlAvailable = true;
            }
            catch (Exception exception)
            {
                Log.Warning("[EPrimeReadouts] Could not bind Unity's passive "
                    + "text-field bridge; falling back to normal Tab focus. "
                    + exception);
            }
        }

        internal static string Draw(Rect rect, string text, GUIStyle style)
        {
            if (!passiveControlAvailable)
                return GUI.TextField(rect, text ?? "", style);
            Content.text = text ?? "";
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            DoTextField(rect, id, Content, multiline: false, maxLength: -1,
                style);
            return Content.text;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DoTextField(Rect position, int id,
            GUIContent content, bool multiline, int maxLength, GUIStyle style)
        {
            throw new NotSupportedException(
                "Harmony did not bind UnityEngine.GUI.DoTextField.");
        }
    }
}
