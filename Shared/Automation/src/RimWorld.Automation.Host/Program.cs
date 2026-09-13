using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RimWorld.Automation.Host;

internal static class Program
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size; public string? Reserved; public string? Desktop; public string? Title;
        public int X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public short ShowWindow, ReservedBytes;
        public IntPtr ReservedData, StdInput, StdOutput, StdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr Process, Thread; public uint Id, ThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long ProcessTime, JobTime; public uint Flags;
        public UIntPtr MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcesses;
        public UIntPtr Affinity; public uint Priority, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic; public IoCounters Io;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, int flags, uint access, IntPtr security);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder arguments, IntPtr processSecurity, IntPtr threadSecurity,
        bool inheritHandles, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int kind, ref ExtendedLimits limits, int size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")] private static extern bool TerminateProcess(IntPtr process, uint code);

    private static void Main(string[] arguments)
    {
        if (arguments.Length != 4) return;
        string executable = arguments[0], profile = arguments[1], token = arguments[2], log = arguments[3];
        IntPtr desktop = IntPtr.Zero;
        IntPtr job = IntPtr.Zero;
        ProcessInfo process = default;
        try
        {
            if (!string.Equals(Path.GetFullPath(profile).TrimEnd('\\'), @"D:\Code\RimWorld\AutomationProfiles\Shared", StringComparison.OrdinalIgnoreCase)
                || token.Length != 48 || !token.StartsWith("rimworld-shared-", StringComparison.Ordinal)
                || !Guid.TryParseExact(token.Substring(16), "N", out _))
                throw new InvalidOperationException("The host requires the canonical isolated profile and a fresh session token.");
            // The test process receives its own Win32 desktop. Never call
            // SwitchDesktop or attach input queues to the user's desktop.
            string name = "RimWorld-" + token;
            desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x01ff, IntPtr.Zero);
            if (desktop == IntPtr.Zero) throw new Win32Exception();
            // The host owns the entire child-process tree, including crash reporters.
            // Closing the host also closes this handle and terminates that tree.
            job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) throw new Win32Exception();
            var limits = new ExtendedLimits { Basic = new BasicLimits { Flags = 0x2000 } };
            if (!SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<ExtendedLimits>())) throw new Win32Exception();
            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>(), Desktop = name, Flags = 1, ShowWindow = 1 };
            var command = new StringBuilder(Quote(executable) + " -savedatafolder=" + Quote(profile) + " -automationtoken=" + token
                + " -logFile " + Quote(log) + " -screen-fullscreen 0 -screen-width 1920 -screen-height 1080");
            if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false, 4, IntPtr.Zero,
                Path.GetDirectoryName(executable)!, ref startup, out process)) throw new Win32Exception();
            if (!AssignProcessToJobObject(job, process.Process)) throw new Win32Exception();
            if (ResumeThread(process.Thread) == uint.MaxValue) throw new Win32Exception();
            CloseHandle(process.Thread); process.Thread = IntPtr.Zero;
            WaitForSingleObject(process.Process, uint.MaxValue);
        }
        catch (Exception error)
        {
            if (process.Process != IntPtr.Zero) TerminateProcess(process.Process, 1);
            File.WriteAllText(log + ".host-error.txt", error.ToString());
        }
        finally
        {
            if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
            if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
            if (job != IntPtr.Zero) CloseHandle(job);
            if (desktop != IntPtr.Zero) CloseDesktop(desktop);
        }
    }
    private static string Quote(string value)
    {
        if (value.Contains('"') || value.EndsWith('\\')) throw new ArgumentException("Invalid launch path.");
        return "\"" + value + "\"";
    }
}
