using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace ConflictsProbe;

// Player report 2026-11-01 (Workshop comments, "Nimn"): the archotech left
// kidney "is the same as" the archotech liver, torso implants "replace" the
// archotech skin, and Beyond Our Reach's tenebral kinds do not exclude the
// vanilla ones. The named kinds are Nighty's Archotech Bionics
// (ArchotechKidney, ArchotechLiver, ArchotechSkinImplant) and Integrated
// Implants' subdermal armour. This probe asks the real catalog and the real
// plan command, on the loaded body set, and dumps the answers.
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.conflicts";
    static bool done;
    static int failures;
    static string Root => GenFilePaths.SaveDataFolderPath;
    static readonly Type Catalogs = AccessTools.TypeByName("Implanner.Catalogs");
    static readonly Type Conflicts = AccessTools.TypeByName("Implanner.ImplantConflicts");

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[Conflicts] " + message);
        File.AppendAllText(Path.Combine(Root, "conflicts.txt"), message + Environment.NewLine);
    }

    static void Check(bool condition, string message)
    {
        Write((condition ? "PASS " : "FAIL ") + message);
        if (!condition) failures++;
    }

    static void Update()
    {
        if (done || Current.ProgramState != ProgramState.Playing
            || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        done = true;
        try
        {
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            Find.TickManager.Pause();
            Run();
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            failures++;
        }
        string status = failures == 0 ? "SUCCESS" : "FAILED " + failures;
        Write(status);
        File.WriteAllText(Path.Combine(Root, "conflicts-stage.txt"), status);
    }

    // ------------------------------------------------------- catalog access

    static object Entry(string def) =>
        AccessTools.Method(Catalogs, "ImplantByDefName").Invoke(null, new object[] { def });

    static IList Entries() => (IList)AccessTools.Method(Catalogs, "Implants").Invoke(null, null);

    static HediffDef Def(object entry) => (HediffDef)Traverse.Create(entry).Property("Def").GetValue();

    static bool IsReplacement(object entry) => Traverse.Create(entry).Property<bool>("IsReplacement").Value;

    static List<string> SlotKeys(object entry)
    {
        var result = new List<string>();
        foreach (object slot in (IList)Traverse.Create(entry).Property("Slots").GetValue())
            result.Add(Traverse.Create(slot).Property<PartAddress>("Address").Value.Key);
        return result;
    }

    static PartAddress Slot(string text)
    {
        if (!PartAddress.TryParse(text, out PartAddress address)) throw new Exception("slot " + text);
        return address;
    }

    static bool Conflict(string a, string slotA, string b, string slotB) => (bool)AccessTools
        .Method(Conflicts, "Conflicts",
            new[] { typeof(string), typeof(string), typeof(PartAddress), typeof(string), typeof(PartAddress) })
        .Invoke(null, new object[] { "", a, Slot(slotA), b, Slot(slotB) });

    static bool Tagged(object entry, string tag)
    {
        List<string> tags = Def(entry).tags;
        if (tags != null && tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase))) return true;
        var incompatible = (IEnumerable<string>)Traverse.Create(entry).Property("IncompatibleTags").GetValue();
        return incompatible.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
    }

    static IEnumerable<BodyPartRecord> Ancestors(BodyPartRecord record)
    {
        for (BodyPartRecord parent = record.parent; parent != null; parent = parent.parent)
            yield return parent;
    }

    static bool Has(Plan plan, string def, string slot)
    {
        PartAddress address = Slot(slot);
        return plan.Implants.Any(g => g.ImplantDefName == def && g.HasSlot(address));
    }

    static string Join(IEnumerable<string> items) => string.Join(", ", items);

    // ------------------------------------------------------------------ run

    static void Run()
    {
        bool bor = ModsConfig.IsActive("bambaryla.bor");
        Write("mods: archotech bionics=" + ModsConfig.IsActive("nighty.archotechbionics")
            + " integrated implants=" + ModsConfig.IsActive("lts.i")
            + " fsf abe=" + ModsConfig.IsActive("frozensnowfox.advancedbionicsexpansion")
            + " beyond our reach=" + bor);

        // Plannable bodies and where kidneys and liver sit on each.
        BodyPartDef kidney = DefDatabase<BodyPartDef>.GetNamed("Kidney");
        BodyPartDef liver = DefDatabase<BodyPartDef>.GetNamed("Liver");
        var bodies = (List<BodyDef>)AccessTools.Method(Catalogs, "PlannableBodies").Invoke(null, null);
        bool anyNested = false;
        foreach (BodyDef body in bodies)
        {
            List<BodyPartRecord> kidneys = body.GetPartsWithDef(kidney);
            List<BodyPartRecord> livers = body.GetPartsWithDef(liver);
            bool nested = livers.Any(l => Ancestors(l).Any(a => a.def == kidney))
                || kidneys.Any(k => Ancestors(k).Any(a => a.def == liver));
            anyNested |= nested;
            Write("body " + body.defName + ": kidneys=" + kidneys.Count + " livers=" + livers.Count
                + " parts=" + body.AllParts.Count + (nested ? " NESTED" : ""));
        }
        Write("plannable bodies: " + bodies.Count + (anyNested ? " (a body nests kidney and liver)" : " (no kidney/liver nesting)"));

        string[] watch =
        {
            "ArchotechKidney", "ArchotechLiver", "ArchotechSkinImplant", "LTS_SubdermalArmour",
            "FSFArchotechSkinI", "ArmorskinGland", "FSFArchotechKidney", "DetoxifierKidney",
            "BOR_TenebralBionicKidney", "BOR_TenebralBionicLiver", "BOR_TenebralBionicArm",
            "ArmorskinGlandTenebral", "ToughskinGlandTenebral",
        };
        foreach (string def in watch)
        {
            object entry = Entry(def);
            Write("entry " + def + ": " + (entry == null ? "absent"
                : Join(SlotKeys(entry)) + (IsReplacement(entry) ? " [replacement]" : " [implant]")));
        }

        Check(Entry("ArchotechKidney") != null && Entry("ArchotechLiver") != null
            && Entry("ArchotechSkinImplant") != null && Entry("LTS_SubdermalArmour") != null,
            "fixture: the reporter's kinds are in the catalog");
        if (failures > 0) return;

        // K: kidney versus liver.
        Check(!Conflict("ArchotechKidney", "Kidney#0", "ArchotechLiver", "Liver#0"),
            "K1 the archotech left kidney and the archotech liver coexist");
        Check(!Conflict("ArchotechKidney", "Kidney#1", "ArchotechLiver", "Liver#0"),
            "K1 the archotech right kidney and the archotech liver coexist");
        List<string> kidneyClash = Entries().Cast<object>()
            .Where(e => SlotKeys(e).Contains("Kidney#0") && Conflict(Def(e).defName, "Kidney#0", "ArchotechLiver", "Liver#0"))
            .Select(e => Def(e).defName).ToList();
        Check(kidneyClash.Count == 0, "K2 no left-kidney kind excludes the archotech liver (" + Join(kidneyClash) + ")");
        List<string> liverClash = Entries().Cast<object>()
            .Where(e => SlotKeys(e).Contains("Liver#0") && Conflict(Def(e).defName, "Liver#0", "ArchotechKidney", "Kidney#0"))
            .Select(e => Def(e).defName).ToList();
        Check(liverClash.Count == 0, "K2 no liver kind excludes the archotech left kidney (" + Join(liverClash) + ")");
        if (Entry("FSFArchotechKidney") != null)
            Check(Conflict("ArchotechKidney", "Kidney#0", "FSFArchotechKidney", "Kidney#0")
                && !Conflict("ArchotechKidney", "Kidney#0", "FSFArchotechKidney", "Kidney#1"),
                "K3 two archotech kidneys exclude each other on the same kidney only");

        // S: the archotech skin implant versus the torso.
        Check(!Conflict("ArchotechSkinImplant", "Torso#0", "LTS_SubdermalArmour", "Torso#0"),
            "S1 the archotech skin implant and subdermal armour coexist");
        if (Entry("ArmorskinGland") != null)
            Check(Conflict("ArchotechSkinImplant", "Torso#0", "ArmorskinGland", "Torso#0"),
                "S3 the archotech skin implant excludes the armorskin gland");
        var torsoKinds = Entries().Cast<object>()
            .Where(e => Def(e).defName != "ArchotechSkinImplant" && SlotKeys(e).Contains("Torso#0")).ToList();
        var torsoClash = torsoKinds
            .Where(e => Conflict(Def(e).defName, "Torso#0", "ArchotechSkinImplant", "Torso#0")).ToList();
        var unexpected = torsoClash.Where(e => !Tagged(e, "ArmorGland") && !IsReplacement(e))
            .Select(e => Def(e).defName).ToList();
        Write("torso kinds: " + torsoKinds.Count + "; excluding the archotech skin implant: "
            + Join(torsoClash.Select(e => Def(e).defName)));
        Check(unexpected.Count == 0,
            "S2 only ArmorGland kinds exclude the archotech skin implant (unexpected: " + Join(unexpected) + ")");

        // T: Beyond Our Reach tenebral kinds.
        if (bor)
        {
            string[] template = { "Eye", "Spine", "Heart", "Liver", "Kidney", "Lung", "Stomach", "Jaw", "Arm", "Leg", "Ear", "Tongue" };
            List<string> missing = template.Select(t => "BOR_TenebralBionic" + t).Where(d => Entry(d) == null).ToList();
            Check(missing.Count == 0, "T0 every tenebral template kind is in the catalog (missing: " + Join(missing) + ")");
            if (Entry("BOR_TenebralBionicKidney") != null)
                Check(Conflict("BOR_TenebralBionicKidney", "Kidney#0", "ArchotechKidney", "Kidney#0")
                    && !Conflict("BOR_TenebralBionicKidney", "Kidney#0", "ArchotechKidney", "Kidney#1")
                    && !Conflict("BOR_TenebralBionicKidney", "Kidney#0", "ArchotechLiver", "Liver#0"),
                    "T1 the tenebral kidney excludes the archotech kidney on the same kidney only");
            object arm = Entry("BOR_TenebralBionicArm");
            if (arm != null && Entry("BionicArm") != null)
            {
                string armSlot = SlotKeys(arm)[0];
                Check(Conflict("BOR_TenebralBionicArm", armSlot, "BionicArm", armSlot),
                    "T1 the tenebral arm excludes the bionic arm on " + armSlot);
            }
            if (Entry("ArmorskinGlandTenebral") != null)
                Check(Conflict("ArmorskinGlandTenebral", "Torso#0", "ArmorskinGland", "Torso#0")
                    && Conflict("ArmorskinGlandTenebral", "Torso#0", "ArchotechSkinImplant", "Torso#0")
                    && !Conflict("ArmorskinGlandTenebral", "Torso#0", "LTS_SubdermalArmour", "Torso#0"),
                    "T2 the tenebral armorskin gland excludes the vanilla gland and the archotech skin implant, not subdermal armour");
            else
                Write("T2 skipped: ArmorskinGlandTenebral absent (Beyond Our Reach's Royalty-variants setting is off)");
        }

        // P: the plan command the picker issues (the click IS the choice).
        ImplannerStore store = ImplannerStore.Current;
        PlannerModel model = store.Model;
        Plan plan = model.CreatePlan("Conflict probe", store.TakePlanId);
        model.SetImplantSlot(plan.Id, "ArchotechLiver", Slot("Liver#0"), true);
        model.SetImplantSlot(plan.Id, "ArchotechKidney", Slot("Kidney#0"), true);
        Check(Has(plan, "ArchotechLiver", "Liver#0") && Has(plan, "ArchotechKidney", "Kidney#0"),
            "P1 picking the archotech left kidney keeps the archotech liver planned");
        model.SetImplantSlot(plan.Id, "LTS_SubdermalArmour", Slot("Torso#0"), true);
        model.SetImplantSlot(plan.Id, "ArchotechSkinImplant", Slot("Torso#0"), true);
        Check(Has(plan, "LTS_SubdermalArmour", "Torso#0") && Has(plan, "ArchotechSkinImplant", "Torso#0"),
            "P2 the archotech skin implant and subdermal armour stay planned together");
        if (Entry("ArmorskinGland") != null)
        {
            model.SetImplantSlot(plan.Id, "ArmorskinGland", Slot("Torso#0"), true);
            Check(Has(plan, "ArmorskinGland", "Torso#0") && !Has(plan, "ArchotechSkinImplant", "Torso#0")
                && Has(plan, "LTS_SubdermalArmour", "Torso#0"),
                "P3 picking the armorskin gland replaces the archotech skin implant and nothing else");
        }
        if (bor && Entry("BOR_TenebralBionicKidney") != null)
        {
            model.SetImplantSlot(plan.Id, "BOR_TenebralBionicKidney", Slot("Kidney#0"), true);
            Check(Has(plan, "BOR_TenebralBionicKidney", "Kidney#0") && !Has(plan, "ArchotechKidney", "Kidney#0")
                && Has(plan, "ArchotechLiver", "Liver#0"),
                "P4 picking the tenebral kidney replaces the archotech kidney and keeps the liver");
        }
        Write("plan goals: " + Join(plan.Implants.Select(g => g.ImplantDefName + "[" + Join(g.Slots.Select(s => s.Key)) + "]")));
    }
}
