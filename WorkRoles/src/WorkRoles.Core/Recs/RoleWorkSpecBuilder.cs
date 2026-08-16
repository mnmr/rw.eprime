using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Recs
{
    /// Builds the immutable RoleWorkSpec from expanded coverage and the job
    /// profile index. Owns the retained evidence weighting: skill-less givers
    /// weigh 0, XP-training givers weigh 4, participation at half the role's
    /// skilled weight. The primary-by-importance used skill always
    /// participates, so a role with used evidence always keeps a decisive
    /// skill even when several minority skills split below the share bar.
    public static class RoleWorkSpecBuilder
    {
        private sealed class SkillTotals
        {
            internal string SkillDefName;
            internal RoleWorkEffect Effects;
            internal int UsedGivers;
            internal int TrainedGivers;
            internal int GatedContents;
            internal int WeightedGivers;
            internal int Importance;
            internal bool Participates;
        }

        public static RoleWorkSpec Build(
            int roleId,
            IReadOnlyList<string> orderedGivers,
            IReadOnlyList<string> seedWorkTypes,
            IReadOnlyList<string> literalWorkTypes,
            Func<string, string> workTypeOf,
            IReadOnlyDictionary<string, int> naturalPriorities,
            IReadOnlyDictionary<string, JobProfileGiverFacts> giverFacts,
            JobProfileIndex index,
            IReadOnlyList<string> assignmentSkillGates,
            ISet<string> excludedProfileGivers = null)
        {
            if (workTypeOf == null)
                throw new ArgumentNullException(nameof(workTypeOf));
            var literalSet = new HashSet<string>(StringComparer.Ordinal);
            if (literalWorkTypes != null)
                foreach (string literal in literalWorkTypes)
                    if (!string.IsNullOrEmpty(literal)) literalSet.Add(literal);
            var capabilityOrder = new List<string>();
            var giversByWorkType =
                new Dictionary<string, List<RoleWorkGiverSpec>>(
                    StringComparer.Ordinal);
            // Entry-declared work types seed capabilities even when no giver
            // resolves under them (unknown or empty work types stay real
            // capability requirements).
            if (seedWorkTypes != null)
                foreach (string seed in seedWorkTypes)
                    if (!string.IsNullOrEmpty(seed)
                        && !giversByWorkType.ContainsKey(seed))
                    {
                        giversByWorkType.Add(seed, new List<RoleWorkGiverSpec>());
                        capabilityOrder.Add(seed);
                    }
            var seenGivers = new HashSet<string>(StringComparer.Ordinal);
            if (orderedGivers != null)
                foreach (string giverName in orderedGivers)
                {
                    if (string.IsNullOrEmpty(giverName)
                        || !seenGivers.Add(giverName))
                        continue;
                    string workType = workTypeOf(giverName);
                    if (workType == null) continue;
                    if (!giversByWorkType.TryGetValue(
                            workType, out List<RoleWorkGiverSpec> givers))
                    {
                        givers = new List<RoleWorkGiverSpec>();
                        giversByWorkType.Add(workType, givers);
                        capabilityOrder.Add(workType);
                    }
                    givers.Add(GiverSpecOf(giverName, giverFacts, index));
                }

            var capabilities =
                new List<RoleWorkCapabilitySpec>(capabilityOrder.Count);
            foreach (string workType in capabilityOrder)
            {
                int priority = 0;
                naturalPriorities?.TryGetValue(workType, out priority);
                capabilities.Add(new RoleWorkCapabilitySpec(
                    workType,
                    priority,
                    literalSet.Contains(workType),
                    ReadOnly(giversByWorkType[workType])));
            }
            return Assemble(
                roleId,
                capabilities,
                assignmentSkillGates,
                (caps, totals) => WeightedTotals(
                    caps, totals, excludedProfileGivers));
        }

        /// Assembles a spec from ready-made capability structures, applying
        /// the standard weighting; for hand-built fixtures and adapters that
        /// already resolved their givers.
        public static RoleWorkSpec Build(
            int roleId,
            IReadOnlyList<RoleWorkCapabilitySpec> capabilities,
            IReadOnlyList<string> assignmentSkillGates)
            => Assemble(
                capabilities == null
                    ? new List<RoleWorkCapabilitySpec>()
                    : new List<RoleWorkCapabilitySpec>(capabilities),
                roleId,
                assignmentSkillGates);

        private static RoleWorkSpec Assemble(
            List<RoleWorkCapabilitySpec> capabilities,
            int roleId,
            IReadOnlyList<string> assignmentSkillGates)
            => Assemble(
                roleId,
                capabilities,
                assignmentSkillGates,
                (caps, totals) => WeightedTotals(caps, totals, null));

        /// Composite spec: union of member givers by (workType, giver) key.
        /// Participation is preserved per member so a bundle of specialists
        /// keeps each specialist's participating skills.
        public static RoleWorkSpec Merge(
            int roleId,
            IReadOnlyList<RoleWorkSpec> memberSpecs,
            IReadOnlyList<string> assignmentSkillGates)
        {
            var capabilityOrder = new List<string>();
            var giversByWorkType =
                new Dictionary<string, List<RoleWorkGiverSpec>>(
                    StringComparer.Ordinal);
            var literalWorkTypes = new HashSet<string>(StringComparer.Ordinal);
            var priorities = new Dictionary<string, int>(StringComparer.Ordinal);
            var seenGivers = new HashSet<string>(StringComparer.Ordinal);
            var memberParticipation =
                new HashSet<string>(StringComparer.Ordinal);
            if (memberSpecs != null)
                foreach (RoleWorkSpec member in memberSpecs)
                {
                    if (member == null) continue;
                    foreach (RoleSkillFact fact in member.Skills)
                        if (fact.Participates)
                            memberParticipation.Add(fact.SkillDefName);
                    foreach (RoleWorkCapabilitySpec capability
                        in member.Capabilities)
                    {
                        if (!giversByWorkType.TryGetValue(
                                capability.WorkTypeDefName,
                                out List<RoleWorkGiverSpec> givers))
                        {
                            givers = new List<RoleWorkGiverSpec>();
                            giversByWorkType.Add(
                                capability.WorkTypeDefName, givers);
                            capabilityOrder.Add(capability.WorkTypeDefName);
                        }
                        if (capability.IncludesWholeWorkType)
                            literalWorkTypes.Add(capability.WorkTypeDefName);
                        if (!priorities.TryGetValue(
                                capability.WorkTypeDefName, out int priority)
                            || capability.NaturalPriority > priority)
                            priorities[capability.WorkTypeDefName] =
                                capability.NaturalPriority;
                        foreach (RoleWorkGiverSpec giver in capability.Givers)
                            if (seenGivers.Add(
                                capability.WorkTypeDefName + "/"
                                + giver.WorkGiverDefName))
                                givers.Add(giver);
                    }
                }

            var capabilities =
                new List<RoleWorkCapabilitySpec>(capabilityOrder.Count);
            foreach (string workType in capabilityOrder)
                capabilities.Add(new RoleWorkCapabilitySpec(
                    workType,
                    priorities[workType],
                    literalWorkTypes.Contains(workType),
                    ReadOnly(giversByWorkType[workType])));
            return Assemble(
                roleId,
                capabilities,
                assignmentSkillGates,
                (caps, totals) => MemberTotals(caps, totals, memberParticipation));
        }

        private static RoleWorkSpec Assemble(
            int roleId,
            List<RoleWorkCapabilitySpec> capabilities,
            IReadOnlyList<string> assignmentSkillGates,
            Action<List<RoleWorkCapabilitySpec>, List<SkillTotals>> totalsOf)
        {
            var totals = new List<SkillTotals>();
            totalsOf(capabilities, totals);

            SkillTotals primary = null;
            foreach (SkillTotals skill in totals)
            {
                skill.Importance = skill.UsedGivers + 2 * skill.TrainedGivers
                    + skill.GatedContents;
                if (skill.UsedGivers > 0 && Better(skill, primary))
                    primary = skill;
            }
            if (primary != null) primary.Participates = true;

            totals.Sort((left, right) =>
            {
                if (left == primary != (right == primary))
                    return left == primary ? -1 : 1;
                if (left.Participates != right.Participates)
                    return left.Participates ? -1 : 1;
                if (left.Importance != right.Importance)
                    return right.Importance - left.Importance;
                return string.CompareOrdinal(
                    left.SkillDefName, right.SkillDefName);
            });

            bool isSkilled = false;
            var facts = new List<RoleSkillFact>(totals.Count);
            foreach (SkillTotals skill in totals)
            {
                if (skill.UsedGivers > 0) isSkilled = true;
                facts.Add(new RoleSkillFact(
                    skill.SkillDefName,
                    skill.Effects,
                    skill.UsedGivers,
                    skill.TrainedGivers,
                    skill.GatedContents,
                    skill.Importance,
                    skill.Participates,
                    skill == primary));
            }

            bool hunting = false;
            foreach (RoleWorkCapabilitySpec capability in capabilities)
                if (capability.WorkTypeDefName == "Hunting") hunting = true;
            return new RoleWorkSpec(
                roleId,
                ReadOnly(capabilities),
                isSkilled && !hunting
                    ? RoleWorkCapabilityRequirement.All
                    : RoleWorkCapabilityRequirement.Any,
                ReadOnly(facts),
                CopyGates(assignmentSkillGates),
                primary?.SkillDefName,
                isSkilled);
        }

        /// Base roles: giver counts with the weighted share filter. A giver
        /// with no skill facts weighs 0, an XP-training giver weighs 4.
        /// Excluded givers (a training role's target work) keep their unit in
        /// the capability but contribute nothing to the profile.
        private static void WeightedTotals(
            List<RoleWorkCapabilitySpec> capabilities,
            List<SkillTotals> totals,
            ISet<string> excludedProfileGivers)
        {
            var bySkill = new Dictionary<string, SkillTotals>(
                StringComparer.Ordinal);
            int roleWeight = 0;
            foreach (RoleWorkCapabilitySpec capability in capabilities)
                foreach (RoleWorkGiverSpec giver in capability.Givers)
                {
                    if (excludedProfileGivers != null
                        && excludedProfileGivers.Contains(
                            giver.WorkGiverDefName))
                        continue;
                    bool gated = HasGates(giver);
                    if (giver.UsedSkills.Count == 0
                        && giver.TrainedSkillDefNames.Count == 0
                        && !gated)
                        continue;
                    int weight = giver.TrainedSkillDefNames.Count > 0 ? 4 : 1;
                    roleWeight += weight;
                    var touched = new HashSet<string>(StringComparer.Ordinal);
                    foreach (RoleSkillUseSpec use in giver.UsedSkills)
                    {
                        SkillTotals skill = Of(bySkill, totals, use.SkillDefName);
                        if (skill == null) continue;
                        skill.UsedGivers++;
                        skill.Effects |= use.Effects;
                        if (touched.Add(use.SkillDefName))
                            skill.WeightedGivers += weight;
                    }
                    foreach (string trained in giver.TrainedSkillDefNames)
                    {
                        SkillTotals skill = Of(bySkill, totals, trained);
                        if (skill == null) continue;
                        skill.TrainedGivers++;
                        if (touched.Add(trained))
                            skill.WeightedGivers += weight;
                    }
                    AddGateTotals(giver, bySkill, totals, touched, weight);
                }
            foreach (SkillTotals skill in totals)
                skill.Participates = skill.WeightedGivers * 2 >= roleWeight;
        }

        /// Composite roles: counts recomputed from the deduplicated giver
        /// union; participation copied from the members that supplied them.
        private static void MemberTotals(
            List<RoleWorkCapabilitySpec> capabilities,
            List<SkillTotals> totals,
            HashSet<string> memberParticipation)
        {
            var bySkill = new Dictionary<string, SkillTotals>(
                StringComparer.Ordinal);
            foreach (RoleWorkCapabilitySpec capability in capabilities)
                foreach (RoleWorkGiverSpec giver in capability.Givers)
                {
                    foreach (RoleSkillUseSpec use in giver.UsedSkills)
                    {
                        SkillTotals skill = Of(bySkill, totals, use.SkillDefName);
                        if (skill == null) continue;
                        skill.UsedGivers++;
                        skill.Effects |= use.Effects;
                    }
                    foreach (string trained in giver.TrainedSkillDefNames)
                    {
                        SkillTotals skill = Of(bySkill, totals, trained);
                        if (skill != null) skill.TrainedGivers++;
                    }
                    AddGateTotals(giver, bySkill, totals, null, 0);
                }
            foreach (SkillTotals skill in totals)
                skill.Participates =
                    memberParticipation.Contains(skill.SkillDefName);
        }

        private static void AddGateTotals(
            RoleWorkGiverSpec giver,
            Dictionary<string, SkillTotals> bySkill,
            List<SkillTotals> totals,
            HashSet<string> touched,
            int weight)
        {
            foreach (RoleWorkContentSpec content in giver.Contents)
                foreach (RoleContentGate gate in content.Gates)
                {
                    SkillTotals skill = Of(bySkill, totals, gate.SkillDefName);
                    if (skill == null) continue;
                    skill.GatedContents++;
                    if (touched != null && touched.Add(gate.SkillDefName))
                        skill.WeightedGivers += weight;
                }
        }

        private static bool HasGates(RoleWorkGiverSpec giver)
        {
            foreach (RoleWorkContentSpec content in giver.Contents)
                if (content.Gates.Count > 0) return true;
            return false;
        }

        private static bool Better(SkillTotals candidate, SkillTotals best)
        {
            if (best == null) return true;
            if (candidate.Importance != best.Importance)
                return candidate.Importance > best.Importance;
            if (candidate.TrainedGivers != best.TrainedGivers)
                return candidate.TrainedGivers > best.TrainedGivers;
            return string.CompareOrdinal(
                candidate.SkillDefName, best.SkillDefName) < 0;
        }

        private static SkillTotals Of(
            Dictionary<string, SkillTotals> bySkill,
            List<SkillTotals> totals,
            string skillDefName)
        {
            if (string.IsNullOrEmpty(skillDefName)) return null;
            if (!bySkill.TryGetValue(skillDefName, out SkillTotals skill))
            {
                skill = new SkillTotals { SkillDefName = skillDefName };
                bySkill.Add(skillDefName, skill);
                totals.Add(skill);
            }
            return skill;
        }

        private static RoleWorkGiverSpec GiverSpecOf(
            string giverName,
            IReadOnlyDictionary<string, JobProfileGiverFacts> giverFacts,
            JobProfileIndex index)
        {
            JobProfileGiverFacts facts = null;
            giverFacts?.TryGetValue(giverName, out facts);
            if (facts == null)
                return new RoleWorkGiverSpec(giverName, null, null, null);

            var contents = new List<RoleWorkContentSpec>();
            var usedOrder = new List<string>();
            var usedEffects = new Dictionary<string, RoleWorkEffect>(
                StringComparer.Ordinal);
            if (facts.UsesRecipes && index != null)
            {
                foreach (int recipeIdentity in facts.RecipeIdentities)
                {
                    if (!index.Recipes.TryGetValue(
                            recipeIdentity, out JobProfileRecipeSource recipe))
                        continue;
                    string skill = recipe.WorkSkill?.DefName;
                    var gates = new List<RoleContentGate>(
                        recipe.SkillRequirements.Count);
                    foreach (JobProfileSkillRequirementSource requirement
                        in recipe.SkillRequirements)
                        gates.Add(new RoleContentGate(
                            requirement.SkillDefName, requirement.MinLevel));
                    // Ungated skill-carrying recipes need no content record:
                    // their facts are already in the giver unions.
                    if (gates.Count > 0 || recipe.DefName != null)
                        contents.Add(new RoleWorkContentSpec(
                            RoleWorkContentKind.Recipe,
                            recipe.DefName,
                            skill,
                            recipe.Effects,
                            recipe.WorkSkillLearnFactor > 0f,
                            ReadOnly(gates)));
                    if (skill != null)
                    {
                        if (!usedEffects.ContainsKey(skill))
                            usedOrder.Add(skill);
                        usedEffects.TryGetValue(
                            skill, out RoleWorkEffect effects);
                        usedEffects[skill] = effects | recipe.Effects
                            | CuratedEffectFor(facts, skill);
                    }
                }
            }
            else
            {
                foreach (string skill in facts.UsedSkillDefNames)
                    if (skill != null && !usedEffects.ContainsKey(skill))
                    {
                        usedOrder.Add(skill);
                        usedEffects[skill] = CuratedEffectFor(facts, skill);
                    }
                AddGateContents(
                    giverName, "ConstructFinishFrames",
                    index?.ConstructionGates,
                    index?.ConstructionRequirement,
                    RoleWorkContentKind.Buildable, contents);
                AddGateContents(
                    giverName, "GrowerSow",
                    index?.SowingGates,
                    index?.SowingRequirement,
                    RoleWorkContentKind.Plant, contents);
            }

            var used = new List<RoleSkillUseSpec>(usedOrder.Count);
            foreach (string skill in usedOrder)
                used.Add(new RoleSkillUseSpec(skill, usedEffects[skill]));
            return new RoleWorkGiverSpec(
                giverName,
                ReadOnly(used),
                facts.TrainedSkillDefNames,
                ReadOnly(contents));
        }

        private static RoleWorkEffect CuratedEffectFor(
            JobProfileGiverFacts facts, string skillDefName)
        {
            IReadOnlyList<JobProfileSkillEffect> curated =
                facts.CuratedSkillEffects;
            RoleWorkEffect effects = RoleWorkEffect.Unspecified;
            for (int index = 0; index < curated.Count; index++)
                if (curated[index].SkillDefName == skillDefName)
                    effects |= curated[index].Effects;
            return effects;
        }

        private static void AddGateContents(
            string giverName,
            string gatedGiverName,
            IReadOnlyList<JobProfileContentGateFacts> gateFacts,
            JobProfileRequirementFacts requirement,
            RoleWorkContentKind kind,
            List<RoleWorkContentSpec> contents)
        {
            if (giverName != gatedGiverName
                || requirement?.SkillDefName == null)
                return;
            if (gateFacts != null && gateFacts.Count > 0)
            {
                foreach (JobProfileContentGateFacts gate in gateFacts)
                    contents.Add(new RoleWorkContentSpec(
                        kind,
                        gate.DefName,
                        null,
                        RoleWorkEffect.Unspecified,
                        false,
                        new[]
                        {
                            new RoleContentGate(
                                requirement.SkillDefName, gate.MinLevel),
                        }));
                return;
            }
            // Aggregate-only source (pre-content-record adapter or baseline):
            // emit anonymous records so gate counts and participation match;
            // tooltips skip null names. Disappears once sources carry names.
            for (int index = 0; index < requirement.Gated; index++)
                contents.Add(new RoleWorkContentSpec(
                    kind,
                    null,
                    null,
                    RoleWorkEffect.Unspecified,
                    false,
                    new[]
                    {
                        new RoleContentGate(
                            requirement.SkillDefName, requirement.Floor),
                    }));
        }

        private static IReadOnlyList<string> CopyGates(
            IReadOnlyList<string> gates)
        {
            if (gates == null || gates.Count == 0)
                return Array.Empty<string>();
            var copy = new List<string>(gates.Count);
            foreach (string gate in gates)
                if (!string.IsNullOrEmpty(gate) && !copy.Contains(gate))
                    copy.Add(gate);
            return new ReadOnlyCollection<string>(copy);
        }

        private static IReadOnlyList<T> ReadOnly<T>(List<T> source) =>
            new ReadOnlyCollection<T>(source);
    }
}
