using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Xml;
using HarmonyLib;
using RimWorld.Automation.Core;
using Verse;

namespace LocalizationProbe;

// The current Fisso fixture contains three jobs without a def. Repair only
// those in the in-memory test XML before vanilla initializes their drivers.
// The verified source and the run's on-disk save remain byte-for-byte intact.
[StaticConstructorOnStartup]
internal static class Fixture
{
    private static readonly List<string> repaired = new();
    private static readonly string profile = "";

    static Fixture()
    {
        string token = "";
        foreach (string argument in Environment.GetCommandLineArgs())
        {
            if (argument.StartsWith("-savedatafolder=", StringComparison.OrdinalIgnoreCase)) profile = argument.Substring(16).Trim('"');
            if (argument.StartsWith("-automationtoken=", StringComparison.Ordinal)) token = argument.Substring(17);
        }
        if (!RunIdentity.Matches(profile, token)) return;
        new Harmony("rimshared.tests.localization.fixture").Patch(
            AccessTools.Method(typeof(Pawn), nameof(Pawn.ExposeData)),
            prefix: new HarmonyMethod(typeof(Fixture), nameof(BeforePawnLoad)));
    }

    private static void BeforePawnLoad()
    {
        if (Scribe.mode != LoadSaveMode.LoadingVars) return;
        XmlNode pawn = Scribe.loader.curXmlParent;
        XmlElement? job = pawn.SelectSingleNode("jobs/curJob[not(def) and not(@IsNull='True')]") as XmlElement;
        if (job == null) return;
        XmlNode tracker = job.ParentNode!;
        repaired.Add(pawn.SelectSingleNode("id")?.InnerText ?? "unknown pawn");
        job.RemoveAll();
        job.SetAttribute("IsNull", "True");
        if (tracker.SelectSingleNode("curDriver") is XmlElement driver)
        {
            driver.RemoveAll();
            driver.SetAttribute("IsNull", "True");
        }
        if (pawn.SelectSingleNode("pather") is XmlNode pather) pawn.RemoveChild(pather);
        File.WriteAllText(Path.Combine(profile, "fixture-repairs.json"), JsonSerializer.Serialize(new
        {
            reason = "Source current job has no def; reset job, driver and path follower in memory only",
            pawns = repaired
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
