using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld.Automation.Core;
using UnityEngine;
using Verse;
using Command = RimWorld.Automation.Core.Command;

namespace RimWorld.Automation;

public sealed class AutomationRunner : MonoBehaviour
{
    private sealed class Work
    {
        internal readonly Command Command;
        internal readonly TaskCompletionSource<Reply> Completion = new TaskCompletionSource<Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly long Deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 30;
        internal Work(Command command) { Command = command; }
    }

    // Owner: isolated test process. Key: launch token and PID. Value: one pending
    // command, never a game-model cache. Dependencies: explicit pipe requests only.
    // Refresh: main-thread Update; teardown: OnDestroy/OnApplicationQuit cancel the
    // pipe, fail pending work, release virtual input and remove session patches.
    private readonly object gate = new object();
    private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
    private NamedPipeServerStream? pipe;
    private Work? pending;
    private Work? active;
    private Event[] events = Array.Empty<Event>();
    private int nextEvent;
    private int finishAfterFrame;
    private bool disposed;
    private bool clickedRoot;
    private Window? focusAtRootClick;
    internal static Event? PendingEvent;
    internal static Window? TargetWindow;
    private static readonly AccessTools.FieldRef<WindowStack, Window> FocusedWindow = AccessTools.FieldRefAccess<WindowStack, Window>("focusedWindow");
    internal static int LastInjectionFrame = -2;

    // Process-local virtual input. Only the main thread reads/writes this state.
    internal static Vector2 Pointer = new Vector2(-1000, -1000);
    internal static int HeldButton = -1;
    internal static int DownFrame = -1;
    internal static int UpFrame = -1;
    internal static int LastButton = -1;
    internal static KeyCode Key;
    internal static EventModifiers Modifiers;
    internal static int KeyDownFrame = -1;
    internal static int KeyUpFrame = -1;

    private void Awake() => Task.Run(Serve);

    private async Task Serve()
    {
        string name = "rimworld-automation-" + Process.GetCurrentProcess().Id + "-" + AutomationMod.Token;
        while (!shutdown.IsCancellationRequested)
        {
            try
            {
                using var connection = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                lock (gate) { if (disposed) return; pipe = connection; }
                await connection.WaitForConnectionAsync(shutdown.Token).ConfigureAwait(false);
                // Bound connected clients too, including those that never finish sending.
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
                timeout.CancelAfter(30000);
                using var closeOnTimeout = timeout.Token.Register(connection.Dispose);
                var work = new Work(Wire.Read(connection));
                lock (gate) { if (disposed) return; pending = work; }
                using var cancelWork = timeout.Token.Register(() => work.Completion.TrySetCanceled());
                Reply reply = await work.Completion.Task.ConfigureAwait(false);
                Wire.Write(connection, reply);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is OperationCanceledException || error is ObjectDisposedException || error is System.Runtime.Serialization.SerializationException)
            {
                // A disconnected/expired transport cancels its request. No Unity access
                // or retries on this thread; the next Update releases any held input.
            }
            finally { lock (gate) { pipe = null; } }
        }
    }

    private void Update()
    {
        if (disposed) return;
        if (active != null)
        {
            if (active.Completion.Task.IsCompleted || Stopwatch.GetTimestamp() >= active.Deadline)
            {
                bool uncertainInput = events.Length != 0 || PendingEvent != null;
                Complete(new Reply { error = "Command timed out before completion." });
                // A lost release can leave game-owned drag/capture state alive.
                // End this disposable session instead of accepting more input.
                if (uncertainInput) { Application.Quit(); Dispose(); }
                return;
            }
            if (PendingEvent != null || Time.frameCount <= LastInjectionFrame + 1) return;
            if (nextEvent < events.Length)
            {
                Event input = events[nextEvent++];
                if (input.rawType == EventType.KeyDown)
                {
                    Window? focused = FocusedWindow(Find.WindowStack);
                    TargetWindow = clickedRoot && focused == focusAtRootClick && Find.WindowStack.GetsInput(null)
                        ? null : Visible(focused) ? focused : null;
                }
                if (input.rawType == EventType.MouseDown)
                {
                    clickedRoot = TargetWindow == null;
                    focusAtRootClick = FocusedWindow(Find.WindowStack);
                }
                Dispatch(input);
                finishAfterFrame = Time.frameCount + 2;
            }
            else if (finishAfterFrame > 0 && Time.frameCount >= finishAfterFrame) Complete(Status());
            return;
        }
        lock (gate) { active = pending; pending = null; }
        if (active == null) return;
        if (active.Completion.Task.IsCompleted || Stopwatch.GetTimestamp() >= active.Deadline)
        { Complete(new Reply { error = "Command expired before dispatch." }); return; }
        try
        {
            Command command = active.Command;
            if (command.command == "status") { Complete(Status()); return; }
            if (command.command == "capture")
            { finishAfterFrame = 0; StartCoroutine(Capture(active)); return; }
            if (command.command == "quit")
            { Complete(Status()); Application.Quit(); return; }
            if (LongEventHandler.AnyEventNowOrWaiting) throw new InvalidOperationException("The game is loading; input is unavailable.");
            events = BuildEvents(command);
            TargetWindow = FindTargetWindow(command);
            nextEvent = 0;
            finishAfterFrame = Time.frameCount + 2;
        }
        catch (Exception error) { Complete(new Reply { error = error.Message }); }
    }

    private static Reply Status() => new Reply
    {
        ok = true, processId = Process.GetCurrentProcess().Id,
        ready = GenScene.InPlayScene && Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null && !LongEventHandler.AnyEventNowOrWaiting,
        width = Screen.width, height = Screen.height, uiScale = Prefs.UIScale,
        audioVolume = AudioListener.volume, runtimeAssembly = typeof(AutomationRunner).Assembly.Location,
        runtimeModRoot = AutomationMod.RootDirectory,
        x = Mathf.RoundToInt(Pointer.x), y = Mathf.RoundToInt(Pointer.y)
    };

    private static void ValidatePoint(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Screen.width || y >= Screen.height)
            throw new ArgumentOutOfRangeException("point", "Coordinates must be inside the game frame.");
    }

    private static Event[] BuildEvents(Command command)
    {
        if (command.command == "type")
        {
            KeyStroke[] strokes = KeySequence.Parse(command.text);
            var typed = new Event[strokes.Length * 2];
            for (int i = 0; i < strokes.Length; i++)
            {
                var stroke = strokes[i];
                var key = (KeyCode)Enum.Parse(typeof(KeyCode), stroke.Key);
                EventModifiers modifiers = EventModifiers.None;
                if ((stroke.Modifiers & KeyModifiers.Shift) != 0) modifiers |= EventModifiers.Shift;
                if ((stroke.Modifiers & KeyModifiers.Control) != 0) modifiers |= EventModifiers.Control;
                if ((stroke.Modifiers & KeyModifiers.Alt) != 0) modifiers |= EventModifiers.Alt;
                if (stroke.Character == '\0' && key != KeyCode.Return && key != KeyCode.Tab && key != KeyCode.Space)
                    modifiers |= EventModifiers.FunctionKey;
                typed[i * 2] = new Event { type = EventType.KeyDown, keyCode = key, character = stroke.Character, modifiers = modifiers, mousePosition = Pointer };
                typed[i * 2 + 1] = new Event { type = EventType.KeyUp, keyCode = key, modifiers = modifiers, mousePosition = Pointer };
            }
            return typed;
        }
        ValidatePoint(command.x, command.y);
        if (command.button < 0 || command.button > 2) throw new ArgumentOutOfRangeException("button");
        var point = new Vector2(command.x, command.y);
        Event MouseEvent(EventType type, Vector2 position, Vector2 delta = default) => new Event
        { type = type, mousePosition = position, button = command.button, delta = delta, clickCount = 1 };
        switch (command.command)
        {
            case "hover": return new[] { MouseEvent(EventType.MouseMove, point) };
            case "click": return new[] { MouseEvent(EventType.MouseDown, point), MouseEvent(EventType.MouseUp, point) };
            case "scroll":
                if (Math.Abs((long)command.notches) > 100) throw new ArgumentOutOfRangeException("notches");
                var wheel = new Event[Math.Abs(command.notches)];
                for (int i = 0; i < wheel.Length; i++) wheel[i] = MouseEvent(EventType.ScrollWheel, point, new Vector2(0, -Math.Sign(command.notches) * 3));
                return wheel;
            case "drag":
                ValidatePoint(command.x2, command.y2);
                var drag = new Event[10];
                drag[0] = MouseEvent(EventType.MouseDown, point);
                Vector2 delta = (new Vector2(command.x2, command.y2) - point) / 8;
                for (int i = 1; i <= 8; i++) drag[i] = MouseEvent(EventType.MouseDrag, point + delta * i, delta);
                drag[9] = MouseEvent(EventType.MouseUp, new Vector2(command.x2, command.y2));
                return drag;
            default: throw new ArgumentException("Unknown automation command: " + command.command);
        }
    }

    private static Window? FindTargetWindow(Command command)
    {
        WindowStack stack = Find.WindowStack;
        if (command.command == "type") return null; // Resolve for each key, after any preceding dialog transition.
        Vector2 point = new Vector2(command.x, command.y) / Prefs.UIScale;
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            Window window = stack[i];
            if (!Visible(window)) continue;
            if (window.windowRect.Contains(point)) return window;
            if (window.absorbInputAroundWindow) return null;
        }
        return null;
    }

    private static bool Visible(Window? window) => window != null && window.IsOpen
        && (!window.onlyDrawInDevMode || Prefs.DevMode)
        && (window.drawInScreenshotMode || !Find.UIRoot.screenshotMode.Active);

    private static void Dispatch(Event input)
    {
        if (TargetWindow != null && !TargetWindow.IsOpen) TargetWindow = null;
        Pointer = input.mousePosition;
        if (input.type == EventType.MouseDown) { HeldButton = LastButton = input.button; DownFrame = Time.frameCount; }
        if (input.type == EventType.MouseUp) { HeldButton = -1; LastButton = input.button; UpFrame = Time.frameCount; }
        if (input.type == EventType.KeyDown) { Key = input.keyCode; Modifiers = input.modifiers; KeyDownFrame = Time.frameCount; }
        if (input.type == EventType.KeyUp) { Key = input.keyCode; Modifiers = EventModifiers.None; KeyUpFrame = Time.frameCount; }
        PendingEvent = input;
    }

    private IEnumerator Capture(Work work)
    {
        yield return new WaitForEndOfFrame();
        if (active != work || work.Completion.Task.IsCompleted) yield break;
        Texture2D? texture = null;
        try
        {
            texture = ScreenCapture.CaptureScreenshotAsTexture();
            Reply reply = Status();
            reply.image = texture.EncodeToPNG();
            Complete(reply);
        }
        catch (Exception error) { Complete(new Reply { error = error.Message }); }
        finally { if (texture != null) Destroy(texture); }
    }

    private void Complete(Reply reply)
    {
        Work? completed = active;
        active = null;
        events = Array.Empty<Event>();
        nextEvent = 0;
        finishAfterFrame = 0;
        HeldButton = -1;
        PendingEvent = null;
        TargetWindow = null;
        VirtualWindowDrag.Reset();
        Key = KeyCode.None;
        Modifiers = EventModifiers.None;
        completed?.Completion.TrySetResult(reply);
    }

    private void OnApplicationQuit() => Dispose();
    private void OnDestroy() => Dispose();
    private void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            shutdown.Cancel();
            pipe?.Dispose();
            pending?.Completion.TrySetCanceled();
            pending = null;
        }
        active?.Completion.TrySetCanceled();
        Complete(new Reply { error = "Automation stopped." });
        StopAllCoroutines();
        Pointer = new Vector2(-1000, -1000);
        focusAtRootClick = null;
        clickedRoot = false;
        AutomationMod.Patches?.UnpatchAll(AutomationMod.Patches.Id);
        shutdown.Dispose();
    }
}
