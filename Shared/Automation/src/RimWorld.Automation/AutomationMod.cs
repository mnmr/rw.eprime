using System;
using System.IO;
using HarmonyLib;
using RimWorld.Automation.Core;
using UnityEngine;
using Verse;

namespace RimWorld.Automation;

public sealed class AutomationMod : Mod
{
    internal static string Token = "";
    // Process launch metadata, captured from the actual ModContentPack. It is
    // immutable for this process and does not retain a game/world/map owner.
    internal static string RootDirectory = "";
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
        if (!RunIdentity.Matches(profile, Token))
        { Token = ""; return; }
        RootDirectory = content.RootDir;
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
            AutomationAudio.Mute();
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
