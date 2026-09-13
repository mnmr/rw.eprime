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
        var name = new StringBuilder(256);
        if (!GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()), 2, name, name.Capacity * 2, out _)
            || name.ToString() != "RimWorld-" + AutomationMod.Token)
            throw new InvalidOperationException("Automation requires its own isolated Windows desktop.");
    }
}
