using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RimWorld.Automation;

internal static class DesktopIsolation
{
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformation(IntPtr handle, int kind, StringBuilder value, int bytes, out int needed);

    internal static void AssertPrivateDesktop()
    {
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            string display = Environment.GetEnvironmentVariable("DISPLAY") ?? "";
            string authority = Environment.GetEnvironmentVariable("XAUTHORITY") ?? "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(display, @"^:[0-9]+$")
                || display != Environment.GetEnvironmentVariable("RIMWORLD_AUTOMATION_DISPLAY")
                || Environment.GetEnvironmentVariable("RIMWORLD_AUTOMATION_TOKEN") != AutomationMod.Token
                || !authority.EndsWith("/" + AutomationMod.Token + "/Xauthority", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
                throw new InvalidOperationException("Automation requires its own authenticated private X display.");
            return;
        }
        var name = new StringBuilder(256);
        if (!GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()), 2, name, name.Capacity * 2, out _)
            || name.ToString() != "RimWorld-" + AutomationMod.Token)
            throw new InvalidOperationException("Automation requires its own isolated Windows desktop.");
    }
}
