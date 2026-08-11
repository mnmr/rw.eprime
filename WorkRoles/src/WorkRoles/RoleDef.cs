using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    /// Recommendation tuning: skill classification, importance, time profile,
    /// holder scale, champion-penalty policy and the role's training path.
    public class RoleTuning
    {
        public RoleTuningSkills skills;
        public RoleCategory category;
        public RoleTime time;
        /// Legacy-ignored: named scales retired in favor of colonyMin/coverage.
        /// Kept so third-party defs still declaring it load without errors.
        public string scale;
        /// False = repeat championships use the occasional-work penalty.
        public bool championPenalty = true;
        /// Minimum biological age (years) for holding the role; -1 = derive
        /// from the covered work types' lowest vanilla unlock age at seed time.
        public int minAge = -1;
        /// Maximum biological age (years, inclusive) for holding the role;
        /// 0 = no gate.
        public int maxAge;
        /// Assignment scaling inputs (future scale replacement): minimum
        /// assignment count (0-30) and ideal colonist percentage (0-100).
        public int colonyMin;
        public int coverage;
        /// The role's own training path: this role plus its training roles
        /// with skill bands. Entries whose role is absent (DLC/mod gated) are
        /// skipped at seed time. Empty = the implicit self-only path.
        public List<RoleTrainingEntry> training = new List<RoleTrainingEntry>();
    }

    public class RoleTrainingEntry
    {
        /// RoleDef defName; entry order is the final stable tie-breaker.
        public string role;
        /// [min, max) skill band on the 0..21 axis (21 = open top).
        public int min;
        public int max = SkillProgressionMath.MaxLevel;
    }

    public class RoleTuningSkills
    {
        public List<string> required = new List<string>();
        public List<string> optional = new List<string>();
    }

    public class RoleDef : Def
    {
        /// Optional invariant persisted name when it intentionally differs from
        /// the readable form of defName. Never translate this field.
        public string seedLabel;
        /// Entries as "WorkType:DefName" or "WorkGiver:DefName" strings (tolerant of missing defs).
        public List<string> entries = new List<string>();
        public bool autoAssign;
        /// A PaletteDef defName ("slate-700"); preferred over the inline pair.
        public string colorRef;
        /// Inline one-off color (kept for mods that don't want a palette entry).
        public Color color = Color.white;
        public bool hasCustomColor;
        public string iconPath;

        /// Recommendation tuning block. Always non-null after PostLoad; defs
        /// without one get theirs built from the legacy fields below.
        public RoleTuning tuning;

        /// Legacy pre-tuning elements (third-party defs on the old schema).
        /// holderScale is legacy-ignored like tuning.scale; the penalty flag
        /// still migrates in PostLoad.
        public string holderScale;
        public bool usesOccasionalRepeatChampionPenalty;

        /// Blocker role: its jobs are never done and are vetoed in all later roles.
        public bool blocker;

        /// Recommendation-only template policy. These values are read through
        /// templateDefName and are not persisted into saves.
        public bool preserveRecommendationOrder;
        public RecommendationSpecialRoleKind recommendationSpecialRole;

        public override void PostLoad()
        {
            base.PostLoad();
            if (tuning == null)
            {
                tuning = new RoleTuning();
                if (usesOccasionalRepeatChampionPenalty) tuning.championPenalty = false;
            }
            tuning.skills ??= new RoleTuningSkills();
            tuning.skills.required ??= new List<string>();
            tuning.skills.optional ??= new List<string>();
            tuning.training ??= new List<RoleTrainingEntry>();
        }

        /// Role-list group name (resolved to a RoleGroupDef invariant name); empty = Default.
        public string group;
        /// Time rule: 24-char bitstring, hour 0 leftmost, '1' = active. Null = always.
        public string activeHours;
        /// Location rule: any of Settlements, Caravans. Empty = anywhere.
        public List<string> locations = new List<string>();

        public List<JobEntry> ParsedEntries()
        {
            var parsed = new List<JobEntry>();
            foreach (var raw in entries)
            {
                if (JobEntry.TryDecode(raw, out var entry)) parsed.Add(entry);
                else Log.Warning($"[WorkRoles] RoleDef {defName}: unparseable entry '{raw}'");
            }
            return parsed;
        }

        /// Stable fingerprint of the def's substance — everything copied onto
        /// roles at creation (colors excluded: color drift shouldn't read as
        /// role drift). Computed on demand, never stored in XML; saves stamp it
        /// per seeded role so later loads can tell def drift from player edits.
        public uint StableHash()
        {
            // Hash-input change is safe: templateHash has no readers yet.
            var text = string.Join("\n",
                defName, autoAssign ? "1" : "0", blocker ? "1" : "0", iconPath,
                SeededDefIdentity.GroupIdentity(this), activeHours, string.Join("|", locations),
                ((int)(tuning?.category ?? RoleCategory.None)).ToString(),
                ((int)(tuning?.time ?? RoleTime.None)).ToString(),
                tuning?.championPenalty == false ? "0" : "1",
                (tuning?.colonyMin ?? 0).ToString(),
                (tuning?.coverage ?? 0).ToString(),
                string.Join("|", tuning?.skills?.required ?? new List<string>()),
                string.Join("|", tuning?.skills?.optional ?? new List<string>()),
                string.Join("|", (tuning?.training ?? new List<RoleTrainingEntry>())
                    .Select(entry => entry.role + ":" + entry.min + ":" + entry.max)),
                string.Join("|", entries));
            return Seeding.Fnv1a(text);
        }

        /// The def's color: colorRef resolves through PaletteDef, else the
        /// inline color/hasCustomColor pair.
        public (bool has, Color color) ResolvedColor()
        {
            if (!colorRef.NullOrEmpty())
            {
                var palette = DefDatabase<PaletteDef>.GetNamedSilentFail(colorRef);
                if (palette != null) return (true, palette.color);
            }
            return (hasCustomColor, color);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var error in base.ConfigErrors())
                yield return error;
            if (!colorRef.NullOrEmpty()
                && DefDatabase<PaletteDef>.GetNamedSilentFail(colorRef) == null)
                yield return $"unknown colorRef '{colorRef}'";
            foreach (var skill in (tuning?.skills?.required ?? new List<string>())
                     .Concat(tuning?.skills?.optional ?? new List<string>()))
                if (DefDatabase<SkillDef>.GetNamedSilentFail(skill) == null)
                    yield return $"tuning skill '{skill}' matches no SkillDef";
            foreach (var entry in tuning?.training ?? new List<RoleTrainingEntry>())
                if (entry.role.NullOrEmpty())
                    yield return "training entry without a role";
        }
    }
}
