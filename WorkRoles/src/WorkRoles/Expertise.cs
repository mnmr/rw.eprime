using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles
{
    /// Read-only reflection bridge to Vanilla Skills Expanded's expertise system
    /// (per-pawn skill specializations like "Chef", each tied to one SkillDef).
    /// No hard dependency: everything degrades to "no expertise" when VSE is
    /// absent or its internals change.
    public static class Expertise
    {
        public readonly struct Entry
        {
            public readonly SkillDef Skill;
            public readonly string Label;
            public readonly string Description; // rich tooltip (name, level, effects)

            public Entry(SkillDef skill, string label, string description)
            {
                Skill = skill;
                Label = label;
                Description = description;
            }
        }

        private static bool initialized;
        private static MethodInfo trackerOf;        // static ExpertiseTrackers.Expertise(Pawn)
        private static PropertyInfo allExpertise;   // ExpertiseTracker.AllExpertise -> List<ExpertiseRecord>
        private static FieldInfo recordDef;         // ExpertiseRecord.def
        private static MethodInfo fullDescription;  // ExpertiseRecord.FullDescription(), when parameterless
        private static FieldInfo defSkill;          // ExpertiseDef.skill

        private static readonly List<Entry> Empty = new List<Entry>();

        // Open-window snapshot (UiVersion): reflection on a per-cell/per-pass
        // path is banned — skill cells and fit tooltips call For constantly.
        private static readonly Dictionary<Pawn, List<Entry>> cache = new Dictionary<Pawn, List<Entry>>();
        private static int cacheStamp = -1;

        /// Window open: expertise gained since the last snapshot must show.
        internal static void InvalidateSnapshot() => cacheStamp = -1;

        /// The pawn's expertises; empty when VSE is absent or the pawn has none.
        public static List<Entry> For(Pawn pawn)
        {
            EnsureInit();
            if (trackerOf == null || pawn == null || pawn.skills == null) return Empty;
            if (cacheStamp != UiVersion.Current)
            {
                cache.Clear();
                cacheStamp = UiVersion.Current;
            }
            if (!cache.TryGetValue(pawn, out var entries))
                cache[pawn] = entries = ForUncached(pawn);
            return entries;
        }

        private static List<Entry> ForUncached(Pawn pawn)
        {
            try
            {
                var tracker = trackerOf.Invoke(null, new object[] { pawn });
                if (allExpertise.GetValue(tracker) is not IList records || records.Count == 0)
                    return Empty;

                var result = new List<Entry>(records.Count);
                foreach (var record in records)
                {
                    if (record == null) continue;
                    var def = recordDef.GetValue(record) as Def;
                    var skill = def != null ? defSkill.GetValue(def) as SkillDef : null;
                    if (skill == null) continue;
                    string label = def.LabelCap;
                    string description = fullDescription?.Invoke(record, null) as string ?? label;
                    result.Add(new Entry(skill, label, description));
                }
                return result;
            }
            catch (Exception e)
            {
                Log.Warning($"[WorkRoles] VSE expertise integration failed, expertise will be ignored: {e}");
                trackerOf = null; // disable further attempts
                return Empty;
            }
        }

        private static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                var trackers = AccessTools.TypeByName("VSE.Expertise.ExpertiseTrackers");
                if (trackers == null) return;
                trackerOf = AccessTools.Method(trackers, "Expertise", new[] { typeof(Pawn) });

                var trackerType = AccessTools.TypeByName("VSE.Expertise.ExpertiseTracker");
                var recordType = AccessTools.TypeByName("VSE.Expertise.ExpertiseRecord");
                var defType = AccessTools.TypeByName("VSE.Expertise.ExpertiseDef");
                allExpertise = AccessTools.Property(trackerType, "AllExpertise");
                recordDef = AccessTools.Field(recordType, "def");
                defSkill = AccessTools.Field(defType, "skill");
                var describe = AccessTools.Method(recordType, "FullDescription");
                if (describe != null && describe.GetParameters().Length == 0)
                    fullDescription = describe;

                if (trackerOf == null || allExpertise == null || recordDef == null || defSkill == null)
                {
                    Log.Warning("[WorkRoles] VSE expertise API not found in the expected shape; expertise will be ignored");
                    trackerOf = null;
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[WorkRoles] VSE expertise integration failed, expertise will be ignored: {e}");
                trackerOf = null;
            }
        }
    }
}
