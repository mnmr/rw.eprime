using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using RimWorld;
using RimWorld.Automation.Core;
using UnityEngine;
using Verse;

namespace VanillaLanguageAudit;

// Test-only, one-shot observer. No localization assets, defs, patches, save load,
// recurring work or mutation of native translations are introduced by this mod.
public sealed class Probe : Mod
{
    private static string profile = "";
    private static readonly Action InitializeAction = Initialize;
#if RIMWORLD_1_6
    private static readonly Action CaptureAction = Capture;
#endif

    public Probe(ModContentPack content) : base(content)
    {
        string token = "";
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)) profile = arg.Substring(16).Trim('"');
            if (arg.StartsWith("-automationtoken=", StringComparison.Ordinal)) token = arg.Substring(17);
        }
        if (!RunIdentity.Matches(profile, token) || !File.Exists(Path.Combine(profile, "Config", "vanilla-language-audit.enabled")))
        { profile = ""; return; }
        LongEventHandler.ExecuteWhenFinished(InitializeAction);
    }

    private static void Initialize()
    {
#if RIMWORLD_1_6
        // ExecuteToExecuteWhenFinished iterates its growing callback list.
        // Appending here puts capture after all native callbacks already
        // queued by PlayDataLoader (including after-implied-def injection
        // and static constructors), before the first runtime Update. This
        // avoids blocking the WSL automation status dispatcher during the
        // expensive native report while preserving natural injection order.
        LongEventHandler.ExecuteWhenFinished(CaptureAction);
#else
        // Constructor callbacks precede the game's queued after-implied-def
        // injections. Wait for a subsequent main-menu update with no events.
        var owner = new GameObject("Vanilla language audit");
        owner.AddComponent<AuditRunner>();
#endif
    }

    internal static void Capture()
    {
        string output = Path.Combine(profile, "VanillaLanguageAudit");
        Directory.CreateDirectory(output);
        var xml = new XmlDocument();
        XmlElement root = xml.CreateElement("audit"); xml.AppendChild(root);
        try
        {
            LoadedLanguage active = LanguageDatabase.activeLanguage;
            LoadedLanguage english = LanguageDatabase.defaultLanguage;
            active.LoadData(); english.LoadData();
            root.SetAttribute("language", active.LegacyFolderName);
            root.SetAttribute("folder", active.folderName);
            root.SetAttribute("expected", File.ReadAllText(Path.Combine(profile, "Config", "vanilla-language-audit.enabled")).Trim());
            root.SetAttribute("programState", Current.ProgramState.ToString());
            root.SetAttribute("playDataLoaded", PlayDataLoader.Loaded.ToString());
            root.SetAttribute("longEventsPending", LongEventHandler.AnyEventNowOrWaiting.ToString());
#if RIMWORLD_1_6
            root.SetAttribute("captureBoundary", "appended final post-load callback after native injection and static constructors");
#else
            root.SetAttribute("captureBoundary", "first settled main-menu Update after native injection");
#endif
            root.SetAttribute("version", VersionControl.CurrentVersionStringWithRev);
            root.SetAttribute("assembly", typeof(LoadedLanguage).Assembly.FullName);
            root.SetAttribute("activeAnyError", active.anyError.ToString());
            root.SetAttribute("englishAnyError", english.anyError.ToString());
            root.SetAttribute("activeKeyed", active.keyedReplacements.Count.ToString());
            root.SetAttribute("englishKeyed", english.keyedReplacements.Count.ToString());
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                XmlElement e = xml.CreateElement("mod");
                e.SetAttribute("packageId", mod.PackageId); e.SetAttribute("name", mod.Name); e.SetAttribute("root", mod.RootDir); root.AppendChild(e);
            }
            var report = new StringBuilder("Translation report for " + active + Environment.NewLine);
            var unnecessary = new List<string>();
            string[] methods = {
                "AppendGeneralLoadErrors", "AppendDefInjectionsLoadErros", "AppendMissingKeyedTranslations",
                "AppendMissingDefInjections", "AppendMissingBackstories", "AppendUnnecessaryDefInjections",
                "AppendRenamedDefInjections", "AppendArgumentCountMismatches", "AppendUnnecessaryKeyedTranslations",
                "AppendKeyedTranslationsMatchingEnglish", "AppendBackstoriesMatchingEnglish", "AppendDefInjectionsSyntaxSuggestions",
                "AppendTKeySystemErrors", "AppendObsoleteBackstoryTranslations"
            };
            foreach (string method in methods)
            {
                XmlElement e = xml.CreateElement("section"); e.SetAttribute("method", method); root.AppendChild(e);
                var section = new StringBuilder();
                try
                {
                    MethodInfo native = typeof(LanguageReportGenerator).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
                    if (native == null) throw new MissingMethodException(typeof(LanguageReportGenerator).FullName, method);
                    object[] args = native.GetParameters().Length == 2 ? new object[] { section, unnecessary } : new object[] { section };
                    native.Invoke(null, args);
                    e.SetAttribute("status", "ok");
                }
                catch (Exception ex)
                {
                    e.SetAttribute("status", "failed"); e.InnerText = ex.ToString();
                    section.AppendLine("NATIVE REPORT FAILURE: " + method + " " + ex);
                }
                File.WriteAllText(Path.Combine(output, method + ".txt"), section.ToString()); report.Append(section);
            }
            File.WriteAllText(Path.Combine(output, "TranslationReport.txt"), report.ToString());
            // Native file provenance is retained separately because some report
            // sections show just a basename, not the owning official package.
            using (var keyed = new StreamWriter(Path.Combine(output, "KeyedSources.tsv")))
            {
                keyed.WriteLine("language\tkey\tsource");
                foreach (var item in active.keyedReplacements) keyed.WriteLine("active\t" + item.Key + "\t" + item.Value.fileSourceFullPath);
                foreach (var item in english.keyedReplacements) keyed.WriteLine("english\t" + item.Key + "\t" + item.Value.fileSourceFullPath);
            }
            root.SetAttribute("status", "complete");
        }
        catch (Exception ex) { root.SetAttribute("status", "failed"); root.InnerText += ex.ToString(); }
        xml.Save(Path.Combine(output, "audit.xml"));
        File.WriteAllText(Path.Combine(output, "complete.txt"), root.GetAttribute("status"));
        Log.Message("[VanillaLanguageAudit] " + root.GetAttribute("status") + " " + root.GetAttribute("language"));
        // launch.ps1's sanctioned main-menu mode returns when this owned game
        // exits; it intentionally never declares a save-loaded ready state.
        UnityEngine.Application.Quit();
    }
}

public sealed class AuditRunner : MonoBehaviour
{
    private void Update()
    {
        if (!PlayDataLoader.Loaded || LongEventHandler.AnyEventNowOrWaiting || Current.ProgramState != ProgramState.Entry) return;
        enabled = false;
        try { Probe.Capture(); }
        finally { UnityEngine.Object.Destroy(gameObject); }
    }
}
