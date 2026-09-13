using System.Collections.Generic;
using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWorld.Automation;

[HarmonyPatch(typeof(GUIUtility), "BeginGUI")]
internal static class PlayerGuiEvent
{
    private static readonly AccessTools.FieldRef<Event, IntPtr> Pointer = AccessTools.FieldRefAccess<Event, IntPtr>("m_Ptr");
    private static readonly Action<Event, IntPtr> Copy = (Action<Event, IntPtr>)Delegate.CreateDelegate(typeof(Action<Event, IntPtr>), AccessTools.Method(typeof(Event), "CopyFromPtr"));
    private static void Prefix(int instanceID)
    {
        if (Event.current == null || Current.Root == null || instanceID != Current.Root.GetInstanceID()) return;
        Event.current.mousePosition = AutomationRunner.Pointer;
        if (AutomationRunner.PendingEvent == null || AutomationRunner.TargetWindow != null || Event.current.type != EventType.Layout) return;
        Copy(Event.current, Pointer(AutomationRunner.PendingEvent));
        AutomationRunner.PendingEvent = null;
        AutomationRunner.LastInjectionFrame = Time.frameCount;
    }
}

[HarmonyPatch(typeof(Window), "InnerWindowOnGUI")]
internal static class WindowGuiEvent
{
    internal static bool Active;
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Window __instance, ref Event? __state)
    {
        Event? input = AutomationRunner.PendingEvent;
        Window? target = AutomationRunner.TargetWindow;
        if (input == null || target != __instance || Event.current.type != EventType.Repaint) return;
        __state = Event.current;
        Event.current = input;
        VirtualPointerClip.Reapply();
        Active = true;
        AutomationRunner.PendingEvent = null;
        AutomationRunner.LastInjectionFrame = Time.frameCount;
    }
    private static void Finalizer(Event? __state) { if (__state != null) { Active = false; Event.current = __state; } }
}

internal static class InputEventType
{
    // Native windows ignore synthetic mouse events. Adapt only the explicitly
    // targeted window's input scope; real GUI controls still own capture, hit
    // testing, Use(), toggles and activation. Never force a button's result.
    internal static EventType Read(Event input)
    {
        EventType type = input.type;
        return WindowGuiEvent.Active && input == Event.current && GUI.enabled && type == EventType.Ignore ? input.rawType : type;
    }

    internal static EventType ForControl(Event input, int controlId)
    {
        EventType type = input.GetTypeForControl(controlId);
        if (!WindowGuiEvent.Active || input != Event.current || !GUI.enabled) return type;
        if (type != EventType.Ignore || Read(input) == EventType.Ignore) return type;
        EventType raw = input.rawType;
        if (raw != EventType.MouseDown && raw != EventType.MouseUp && raw != EventType.MouseDrag) return type;
        return GUIUtility.hotControl == 0 || GUIUtility.hotControl == controlId ? raw : type;
    }
}

internal static class OwnedInputAssemblies
{
    // Explicit startup allowlist. Never rewrite third-party mod methods.
    internal static IEnumerable<Assembly> All()
    {
        yield return typeof(GUI).Assembly;
        yield return typeof(Root).Assembly;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name is "EPrimeReadouts" or "WorkRoles" or "QualityJobs" or "Implanner")
                yield return assembly;
    }
}

[HarmonyPatch]
internal static class ControlEventTypes
{
    private static readonly MethodInfo Original = AccessTools.PropertyGetter(typeof(Event), nameof(Event.type));
    private static readonly MethodInfo Replacement = AccessTools.Method(typeof(InputEventType), nameof(InputEventType.Read));
    private static readonly MethodInfo OriginalControl = AccessTools.Method(typeof(Event), nameof(Event.GetTypeForControl));
    private static readonly MethodInfo ReplacementControl = AccessTools.Method(typeof(InputEventType), nameof(InputEventType.ForControl));
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var assembly in OwnedInputAssemblies.All())
        foreach (Type type in assembly.GetTypes())
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
            foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(method))
                if (instruction.Calls(Original) || instruction.Calls(OriginalControl)) { yield return method; break; }
        }
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(Original)) { instruction.opcode = OpCodes.Call; instruction.operand = Replacement; }
            else if (instruction.Calls(OriginalControl)) { instruction.opcode = OpCodes.Call; instruction.operand = ReplacementControl; }
            yield return instruction;
        }
    }
}

[HarmonyPatch(typeof(GUI), nameof(GUI.DragWindow), new Type[] { })]
internal static class VirtualWindowDrag
{
    // One explicit gesture owns this presentation state. Complete/Dispose release
    // it, including cancellation. No state is retained across commands or worlds.
    private static Window? dragging;
    private static Vector2 origin;
    private static Rect initial;
    private static Window? changed;
    private static Rect desired;

    private static bool Prefix()
    {
        if (!WindowGuiEvent.Active) return true;
        Window? target = AutomationRunner.TargetWindow;
        if (target == null || !GUI.enabled) return false;
        Event input = Event.current;
        EventType type = InputEventType.Read(input);
        if (type == EventType.MouseDown && input.button == 0 && GUIUtility.hotControl == 0)
        {
            dragging = target;
            origin = AutomationRunner.Pointer;
            initial = target.windowRect;
            input.Use();
        }
        else if (dragging == target && (type == EventType.MouseDrag || type == EventType.MouseUp))
        {
            desired = initial;
            desired.position += (AutomationRunner.Pointer - origin) / Prefs.UIScale;
            changed = target;
            input.Use();
            if (type == EventType.MouseUp) dragging = null;
        }
        return false;
    }

    internal static void Apply(Window window)
    {
        if (changed != window) return;
        // GUI.Window returns its native rectangle after the content callback.
        window.windowRect = desired;
        changed = null;
    }
    internal static void Reset() { dragging = null; changed = null; }
}

[HarmonyPatch(typeof(Window), nameof(Window.WindowOnGUI))]
internal static class PublishWindowDrag
{
    private static void Postfix(Window __instance) => VirtualWindowDrag.Apply(__instance);
}

[HarmonyPatch(typeof(Root), nameof(Root.OnGUI))]
internal static class VirtualPointerClip
{
    // Native IMGUI caches an absolute pointer separately from Event.mousePosition.
    // Each root/window context therefore needs its own balanced input transform.
    // The render transform stays identity: native painting and clipping are retained.
    private static readonly Type Clip = AccessTools.TypeByName("UnityEngine.GUIClip");
    private static readonly Func<Vector2> AbsolutePosition = (Func<Vector2>)Delegate.CreateDelegate(typeof(Func<Vector2>), AccessTools.Method(Clip, "GetAbsoluteMousePosition"));
    private delegate void PushClip(Matrix4x4 renderTransform, Matrix4x4 inputTransform, Rect clip);
    private static readonly PushClip Push = (PushClip)Delegate.CreateDelegate(typeof(PushClip), AccessTools.Method(Clip, "Internal_PushParentClip", new[] { typeof(Matrix4x4), typeof(Matrix4x4), typeof(Rect) }));
    private static readonly Action Pop = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(Clip, "Internal_PopParentClip"));
    internal static readonly Action Reapply = (Action)Delegate.CreateDelegate(typeof(Action), AccessTools.Method(Clip, "Reapply"));
    internal static void Prefix(ref bool __state)
    {
        Vector2 offset = AbsolutePosition() - AutomationRunner.Pointer;
        Push(Matrix4x4.identity, Matrix4x4.Translate(offset), new Rect(0, 0, Screen.width, Screen.height));
        __state = true;
    }
    internal static void Finalizer(bool __state) { if (__state) Pop(); }
}

[HarmonyPatch(typeof(GUI), "CallWindowDelegate")]
internal static class VirtualWindowPointerClip
{
    private static void Prefix(ref bool __state) => VirtualPointerClip.Prefix(ref __state);
    private static void Finalizer(bool __state) => VirtualPointerClip.Finalizer(__state);
}

// These patches are installed only in the canonical isolated automation process.
// No OS input, focus, clipboard, or other-mod methods are touched.
[HarmonyPatch(typeof(Input), nameof(Input.mousePosition), MethodType.Getter)]
internal static class VirtualMousePosition
{
    private static bool Prefix(ref Vector3 __result)
    {
        Vector2 point = AutomationRunner.Pointer;
        __result = new Vector3(point.x, Screen.height - point.y, 0);
        return false;
    }
}

[HarmonyPatch(typeof(Event), nameof(Event.mousePosition), MethodType.Getter)]
internal static class VirtualEventPosition
{
    private static bool Prefix(Event __instance, ref Vector2 __result)
    {
        if (__instance != Event.current) return true;
        Vector2 origin = UI.GUIToScreenPoint(Vector2.zero);
        __result = AutomationRunner.Pointer / Prefs.UIScale - origin;
        return false;
    }
}

internal static class ButtonState
{
    internal static bool Held(int button) => AutomationRunner.HeldButton == button;
    internal static bool Down(int button) => AutomationRunner.LastButton == button && AutomationRunner.DownFrame == Time.frameCount;
    internal static bool Up(int button) => AutomationRunner.LastButton == button && AutomationRunner.UpFrame == Time.frameCount;
}

[HarmonyPatch]
internal static class GameButtonQueries
{
    private static readonly MethodInfo Held = AccessTools.Method(typeof(Input), nameof(Input.GetMouseButton));
    private static readonly MethodInfo Down = AccessTools.Method(typeof(Input), nameof(Input.GetMouseButtonDown));
    private static readonly MethodInfo Up = AccessTools.Method(typeof(Input), nameof(Input.GetMouseButtonUp));
    private static readonly MethodInfo VirtualHeld = AccessTools.Method(typeof(ButtonState), nameof(ButtonState.Held));
    private static readonly MethodInfo VirtualDown = AccessTools.Method(typeof(ButtonState), nameof(ButtonState.Down));
    private static readonly MethodInfo VirtualUp = AccessTools.Method(typeof(ButtonState), nameof(ButtonState.Up));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Startup only: redirect the game's callers, because Harmony cannot patch
        // these native extern Unity functions. Other mods' methods are not patched.
        foreach (Assembly assembly in OwnedInputAssemblies.All())
        foreach (Type type in assembly.GetTypes())
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (method.ContainsGenericParameters || method.GetMethodBody() == null) continue;
            foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(method))
                if (instruction.Calls(Held) || instruction.Calls(Down) || instruction.Calls(Up))
                { yield return method; break; }
        }
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(Held)) instruction.operand = VirtualHeld;
            else if (instruction.Calls(Down)) instruction.operand = VirtualDown;
            else if (instruction.Calls(Up)) instruction.operand = VirtualUp;
            yield return instruction;
        }
    }
}

[HarmonyPatch(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) })]
internal static class VirtualKey
{
    private static bool Prefix(KeyCode key, ref bool __result)
    {
        // A key routed to a dialog must not also drive the map camera during
        // the root's separate GUI pass.
        if (AutomationRunner.TargetWindow != null && !WindowGuiEvent.Active) { __result = false; return false; }
        __result = key switch
        {
            KeyCode.LeftControl or KeyCode.RightControl => (AutomationRunner.Modifiers & EventModifiers.Control) != 0,
            KeyCode.LeftShift or KeyCode.RightShift => (AutomationRunner.Modifiers & EventModifiers.Shift) != 0,
            KeyCode.LeftAlt or KeyCode.RightAlt => (AutomationRunner.Modifiers & EventModifiers.Alt) != 0,
            _ => key != KeyCode.None && key == AutomationRunner.Key && AutomationRunner.KeyUpFrame < AutomationRunner.KeyDownFrame
        };
        return false;
    }
}
[HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), new[] { typeof(KeyCode) })]
internal static class VirtualKeyDown
{
    private static bool Prefix(KeyCode key, ref bool __result)
    { __result = (AutomationRunner.TargetWindow == null || WindowGuiEvent.Active) && key != KeyCode.None && key == AutomationRunner.Key && AutomationRunner.KeyDownFrame == Time.frameCount; return false; }
}
[HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), new[] { typeof(KeyCode) })]
internal static class VirtualKeyUp
{
    private static bool Prefix(KeyCode key, ref bool __result)
    { __result = (AutomationRunner.TargetWindow == null || WindowGuiEvent.Active) && key != KeyCode.None && key == AutomationRunner.Key && AutomationRunner.KeyUpFrame == Time.frameCount; return false; }
}

