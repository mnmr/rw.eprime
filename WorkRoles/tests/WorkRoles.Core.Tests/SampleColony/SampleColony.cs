using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Tests.SampleColony;

/// Builds the recommendation engine's ColonyView from the Fisso-NAM savegame
/// facts in SampleColonyData, mirroring RecsAdapter's projection: the same catalog
/// builder, order-template resolution, def-tuning demand and signal pipeline the
/// game uses. Every Build* method returns fresh mutable instances.
public static class SampleColony
{
    /// The window cohort: colonists on the save's current map.
    public static ColonyView BuildColonyView() => BuildColonyView(CurrentMapPawns);

    public static ColonyView BuildColonyView(IEnumerable<SamplePawn> pawns)
    {
        RecommendationCatalogProjection catalog = BuildCatalog();
        List<int> template = OrderTemplate.ResolveTemplate(SampleColonyData.RecommendationOrder, catalog.Roles);
        return catalog.CreateColony(template, pawns.Select(BuildPawnView));
    }

    public static IReadOnlyList<SamplePawn> AllPawns => SampleColonyData.Pawns;

    public static IReadOnlyList<SamplePawn> CurrentMapPawns { get; } = SampleColonyData.Pawns.Where(p => p.MapIndex == SampleColonyData.CurrentMapIndex).ToList();

    /// Finds a pawn by any name part shown in game: short label first, then
    /// first, nick or last name.
    public static SamplePawn Pawn(string name) =>
        SampleColonyData.Pawns.FirstOrDefault(p => p.Name == name) ?? SampleColonyData.Pawns.First(p => p.FirstName == name || p.NickName == name || p.LastName == name);

    public static int RoleId(string label) => SampleColonyData.Roles.First(r => r.Label == label).Id;

    public static string RoleLabel(int id) => SampleColonyData.Roles.First(r => r.Id == id).Label;

    // ---------------------------------------------------------------- catalog

    public static RecommendationCatalogProjection BuildCatalog() => Project(BuildSources());

    /// Template roles carry the shipped def tuning; player-made roles carry
    /// their own stored demand numbers.
    private static (int ColonyMin, int Coverage) DemandOf(SampleRole role) =>
        role.TemplateDefName != null && RoleDefaults.ByDefName.TryGetValue(role.TemplateDefName, out RoleDefaults.DefTuning tuning)
            ? (tuning.ColonyMin, tuning.Coverage)
            : (role.ColonyMin, role.Coverage);

    private static List<RecommendationRoleSource> BuildSources()
    {
        var sources = new List<RecommendationRoleSource>(SampleColonyData.Roles.Length);
        foreach (SampleRole role in SampleColonyData.Roles)
        {
            RoleDefaults.DefTuning tuning = role.TemplateDefName != null && RoleDefaults.ByDefName.TryGetValue(role.TemplateDefName, out RoleDefaults.DefTuning found) ? found : null;
            var entries = new List<JobEntry>(role.Entries.Length);
            foreach (string raw in role.Entries)
                if (JobEntry.TryDecode(raw, out JobEntry entry))
                    entries.Add(entry);
            (int colonyMin, int coverage) = DemandOf(role);
            sources.Add(
                new RecommendationRoleSource
                {
                    Id = role.Id,
                    TemplateDefName = role.TemplateDefName,
                    Entries = entries,
                    ColonyMin = colonyMin,
                    Coverage = coverage,
                    AutoAssign = role.AutoAssign,
                    HasRules = role.HasRules,
                    Blocker = role.Blocker,
                    PreserveRecommendationOrder = role.PreserveRecommendationOrder,
                    ChampionPenalty = tuning?.ChampionPenalty ?? true,
                    Category = tuning?.Category ?? default,
                    Time = tuning?.Time ?? default,
                    DeclaredRequiredSkills = tuning == null ? null : [.. tuning.RequiredSkills],
                    DeclaredOptionalSkills = tuning == null ? null : [.. tuning.OptionalSkills],
                    Available = true,
                    Enabled = true,
                    SpecialRole = role.SpecialRole == null ? RecommendationSpecialRoleKind.None : Enum.Parse<RecommendationSpecialRoleKind>(role.SpecialRole, true),
                }
            );
        }
        return sources;
    }

    private static RecommendationCatalogProjection Project(List<RecommendationRoleSource> sources) =>
        RecommendationCatalogBuilder.Build(sources, BuildPaths(), JobCatalog, VanillaWorkOrder.NaturalPriority, VanillaJobSkillBaseline.Index);

    // Captured anchors are intentionally dropped: role-owned paths place
    // through the recommendation order, mirroring the live migration.
    /// The save decides which paths exist; a path targeting a template role
    /// rebuilds its members and bands from the def training so def changes
    /// reach the tests. Player-made targets keep their stored bands.
    public static List<PathView> BuildPaths()
    {
        var idByDefName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (SampleRole role in SampleColonyData.Roles)
            if (role.TemplateDefName != null)
                idByDefName.TryAdd(role.TemplateDefName, role.Id);
        return SampleColonyData
            .Paths.Select(stored =>
                DefTrainingPath(stored, idByDefName)
                ?? new PathView
                {
                    Id = stored.Id,
                    RoleIds = stored.RoleIds.ToList(),
                    BandMins = stored.BandMins.ToList(),
                    BandMaxes = stored.BandMaxes.ToList(),
                }
            )
            .ToList();
    }

    private static PathView DefTrainingPath(SamplePath stored, Dictionary<string, int> idByDefName)
    {
        int targetRoleId = StoredTargetRoleId(stored);
        SampleRole target = SampleColonyData.Roles.FirstOrDefault(role => role.Id == targetRoleId);
        if (target?.TemplateDefName == null || !RoleDefaults.ByDefName.TryGetValue(target.TemplateDefName, out RoleDefaults.DefTuning tuning) || tuning.Training.Count == 0)
            return null;
        var path = new PathView { Id = stored.Id };
        foreach ((string roleDefName, int min, int max) in tuning.Training)
        {
            if (!idByDefName.TryGetValue(roleDefName, out int roleId))
                continue;
            path.RoleIds.Add(roleId);
            path.BandMins.Add(min);
            path.BandMaxes.Add(max);
        }
        return path.RoleIds.Count >= 2 && path.RoleIds.Contains(targetRoleId) ? path : null;
    }

    /// The stored path's owner: the entry with the uniquely highest band
    /// minimum, mirroring PathActivation.UniqueTargetRoleId.
    private static int StoredTargetRoleId(SamplePath stored)
    {
        int highestMin = int.MinValue;
        int at = -1;
        bool unique = true;
        for (int index = 0; index < stored.RoleIds.Length; index++)
        {
            if (stored.BandMins[index] > highestMin)
            {
                highestMin = stored.BandMins[index];
                at = index;
                unique = true;
            }
            else if (stored.BandMins[index] == highestMin)
            {
                unique = false;
            }
        }
        return unique && at >= 0 ? stored.RoleIds[at] : -1;
    }

    private static readonly SampleJobCatalog JobCatalog = new();

    /// Work-type membership recorded in the save's role snapshots (includes the
    /// modded givers active in that game), falling back to the vanilla all-DLC
    /// baseline for anything the snapshots don't cover.
    private sealed class SampleJobCatalog : IJobCatalog
    {
        private readonly Dictionary<string, string[]> typeToGivers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> giverToType = new(StringComparer.Ordinal);

        internal SampleJobCatalog()
        {
            foreach (SampleRole role in SampleColonyData.Roles)
            foreach ((string workType, string[] givers) in role.WorkTypeSnapshots)
            {
                if (!typeToGivers.TryGetValue(workType, out string[] known) || givers.Length > known.Length)
                    typeToGivers[workType] = givers;
                foreach (string giver in givers)
                    giverToType.TryAdd(giver, workType);
            }
        }

        public IReadOnlyList<string> WorkGiversOf(string workTypeDefName) =>
            workTypeDefName != null && typeToGivers.TryGetValue(workTypeDefName, out string[] snapshot) ? snapshot
            : workTypeDefName != null && VanillaWorkOrder.GiversInOrder.TryGetValue(workTypeDefName, out string[] vanilla) ? vanilla
            : [];

        public string WorkTypeOf(string workGiverDefName) =>
            workGiverDefName != null && giverToType.TryGetValue(workGiverDefName, out string snapshot) ? snapshot
            : workGiverDefName != null && VanillaGiverBaseline.GiverWorkType.TryGetValue(workGiverDefName, out string vanilla) ? vanilla
            : null;

        public bool IsEmergency(string workGiverDefName) => false;
    }

    // ---------------------------------------------------------------- pawns

    public static PawnView BuildPawnView(SamplePawn pawn)
    {
        var view = new PawnView
        {
            BiologicalAgeTicks = pawn.AgeBiologicalTicks,
            HasRangedWeapon = pawn.HasRangedWeapon,
            FireFear = pawn.FireFear,
            CapableWorkTypes = [.. pawn.CapableWorkTypes],
        };
        foreach ((string skill, int level, _) in pawn.Skills)
            view.SkillLevels[skill] = level;
        view.SkillLevels.TryGetValue("Shooting", out view.ShootingLevel);
        PawnSignalViewProjection.Apply(BuildSignalSnapshot(pawn), view);
        foreach ((int roleId, string state, bool pinned) in pawn.Assignments)
            view.Existing.Add(
                new AssignmentView
                {
                    RoleId = roleId,
                    Enabled = state != "Disabled",
                    Pinned = pinned,
                }
            );
        return view;
    }

    /// Mirrors the game-side signal providers over the extracted raw facts:
    /// vanilla passions, Biotech aptitude genes (implant twin for xenogenes of
    /// a custom xenogerm), catalogued genes and traits. VSE, More Than Capable
    /// and Anomaly trait/hediff aptitudes are absent in this save.
    public static PawnSignalSnapshot BuildSignalSnapshot(SamplePawn pawn)
    {
        SignalCatalog catalog = SignalCatalog.Default;
        List<Signal> signals = [];
        List<string> enabledSkills = [];
        List<string> persistentlyBad = [];

        foreach ((string skill, _, string passion) in pawn.Skills)
        {
            enabledSkills.Add(skill);
            if (passion == "None")
            {
                persistentlyBad.Add(skill);
                continue;
            }
            SignalDefinition definition = catalog.Find(SignalSourceKind.Passion, passion).FirstOrDefault();
            if (definition != null)
                signals.Add(SignalFactory.Instantiate(definition, skill));
        }

        foreach ((string geneDef, string template, string skill, bool xenogene) in pawn.AptitudeGenes)
        {
            bool implanted = pawn.CustomXenotype && xenogene;
            foreach (SignalDefinition definition in catalog.Find(SignalSourceKind.Gene, template))
            {
                bool implantTwin = string.Equals(definition.Source.EffectDiscriminator, VanillaSignalDefinitions.ImplantDiscriminator, StringComparison.Ordinal);
                if (implantTwin != implanted)
                    continue;
                signals.Add(SignalFactory.Instantiate(definition, skill, geneDef));
            }
        }

        foreach (string gene in pawn.ActiveGenes)
        foreach (SignalDefinition definition in catalog.Find(SignalSourceKind.Gene, gene))
        {
            if (definition.Source.DefName.StartsWith("Aptitude", StringComparison.Ordinal))
                continue;
            signals.Add(SignalFactory.Instantiate(definition));
        }

        // No trait in this save carries aptitudes (Anomaly is absent), so the
        // game's aptitude-twin exclusion never fires here.
        foreach ((string trait, int degree) in pawn.Traits)
        foreach (SignalDefinition definition in catalog.Find(SignalSourceKind.Trait, trait, degree))
            signals.Add(SignalFactory.Instantiate(definition));

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(signals, enabledSkills, crossSkillEffectsEnabled: false, persistentlyBadSkillDefNames: persistentlyBad);
        return PawnSignalSnapshot.Create(enabledSkills, snapshot);
    }
}
