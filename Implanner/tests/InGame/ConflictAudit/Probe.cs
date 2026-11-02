using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using Implanner;
using Implanner.Core;
using RimWorld;
using Verse;

namespace ConflictAuditProbe;

// Mod-agnostic oracle for the planned-slot conflict rule: for every pair of
// catalog kinds that can meet on one anatomy instance (same record, or one
// kind's record under the other's when either clears its part), install one
// through its real surgery worker on a test pawn, ask the other kind's
// worker whether its record is still offered, install it too and check both
// hediffs survived; in both orders. The game's answer is "coexist" when some
// order keeps both. Implanner must say conflict exactly when no order does.
//  STRICT: Implanner excludes a pair the game lets coexist (in some order).
//  LAX:    Implanner allows a pair the game never keeps together.
//  ORDER:  the game keeps the pair only in one order (automation must put
//          them in that way round; informational, with the failing step).
// Pairs of two plain implants installed by the unmodified vanilla implant
// worker with no incompatibility or removal tags cannot be refused by the
// game and are counted as assumed rather than installed.
// Runs on the plannable bodies named in conflict-audit-bodies.txt (all when
// absent) for which a pawn can be generated, in per-frame slices with time
// running (the run guard wants frames and ticks), within a fixed budget;
// each body also reports its catalog slots missing or extra versus human,
// and the kinds whose surgery is not offered on a fresh pawn of that body.
[StaticConstructorOnStartup]
public static class Probe
{
    const string Package = "eprime.implanner.conflictaudit";
    const double BudgetSeconds = 46;
    const int SliceMilliseconds = 200;
    static string Root => GenFilePaths.SaveDataFolderPath;
    static readonly Type Catalogs = AccessTools.TypeByName("Implanner.Catalogs");
    static readonly Type Conflicts = AccessTools.TypeByName("Implanner.ImplantConflicts");
    static readonly List<string> lines = new();
    static int phase, failures;
    static Stopwatch clock;

    static Probe() => new Harmony(Package).Patch(
        AccessTools.Method(typeof(Root_Play), "Update"),
        postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    static void Write(string message)
    {
        Log.Message("[ConflictAudit] " + message);
        lines.Add(message);
    }

    static void Update()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing
            || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            foreach (Window window in Find.WindowStack.Windows.ToArray())
                if (!(window is MainTabWindow)) window.Close(false);
            if (phase == 0) Init();
            var slice = Stopwatch.StartNew();
            while (phase != 3 && slice.ElapsedMilliseconds < SliceMilliseconds) Step();
            if (phase == 3) Finish();
        }
        catch (Exception ex)
        {
            Write("ERROR " + ex);
            failures++;
            Finish();
        }
    }

    static void Finish()
    {
        if (pawn != null) { pawn.Destroy(); pawn = null; }
        phase = 99;
        Find.TickManager.Pause();
        string status = failures == 0 ? "SUCCESS" : "FAILED " + failures;
        Write(status + " after " + clock.Elapsed.TotalSeconds.ToString("0") + " s");
        File.WriteAllLines(Path.Combine(Root, "conflict-audit.txt"), lines);
        File.WriteAllText(Path.Combine(Root, "conflict-audit-stage.txt"), status);
    }

    // ------------------------------------------------------- catalog access

    sealed class Kind
    {
        public HediffDef Def;
        public bool Replacement, Wipes;
        public List<RecipeDef> Recipes;
        public List<(PartAddress Address, BodyPartDef Part)> Slots;
        public string Mod => Def.modContentPack?.Name ?? "?";
        public bool Clears => Replacement || Wipes;
    }

    sealed class Probe1
    {
        public Kind Kind;
        public PartAddress Address;
        public BodyPartRecord Record;
        public RecipeDef Recipe;      // the surgery that offers Record on a fresh pawn
        public int Index;
        public List<Probe1> Partners = new();
        /// Plain implant through the unmodified vanilla worker with no tags:
        /// the game cannot refuse it next to another such kind.
        public bool Trivial => !Kind.Clears && Recipe.Worker.GetType() == typeof(Recipe_InstallImplant)
            && Recipe.incompatibleWithHediffTags.NullOrEmpty() && Kind.Def.removeWithTags.NullOrEmpty()
            && Kind.Def.tags.NullOrEmpty();
    }

    static List<Kind> Kinds()
    {
        var result = new List<Kind>();
        foreach (object entry in (IList)AccessTools.Method(Catalogs, "Implants").Invoke(null, null))
        {
            var kind = new Kind
            {
                Def = (HediffDef)Traverse.Create(entry).Property("Def").GetValue(),
                Replacement = Traverse.Create(entry).Property<bool>("IsReplacement").Value,
                Wipes = Traverse.Create(entry).Property<bool>("WipesPart").Value,
                Recipes = (List<RecipeDef>)Traverse.Create(entry).Property("SurgeryRecipes").GetValue(),
                Slots = new List<(PartAddress, BodyPartDef)>(),
            };
            foreach (object slot in (IList)Traverse.Create(entry).Property("Slots").GetValue())
                kind.Slots.Add((Traverse.Create(slot).Property<PartAddress>("Address").Value,
                    (BodyPartDef)Traverse.Create(slot).Property("Part").GetValue()));
            result.Add(kind);
        }
        return result;
    }

    static bool ImplannerConflict(string body, Probe1 a, Probe1 b) => (bool)AccessTools
        .Method(Conflicts, "Conflicts",
            new[] { typeof(string), typeof(string), typeof(PartAddress), typeof(string), typeof(PartAddress) })
        .Invoke(null, new object[] { body, a.Kind.Def.defName, a.Address, b.Kind.Def.defName, b.Address });

    static HashSet<string> CarriedKeys(BodyDef on)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (Kind kind in kinds)
            foreach ((PartAddress address, BodyPartDef part) in kind.Slots)
                if (address.Occurrence < on.GetPartsWithDef(part).Count) keys.Add(address.Key);
        return keys;
    }

    // ------------------------------------------------------------ the pawn

    static Pawn pawn;
    static BodyDef body;

    static bool Offers(RecipeDef recipe, BodyPartRecord record)
    {
        try { return recipe.Worker.GetPartsToApplyOn(pawn, recipe).Contains(record); }
        catch { return false; }
    }

    static bool Apply(RecipeDef recipe, BodyPartRecord record)
    {
        try { recipe.Worker.ApplyOnPawn(pawn, record, null, new List<Thing>(), null); return true; }
        catch { return false; }
    }

    static bool Present(HediffDef def, BodyPartRecord record)
    {
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
            if (hediffs[i].def == def && hediffs[i].Part == record) return true;
        return false;
    }

    /// Back to a natural body: RestorePart skips keepOnBodyPartRestoration
    /// hediffs (FSF's immortality core under Anomaly), so those go by hand.
    static void Reset()
    {
        pawn.health.RestorePart(body.corePart);
        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = hediffs.Count - 1; i >= 0; i--)
            if (hediffs[i].Part != null) pawn.health.RemoveHediff(hediffs[i]);
    }

    static bool IsAncestor(BodyPartRecord ancestor, BodyPartRecord record)
    {
        for (BodyPartRecord p = record.parent; p != null; p = p.parent)
            if (p == ancestor) return true;
        return false;
    }

    static Pawn MakePawn(BodyDef wanted)
    {
        foreach (PawnKindDef kind in DefDatabase<PawnKindDef>.AllDefsListForReading)
        {
            if (kind.race?.race == null || !kind.race.race.Humanlike || kind.race.race.body != wanted) continue;
            try
            {
                var request = new PawnGenerationRequest(kind, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true, allowDead: false, allowDowned: false, canGeneratePawnRelations: false,
                    colonistRelationChanceFactor: 0f, fixedBiologicalAge: 30f, fixedChronologicalAge: 30f);
                Pawn p = PawnGenerator.GeneratePawn(request);
                if (p?.health?.hediffSet != null && p.RaceProps.body == wanted) return p;
            }
            catch (Exception ex) { Write("pawn for " + wanted.defName + " via " + kind.defName + " failed: " + ex.Message); }
        }
        return null;
    }

    // ---------------------------------------------------------- the sweep

    static List<Kind> kinds;
    static List<BodyDef> bodies;
    static HashSet<string> humanKeys;
    static int bodyIndex, shard, shards = 1;
    static List<Probe1> probes;
    static int pairCount, assumed, measured, firstIndex, partnerIndex;
    static bool firstInstalled, exhausted;
    /// Ordered result: null = both kept, else why not.
    static Dictionary<(int, int), string> outcome;

    static void Init()
    {
        clock = Stopwatch.StartNew();
        DebugSettings.pauseOnError = false;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        kinds = Kinds();
        bodies = (List<BodyDef>)AccessTools.Method(Catalogs, "PlannableBodies").Invoke(null, null);
        Write("catalog kinds: " + kinds.Count + "; plannable bodies: " + string.Join(", ", bodies.Select(b => b.defName)));
        BodyDef human = bodies.FirstOrDefault(b => b.defName == "Human") ?? bodies[0];
        humanKeys = CarriedKeys(human);
        string selection = Path.Combine(Root, "conflict-audit-bodies.txt");
        if (File.Exists(selection))
        {
            var wanted = new HashSet<string>(File.ReadAllText(selection)
                .Split(new[] { ',', ';', '\r', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            bodies = bodies.Where(b => wanted.Contains(b.defName)).ToList();
            Write("auditing bodies: " + string.Join(", ", bodies.Select(b => b.defName)));
        }
        // "k/n": only pairs whose index sum is k modulo n, both directions,
        // so a large catalog is covered by n runs.
        string shardFile = Path.Combine(Root, "conflict-audit-shard.txt");
        if (File.Exists(shardFile))
        {
            string[] parts = File.ReadAllText(shardFile).Trim().Split('/');
            shard = int.Parse(parts[0]);
            shards = int.Parse(parts[1]);
            Write("shard " + shard + " of " + shards);
        }
        bodyIndex = 0;
        phase = 1;
    }

    static void Step()
    {
        if (phase == 1) { BeginBody(); return; }
        if (phase == 2) { Measure(); return; }
    }

    static void BeginBody()
    {
        if (bodyIndex >= bodies.Count) { phase = 3; return; }
        if (clock.Elapsed.TotalSeconds > BudgetSeconds)
        {
            Write("budget exhausted before body " + bodies[bodyIndex].defName);
            phase = 3;
            return;
        }
        body = bodies[bodyIndex];
        HashSet<string> keys = CarriedKeys(body);
        var missing = humanKeys.Where(k => !keys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var extra = keys.Where(k => !humanKeys.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Write("body " + body.defName + ": " + body.AllParts.Count + " parts, " + keys.Count + " catalog slots; versus human missing "
            + missing.Count + (missing.Count > 0 ? " (" + string.Join(", ", missing.Take(40)) + (missing.Count > 40 ? ", ..." : "") + ")" : "")
            + ", extra " + extra.Count + (extra.Count > 0 ? " (" + string.Join(", ", extra.Take(40)) + (extra.Count > 40 ? ", ..." : "") + ")" : ""));
        pawn = MakePawn(body);
        if (pawn == null)
        {
            Write("body " + body.defName + ": no pawn kind generates it; skipped");
            bodyIndex++;
            return;
        }
        Write("body " + body.defName + ": pawn " + pawn.kindDef.defName + " (" + pawn.def.defName + ")");
        Reset();

        // Every (kind, slot) this body carries whose surgery offers the
        // record on the fresh pawn.
        probes = new List<Probe1>();
        var notOffered = new List<string>();
        foreach (Kind kind in kinds)
            foreach ((PartAddress address, BodyPartDef part) in kind.Slots)
            {
                List<BodyPartRecord> records = body.GetPartsWithDef(part);
                if (address.Occurrence >= records.Count) continue;
                BodyPartRecord record = records[address.Occurrence];
                RecipeDef recipe = null;
                foreach (RecipeDef candidate in kind.Recipes)
                    if (candidate.appliedOnFixedBodyParts != null && candidate.appliedOnFixedBodyParts.Contains(part)
                        && Offers(candidate, record)) { recipe = candidate; break; }
                if (recipe == null) { notOffered.Add(kind.Def.defName + "@" + address.Key); continue; }
                probes.Add(new Probe1 { Kind = kind, Address = address, Record = record, Recipe = recipe, Index = probes.Count });
            }
        Write("body " + body.defName + ": " + probes.Count + " installable kind-slots; not offered on a fresh pawn: "
            + notOffered.Count + (notOffered.Count > 0 ? " (" + string.Join(", ", notOffered.Take(60)) + (notOffered.Count > 60 ? ", ..." : "") + ")" : ""));

        // Related pairs: same record, or ancestry with a part-clearing kind.
        pairCount = 0;
        assumed = 0;
        for (int i = 0; i < probes.Count; i++)
            for (int j = i + 1; j < probes.Count; j++)
            {
                Probe1 a = probes[i], b = probes[j];
                if (a.Kind == b.Kind) continue;
                bool same = a.Record == b.Record;
                bool clears = a.Kind.Clears || b.Kind.Clears;
                if (!(same || (clears && (IsAncestor(a.Record, b.Record) || IsAncestor(b.Record, a.Record))))) continue;
                if ((i + j) % shards != shard) continue;
                pairCount++;
                if (a.Trivial && b.Trivial) { assumed++; continue; }
                a.Partners.Add(b);
                b.Partners.Add(a);
            }
        Write("body " + body.defName + ": " + pairCount + " related pairs" + (shards > 1 ? " in this shard" : "") + ", " + assumed
            + " assumed coexisting (plain vanilla implants), " + (pairCount - assumed) + " to install");
        outcome = new Dictionary<(int, int), string>();
        measured = 0;
        firstIndex = 0;
        partnerIndex = 0;
        firstInstalled = false;
        exhausted = false;
        phase = 2;
    }

    /// One ordered install (first already installed, then one partner).
    static void Measure()
    {
        if (firstIndex >= probes.Count || clock.Elapsed.TotalSeconds > BudgetSeconds)
        {
            exhausted = firstIndex < probes.Count;
            EndBody();
            return;
        }
        Probe1 first = probes[firstIndex];
        if (!firstInstalled)
        {
            if (first.Partners.Count == 0) { firstIndex++; return; }
            Reset();
            if (!Apply(first.Recipe, first.Record) || !Present(first.Kind.Def, first.Record))
            {
                firstIndex++;
                return;
            }
            firstInstalled = true;
            partnerIndex = 0;
        }
        if (partnerIndex >= first.Partners.Count)
        {
            firstIndex++;
            firstInstalled = false;
            return;
        }
        Probe1 second = first.Partners[partnerIndex++];
        string why;
        if (!Offers(second.Recipe, second.Record)) why = "second not offered";
        else if (!Apply(second.Recipe, second.Record)) why = "second's surgery threw";
        else
        {
            bool p1 = Present(first.Kind.Def, first.Record), p2 = Present(second.Kind.Def, second.Record);
            why = p1 && p2 ? null : !p1 && !p2 ? "both gone" : !p1 ? "first removed" : "second not added";
        }
        outcome[(first.Index, second.Index)] = why;
        measured++;
        // Back to "first installed" for the next partner.
        Reset();
        Apply(first.Recipe, first.Record);
    }

    static void EndBody()
    {
        Reset();
        var strict = new List<string>();
        var lax = new List<string>();
        var order = new List<string>();
        int compared = 0;
        foreach (Probe1 a in probes)
            foreach (Probe1 b in a.Partners)
            {
                if (a.Index >= b.Index) continue;
                if (!outcome.TryGetValue((a.Index, b.Index), out string ab) || !outcome.TryGetValue((b.Index, a.Index), out string ba)) continue;
                compared++;
                bool abKeeps = ab == null, baKeeps = ba == null;
                bool coexist = abKeeps || baKeeps;
                bool conflict = ImplannerConflict(body.defName, a, b);
                string pair = a.Kind.Def.defName + "@" + a.Address.Key + " + " + b.Kind.Def.defName + "@" + b.Address.Key
                    + " [" + a.Kind.Mod + " / " + b.Kind.Mod + "]";
                string detail = abKeeps && baKeeps ? "" : abKeeps ? " (only A then B; B then A: " + ba + ")" : baKeeps ? " (only B then A; A then B: " + ab + ")" : " (A then B: " + ab + "; B then A: " + ba + ")";
                if (conflict && coexist) strict.Add(pair + detail);
                else if (!conflict && !coexist) lax.Add(pair + detail);
                else if (!conflict && !(abKeeps && baKeeps)) order.Add(pair + detail);
            }
        Write("body " + body.defName + ": compared " + compared + " of " + (pairCount - assumed) + " installed pairs (" + measured + " ordered installs"
            + (exhausted ? ", budget exhausted" : "") + ") at " + clock.Elapsed.TotalSeconds.ToString("0") + " s");
        Report("STRICT (Implanner excludes, game coexists)", strict);
        Report("LAX (Implanner allows, game never keeps both)", lax);
        Report("ORDER (coexist in one order only)", order);
        if (strict.Count > 0 || lax.Count > 0) failures++;
        pawn.Destroy();
        pawn = null;
        bodyIndex++;
        phase = exhausted ? 3 : 1;
    }

    static void Report(string title, List<string> items)
    {
        Write(title + ": " + items.Count);
        foreach (string item in items.Take(250)) Write("  " + item);
        if (items.Count > 250) Write("  ... " + (items.Count - 250) + " more");
    }
}
