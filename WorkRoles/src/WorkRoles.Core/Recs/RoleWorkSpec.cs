using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Recs
{
    public enum RoleWorkContentKind : byte
    {
        Recipe,
        Plant,
        Buildable,
    }

    /// Presentation-only facts: no eligibility, ranking, or path rule reads
    /// effect flags. A missing or wrong flag is a cosmetic defect by design.
    [Flags]
    public enum RoleWorkEffect : byte
    {
        Unspecified = 0,
        Speed = 1,
        Quality = 2,
        Yield = 4,
        Success = 8,
    }

    public static class RoleWorkEffectRules
    {
        public static RoleWorkEffect ForRecipe(
            bool affectsSpeed,
            bool affectsYield,
            bool affectsQuality,
            bool affectsSuccess)
        {
            RoleWorkEffect effects = RoleWorkEffect.Unspecified;
            if (affectsSpeed) effects |= RoleWorkEffect.Speed;
            if (affectsYield) effects |= RoleWorkEffect.Yield;
            if (affectsQuality) effects |= RoleWorkEffect.Quality;
            if (affectsSuccess) effects |= RoleWorkEffect.Success;
            return effects;
        }
    }

    public enum RoleWorkCapabilityRequirement : byte
    {
        Any,
        All,
    }

    public readonly struct RoleSkillUseSpec
    {
        public RoleSkillUseSpec(string skillDefName, RoleWorkEffect effects)
        {
            SkillDefName = skillDefName;
            Effects = effects;
        }

        public string SkillDefName { get; }
        public RoleWorkEffect Effects { get; }
    }

    public readonly struct RoleContentGate
    {
        public RoleContentGate(string skillDefName, int minimumLevel)
        {
            SkillDefName = skillDefName;
            MinimumLevel = minimumLevel;
        }

        public string SkillDefName { get; }
        public int MinimumLevel { get; }
    }

    /// One piece of content reachable through a giver. Recipe contents carry
    /// the recipe's own skill facts; Plant and Buildable contents carry gates
    /// only (their soft skill facts belong to the giver).
    public sealed class RoleWorkContentSpec
    {
        public RoleWorkContentSpec(
            RoleWorkContentKind kind,
            string defName,
            string usedSkillDefName,
            RoleWorkEffect effects,
            bool trainsUsedSkill,
            IReadOnlyList<RoleContentGate> gates)
        {
            Kind = kind;
            DefName = defName;
            UsedSkillDefName = usedSkillDefName;
            Effects = effects;
            TrainsUsedSkill = trainsUsedSkill;
            Gates = gates ?? Array.Empty<RoleContentGate>();
        }

        public RoleWorkContentKind Kind { get; }
        public string DefName { get; }
        /// Recipe work skill; null for gate-only content.
        public string UsedSkillDefName { get; }
        public RoleWorkEffect Effects { get; }
        public bool TrainsUsedSkill { get; }
        public IReadOnlyList<RoleContentGate> Gates { get; }
    }

    /// One covered WorkGiver. UsedSkills and TrainedSkillDefNames are the
    /// distinct unions of the giver's own facts (direct givers) or its
    /// reachable contents' facts (bill givers); a skill appears once with its
    /// effects unioned.
    public sealed class RoleWorkGiverSpec
    {
        public RoleWorkGiverSpec(
            string workGiverDefName,
            IReadOnlyList<RoleSkillUseSpec> usedSkills,
            IReadOnlyList<string> trainedSkillDefNames,
            IReadOnlyList<RoleWorkContentSpec> contents)
        {
            WorkGiverDefName = workGiverDefName;
            UsedSkills = usedSkills ?? Array.Empty<RoleSkillUseSpec>();
            TrainedSkillDefNames = trainedSkillDefNames ?? Array.Empty<string>();
            Contents = contents ?? Array.Empty<RoleWorkContentSpec>();
        }

        public string WorkGiverDefName { get; }
        public IReadOnlyList<RoleSkillUseSpec> UsedSkills { get; }
        public IReadOnlyList<string> TrainedSkillDefNames { get; }
        /// Empty for direct givers and givers with no gate-bearing content.
        public IReadOnlyList<RoleWorkContentSpec> Contents { get; }
    }

    public sealed class RoleWorkCapabilitySpec
    {
        public RoleWorkCapabilitySpec(
            string workTypeDefName,
            int naturalPriority,
            bool includesWholeWorkType,
            IReadOnlyList<RoleWorkGiverSpec> givers)
        {
            WorkTypeDefName = workTypeDefName;
            NaturalPriority = naturalPriority;
            IncludesWholeWorkType = includesWholeWorkType;
            Givers = givers ?? Array.Empty<RoleWorkGiverSpec>();
        }

        /// The pawn must have this work type enabled to execute Givers.
        public string WorkTypeDefName { get; }
        public int NaturalPriority { get; }
        /// True when the role directly contains the complete work type rather
        /// than individual givers; preserves special-role classification input.
        public bool IncludesWholeWorkType { get; }
        public IReadOnlyList<RoleWorkGiverSpec> Givers { get; }
    }

    /// One skill's role-level facts. Counts are giver counts, never content
    /// counts; Participates is computed once by the builder, never mutated.
    public sealed class RoleSkillFact
    {
        public RoleSkillFact(
            string skillDefName,
            RoleWorkEffect effects,
            int usedGivers,
            int trainedGivers,
            int gatedContents,
            int importance,
            bool participates,
            bool primary)
        {
            SkillDefName = skillDefName;
            Effects = effects;
            UsedGivers = usedGivers;
            TrainedGivers = trainedGivers;
            GatedContents = gatedContents;
            Importance = importance;
            Participates = participates;
            Primary = primary;
        }

        public string SkillDefName { get; }
        /// Union of effect kinds recorded wherever this skill is used.
        /// Presentation-only; see RoleWorkEffect.
        public RoleWorkEffect Effects { get; }
        public int UsedGivers { get; }
        public int TrainedGivers { get; }
        public int GatedContents { get; }
        public int Importance { get; }
        /// Path contributions and compact display read Participates; signals,
        /// dampening, and champion overlap read the primary and gate-bearing
        /// facts, unchanged from the pre-spec engine.
        public bool Participates { get; }
        public bool Primary { get; }
    }

    /// The complete immutable work-facts projection for one role, shared by
    /// the recommendation engine and the role-options presentation.
    public sealed class RoleWorkSpec
    {
        public static readonly RoleWorkSpec Empty = new RoleWorkSpec(
            0,
            Array.Empty<RoleWorkCapabilitySpec>(),
            RoleWorkCapabilityRequirement.Any,
            Array.Empty<RoleSkillFact>(),
            Array.Empty<string>(),
            null,
            false);

        public RoleWorkSpec(
            int roleId,
            IReadOnlyList<RoleWorkCapabilitySpec> capabilities,
            RoleWorkCapabilityRequirement capabilityRequirement,
            IReadOnlyList<RoleSkillFact> skills,
            IReadOnlyList<string> assignmentSkillGates,
            string primarySkillDefName,
            bool isSkilled)
        {
            RoleId = roleId;
            Capabilities = capabilities ?? Array.Empty<RoleWorkCapabilitySpec>();
            CapabilityRequirement = capabilityRequirement;
            Skills = skills ?? Array.Empty<RoleSkillFact>();
            AssignmentSkillGates = assignmentSkillGates ?? Array.Empty<string>();
            PrimarySkillDefName = primarySkillDefName;
            IsSkilled = isSkilled;
            var workTypes = new string[Capabilities.Count];
            int maxPriority = 0;
            bool hunting = false;
            for (int index = 0; index < Capabilities.Count; index++)
            {
                RoleWorkCapabilitySpec capability = Capabilities[index];
                workTypes[index] = capability.WorkTypeDefName;
                if (capability.NaturalPriority > maxPriority)
                    maxPriority = capability.NaturalPriority;
                if (capability.WorkTypeDefName == "Hunting") hunting = true;
            }
            CapabilityWorkTypes = workTypes;
            MaxNaturalPriority = maxPriority;
            HasHuntingCapability = hunting;
        }

        public int RoleId { get; }
        public IReadOnlyList<RoleWorkCapabilitySpec> Capabilities { get; }
        /// All when the role is skilled (Hunting excepted); Any otherwise.
        /// Policy, not a fact: derived in one place by the builder.
        public RoleWorkCapabilityRequirement CapabilityRequirement { get; }
        /// Complete ordered facts: participating first, then importance, then
        /// ordinal defName. Full fidelity; nothing is dropped for display.
        public IReadOnlyList<RoleSkillFact> Skills { get; }
        /// User-authored enablement gates (Role.requiredSkills); no level.
        public IReadOnlyList<string> AssignmentSkillGates { get; }
        /// Primary among participating used skills; null when none.
        public string PrimarySkillDefName { get; }
        /// True when at least one participating skill has used evidence: the
        /// existing skilled/unskilled classification boundary, unchanged.
        public bool IsSkilled { get; }
        /// Capability work types in capability order; derived once.
        public IReadOnlyList<string> CapabilityWorkTypes { get; }
        public int MaxNaturalPriority { get; }
        public bool HasHuntingCapability { get; }

        public bool HasLiteralWorkType(string defName)
        {
            if (defName == null) return false;
            for (int index = 0; index < Capabilities.Count; index++)
                if (Capabilities[index].IncludesWholeWorkType
                    && Capabilities[index].WorkTypeDefName == defName)
                    return true;
            return false;
        }

        /// Content equality for the catalog's identity-preserving rebuilds:
        /// an equal rebuild must reuse the previously published instance.
        public static bool StructurallyEqual(RoleWorkSpec left, RoleWorkSpec right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            if (left.RoleId != right.RoleId
                || left.CapabilityRequirement != right.CapabilityRequirement
                || left.PrimarySkillDefName != right.PrimarySkillDefName
                || left.IsSkilled != right.IsSkilled
                || left.Capabilities.Count != right.Capabilities.Count
                || left.Skills.Count != right.Skills.Count
                || left.AssignmentSkillGates.Count != right.AssignmentSkillGates.Count)
                return false;
            for (int index = 0; index < left.AssignmentSkillGates.Count; index++)
                if (left.AssignmentSkillGates[index] != right.AssignmentSkillGates[index])
                    return false;
            for (int index = 0; index < left.Skills.Count; index++)
            {
                RoleSkillFact a = left.Skills[index];
                RoleSkillFact b = right.Skills[index];
                if (a.SkillDefName != b.SkillDefName
                    || a.Effects != b.Effects
                    || a.UsedGivers != b.UsedGivers
                    || a.TrainedGivers != b.TrainedGivers
                    || a.GatedContents != b.GatedContents
                    || a.Participates != b.Participates
                    || a.Primary != b.Primary)
                    return false;
            }
            for (int index = 0; index < left.Capabilities.Count; index++)
                if (!CapabilityEqual(
                        left.Capabilities[index], right.Capabilities[index]))
                    return false;
            return true;
        }

        private static bool CapabilityEqual(
            RoleWorkCapabilitySpec left, RoleWorkCapabilitySpec right)
        {
            if (left.WorkTypeDefName != right.WorkTypeDefName
                || left.NaturalPriority != right.NaturalPriority
                || left.IncludesWholeWorkType != right.IncludesWholeWorkType
                || left.Givers.Count != right.Givers.Count)
                return false;
            for (int index = 0; index < left.Givers.Count; index++)
            {
                RoleWorkGiverSpec a = left.Givers[index];
                RoleWorkGiverSpec b = right.Givers[index];
                if (a.WorkGiverDefName != b.WorkGiverDefName
                    || a.UsedSkills.Count != b.UsedSkills.Count
                    || a.TrainedSkillDefNames.Count != b.TrainedSkillDefNames.Count
                    || a.Contents.Count != b.Contents.Count)
                    return false;
                for (int at = 0; at < a.UsedSkills.Count; at++)
                    if (a.UsedSkills[at].SkillDefName != b.UsedSkills[at].SkillDefName
                        || a.UsedSkills[at].Effects != b.UsedSkills[at].Effects)
                        return false;
                for (int at = 0; at < a.TrainedSkillDefNames.Count; at++)
                    if (a.TrainedSkillDefNames[at] != b.TrainedSkillDefNames[at])
                        return false;
                for (int at = 0; at < a.Contents.Count; at++)
                    if (!ContentEqual(a.Contents[at], b.Contents[at]))
                        return false;
            }
            return true;
        }

        private static bool ContentEqual(
            RoleWorkContentSpec left, RoleWorkContentSpec right)
        {
            if (left.Kind != right.Kind
                || left.DefName != right.DefName
                || left.UsedSkillDefName != right.UsedSkillDefName
                || left.Effects != right.Effects
                || left.TrainsUsedSkill != right.TrainsUsedSkill
                || left.Gates.Count != right.Gates.Count)
                return false;
            for (int index = 0; index < left.Gates.Count; index++)
                if (left.Gates[index].SkillDefName != right.Gates[index].SkillDefName
                    || left.Gates[index].MinimumLevel != right.Gates[index].MinimumLevel)
                    return false;
            return true;
        }
    }
}
