using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using RimWorld.Automation.Core;

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
    [DllImport("kernel32.dll")] private static extern bool GetExitCodeProcess(IntPtr process, out uint code);

    private static void Main(string[] arguments)
    {
        if (arguments.Length != 4) return;
        string executable = arguments[0], profile = arguments[1], token = arguments[2], log = arguments[3];
        IntPtr desktop = IntPtr.Zero;
        IntPtr job = IntPtr.Zero;
        ProcessInfo process = default;
        FileStream? lease = null;
        bool ownsRun = false;
        DateTime startedUtc = DateTime.UtcNow;
        void Publish(string state, uint? exitCode = null)
        {
            string path = Path.Combine(profile, "host.json");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(new {
                    state, token, hostProcessId = Environment.ProcessId, gameProcessId = process.Id,
                    startedUtc, heartbeatUtc = DateTime.UtcNow, exitCode
                }));
                File.Move(temporary, path, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        try
        {
            if (!RunIdentity.Matches(profile, token)
                || !string.Equals(Path.GetFullPath(executable), Path.Combine(profile, "Game", "RimWorldWin64.exe"), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFullPath(log), Path.Combine(profile, "Player.log"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The host requires a managed run and its matching session token/executable/log.");
            for (DirectoryInfo? parent = new DirectoryInfo(profile); parent != null; parent = parent.Parent)
                if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Run ancestors cannot be links.");
            if (!File.Exists(Path.Combine(profile, "run.json")) || File.Exists(Path.Combine(profile, "removing.json")))
                throw new InvalidOperationException("Run is not prepared or is being removed.");
            // Discovery briefly probes the lease. Retry sharing violations so
            // an observation racing startup cannot abort a valid run.
            DateTime lockDeadline = DateTime.UtcNow.AddSeconds(2);
            while (lease == null)
            {
                try { lease = new FileStream(Path.Combine(profile, "run.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 && DateTime.UtcNow < lockDeadline)
                { Thread.Sleep(10); }
            }
            // Single-use runs prevent old commands from targeting a replacement
            // process with reused profile/token metadata after a restart.
            using (var once = new FileStream(Path.Combine(profile, "started.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                JsonSerializer.Serialize(once, new { token, startedUtc });
            ownsRun = true;
            Publish("starting");
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
            do { Publish("running"); } while (WaitForSingleObject(process.Process, 5000) == 258);
            if (!GetExitCodeProcess(process.Process, out uint exitCode)) throw new Win32Exception();
            Publish(exitCode == 0 ? "stopped" : "failed", exitCode);
        }
        catch (Exception error)
        {
            if (process.Process != IntPtr.Zero) TerminateProcess(process.Process, 1);
            // A rejected second host must never overwrite the live run's status.
            if (ownsRun)
            {
                File.WriteAllText(log + ".host-error.txt", error.ToString());
                Publish("failed");
            }
        }
        finally
        {
            if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
            if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
            if (job != IntPtr.Zero) CloseHandle(job);
            if (desktop != IntPtr.Zero) CloseDesktop(desktop);
            lease?.Dispose();
        }
    }
    private static string Quote(string value)
    {
        if (value.Contains('"') || value.EndsWith('\\')) throw new ArgumentException("Invalid launch path.");
        return "\"" + value + "\"";
    }
}
