using System;

namespace RimWorld.Automation.Core;

public static class RunIdentity
{
    public const string RunsRoot = @"D:\Code\RimWorld\AutomationProfiles\Shared\Runs";
    public static bool Matches(string profile, string token)
    {
        const string prefix = "rimworld-shared-";
        if (string.IsNullOrEmpty(profile) || token == null || token.Length != 48
            || !token.StartsWith(prefix, StringComparison.Ordinal)) return false;
        string id = token.Substring(prefix.Length);
        for (int i = 0; i < id.Length; i++)
            if (!(id[i] >= '0' && id[i] <= '9') && !(id[i] >= 'a' && id[i] <= 'f')) return false;
        return string.Equals(profile.Replace('/', '\\').TrimEnd('\\'), RunsRoot + "\\" + id,
            StringComparison.OrdinalIgnoreCase);
    }
}
