using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimShared.Tests.HelpShots
{
    /// Recorder hooks on the game's own widgets, the same for every mod:
    ///   label:<text>  check:<text>  btn:<text>  img:<texture>  textfield
    ///   win:<id> and tip (immediate windows; tip = the tooltip layer)
    ///   window:<type> (every window of the stack, float menus included)
    ///   scroll (scroll view viewport)  section (menu section)  box (solid box)
    ///   selected (selection highlight)  clip (GUI group)
    public static class VanillaHooks
    {
        public static void Install(Harmony harmony)
        {
            Patch(harmony, AccessTools.Method(typeof(Widgets), "Label", new[] { typeof(Rect), typeof(string) }), nameof(Label));
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(Widgets)))
            {
                ParameterInfo[] p = method.GetParameters();
                if (p.Length == 0 || p[0].ParameterType != typeof(Rect)) continue;
                if (p.Length == 1 && method.Name == "DrawMenuSection") Patch(harmony, method, nameof(Section));
                else if (p.Length == 1 && method.Name == "DrawHighlightSelected") Patch(harmony, method, nameof(Selected));
                else if (p.Length == 1 && method.Name == "BeginGroup") Patch(harmony, method, nameof(Clip));
                else if (method.Name == "BeginScrollView") Patch(harmony, method, nameof(Scroll));
                else if (method.Name == "DrawBoxSolid") Patch(harmony, method, nameof(Box));
                if (p.Length < 2) continue;
                if (method.Name == "CheckboxLabeled" && p[1].ParameterType == typeof(string))
                    Patch(harmony, method, nameof(Checkbox));
                // The seven-parameter overload is the one the others call.
                else if (method.Name == "ButtonText" && p[1].ParameterType == typeof(string) && p.Length == 7)
                    Patch(harmony, method, nameof(Button));
                else if (method.Name == "ButtonImage" && p[1].ParameterType == typeof(Texture2D) && p.Length <= 4)
                    Patch(harmony, method, nameof(Image));
                else if (method.Name == "TextField" && p[1].ParameterType == typeof(string) && p.Length == 2)
                    Patch(harmony, method, nameof(TextField));
            }
            Patch(harmony, AccessTools.Method(typeof(WindowStack), "ImmediateWindow"), nameof(Immediate));
            Patch(harmony, AccessTools.Method(typeof(Window), "WindowOnGUI"), nameof(StackWindow));
        }

        /// Adds a recorder prefix (a public static method of holder, or of
        /// this class) to a draw method; a missing target fails the run.
        public static void Patch(Harmony harmony, MethodInfo target, string prefix, Type holder = null)
        {
            if (target == null) throw new InvalidOperationException("hook target missing for " + prefix);
            harmony.Patch(target, prefix: new HarmonyMethod(holder ?? typeof(VanillaHooks), prefix));
        }

        private static readonly System.Collections.Generic.Dictionary<MethodBase, string> drawn =
            new System.Collections.Generic.Dictionary<MethodBase, string>();

        /// Records "<prefix>:<Method>" (the rectangle it was given) for every
        /// method the type declares whose name starts with Draw or Do and
        /// whose first parameter is a Rect: a mod's panels, rows and dialogs
        /// become regions without a hook each.
        public static void PatchDrawMethods(Harmony harmony, Type type, string prefix)
        {
            if (type == null) throw new InvalidOperationException("no type for " + prefix);
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
            {
                ParameterInfo[] p = method.GetParameters();
                if (p.Length == 0 || p[0].ParameterType != typeof(Rect) || method.IsAbstract
                    || method.IsGenericMethodDefinition
                    || !(method.Name.StartsWith("Draw") || method.Name.StartsWith("Do"))) continue;
                drawn[method] = prefix + ":" + method.Name;
                Patch(harmony, method, nameof(Drawn));
            }
        }

        /// Records the Rect first parameter of a method (every overload
        /// that starts with a Rect) under a fixed id.
        public static void Region(Harmony harmony, Type type, string method, string id)
        {
            int patched = 0;
            foreach (MethodInfo candidate in AccessTools.GetDeclaredMethods(type))
            {
                ParameterInfo[] p = candidate.GetParameters();
                if (candidate.Name != method || p.Length == 0 || p[0].ParameterType != typeof(Rect)
                    || candidate.IsGenericMethodDefinition) continue;
                drawn[candidate] = id;
                Patch(harmony, candidate, nameof(Drawn));
                patched++;
            }
            if (patched == 0) throw new InvalidOperationException("hook target missing for " + id);
        }

        public static void Drawn(Rect __0, MethodBase __originalMethod) =>
            Recorder.Add(drawn[__originalMethod], __0);

        public static void Label(Rect rect, string label) => Recorder.Add("label:" + Recorder.Clean(label), rect);
        public static void Checkbox(Rect rect, string label) => Recorder.Add("check:" + Recorder.Clean(label), rect);
        public static void Button(Rect rect, string label) => Recorder.Add("btn:" + Recorder.Clean(label), rect);
        public static void Image(Rect butRect, Texture2D tex) => Recorder.Add("img:" + (tex == null ? "" : tex.name), butRect);
        public static void TextField(Rect rect) => Recorder.Add("textfield", rect);
        public static void Section(Rect rect) => Recorder.Add("section", rect);
        public static void Selected(Rect rect) => Recorder.Add("selected", rect);
        public static void Clip(Rect rect) => Recorder.Add("clip", rect);
        public static void Scroll(Rect outRect) => Recorder.Add("scroll", outRect);
        public static void Box(Rect rect) => Recorder.Add("box", rect);

        public static void Immediate(int ID, Rect rect, WindowLayer layer)
        {
            Recorder.Add("win:" + ID, rect, screen: true);
            // Tooltips are the immediate windows of the top layer.
            if (layer == WindowLayer.Super) Recorder.Add("tip", rect, screen: true);
        }

        public static void StackWindow(Window __instance) =>
            Recorder.Add("window:" + __instance.GetType().Name, __instance.windowRect, screen: true);
    }
}
