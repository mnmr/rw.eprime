using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;
using WorkRoles.Lab.Data;

namespace WorkRoles.Core.Tests;

/// Builds the recommendation engine's ColonyView from the Fisso-NAM savegame
/// facts in SampleColonyData, mirroring RecsAdapter's projection: the same catalog
/// builder, order-template resolution, scale decoding and signal pipeline the
/// game uses. Every Build* method returns fresh mutable instances.
public static class SampleColony
{
    /// The window cohort: colonists on the save's current map.
    public static ColonyView BuildColonyView() => BuildColonyView(CurrentMapPawns);

    public static ColonyView BuildColonyView(IEnumerable<SamplePawn> pawns)
    {
        RecommendationCatalogProjection catalog = BuildCatalog();
        List<int> template = OrderTemplate.ResolveTemplate(
            SampleColonyData.RecommendationOrder, catalog.Roles);
        return catalog.CreateColony(template, pawns.Select(BuildPawnView));
    }

    public static IReadOnlyList<SamplePawn> AllPawns => SampleColonyData.Pawns;

    public static IReadOnlyList<SamplePawn> CurrentMapPawns { get; } =
        SampleColonyData.Pawns
            .Where(p => p.MapIndex == SampleColonyData.CurrentMapIndex)
            .ToList();

    /// Finds a pawn by any name part shown in game: short label first, then
    /// first, nick or last name.
    public static SamplePawn Pawn(string name) =>
        SampleColonyData.Pawns.FirstOrDefault(p => p.Name == name)
        ?? SampleColonyData.Pawns.First(p =>
            p.FirstName == name || p.NickName == name || p.LastName == name);

    public static int RoleId(string label) =>
        SampleColonyData.Roles.First(r => r.Label == label).Id;

    public static string RoleLabel(int id) =>
        SampleColonyData.Roles.First(r => r.Id == id).Label;

    // ---------------------------------------------------------------- catalog

    public static RecommendationCatalogProjection BuildCatalog()
    {
        Dictionary<string, RoleAssignmentStrategy> scales = DecodeScales();
        var sources = new List<RecommendationRoleSource>(SampleColonyData.Roles.Length);
        foreach (SampleRole role in SampleColonyData.Roles)
        {
            scales.TryGetValue(role.HolderScale ?? "", out RoleAssignmentStrategy strategy);
            var entries = new List<JobEntry>(role.Entries.Length);
            foreach (string raw in role.Entries)
                if (JobEntry.TryDecode(raw, out JobEntry entry))
                    entries.Add(entry);
            sources.Add(new RecommendationRoleSource
            {
                Id = role.Id,
                Entries = entries,
                AutoAssign = role.AutoAssign,
                HasRules = role.HasRules,
                Blocker = role.Blocker,
                PreserveRecommendationOrder = role.PreserveRecommendationOrder,
                ChampionPenalty = !role.UsesOccasionalRepeatChampionPenalty,
                Scale = strategy?.Scale,
                Mode = strategy?.Mode ?? ScaleMode.Never,
                Available = true,
                Enabled = true,
                SpecialRole = role.SpecialRole == null
                    ? RecommendationSpecialRoleKind.None
                    : Enum.Parse<RecommendationSpecialRoleKind>(role.SpecialRole, true),
            });
        }
        return RecommendationCatalogBuilder.Build(
            sources,
            BuildPaths(),
            JobCatalog,
            VanillaWorkOrder.NaturalPriority,
            VanillaJobSkillBaseline.Index);
    }

    // Captured anchors are intentionally dropped: role-owned paths place
    // through the recommendation order, mirroring the live migration.
    public static List<PathView> BuildPaths() =>
        SampleColonyData.Paths.Select(p => new PathView
        {
            Id = p.Id,
            RoleIds = p.RoleIds.ToList(),
            BandMins = p.BandMins.ToList(),
            BandMaxes = p.BandMaxes.ToList(),
        }).ToList();

    /// Decoded via the same production codec RoleStore uses on load.
    public static Dictionary<string, RoleAssignmentStrategy> DecodeScales()
    {
        var result = new Dictionary<string, RoleAssignmentStrategy>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string raw in SampleColonyData.HolderScales)
        {
            string[] parts = raw.Split('\n');
            var bands = new HolderScale
            {
                RequiredTotals = HolderScaleCodec.DecodeRow(parts[1], 0),
                TrainingWaivers = HolderScaleCodec.DecodeRow(parts[2], 0),
                Max = HolderScaleCodec.DecodeRow(parts[3], RoleHolderRange.Uncapped),
            };
            bool preset = parts.Length > 4 && parts[4].Trim() == "1";
            string modeToken = parts.Length > 5 ? parts[5] : null;
            RoleAssignmentStrategy strategy = RoleAssignmentStrategy.FromRows(
                parts[0].Trim(), preset, modeToken, bands);
            result[strategy.Name] = strategy;
        }
        return result;
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
                    if (!typeToGivers.TryGetValue(workType, out string[] known)
                        || givers.Length > known.Length)
                        typeToGivers[workType] = givers;
                    foreach (string giver in givers)
                        giverToType.TryAdd(giver, workType);
                }
        }

        public IReadOnlyList<string> WorkGiversOf(string workTypeDefName) =>
            workTypeDefName != null
            && typeToGivers.TryGetValue(workTypeDefName, out string[] snapshot)
                ? snapshot
                : workTypeDefName != null
                  && VanillaWorkOrder.GiversInOrder.TryGetValue(
                      workTypeDefName, out string[] vanilla)
                    ? vanilla
                    : Array.Empty<string>();

        public string WorkTypeOf(string workGiverDefName) =>
            workGiverDefName != null
            && giverToType.TryGetValue(workGiverDefName, out string snapshot)
                ? snapshot
                : workGiverDefName != null
                  && VanillaGiverBaseline.GiverWorkType.TryGetValue(
                      workGiverDefName, out string vanilla)
                    ? vanilla
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
            CapableWorkTypes = new HashSet<string>(pawn.CapableWorkTypes),
        };
        foreach ((string skill, int level, _) in pawn.Skills)
            view.SkillLevels[skill] = level;
        view.SkillLevels.TryGetValue("Shooting", out view.ShootingLevel);
        PawnSignalViewProjection.Apply(BuildSignalSnapshot(pawn), view);
        foreach ((int roleId, string state, bool pinned) in pawn.Assignments)
            view.Existing.Add(new AssignmentView
            {
                RoleId = roleId,
                Enabled = state != "Disabled",
                Pinned = pinned,
            });
        return view;
    }

    /// Mirrors the game-side signal providers over the extracted raw facts:
    /// vanilla passions, Biotech aptitude genes (implant twin for xenogenes of
    /// a custom xenogerm), catalogued genes and traits. VSE, More Than Capable
    /// and Anomaly trait/hediff aptitudes are absent in this save.
    public static PawnSignalSnapshot BuildSignalSnapshot(SamplePawn pawn)
    {
        SignalCatalog catalog = SignalCatalog.Default;
        var signals = new List<Signal>();
        var enabledSkills = new List<string>();
        var persistentlyBad = new List<string>();

        foreach ((string skill, _, string passion) in pawn.Skills)
        {
            enabledSkills.Add(skill);
            if (passion == "None")
            {
                persistentlyBad.Add(skill);
                continue;
            }
            SignalDefinition definition =
                catalog.Find(SignalSourceKind.Passion, passion).FirstOrDefault();
            if (definition != null)
                signals.Add(SignalFactory.Instantiate(definition, skill));
        }

        foreach ((string geneDef, string template, string skill, bool xenogene)
                 in pawn.AptitudeGenes)
        {
            bool implanted = pawn.CustomXenotype && xenogene;
            foreach (SignalDefinition definition in
                     catalog.Find(SignalSourceKind.Gene, template))
            {
                bool implantTwin = string.Equals(
                    definition.Source.EffectDiscriminator,
                    VanillaSignalDefinitions.ImplantDiscriminator,
                    StringComparison.Ordinal);
                if (implantTwin != implanted) continue;
                signals.Add(SignalFactory.Instantiate(definition, skill, geneDef));
            }
        }

        foreach (string gene in pawn.ActiveGenes)
            foreach (SignalDefinition definition in
                     catalog.Find(SignalSourceKind.Gene, gene))
            {
                if (definition.Source.DefName.StartsWith("Aptitude", StringComparison.Ordinal))
                    continue;
                signals.Add(SignalFactory.Instantiate(definition));
            }

        // No trait in this save carries aptitudes (Anomaly is absent), so the
        // game's aptitude-twin exclusion never fires here.
        foreach ((string trait, int degree) in pawn.Traits)
            foreach (SignalDefinition definition in
                     catalog.Find(SignalSourceKind.Trait, trait, degree))
                signals.Add(SignalFactory.Instantiate(definition));

        SignalSnapshot snapshot = SignalSnapshotBuilder.Build(
            signals, enabledSkills,
            crossSkillEffectsEnabled: false,
            persistentlyBadSkillDefNames: persistentlyBad);
        return PawnSignalSnapshot.Create(enabledSkills, snapshot);
    }
}
