using System.Diagnostics;
using System.Text.Json;

namespace RimWorld.Automation.Host;

internal static class WslHost
{
    internal static int Run(string profile, string token, Action<uint> heartbeat)
    {
        using JsonDocument record = JsonDocument.Parse(File.ReadAllText(Path.Combine(profile, "run.json")));
        JsonElement root = record.RootElement;
        if (root.GetProperty("backend").GetString() != "wsl") throw new InvalidOperationException("Linux executable requires a WSL run.");
        string linuxProfile = "/mnt/d/Code/RimWorld/AutomationProfiles/Shared/Runs/" + token[16..];
        var start = new ProcessStartInfo("wsl.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "--distribution", root.GetProperty("wslDistribution").GetString()!, "--exec", "python3",
            linuxProfile + "/Host/linux-host.py", "run", linuxProfile, token, root.GetProperty("owner").GetString()! })
            start.ArgumentList.Add(argument);
        using Process child = Process.Start(start) ?? throw new InvalidOperationException("Could not start WSL supervisor.");
        Task<string> output = child.StandardOutput.ReadToEndAsync();
        Task<string> error = child.StandardError.ReadToEndAsync();
        try
        {
            while (!child.WaitForExit(1000))
            {
                child.StandardInput.WriteLine(token);
                child.StandardInput.Flush();
                string linuxState = Path.Combine(profile, "linux-host.json");
                uint gameId = 0;
                if (File.Exists(linuxState))
                {
                    using JsonDocument state = JsonDocument.Parse(File.ReadAllText(linuxState));
                    if (state.RootElement.GetProperty("token").GetString() != token) throw new InvalidOperationException("Linux host identity mismatch.");
                    gameId = state.RootElement.GetProperty("gameProcessId").GetUInt32();
                }
                heartbeat(gameId);
            }
        }
        finally
        {
            // EOF makes the Linux supervisor terminate its exact child sessions.
            // The 15-second heartbeat deadline also bounds abrupt host death.
            child.StandardInput.Close();
            if (!child.WaitForExit(20000)) throw new TimeoutException("WSL supervisor failed to tear down after host disconnect.");
        }
        File.WriteAllText(Path.Combine(profile, "linux-host.log"), output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
        return child.ExitCode;
    }
}
