using System;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWorld.Automation;

public sealed class AutomationMod : Mod
{
    internal static string Token = "";
    internal static Harmony? Patches;
    private static readonly Action InitializeAction = Initialize;

    public AutomationMod(ModContentPack content) : base(content)
    {
        string profile = "";
        foreach (string argument in Environment.GetCommandLineArgs())
        {
            if (argument.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)) profile = argument.Substring(16).Trim('"');
            if (argument.StartsWith("-automationtoken=rimworld-shared-", StringComparison.Ordinal)) Token = argument.Substring(17);
        }
        const string canonicalProfile = @"D:\Code\RimWorld\AutomationProfiles\Shared";
        if (string.IsNullOrEmpty(profile) || !string.Equals(Path.GetFullPath(profile).TrimEnd('\\'), canonicalProfile, StringComparison.OrdinalIgnoreCase)
            || Token.Length != 48 || !Guid.TryParseExact(Token.Substring(16), "N", out _))
        { Token = ""; return; }
        LongEventHandler.ExecuteWhenFinished(InitializeAction);
    }

    private static void Initialize()
    {
        DesktopIsolation.AssertPrivateDesktop();
        Patches = new Harmony("eprime.sharedautomation.session");
        GameObject? owner = null;
        try
        {
            Patches.PatchAll(typeof(AutomationMod).Assembly);
            owner = new GameObject("Shared Automation");
            UnityEngine.Object.DontDestroyOnLoad(owner);
            owner.AddComponent<AutomationRunner>();
            Application.runInBackground = true;
            Log.Message("[SharedAutomation] ready for background commands");
        }
        catch
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            Patches.UnpatchAll(Patches.Id);
            throw;
        }
    }
}
