if (-not ('SharedForegroundAudit' -as [type])) {
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
public static class SharedForegroundAudit {
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    static readonly HashSet<uint> seen = new HashSet<uint>();
    static volatile bool stop;
    static Thread thread;
    public static void Start() {
        seen.Clear(); stop = false;
        thread = new Thread(() => {
            while (!stop) {
                GetWindowThreadProcessId(GetForegroundWindow(), out uint processId);
                lock (seen) seen.Add(processId);
                Thread.Sleep(10);
            }
        });
        thread.IsBackground = true; thread.Start();
    }
    public static uint[] Finish() {
        stop = true; thread.Join();
        lock (seen) { var result = new uint[seen.Count]; seen.CopyTo(result); return result; }
    }
}
'@
}
