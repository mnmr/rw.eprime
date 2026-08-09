// Extracted from savegame Fisso-NAM.rws (RimWorld 1.6.4871, Core+Biotech+Odyssey,
// WorkRoles 1.3.5) on 2026-08-09. Levels include Biotech gene aptitudes; capable
// work types and disabled skills are precomputed from backstory/trait/gene data.
namespace WorkRoles.Core.Tests;

public sealed class SamplePawn
{
    /// In-game short label: nickname when set, else first name.
    public string Name;
    public string FirstName;
    public string NickName;
    public string LastName;
    public string FullName => NickName == null
        ? $"{FirstName} {LastName}"
        : $"{FirstName} \"{NickName}\" {LastName}";
    public string ThingId;
    public int MapIndex;
    public long AgeBiologicalTicks;
    public bool HasRangedWeapon;
    public bool FireFear;
    public bool CustomXenotype;
    /// Enabled skills only; Level includes gene aptitudes, Passion is None/Minor/Major.
    public (string Skill, int Level, string Passion)[] Skills;
    public string[] DisabledSkills;
    public string[] CapableWorkTypes;
    public (string Def, int Degree)[] Traits;
    /// Active genes (endo+xeno, override-filtered), including aptitude genes.
    public string[] ActiveGenes;
    public (string GeneDef, string Template, string Skill, bool Xenogene)[] AptitudeGenes;
    public (int RoleId, string State, bool Pinned)[] Assignments;
}

public sealed class SampleRole
{
    public int Id;
    public string Label;
    public string TemplateDefName;
    public bool AutoAssign;
    public bool Blocker;
    public bool HasRules;
    public string HolderScale;
    public int GroupId;
    public string[] Entries;
    public (string WorkType, string[] Givers)[] WorkTypeSnapshots;
    public bool PreserveRecommendationOrder;
    public bool UsesOccasionalRepeatChampionPenalty;
    public string SpecialRole;
}

public sealed class SamplePath
{
    public int Id;
    public string Name;
    public int[] RoleIds;
    public int[] BandMins;
    public int[] BandMaxes;
    public int AnchorRoleId;
    public bool AnchorBefore;
}

/// Raw colony facts from the Fisso-NAM savegame. SampleColony turns these
/// into the ColonyView the recommendation engine consumes.
public static class SampleColonyData
{
    public const int CurrentMapIndex = 1;

    public static readonly int[] RecommendationOrder = new[] { 43, 3, 1, 6, 7, 8, 9, 14, 16, 19, 21, 36, 20, 22, 23, 24, 13, 25, 28, 42, 50 };

    /// name, min row, train row, max row, preset flag, mode token: the exact
    /// RoleStore scribe payload, decoded by RoleAssignmentStrategy.FromRows.
    public static readonly string[] HolderScales =
    {
        "Never\n0,0,0,0,0,0,0,0,0,0,0,0\n0,0,0,0,0,0,0,0,0,0,0,0\n0,0,0,0,0,0,0,0,0,0,0,0\n1\n2",
        "Doctoring\n2,2,3,3,4,4,5,5,6,6,7,7\n1,1,2,2,2,2,3,3,3,3,4,4\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Wardening\n2,2,2,3,3,3,4,4,4,5,5,5\n1,1,1,1,1,1,1,1,1,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Research\n3,3,3,4,4,4,4,4,4,4,4,4\n1,1,1,1,1,1,1,1,1,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Mining\n2,2,2,3,3,3,4,4,4,5,5,5\n1,1,1,1,1,1,1,1,1,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Cooking\n2,2,2,3,3,3,4,4,4,5,5,5\n1,1,1,1,1,1,1,1,1,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Crafting\n2,2,2,3,3,3,4,4,4,5,5,5\n1,1,1,1,1,1,1,1,1,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Caretaking\n2,2,2,3,3,3,4,4,4,5,5,5\n1,1,1,1,1,1,1,1,1,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Handling\n2,2,3,3,4,4,5,5,5,5,5,5\n1,1,2,2,2,2,3,3,3,3,3,3\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Smithing\n2,2,3,3,4,4,5,5,5,5,5,5\n1,1,2,2,2,2,3,3,3,3,3,3\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Building\n2,2,3,3,4,4,5,5,6,6,7,7\n1,1,2,2,2,2,3,3,3,3,4,4\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Fishing\n2,2,3,3,4,4,5,5,6,6,7,7\n1,1,1,1,1,1,1,2,2,2,2,2\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Farming\n2,2,3,3,4,4,5,5,6,6,7,7\n1,1,1,1,1,1,1,2,2,2,2,2\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Drug Fabrication\n1,2,2,3,3,3,3,3,3,4,4,4\n0,0,0,0,0,0,0,0,0,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Tailoring\n2,2,3,3,4,4,5,5,5,5,5,5\n1,1,2,2,2,2,3,3,3,3,3,3\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Fabrication\n1,1,2,3,4,4,5,5,6,6,6,6\n1,1,1,2,2,2,3,3,4,4,4,4\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Artistry\n0,1,1,2,2,2,2,2,2,2,2,2\n0,1,1,1,1,1,1,1,1,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Drug Maker\n2,2,3,3,3,4,4,4,5,5,5,5\n1,1,2,2,2,3,3,3,3,3,3,3\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Unskilled\n0,0,0,1,1,1,1,1,1,1,1,1\n0,0,0,0,0,0,0,0,0,0,0,0\n256,256,256,256,256,256,256,256,256,256,256,256\n1\n1",
        "Surplus\n0,0,0,0,0,0,0,0,0,0,0,0\n0,0,0,0,0,0,0,0,0,0,0,0\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n0",
        "Unskilled NoMin\n0,0,0,1,1,1,1,1,1,1,1,1\n0,0,0,1,1,1,1,1,1,1,1,1\n256,256,256,256,256,256,256,256,256,256,256,256\n0\n1",
    };

    public static readonly SampleRole[] Roles =
    {
        new SampleRole
        {
            Id = 1, Label = "Basics",
            TemplateDefName = "WS_Basics", AutoAssign = true, HolderScale = "Never", GroupId = 1,
            Entries = new string[] { "WorkType:PatientBedRest", "WorkType:BasicWorker" },
            WorkTypeSnapshots = new[]
            {
                ("PatientBedRest", new string[] { "PatientGoToBedRecuperate" }),
                ("BasicWorker", new string[] { "Flick", "BasicReleasePrisoner", "Open", "EjectFuel", "ExtractSkull" }),
            },
        },
        new SampleRole
        {
            Id = 2, Label = "Rescuer",
            TemplateDefName = "WS_Rescuer", HolderScale = "Never", GroupId = 8,
            Entries = new string[] { "WorkGiver:DoctorRescue" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 3, Label = "Doctor",
            TemplateDefName = "WS_Doctor", HolderScale = "Doctoring", GroupId = 8, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkType:Doctor" },
            WorkTypeSnapshots = new[]
            {
                ("Doctor", new string[] { "DoctorTendEmergency", "DoctorTendToHumanlikes", "DoctorTendToSelfEmergency", "DoctorTendToSelf", "DoctorFeedHumanlikes", "DoBillsMedicalHumanOperation", "FeedHemogen", "DoctorRescue", "DoctorTendToAnimals", "DoctorFeedAnimals", "DoBillsMedicalAnimalOperation", "TakeToBedToOperate", "VisitSickPawn" }),
            },
        },
        new SampleRole
        {
            Id = 4, Label = "Pyrophobia",
            TemplateDefName = "WS_NoFirefighting", Blocker = true, HolderScale = "Never", GroupId = 1, SpecialRole = "FireBlocker",
            Entries = new string[] { "WorkType:Firefighter" },
            WorkTypeSnapshots = new[]
            {
                ("Firefighter", new string[] { "FightFires" }),
            },
        },
        new SampleRole
        {
            Id = 5, Label = "Medic",
            TemplateDefName = "WS_Medic", HolderScale = "Never", GroupId = 8, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkGiver:DoctorTendEmergency", "WorkGiver:DoctorTendToHumanlikes", "WorkGiver:DoctorTendToSelf", "WorkGiver:DoctorTendToSelfEmergency", "WorkGiver:DoctorRescue", "WorkGiver:DoctorFeedHumanlikes", "WorkGiver:DoctorTendToAnimals", "WorkGiver:DoctorFeedAnimals", "WorkGiver:VisitSickPawn", "WorkGiver:TakeToBedToOperate", "WorkGiver:FeedHemogen", "WorkGiver:DoBillsMedicalAnimalOperation" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 6, Label = "Childcare",
            TemplateDefName = "WS_Childminder", HolderScale = "Caretaking", GroupId = 7, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkType:Childcare" },
            WorkTypeSnapshots = new[]
            {
                ("Childcare", new string[] { "ChildcarerTeach", "BringBabyToSafety", "BreastfeedBaby", "PlayWithBaby", "BottleFeedBaby", "CarryToBreastfeed" }),
            },
        },
        new SampleRole
        {
            Id = 7, Label = "Warden",
            TemplateDefName = "WS_Warden", HolderScale = "Wardening", GroupId = 7, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkType:Warden" },
            WorkTypeSnapshots = new[]
            {
                ("Warden", new string[] { "DoExecution", "ExecuteGuiltyColonist", "ReleasePrisoner", "TakePrisonerToBed", "FeedPrisoner", "DeliverHemogenToPrisoner", "DeliverFoodToPrisoner", "ChatWithPrisoner" }),
            },
        },
        new SampleRole
        {
            Id = 8, Label = "Handler",
            TemplateDefName = "WS_Handler", HolderScale = "Handling", GroupId = 3, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkType:Handling" },
            WorkTypeSnapshots = new[]
            {
                ("Handling", new string[] { "TakeRoamingAnimalsToPen", "HandlingFeedPatientAnimals", "TakeToPen", "Slaughter", "ReleaseToWild", "Milk", "Shear", "Tame", "Train", "RebalanceAnimalsInPens" }),
            },
        },
        new SampleRole
        {
            Id = 9, Label = "Cook",
            TemplateDefName = "WS_Cook", HolderScale = "Cooking", GroupId = 6,
            Entries = new string[] { "WorkType:Cooking" },
            WorkTypeSnapshots = new[]
            {
                ("Cooking", new string[] { "DoBillsCook", "DoBillsCookCampfire", "DoBillsButcherFlesh", "DoBillsBrew" }),
            },
        },
        new SampleRole
        {
            Id = 10, Label = "Butcher",
            TemplateDefName = "WS_Butcher", HolderScale = "Never", GroupId = 6, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkGiver:DoBillsButcherFlesh" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 11, Label = "Brewer",
            TemplateDefName = "WS_Brewer", HolderScale = "Never", GroupId = 6, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkGiver:DoBillsBrew" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 12, Label = "Hunter",
            TemplateDefName = "WS_Hunter", HolderScale = "Never", GroupId = 3, SpecialRole = "Hunter",
            Entries = new string[] { "WorkType:Hunting" },
            WorkTypeSnapshots = new[]
            {
                ("Hunting", new string[] { "HunterHunt" }),
            },
        },
        new SampleRole
        {
            Id = 13, Label = "Fisher",
            TemplateDefName = "WS_Fisher", HolderScale = "Fishing", GroupId = 3,
            Entries = new string[] { "WorkType:Fishing" },
            WorkTypeSnapshots = new[]
            {
                ("Fishing", new string[] { "Fish" }),
            },
        },
        new SampleRole
        {
            Id = 14, Label = "Builder",
            TemplateDefName = "WS_Builder", HolderScale = "Building", GroupId = 2,
            Entries = new string[] { "WorkType:Construction" },
            WorkTypeSnapshots = new[]
            {
                ("Construction", new string[] { "FixBrokenDownBuilding", "Uninstall", "BuildRoofs", "RemoveRoofs", "DeconstructForBlueprint", "ConstructFinishFrames", "ConstructDeliverResourcesToFrames", "ConstructDeliverResourcesToBlueprints", "FillIn", "Deconstruct", "Repair", "ConstructRemoveFloors", "ConstructRemoveFoundations", "ConstructSmoothFloors", "ConstructSmoothWalls", "QJ_FinishQualityWork_Construction" }),
            },
        },
        new SampleRole
        {
            Id = 15, Label = "Handyman",
            TemplateDefName = "WS_Repairer", HolderScale = "Never", GroupId = 2,
            Entries = new string[] { "WorkGiver:Repair", "WorkGiver:FixBrokenDownBuilding", "WorkGiver:Deconstruct", "WorkGiver:DeconstructForBlueprint", "WorkGiver:Uninstall", "WorkGiver:FillIn", "WorkGiver:ConstructRemoveFloors", "WorkGiver:ConstructRemoveFoundations", "WorkGiver:BuildRoofs", "WorkGiver:RemoveRoofs", "WorkGiver:ConstructDeliverResourcesToFrames", "WorkGiver:ConstructDeliverResourcesToBlueprints", "WorkGiver:ConstructSmoothWalls", "WorkGiver:ConstructSmoothFloors" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 16, Label = "Farmer",
            TemplateDefName = "WS_Farmer", HolderScale = "Farming", GroupId = 4,
            Entries = new string[] { "WorkType:Growing", "WorkType:PlantCutting" },
            WorkTypeSnapshots = new[]
            {
                ("Growing", new string[] { "GrowerHarvest", "PlantSeed", "Replant", "GrowerSow" }),
                ("PlantCutting", new string[] { "ExtractTree", "PlantsCut" }),
            },
        },
        new SampleRole
        {
            Id = 17, Label = "Grower",
            TemplateDefName = "WS_Grower", HolderScale = "Never", GroupId = 4,
            Entries = new string[] { "WorkType:Growing" },
            WorkTypeSnapshots = new[]
            {
                ("Growing", new string[] { "GrowerHarvest", "PlantSeed", "Replant", "GrowerSow" }),
            },
        },
        new SampleRole
        {
            Id = 18, Label = "Plant Cutter",
            TemplateDefName = "WS_PlantCutter", HolderScale = "Never", GroupId = 4,
            Entries = new string[] { "WorkType:PlantCutting" },
            WorkTypeSnapshots = new[]
            {
                ("PlantCutting", new string[] { "ExtractTree", "PlantsCut" }),
            },
        },
        new SampleRole
        {
            Id = 19, Label = "Miner",
            TemplateDefName = "WS_Miner", HolderScale = "Mining", GroupId = 4,
            Entries = new string[] { "WorkType:Mining" },
            WorkTypeSnapshots = new[]
            {
                ("Mining", new string[] { "Mine", "Drill" }),
            },
        },
        new SampleRole
        {
            Id = 20, Label = "Smith",
            TemplateDefName = "WS_Smith", HolderScale = "Smithing", GroupId = 2,
            Entries = new string[] { "WorkType:Smithing" },
            WorkTypeSnapshots = new[]
            {
                ("Smithing", new string[] { "DoBillsSubcoreEncoder", "DoBillsMechGestator", "RepairMech", "DoBillsMakeWeapons", "DoBillsMachiningTable", "DoBillsFabricationBench", "QJ_FinishQualityWork_Smithing" }),
            },
        },
        new SampleRole
        {
            Id = 21, Label = "Fabricator",
            TemplateDefName = "WS_Fabricator", HolderScale = "Fabrication", GroupId = 2,
            Entries = new string[] { "WorkGiver:DoBillsFabricationBench" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 22, Label = "Tailor",
            TemplateDefName = "WS_Tailor", HolderScale = "Tailoring", GroupId = 2,
            Entries = new string[] { "WorkType:Tailoring" },
            WorkTypeSnapshots = new[]
            {
                ("Tailoring", new string[] { "DoBillsMakeApparel", "QJ_FinishQualityWork_Tailoring" }),
            },
        },
        new SampleRole
        {
            Id = 23, Label = "Artist",
            TemplateDefName = "WS_Artist", HolderScale = "Artistry", GroupId = 2,
            Entries = new string[] { "WorkType:Art" },
            WorkTypeSnapshots = new[]
            {
                ("Art", new string[] { "RemovePaintBuilding", "RemovePaintFloor", "PaintBuilding", "PaintFloor", "DoBillsSculpt", "QJ_FinishQualityWork_Art" }),
            },
        },
        new SampleRole
        {
            Id = 24, Label = "Crafter",
            TemplateDefName = "WS_Crafter", HolderScale = "Crafting", GroupId = 2,
            Entries = new string[] { "WorkType:Crafting" },
            WorkTypeSnapshots = new[]
            {
                ("Crafting", new string[] { "DoBillsUseCraftingSpot", "DoBillsRefinery", "DoBillsProduceDrugs", "DoBillsStonecut", "DoBillsSmelter" }),
            },
        },
        new SampleRole
        {
            Id = 25, Label = "Grunt",
            TemplateDefName = "WS_Grunt", HolderScale = "Unskilled NoMin", GroupId = 1,
            Entries = new string[] { "WorkType:Hauling", "WorkType:Cleaning" },
            WorkTypeSnapshots = new[]
            {
                ("Hauling", new string[] { "RearmTurrets", "Refuel", "UnloadCarriers", "HelpGatheringItemsForCaravan", "WorkGiver_GeneSplit", "HaulToGeneBank", "GenepackSingle_PlaceInGeneBank", "LoadTransporters", "EmptyWasteContainer", "HaulToGrowthVat", "HaulToPortal", "Strip", "HaulCorpses", "CarryToGeneExtractor", "CarryToGrowthVat", "HaulMechsToCharger", "CarryToSubcoreScanner", "HaulToCarrier", "HaulToSubcoreScanner", "HaulToWastepackAtomizer", "CarryToGeneRipper", "CookFillHopper", "DoBillsCremate", "DoBillsHaulCampfire", "TakeBeerOutOfFermentingBarrel", "EmptyEggBox", "FillFermentingBarrel", "HaulToInventory", "HaulGeneral", "DeliverResourcesToFrames", "DeliverResourcesToBlueprints", "HaulMerge" }),
                ("Cleaning", new string[] { "CleanClearSnow", "CleanFilth", "CleanClearPollution" }),
            },
        },
        new SampleRole
        {
            Id = 26, Label = "Hauler",
            TemplateDefName = "WS_Hauler", HolderScale = "Unskilled", GroupId = 1,
            Entries = new string[] { "WorkType:Hauling" },
            WorkTypeSnapshots = new[]
            {
                ("Hauling", new string[] { "RearmTurrets", "Refuel", "UnloadCarriers", "HelpGatheringItemsForCaravan", "WorkGiver_GeneSplit", "HaulToGeneBank", "GenepackSingle_PlaceInGeneBank", "LoadTransporters", "EmptyWasteContainer", "HaulToGrowthVat", "HaulToPortal", "Strip", "HaulCorpses", "CarryToGeneExtractor", "CarryToGrowthVat", "HaulMechsToCharger", "CarryToSubcoreScanner", "HaulToCarrier", "HaulToSubcoreScanner", "HaulToWastepackAtomizer", "CarryToGeneRipper", "CookFillHopper", "DoBillsCremate", "DoBillsHaulCampfire", "TakeBeerOutOfFermentingBarrel", "EmptyEggBox", "FillFermentingBarrel", "HaulToInventory", "HaulGeneral", "DeliverResourcesToFrames", "DeliverResourcesToBlueprints", "HaulMerge" }),
            },
        },
        new SampleRole
        {
            Id = 27, Label = "Cleaner",
            TemplateDefName = "WS_Cleaner", HolderScale = "Unskilled", GroupId = 1,
            Entries = new string[] { "WorkType:Cleaning" },
            WorkTypeSnapshots = new[]
            {
                ("Cleaning", new string[] { "CleanClearSnow", "CleanFilth", "CleanClearPollution" }),
            },
        },
        new SampleRole
        {
            Id = 28, Label = "Researcher",
            TemplateDefName = "WS_Researcher", HolderScale = "Research", GroupId = 5, PreserveRecommendationOrder = true,
            Entries = new string[] { "WorkType:Research" },
            WorkTypeSnapshots = new[]
            {
                ("Research", new string[] { "Hack", "CreateXenogerm", "StudyArchotechStructures", "Research", "GeneFab_DoBillsGeneBench", "LongRangeScan", "GroundPenetratingScan" }),
            },
        },
        new SampleRole
        {
            Id = 30, Label = "Patient",
            TemplateDefName = "WS_Patient", HolderScale = "Never", GroupId = 1,
            Entries = new string[] { "WorkType:Patient" },
            WorkTypeSnapshots = new[]
            {
                ("Patient", new string[] { "PatientGoToBedEmergencyTreatment", "PatientGoToBedTreatment" }),
            },
        },
        new SampleRole
        {
            Id = 31, Label = "Bedrest",
            TemplateDefName = "WS_Bedrest", HolderScale = "Never", GroupId = 1,
            Entries = new string[] { "WorkType:PatientBedRest" },
            WorkTypeSnapshots = new[]
            {
                ("PatientBedRest", new string[] { "PatientGoToBedRecuperate" }),
            },
        },
        new SampleRole
        {
            Id = 32, Label = "Laborer",
            TemplateDefName = "WS_Laborer", HolderScale = "Never", GroupId = 1,
            Entries = new string[] { "WorkType:BasicWorker" },
            WorkTypeSnapshots = new[]
            {
                ("BasicWorker", new string[] { "Flick", "BasicReleasePrisoner", "Open", "EjectFuel", "ExtractSkull" }),
            },
        },
        new SampleRole
        {
            Id = 34, Label = "Smith Mech",
            HolderScale = "Never", GroupId = 2,
            Entries = new string[] { "WorkGiver:RepairMech", "WorkGiver:DoBillsMechGestator", "WorkGiver:DoBillsSubcoreEncoder" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 35, Label = "Smelter",
            HolderScale = "Never", GroupId = 2,
            Entries = new string[] { "WorkGiver:DoBillsSmelter" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 36, Label = "Drug Maker",
            HolderScale = "Drug Fabrication", GroupId = 2,
            Entries = new string[] { "WorkGiver:DoBillsProduceDrugs" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 37, Label = "Joint Maker",
            HolderScale = "Never", GroupId = 2,
            Entries = new string[] { "WorkGiver:DoBillsProduceDrugs" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 38, Label = "Miner Away",
            HasRules = true, HolderScale = "Never", GroupId = 0,
            Entries = new string[] { "WorkType:Mining" },
            WorkTypeSnapshots = new[]
            {
                ("Mining", new string[] { "Mine", "Drill" }),
            },
        },
        new SampleRole
        {
            Id = 39, Label = "Farmer Away",
            HasRules = true, HolderScale = "Never", GroupId = 0,
            Entries = new string[] { "WorkType:Growing", "WorkType:PlantCutting" },
            WorkTypeSnapshots = new[]
            {
                ("Growing", new string[] { "GrowerHarvest", "PlantSeed", "Replant", "GrowerSow" }),
                ("PlantCutting", new string[] { "ExtractTree", "PlantsCut" }),
            },
        },
        new SampleRole
        {
            Id = 40, Label = "Haul Now",
            HolderScale = "Never", GroupId = 1,
            Entries = new string[] { "WorkGiver:UnloadCarriers", "WorkGiver:HelpGatheringItemsForCaravan", "WorkGiver:HaulToGeneBank", "WorkGiver:GenepackSingle_PlaceInGeneBank", "WorkGiver:LoadTransporters", "WorkGiver:HaulToPortal", "WorkGiver:HaulCorpses", "WorkGiver:CarryToGeneRipper", "WorkGiver:HaulToInventory", "WorkGiver:HaulGeneral" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 43, Label = "Core",
            TemplateDefName = "WS_Core", AutoAssign = true, HolderScale = "Never", GroupId = 1,
            Entries = new string[] { "WorkType:Firefighter", "WorkType:Patient", "WorkGiver:DoctorRescue" },
            WorkTypeSnapshots = new[]
            {
                ("Firefighter", new string[] { "FightFires" }),
                ("Patient", new string[] { "PatientGoToBedEmergencyTreatment", "PatientGoToBedTreatment" }),
            },
        },
        new SampleRole
        {
            Id = 44, Label = "Jailor",
            TemplateDefName = "WS_Jailor", HolderScale = "Never", GroupId = 7, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkGiver:DoExecution", "WorkGiver:ExecuteGuiltyColonist", "WorkGiver:ReleasePrisoner", "WorkGiver:TakePrisonerToBed", "WorkGiver:FeedPrisoner", "WorkGiver:DeliverHemogenToPrisoner", "WorkGiver:DeliverFoodToPrisoner", "WorkGiver:ChatWithPrisoner" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 45, Label = "Nurse",
            TemplateDefName = "WS_Nurse", HolderScale = "Never", GroupId = 8, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkGiver:DoctorTendToSelfEmergency", "WorkGiver:DoctorRescue", "WorkGiver:DoctorFeedHumanlikes", "WorkGiver:DoctorTendToAnimals", "WorkGiver:DoctorFeedAnimals", "WorkGiver:VisitSickPawn", "WorkGiver:TakeToBedToOperate", "WorkGiver:FeedHemogen" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 47, Label = "Painter",
            TemplateDefName = "WS_Painter", HolderScale = "Never", GroupId = 2,
            Entries = new string[] { "WorkGiver:RemovePaintBuilding", "WorkGiver:RemovePaintFloor", "WorkGiver:PaintBuilding", "WorkGiver:PaintFloor" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 48, Label = "Herder",
            TemplateDefName = "WS_Herder", HolderScale = "Never", GroupId = 3, UsesOccasionalRepeatChampionPenalty = true,
            Entries = new string[] { "WorkGiver:TakeRoamingAnimalsToPen", "WorkGiver:HandlingFeedPatientAnimals", "WorkGiver:TakeToPen", "WorkGiver:Slaughter", "WorkGiver:ReleaseToWild", "WorkGiver:Milk", "WorkGiver:Shear", "WorkGiver:RebalanceAnimalsInPens" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 49, Label = "Firefighter",
            TemplateDefName = "WS_Firefighter", HolderScale = "Never", GroupId = 1,
            Entries = new string[] { "WorkType:Firefighter" },
            WorkTypeSnapshots = new[]
            {
                ("Firefighter", new string[] { "FightFires" }),
            },
        },
        new SampleRole
        {
            Id = 50, Label = "Gene Maker",
            HolderScale = "Never", GroupId = 5,
            Entries = new string[] { "WorkGiver:GeneFab_DoBillsGeneBench" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 52, Label = "Mech",
            HolderScale = "Never", GroupId = 2,
            Entries = new string[] { "WorkGiver:RepairMech", "WorkGiver:DoBillsMechGestator", "WorkGiver:DoBillsSubcoreEncoder" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
        new SampleRole
        {
            Id = 53, Label = "Fix Roof",
            HolderScale = "Never", GroupId = 2,
            Entries = new string[] { "WorkGiver:BuildRoofs" },
            WorkTypeSnapshots = new (string, string[])[0],
        },
    };

    public static readonly SamplePath[] Paths =
    {
        new SamplePath
        {
            Id = 1, Name = "Drug Maker",
            RoleIds = new[] { 28, 24, 36 },
            BandMins = new[] { 0, 0, 12 },
            BandMaxes = new[] { 21, 21, 21 },
            AnchorRoleId = 1, AnchorBefore = false,
        },
        new SamplePath
        {
            Id = 2, Name = "Fabricator",
            RoleIds = new[] { 21, 20, 24 },
            BandMins = new[] { 8, 4, 0 },
            BandMaxes = new[] { 21, 21, 21 },
            AnchorRoleId = 1, AnchorBefore = false,
        },
        new SamplePath
        {
            Id = 3, Name = "Cook",
            RoleIds = new[] { 10, 11, 9 },
            BandMins = new[] { 0, 0, 8 },
            BandMaxes = new[] { 8, 8, 21 },
            AnchorRoleId = 1, AnchorBefore = false,
        },
        new SamplePath
        {
            Id = 4, Name = "Farmer",
            RoleIds = new[] { 18, 17, 16 },
            BandMins = new[] { 0, 4, 12 },
            BandMaxes = new[] { 12, 21, 21 },
            AnchorRoleId = 19, AnchorBefore = true,
        },
        new SamplePath
        {
            Id = 5, Name = "Socialist",
            RoleIds = new[] { 44, 7, 6 },
            BandMins = new[] { 0, 4, 6 },
            BandMaxes = new[] { 4, 21, 21 },
            AnchorRoleId = -1, AnchorBefore = true,
        },
        new SamplePath
        {
            Id = 6, Name = "Artist",
            RoleIds = new[] { 47, 23 },
            BandMins = new[] { 0, 8 },
            BandMaxes = new[] { 8, 21 },
            AnchorRoleId = -1, AnchorBefore = true,
        },
        new SamplePath
        {
            Id = 7, Name = "Handler",
            RoleIds = new[] { 48, 8 },
            BandMins = new[] { 0, 8 },
            BandMaxes = new[] { 8, 21 },
            AnchorRoleId = -1, AnchorBefore = true,
        },
        new SamplePath
        {
            Id = 8, Name = "Builder",
            RoleIds = new[] { 15, 14 },
            BandMins = new[] { 0, 8 },
            BandMaxes = new[] { 8, 21 },
            AnchorRoleId = 1, AnchorBefore = false,
        },
        new SamplePath
        {
            Id = 9, Name = "Smith",
            RoleIds = new[] { 24, 20 },
            BandMins = new[] { 0, 4 },
            BandMaxes = new[] { 21, 21 },
            AnchorRoleId = 21, AnchorBefore = false,
        },
        new SamplePath
        {
            Id = 10, Name = "Tailor",
            RoleIds = new[] { 24, 22 },
            BandMins = new[] { 0, 2 },
            BandMaxes = new[] { 21, 21 },
            AnchorRoleId = 20, AnchorBefore = true,
        },
        new SamplePath
        {
            Id = 11, Name = "Doctor",
            RoleIds = new[] { 45, 5, 3 },
            BandMins = new[] { 0, 5, 15 },
            BandMaxes = new[] { 5, 15, 21 },
            AnchorRoleId = 43, AnchorBefore = false,
        },
    };

    public static readonly SamplePawn[] Pawns =
    {
        new SamplePawn
        {
            Name = "Dul", ThingId = "Thing_Human197159", MapIndex = 0,
            FirstName = "Dul", NickName = null, LastName = "Hysott",
            AgeBiologicalTicks = 225094333L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 14, "None"),
                ("Melee", 5, "None"),
                ("Construction", 1, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 16, "Major"),
                ("Plants", 13, "Major"),
                ("Animals", 16, "Minor"),
                ("Crafting", 3, "None"),
                ("Artistic", 3, "None"),
                ("Medicine", 0, "None"),
                ("Social", 11, "Major"),
                ("Intellectual", 5, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Kind", 0), ("Bloodlust", 0), ("Nimble", 0), ("PsychicSensitivity", -1) },
            ActiveGenes = new string[] { "Body_Hulk", "VoiceRoar", "Furskin", "Hair_BaldOnly", "Tail_Furry", "Beard_Always", "AnimalWarcall", "Robust", "MeleeDamage_Strong", "Sleepy", "PsychicAbility_Dull", "NakedSpeed", "WoundHealing_Slow", "Aggression_Aggressive", "AptitudeRemarkable_Animals", "AptitudeTerrible_Mining", "Skin_Melanin3", "Hair_DarkReddish" },
            AptitudeGenes = new[]
            {
                ("AptitudeRemarkable_Animals", "AptitudeRemarkable", "Animals", false),
                ("AptitudeTerrible_Mining", "AptitudeTerrible", "Mining", false),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (9, "Enabled", false),
                (8, "Enabled", false),
                (6, "Enabled", false),
                (7, "Enabled", false),
                (12, "Enabled", false),
                (16, "Enabled", false),
                (23, "Enabled", false),
                (13, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Flow", ThingId = "Thing_Human2069852", MapIndex = 0,
            FirstName = "Florian", NickName = "Flow", LastName = "Wiekhorst",
            AgeBiologicalTicks = 109260554L,
            HasRangedWeapon = true, CustomXenotype = true,
            Skills = new[]
            {
                ("Shooting", 20, "Major"),
                ("Melee", 0, "None"),
                ("Construction", 20, "Major"),
                ("Mining", 0, "None"),
                ("Cooking", 0, "None"),
                ("Plants", 0, "None"),
                ("Animals", 0, "None"),
                ("Crafting", 20, "Major"),
                ("Artistic", 0, "None"),
                ("Medicine", 0, "None"),
                ("Social", 0, "None"),
                ("Intellectual", 0, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Transhumanist", 0), ("SpeedOffset", 2), ("Kind", 0) },
            ActiveGenes = new string[] { "Body_Fat", "Jaw_Heavy", "Brow_Heavy", "Robust", "MinTemp_SmallDecrease", "Skin_Melanin2", "Hair_SandyBlonde", "ArchiteMetabolism", "AptitudeStrong_Shooting", "AptitudeTerrible_Melee", "AptitudeTerrible_Mining", "AptitudeRemarkable_Construction", "AptitudeTerrible_Cooking", "AptitudeTerrible_Plants", "AptitudeTerrible_Animals", "AptitudeRemarkable_Crafting", "AptitudeTerrible_Artistic", "AptitudeTerrible_Medicine", "AptitudeTerrible_Social", "AptitudeTerrible_Intellectual", "PerfectImmunity", "Mood_Optimist", "Deathless", "MaxTemp_SmallDecrease", "TotalHealing", "Unstoppable", "Ageless", "Learning_Fast", "MeleeDamage_Weak", "WoundHealing_SuperFast", "Aggression_DeadCalm", "Beard_BushyOnly", "KindInstinct", "Superclotting", "Sterile", "Pain_Reduced", "MoveSpeed_VeryQuick", "ElongatedFingers" },
            AptitudeGenes = new[]
            {
                ("AptitudeStrong_Shooting", "AptitudeStrong", "Shooting", true),
                ("AptitudeTerrible_Melee", "AptitudeTerrible", "Melee", true),
                ("AptitudeTerrible_Mining", "AptitudeTerrible", "Mining", true),
                ("AptitudeRemarkable_Construction", "AptitudeRemarkable", "Construction", true),
                ("AptitudeTerrible_Cooking", "AptitudeTerrible", "Cooking", true),
                ("AptitudeTerrible_Plants", "AptitudeTerrible", "Plants", true),
                ("AptitudeTerrible_Animals", "AptitudeTerrible", "Animals", true),
                ("AptitudeRemarkable_Crafting", "AptitudeRemarkable", "Crafting", true),
                ("AptitudeTerrible_Artistic", "AptitudeTerrible", "Artistic", true),
                ("AptitudeTerrible_Medicine", "AptitudeTerrible", "Medicine", true),
                ("AptitudeTerrible_Social", "AptitudeTerrible", "Social", true),
                ("AptitudeTerrible_Intellectual", "AptitudeTerrible", "Intellectual", true),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (52, "Enabled", false),
                (24, "Enabled", false),
                (14, "Enabled", false),
                (21, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (12, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Barbor", ThingId = "Thing_Human1678358", MapIndex = 1,
            FirstName = "Barborbar", NickName = "Barbor", LastName = "Bico",
            AgeBiologicalTicks = 184252468L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 17, "Major"),
                ("Melee", 4, "None"),
                ("Construction", 1, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 1, "None"),
                ("Plants", 4, "None"),
                ("Animals", 12, "Minor"),
                ("Crafting", 2, "None"),
                ("Artistic", 5, "None"),
                ("Medicine", 11, "None"),
                ("Social", 7, "None"),
                ("Intellectual", 12, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("PsychicSensitivity", 2) },
            ActiveGenes = new string[] { "Skin_Melanin3", "Hair_DarkBrown" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (5, "Enabled", false),
                (1, "Enabled", false),
                (8, "Enabled", false),
                (6, "Enabled", false),
                (7, "Enabled", false),
                (13, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (12, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Bashy", ThingId = "Thing_Human1981057", MapIndex = 1,
            FirstName = "Caitlin", NickName = "Bashy", LastName = "Pedersen",
            AgeBiologicalTicks = 184104916L,
            Skills = new[]
            {
                ("Shooting", 8, "None"),
                ("Melee", 14, "Major"),
                ("Construction", 2, "None"),
                ("Mining", 2, "None"),
                ("Cooking", 4, "None"),
                ("Plants", 9, "None"),
                ("Animals", 5, "None"),
                ("Crafting", 4, "None"),
                ("Artistic", 0, "None"),
                ("Social", 12, "Major"),
                ("Intellectual", 5, "None"),
            },
            DisabledSkills = new string[] { "Medicine" },
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Cleaning", "Construction", "Cooking", "Crafting", "Firefighter", "Growing", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Beauty", 1) },
            ActiveGenes = new string[] { "Skin_Melanin5", "Hair_MidBlack" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (7, "Enabled", false),
                (39, "Enabled", false),
                (24, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Blackwell", ThingId = "Thing_Human3153002", MapIndex = 1,
            FirstName = "Sadako", NickName = "Blackwell", LastName = "Fischer",
            AgeBiologicalTicks = 81715344L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 17, "Major"),
                ("Melee", 1, "None"),
                ("Construction", 0, "None"),
                ("Mining", 1, "None"),
                ("Cooking", 14, "Minor"),
                ("Plants", 12, "Minor"),
                ("Animals", 1, "None"),
                ("Crafting", 3, "Minor"),
                ("Artistic", 0, "None"),
                ("Medicine", 3, "None"),
                ("Social", 7, "Major"),
                ("Intellectual", 2, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("TooSmart", 0), ("Transhumanist", 0) },
            ActiveGenes = new string[] { "Skin_Melanin4", "Hair_DarkBlack" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (39, "Enabled", false),
                (9, "Enabled", false),
                (6, "Enabled", false),
                (7, "Enabled", false),
                (26, "Enabled", false),
                (12, "Enabled", false),
                (16, "Enabled", false),
                (34, "Enabled", false),
                (24, "Enabled", false),
                (27, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Cockroach", ThingId = "Thing_Human2618185", MapIndex = 1,
            FirstName = "Brabor", NickName = "Cockroach", LastName = "Verea",
            AgeBiologicalTicks = 142670582L,
            HasRangedWeapon = true, CustomXenotype = true,
            Skills = new[]
            {
                ("Shooting", 20, "Major"),
                ("Melee", 0, "None"),
                ("Construction", 0, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 20, "Major"),
                ("Plants", 0, "None"),
                ("Animals", 0, "None"),
                ("Crafting", 20, "Major"),
                ("Artistic", 0, "None"),
                ("Medicine", 0, "None"),
                ("Social", 0, "None"),
                ("Intellectual", 18, "Minor"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Kind", 0), ("Transhumanist", 0), ("SpeedOffset", 1) },
            ActiveGenes = new string[] { "Body_Fat", "Jaw_Heavy", "Brow_Heavy", "Pain_Reduced", "MinTemp_SmallDecrease", "Skin_Melanin3", "Hair_InkBlack", "Immunity_Strong", "Aggression_DeadCalm", "MoveSpeed_Quick", "AptitudeStrong_Shooting", "MeleeDamage_Weak", "Sterile", "AptitudeTerrible_Melee", "AptitudeTerrible_Mining", "AptitudeTerrible_Construction", "AptitudeRemarkable_Cooking", "AptitudeTerrible_Plants", "AptitudeTerrible_Animals", "AptitudeRemarkable_Crafting", "AptitudeTerrible_Artistic", "AptitudeTerrible_Medicine", "AptitudeTerrible_Social", "AptitudeRemarkable_Intellectual", "Ageless", "Learning_Fast", "MaxTemp_LargeIncrease", "Tail_Furry", "UVSensitivity_Mild", "Beauty_VeryUgly", "Robust" },
            AptitudeGenes = new[]
            {
                ("AptitudeStrong_Shooting", "AptitudeStrong", "Shooting", true),
                ("AptitudeTerrible_Melee", "AptitudeTerrible", "Melee", true),
                ("AptitudeTerrible_Mining", "AptitudeTerrible", "Mining", true),
                ("AptitudeTerrible_Construction", "AptitudeTerrible", "Construction", true),
                ("AptitudeRemarkable_Cooking", "AptitudeRemarkable", "Cooking", true),
                ("AptitudeTerrible_Plants", "AptitudeTerrible", "Plants", true),
                ("AptitudeTerrible_Animals", "AptitudeTerrible", "Animals", true),
                ("AptitudeRemarkable_Crafting", "AptitudeRemarkable", "Crafting", true),
                ("AptitudeTerrible_Artistic", "AptitudeTerrible", "Artistic", true),
                ("AptitudeTerrible_Medicine", "AptitudeTerrible", "Medicine", true),
                ("AptitudeTerrible_Social", "AptitudeTerrible", "Social", true),
                ("AptitudeRemarkable_Intellectual", "AptitudeRemarkable", "Intellectual", true),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (9, "Enabled", false),
                (35, "Enabled", false),
                (21, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (36, "Enabled", false),
                (24, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (28, "Enabled", false),
                (50, "Enabled", false),
                (12, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Flo", ThingId = "Thing_Human3818243", MapIndex = 1,
            FirstName = "Flo", NickName = "Flo", LastName = "Sandoval",
            AgeBiologicalTicks = 90017440L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 9, "Minor"),
                ("Melee", 0, "None"),
                ("Construction", 7, "Minor"),
                ("Mining", 5, "None"),
                ("Cooking", 3, "None"),
                ("Plants", 7, "Minor"),
                ("Animals", 3, "None"),
                ("Crafting", 4, "None"),
                ("Artistic", 1, "None"),
                ("Medicine", 7, "Major"),
                ("Social", 4, "None"),
                ("Intellectual", 0, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("TooSmart", 0) },
            ActiveGenes = new string[] { "Skin_Melanin1", "Hair_DarkBlack" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (5, "Enabled", false),
                (1, "Enabled", false),
                (15, "Enabled", false),
                (17, "Enabled", false),
                (18, "Enabled", false),
                (19, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Gorilla", ThingId = "Thing_Human3690364", MapIndex = 1,
            FirstName = "Ñotre", NickName = "Gorilla", LastName = "Vecambacha",
            AgeBiologicalTicks = 73738154L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 3, "None"),
                ("Melee", 5, "Major"),
                ("Construction", 7, "Major"),
                ("Mining", 2, "None"),
                ("Cooking", 3, "Minor"),
                ("Plants", 3, "None"),
                ("Animals", 0, "None"),
                ("Crafting", 4, "Major"),
                ("Artistic", 2, "None"),
                ("Medicine", 4, "Minor"),
                ("Social", 0, "None"),
                ("Intellectual", 0, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("SpeedOffset", 2), ("FastLearner", 0) },
            ActiveGenes = new string[] { "Body_Hulk", "Jaw_Heavy", "Brow_Heavy", "MeleeDamage_Strong", "Robust", "Immunity_Strong", "Aggression_Aggressive", "Learning_Slow", "MoveSpeed_Slow", "Pain_Reduced", "MinTemp_SmallDecrease", "MaxTemp_SmallIncrease", "AptitudePoor_Intellectual", "AptitudePoor_Social", "AptitudePoor_Shooting", "Skin_Melanin2", "Hair_ReddishBrown" },
            AptitudeGenes = new[]
            {
                ("AptitudePoor_Intellectual", "AptitudePoor", "Intellectual", false),
                ("AptitudePoor_Social", "AptitudePoor", "Social", false),
                ("AptitudePoor_Shooting", "AptitudePoor", "Shooting", false),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (53, "Enabled", false),
                (45, "Enabled", false),
                (1, "Enabled", false),
                (39, "Enabled", false),
                (15, "Enabled", false),
                (10, "Enabled", false),
                (11, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (12, "Enabled", false),
                (24, "Enabled", false),
                (25, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Han", ThingId = "Thing_Human3462164", MapIndex = 1,
            FirstName = "Han", NickName = null, LastName = "Nalbant",
            AgeBiologicalTicks = 122010036L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 14, "Major"),
                ("Melee", 6, "Major"),
                ("Construction", 1, "None"),
                ("Mining", 1, "None"),
                ("Cooking", 4, "Minor"),
                ("Plants", 2, "None"),
                ("Animals", 7, "Major"),
                ("Crafting", 4, "Minor"),
                ("Artistic", 2, "None"),
                ("Medicine", 2, "None"),
                ("Social", 1, "None"),
                ("Intellectual", 1, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("NightOwl", 0) },
            ActiveGenes = new string[] { "Skin_Melanin3", "Hair_DarkBrown" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (39, "Enabled", false),
                (26, "Enabled", false),
                (10, "Enabled", false),
                (11, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (48, "Enabled", false),
                (12, "Enabled", false),
                (24, "Enabled", false),
                (13, "Enabled", false),
                (25, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Hasonove", ThingId = "Thing_Human486365", MapIndex = 1,
            FirstName = "Hasonove", NickName = "Hasonove", LastName = "Worrell",
            AgeBiologicalTicks = 142507907L,
            HasRangedWeapon = true, CustomXenotype = true,
            Skills = new[]
            {
                ("Shooting", 20, "Major"),
                ("Melee", 0, "None"),
                ("Construction", 8, "Minor"),
                ("Mining", 0, "None"),
                ("Cooking", 20, "Major"),
                ("Plants", 0, "None"),
                ("Animals", 0, "None"),
                ("Crafting", 20, "Major"),
                ("Artistic", 0, "None"),
                ("Medicine", 0, "None"),
                ("Social", 0, "None"),
                ("Intellectual", 20, "Minor"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("PsychicSensitivity", 1) },
            ActiveGenes = new string[] { "Skin_DeepRed", "Hair_SandyBlonde", "Headbone_MiniHorns", "FireSpew", "FireResistant", "MinTemp_SmallIncrease", "MeleeDamage_Weak", "Skin_Melanin6", "MoveSpeed_Quick", "Deathless", "MaxTemp_SmallDecrease", "WoundHealing_Fast", "VacuumResistance_Partial", "Ageless", "Ears_Pig", "Aggression_DeadCalm", "Beard_BushyOnly", "Tail_Furry", "UVSensitivity_Mild", "Beauty_VeryUgly", "Robust", "AptitudeRemarkable_Intellectual", "AptitudeTerrible_Social", "AptitudeTerrible_Medicine", "AptitudeTerrible_Artistic", "AptitudeRemarkable_Crafting", "AptitudeTerrible_Animals", "AptitudeTerrible_Plants", "AptitudeTerrible_Mining", "AptitudeStrong_Shooting", "AptitudeTerrible_Melee", "Sterile", "Superclotting", "Instability_Major", "PerfectImmunity", "Mood_Optimist", "TotalHealing", "Unstoppable", "AptitudeRemarkable_Cooking" },
            AptitudeGenes = new[]
            {
                ("AptitudeRemarkable_Intellectual", "AptitudeRemarkable", "Intellectual", true),
                ("AptitudeTerrible_Social", "AptitudeTerrible", "Social", true),
                ("AptitudeTerrible_Medicine", "AptitudeTerrible", "Medicine", true),
                ("AptitudeTerrible_Artistic", "AptitudeTerrible", "Artistic", true),
                ("AptitudeRemarkable_Crafting", "AptitudeRemarkable", "Crafting", true),
                ("AptitudeTerrible_Animals", "AptitudeTerrible", "Animals", true),
                ("AptitudeTerrible_Plants", "AptitudeTerrible", "Plants", true),
                ("AptitudeTerrible_Mining", "AptitudeTerrible", "Mining", true),
                ("AptitudeStrong_Shooting", "AptitudeStrong", "Shooting", true),
                ("AptitudeTerrible_Melee", "AptitudeTerrible", "Melee", true),
                ("AptitudeRemarkable_Cooking", "AptitudeRemarkable", "Cooking", true),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (21, "Enabled", false),
                (9, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (36, "Enabled", false),
                (14, "Enabled", false),
                (24, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (28, "Enabled", false),
                (50, "Enabled", false),
                (12, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Hazelnut", ThingId = "Thing_Human3152950", MapIndex = 1,
            FirstName = "Hazel", NickName = "Hazelnut", LastName = "Rella",
            AgeBiologicalTicks = 91762622L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 16, "Major"),
                ("Melee", 9, "Minor"),
                ("Construction", 2, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 7, "Minor"),
                ("Plants", 6, "None"),
                ("Animals", 4, "None"),
                ("Crafting", 0, "None"),
                ("Artistic", 1, "None"),
                ("Medicine", 4, "None"),
                ("Social", 6, "Minor"),
                ("Intellectual", 3, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("PsychicSensitivity", -1) },
            ActiveGenes = new string[] { "Skin_Melanin5", "Hair_DarkBlack" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (39, "Enabled", false),
                (9, "Enabled", false),
                (10, "Enabled", false),
                (11, "Enabled", false),
                (7, "Enabled", false),
                (12, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Jet", ThingId = "Thing_Human260132", MapIndex = 1,
            FirstName = "Hokojin", NickName = "Jet", LastName = "Buraku",
            AgeBiologicalTicks = 177606198L,
            HasRangedWeapon = true, CustomXenotype = true,
            Skills = new[]
            {
                ("Shooting", 20, "Minor"),
                ("Melee", 0, "None"),
                ("Construction", 20, "Major"),
                ("Mining", 0, "None"),
                ("Cooking", 0, "None"),
                ("Plants", 0, "None"),
                ("Animals", 0, "None"),
                ("Crafting", 20, "Major"),
                ("Artistic", 0, "None"),
                ("Social", 0, "None"),
                ("Intellectual", 20, "Major"),
            },
            DisabledSkills = new string[] { "Medicine" },
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Cleaning", "Construction", "Cooking", "Crafting", "Firefighter", "Growing", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Beauty", -1), ("Wimp", 0), ("Kind", 0) },
            ActiveGenes = new string[] { "Skin_Melanin2", "Hair_Blonde", "ArchiteMetabolism", "PerfectImmunity", "Mood_Optimist", "Deathless", "MaxTemp_SmallDecrease", "TotalHealing", "Unstoppable", "Ageless", "Learning_Fast", "Tail_Furry", "UVSensitivity_Mild", "AptitudeRemarkable_Intellectual", "AptitudeRemarkable_Crafting", "AptitudeRemarkable_Construction", "AptitudeStrong_Shooting", "AptitudeTerrible_Melee", "AptitudeTerrible_Mining", "AptitudeTerrible_Cooking", "AptitudeTerrible_Plants", "AptitudeTerrible_Animals", "AptitudeTerrible_Artistic", "AptitudeTerrible_Medicine", "AptitudeTerrible_Social", "ElongatedFingers", "KindInstinct", "Aggression_DeadCalm", "MeleeDamage_Weak", "Superclotting", "WoundHealing_SuperFast", "Pain_Reduced", "Sterile", "MoveSpeed_Quick", "Beauty_VeryUgly", "Robust" },
            AptitudeGenes = new[]
            {
                ("AptitudeRemarkable_Intellectual", "AptitudeRemarkable", "Intellectual", true),
                ("AptitudeRemarkable_Crafting", "AptitudeRemarkable", "Crafting", true),
                ("AptitudeRemarkable_Construction", "AptitudeRemarkable", "Construction", true),
                ("AptitudeStrong_Shooting", "AptitudeStrong", "Shooting", true),
                ("AptitudeTerrible_Melee", "AptitudeTerrible", "Melee", true),
                ("AptitudeTerrible_Mining", "AptitudeTerrible", "Mining", true),
                ("AptitudeTerrible_Cooking", "AptitudeTerrible", "Cooking", true),
                ("AptitudeTerrible_Plants", "AptitudeTerrible", "Plants", true),
                ("AptitudeTerrible_Animals", "AptitudeTerrible", "Animals", true),
                ("AptitudeTerrible_Artistic", "AptitudeTerrible", "Artistic", true),
                ("AptitudeTerrible_Medicine", "AptitudeTerrible", "Medicine", true),
                ("AptitudeTerrible_Social", "AptitudeTerrible", "Social", true),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (53, "Enabled", false),
                (14, "Enabled", false),
                (36, "Enabled", false),
                (21, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (24, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (28, "Enabled", false),
                (50, "Enabled", false),
                (12, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Koen", ThingId = "Thing_Human1959890", MapIndex = 1,
            FirstName = "Koen", NickName = "Koen", LastName = "von Katze",
            AgeBiologicalTicks = 197317097L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 15, "None"),
                ("Melee", 2, "None"),
                ("Construction", 2, "None"),
                ("Mining", 4, "None"),
                ("Cooking", 0, "None"),
                ("Plants", 13, "Major"),
                ("Animals", 13, "Major"),
                ("Crafting", 16, "Major"),
                ("Artistic", 3, "None"),
                ("Medicine", 5, "None"),
                ("Intellectual", 10, "None"),
            },
            DisabledSkills = new string[] { "Social" },
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring" },
            Traits = new[] { ("ShootingAccuracy", -1), ("GreatMemory", 0) },
            ActiveGenes = new string[] { "Nose_Pig", "Ears_Pig", "Body_Fat", "Hands_Pig", "VoicePig", "Pain_Reduced", "Immunity_Strong", "StrongStomach", "RobustDigestion", "Nearsighted", "AptitudePoor_Cooking", "Skin_Melanin3", "Hair_DarkSaturatedReddish" },
            AptitudeGenes = new[]
            {
                ("AptitudePoor_Cooking", "AptitudePoor", "Cooking", false),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (5, "Enabled", false),
                (1, "Enabled", false),
                (8, "Enabled", false),
                (24, "Enabled", false),
                (21, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (12, "Enabled", false),
                (16, "Enabled", false),
                (19, "Enabled", false),
                (13, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Macey", ThingId = "Thing_Human3152865", MapIndex = 1,
            FirstName = "Macey", NickName = "Macey", LastName = "Cox",
            AgeBiologicalTicks = 121466324L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 16, "Major"),
                ("Melee", 2, "None"),
                ("Construction", 3, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 4, "Minor"),
                ("Plants", 0, "None"),
                ("Animals", 0, "None"),
                ("Crafting", 20, "Minor"),
                ("Medicine", 1, "None"),
                ("Intellectual", 19, "Major"),
            },
            DisabledSkills = new string[] { "Artistic", "Social" },
            CapableWorkTypes = new string[] { "BasicWorker", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring" },
            Traits = new[] { ("PsychicSensitivity", 2), ("Wimp", 0), ("Delicate", 0) },
            ActiveGenes = new string[] { "Skin_Melanin4", "Hair_ReddishBrown", "Hair_BaldOnly", "Beard_NoBeardOnly", "Body_Thin", "ElongatedFingers", "Pain_Extra", "Delicate", "Aggression_DeadCalm", "AptitudeRemarkable_Intellectual", "AptitudeRemarkable_Crafting", "AptitudeTerrible_Social", "AptitudePoor_Animals", "AptitudePoor_Plants" },
            AptitudeGenes = new[]
            {
                ("AptitudeRemarkable_Intellectual", "AptitudeRemarkable", "Intellectual", true),
                ("AptitudeRemarkable_Crafting", "AptitudeRemarkable", "Crafting", true),
                ("AptitudeTerrible_Social", "AptitudeTerrible", "Social", true),
                ("AptitudePoor_Animals", "AptitudePoor", "Animals", true),
                ("AptitudePoor_Plants", "AptitudePoor", "Plants", true),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (21, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (36, "Enabled", false),
                (24, "Enabled", false),
                (11, "Enabled", false),
                (12, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (28, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Madcat", ThingId = "Thing_Human669", MapIndex = 1,
            FirstName = "Lewis", NickName = "Madcat", LastName = "von Katze",
            AgeBiologicalTicks = 240945554L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 16, "Minor"),
                ("Melee", 5, "None"),
                ("Construction", 1, "None"),
                ("Mining", 2, "None"),
                ("Cooking", 15, "Major"),
                ("Plants", 10, "None"),
                ("Animals", 10, "Minor"),
                ("Crafting", 9, "None"),
                ("Artistic", 1, "None"),
                ("Medicine", 14, "Minor"),
                ("Social", 10, "Minor"),
                ("Intellectual", 11, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Neurotic", 1), ("PsychicSensitivity", -2) },
            ActiveGenes = new string[] { "Skin_Melanin2", "Hair_MidBlack" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (3, "Enabled", false),
                (1, "Enabled", false),
                (40, "Enabled", true),
                (8, "Enabled", false),
                (9, "Enabled", false),
                (6, "Enabled", false),
                (7, "Enabled", false),
                (17, "Enabled", false),
                (18, "Enabled", false),
                (19, "Enabled", false),
                (13, "Enabled", false),
                (25, "Enabled", false),
                (12, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Morg", ThingId = "Thing_Human3920837", MapIndex = 1,
            FirstName = "Nathan", NickName = "Morg", LastName = "Cruzan",
            AgeBiologicalTicks = 98218950L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 2, "None"),
                ("Melee", 4, "Minor"),
                ("Construction", 8, "Minor"),
                ("Mining", 13, "Major"),
                ("Cooking", 2, "None"),
                ("Plants", 0, "None"),
                ("Animals", 0, "None"),
                ("Crafting", 1, "None"),
                ("Artistic", 3, "None"),
                ("Medicine", 2, "None"),
                ("Social", 1, "None"),
                ("Intellectual", 0, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Undergrounder", 0), ("QuickSleeper", 0), ("Tough", 0) },
            ActiveGenes = new string[] { "Skin_PaleYellow", "Hair_SandyBlonde", "Beard_NoBeardOnly", "Headbone_MiniHorns", "FireSpew", "MoveSpeed_VeryQuick", "FireResistant", "MaxTemp_LargeIncrease", "MinTemp_SmallIncrease", "Immunity_Weak", "WoundHealing_Slow", "MeleeDamage_Weak", "Mood_Pessimist", "AptitudePoor_Plants", "AptitudePoor_Animals", "Skin_Melanin2" },
            AptitudeGenes = new[]
            {
                ("AptitudePoor_Plants", "AptitudePoor", "Plants", false),
                ("AptitudePoor_Animals", "AptitudePoor", "Animals", false),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (19, "Enabled", false),
                (15, "Enabled", false),
                (25, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Niklas", ThingId = "Thing_Human2823094", MapIndex = 1,
            FirstName = "Niklas", NickName = "Niklas", LastName = "Fischer",
            AgeBiologicalTicks = 110711627L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 17, "Major"),
                ("Melee", 5, "None"),
                ("Construction", 2, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 13, "Minor"),
                ("Plants", 6, "None"),
                ("Animals", 12, "Minor"),
                ("Crafting", 8, "Minor"),
                ("Medicine", 2, "None"),
                ("Social", 4, "None"),
                ("Intellectual", 3, "None"),
            },
            DisabledSkills = new string[] { "Artistic" },
            CapableWorkTypes = new string[] { "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Undergrounder", 0), ("ShootingAccuracy", -1), ("DrugDesire", 2), ("PsychicSensitivity", -1) },
            ActiveGenes = new string[] { "Body_Hulk", "VoiceRoar", "Furskin", "Hair_BaldOnly", "Tail_Furry", "Beard_Always", "AnimalWarcall", "Robust", "MeleeDamage_Strong", "Sleepy", "PsychicAbility_Dull", "NakedSpeed", "WoundHealing_Slow", "Aggression_Aggressive", "AptitudeRemarkable_Animals", "AptitudeTerrible_Mining", "Skin_Melanin6", "Hair_DarkBlack" },
            AptitudeGenes = new[]
            {
                ("AptitudeRemarkable_Animals", "AptitudeRemarkable", "Animals", false),
                ("AptitudeTerrible_Mining", "AptitudeTerrible", "Mining", false),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (9, "Disabled", false),
                (35, "Enabled", false),
                (39, "Enabled", false),
                (21, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (8, "Enabled", false),
                (24, "Enabled", false),
                (13, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (12, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Noah", ThingId = "Thing_Human1406569", MapIndex = 1,
            FirstName = "Noah", NickName = "Noah", LastName = "Harvey",
            AgeBiologicalTicks = 111638916L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 17, "Minor"),
                ("Melee", 10, "Minor"),
                ("Construction", 0, "None"),
                ("Mining", 2, "None"),
                ("Cooking", 1, "None"),
                ("Plants", 12, "None"),
                ("Animals", 3, "None"),
                ("Crafting", 1, "None"),
                ("Artistic", 1, "None"),
                ("Medicine", 2, "None"),
                ("Social", 4, "None"),
                ("Intellectual", 3, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Nudist", 0) },
            ActiveGenes = new string[] { "Skin_Melanin3", "Hair_DarkBlack" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (17, "Enabled", false),
                (18, "Enabled", false),
                (40, "Enabled", false),
                (12, "Enabled", false),
                (27, "Enabled", true),
                (26, "Enabled", true),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Pheanox", ThingId = "Thing_Human3287002", MapIndex = 1,
            FirstName = "Fey", NickName = "Pheanox", LastName = "Nickel",
            AgeBiologicalTicks = 121732242L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 9, "None"),
                ("Melee", 1, "None"),
                ("Construction", 3, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 2, "None"),
                ("Plants", 2, "Minor"),
                ("Animals", 1, "Major"),
                ("Crafting", 13, "Minor"),
                ("Artistic", 4, "None"),
                ("Medicine", 0, "None"),
                ("Social", 0, "None"),
                ("Intellectual", 15, "Minor"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Undergrounder", 0), ("Neurotic", 2), ("Gay", 0), ("Wimp", 0), ("Delicate", 0) },
            ActiveGenes = new string[] { "Skin_Melanin2", "Hair_DarkBlack", "Hair_BaldOnly", "Beard_NoBeardOnly", "Body_Thin", "ElongatedFingers", "Pain_Extra", "Delicate", "Aggression_DeadCalm", "AptitudeRemarkable_Intellectual", "AptitudeRemarkable_Crafting", "AptitudeTerrible_Social", "AptitudePoor_Animals", "AptitudePoor_Plants" },
            AptitudeGenes = new[]
            {
                ("AptitudeRemarkable_Intellectual", "AptitudeRemarkable", "Intellectual", true),
                ("AptitudeRemarkable_Crafting", "AptitudeRemarkable", "Crafting", true),
                ("AptitudeTerrible_Social", "AptitudeTerrible", "Social", true),
                ("AptitudePoor_Animals", "AptitudePoor", "Animals", true),
                ("AptitudePoor_Plants", "AptitudePoor", "Plants", true),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (39, "Enabled", false),
                (36, "Enabled", false),
                (21, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (12, "Enabled", false),
                (48, "Enabled", false),
                (18, "Enabled", false),
                (47, "Enabled", false),
                (24, "Enabled", false),
                (13, "Enabled", false),
                (25, "Enabled", false),
                (28, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Pun", ThingId = "Thing_Human110566", MapIndex = 1,
            FirstName = "Waylon", NickName = "Pun", LastName = "Worrell",
            AgeBiologicalTicks = 145037032L,
            HasRangedWeapon = true, CustomXenotype = true,
            Skills = new[]
            {
                ("Shooting", 19, "None"),
                ("Melee", 0, "None"),
                ("Construction", 0, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 0, "None"),
                ("Plants", 20, "Minor"),
                ("Animals", 15, "Major"),
                ("Crafting", 0, "None"),
                ("Artistic", 0, "None"),
                ("Medicine", 20, "Major"),
                ("Social", 20, "Major"),
                ("Intellectual", 2, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Tough", 0), ("Kind", 0) },
            ActiveGenes = new string[] { "Skin_Melanin3", "Hair_SandyBlonde", "ArchiteMetabolism", "PerfectImmunity", "Mood_Optimist", "Deathless", "MaxTemp_SmallDecrease", "Ageless", "Learning_Fast", "TotalHealing", "Unstoppable", "Beauty_VeryUgly", "Robust", "Aggression_DeadCalm", "Beard_BushyOnly", "ChemicalDependency_Psychite", "AptitudeTerrible_Intellectual", "AptitudeRemarkable_Social", "AptitudeRemarkable_Medicine", "AptitudeTerrible_Artistic", "AptitudeTerrible_Crafting", "AptitudeRemarkable_Animals", "AptitudeRemarkable_Plants", "AptitudeTerrible_Cooking", "AptitudeTerrible_Mining", "AptitudeTerrible_Melee", "AptitudeStrong_Shooting", "AptitudeTerrible_Construction", "Instability_Major", "Sterile", "Pain_Reduced", "KindInstinct", "WoundHealing_SuperFast", "Superclotting", "FireResistant", "MoveSpeed_Quick", "MeleeDamage_Weak" },
            AptitudeGenes = new[]
            {
                ("AptitudeTerrible_Intellectual", "AptitudeTerrible", "Intellectual", true),
                ("AptitudeRemarkable_Social", "AptitudeRemarkable", "Social", true),
                ("AptitudeRemarkable_Medicine", "AptitudeRemarkable", "Medicine", true),
                ("AptitudeTerrible_Artistic", "AptitudeTerrible", "Artistic", true),
                ("AptitudeTerrible_Crafting", "AptitudeTerrible", "Crafting", true),
                ("AptitudeRemarkable_Animals", "AptitudeRemarkable", "Animals", true),
                ("AptitudeRemarkable_Plants", "AptitudeRemarkable", "Plants", true),
                ("AptitudeTerrible_Cooking", "AptitudeTerrible", "Cooking", true),
                ("AptitudeTerrible_Mining", "AptitudeTerrible", "Mining", true),
                ("AptitudeTerrible_Melee", "AptitudeTerrible", "Melee", true),
                ("AptitudeStrong_Shooting", "AptitudeStrong", "Shooting", true),
                ("AptitudeTerrible_Construction", "AptitudeTerrible", "Construction", true),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (3, "Enabled", false),
                (1, "Enabled", false),
                (6, "Enabled", false),
                (7, "Enabled", false),
                (39, "Enabled", false),
                (48, "Enabled", false),
                (12, "Enabled", false),
                (13, "Enabled", false),
                (25, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Quinn", ThingId = "Thing_Human3536295", MapIndex = 1,
            FirstName = "Albina", NickName = "Quinn", LastName = "Quinn",
            AgeBiologicalTicks = 107858221L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 15, "Minor"),
                ("Melee", 2, "None"),
                ("Construction", 0, "None"),
                ("Mining", 0, "None"),
                ("Cooking", 1, "None"),
                ("Plants", 2, "None"),
                ("Animals", 4, "None"),
                ("Crafting", 11, "Minor"),
                ("Artistic", 3, "None"),
                ("Medicine", 3, "None"),
                ("Social", 6, "Minor"),
                ("Intellectual", 4, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("ShootingAccuracy", -1), ("Immunity", 1) },
            ActiveGenes = new string[] { "Skin_Melanin4", "Hair_DarkBrown" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (39, "Enabled", false),
                (21, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (6, "Enabled", false),
                (7, "Enabled", false),
                (12, "Enabled", false),
                (24, "Enabled", false),
                (25, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Selma", ThingId = "Thing_Human3686249", MapIndex = 1,
            FirstName = "Selma", NickName = "Selma", LastName = "Love",
            AgeBiologicalTicks = 138803558L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 7, "None"),
                ("Melee", 3, "Minor"),
                ("Construction", 0, "None"),
                ("Mining", 1, "None"),
                ("Cooking", 3, "Minor"),
                ("Plants", 9, "Minor"),
                ("Animals", 1, "None"),
                ("Crafting", 4, "Minor"),
                ("Artistic", 1, "None"),
                ("Medicine", 6, "Minor"),
                ("Social", 0, "None"),
                ("Intellectual", 5, "Minor"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("PsychicSensitivity", -1), ("GreatMemory", 0), ("Nimble", 0) },
            ActiveGenes = new string[] { "Skin_Melanin4", "Hair_DarkSaturatedReddish" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (5, "Enabled", false),
                (1, "Enabled", false),
                (39, "Enabled", false),
                (10, "Enabled", false),
                (11, "Enabled", false),
                (22, "Enabled", false),
                (20, "Enabled", false),
                (12, "Enabled", false),
                (17, "Enabled", false),
                (18, "Enabled", false),
                (24, "Enabled", false),
                (25, "Enabled", false),
                (28, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Severin", ThingId = "Thing_Human1445697", MapIndex = 1,
            FirstName = "Sean", NickName = "Severin", LastName = "Pedersen",
            AgeBiologicalTicks = 225567156L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 14, "None"),
                ("Melee", 6, "None"),
                ("Construction", 1, "None"),
                ("Mining", 20, "Minor"),
                ("Cooking", 6, "None"),
                ("Plants", 14, "Major"),
                ("Animals", 12, "Major"),
                ("Crafting", 3, "None"),
                ("Artistic", 2, "None"),
                ("Medicine", 15, "Major"),
                ("Social", 3, "None"),
                ("Intellectual", 6, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Bloodlust", 0) },
            ActiveGenes = new string[] { "Eyes_Gray", "Skin_LightGray", "DarkVision", "MeleeDamage_Strong", "WoundHealing_Fast", "Nearsighted", "UVSensitivity_Intense", "MoveSpeed_Slow", "CaveDweller", "AptitudeRemarkable_Mining", "Skin_Melanin4", "Hair_MidBlack" },
            AptitudeGenes = new[]
            {
                ("AptitudeRemarkable_Mining", "AptitudeRemarkable", "Mining", false),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (3, "Enabled", false),
                (1, "Enabled", false),
                (8, "Enabled", false),
                (12, "Enabled", false),
                (16, "Enabled", false),
                (19, "Enabled", false),
                (13, "Enabled", false),
                (25, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Takeo", ThingId = "Thing_Human1060", MapIndex = 1,
            FirstName = "Takeo", NickName = "Takeo", LastName = "Mahoney",
            AgeBiologicalTicks = 231364007L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 14, "None"),
                ("Melee", 7, "None"),
                ("Construction", 17, "Major"),
                ("Mining", 10, "None"),
                ("Cooking", 1, "None"),
                ("Plants", 14, "Minor"),
                ("Animals", 5, "Minor"),
                ("Crafting", 7, "None"),
                ("Artistic", 4, "None"),
                ("Medicine", 10, "None"),
                ("Social", 6, "None"),
                ("Intellectual", 14, "Minor"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("Abrasive", 0), ("Masochist", 0), ("Nerves", 1) },
            ActiveGenes = new string[] { "Skin_Melanin4", "Hair_DarkBlack" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (5, "Enabled", false),
                (1, "Enabled", false),
                (14, "Enabled", false),
                (39, "Enabled", false),
                (48, "Enabled", false),
                (12, "Enabled", false),
                (38, "Enabled", false),
                (16, "Enabled", false),
                (37, "Enabled", false),
                (13, "Enabled", false),
                (25, "Enabled", false),
                (28, "Enabled", false),
                (6, "Enabled", true),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Theith", ThingId = "Thing_Human2336382", MapIndex = 1,
            FirstName = "Theith", NickName = null, LastName = "Noin",
            AgeBiologicalTicks = 165193462L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 16, "Minor"),
                ("Melee", 4, "None"),
                ("Construction", 3, "None"),
                ("Mining", 17, "Major"),
                ("Cooking", 4, "Minor"),
                ("Plants", 0, "None"),
                ("Animals", 0, "None"),
                ("Crafting", 2, "None"),
                ("Artistic", 16, "Major"),
                ("Medicine", 0, "None"),
                ("Social", 4, "None"),
                ("Intellectual", 5, "None"),
            },
            DisabledSkills = new string[0],
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Childcare", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring", "Warden" },
            Traits = new[] { ("TooSmart", 0) },
            ActiveGenes = new string[] { "Skin_Orange", "Hair_LightOrange", "Beard_NoBeardOnly", "Headbone_MiniHorns", "FireSpew", "MoveSpeed_VeryQuick", "FireResistant", "MaxTemp_LargeIncrease", "MinTemp_SmallIncrease", "Immunity_Weak", "WoundHealing_Slow", "MeleeDamage_Weak", "Mood_Pessimist", "AptitudePoor_Plants", "AptitudePoor_Animals", "Skin_Melanin1" },
            AptitudeGenes = new[]
            {
                ("AptitudePoor_Plants", "AptitudePoor", "Plants", false),
                ("AptitudePoor_Animals", "AptitudePoor", "Animals", false),
            },
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (27, "Enabled", false),
                (26, "Enabled", false),
                (39, "Enabled", false),
                (10, "Enabled", false),
                (11, "Enabled", false),
                (19, "Enabled", false),
                (23, "Enabled", false),
                (25, "Enabled", false),
                (12, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Tontrool", ThingId = "Thing_Human1840969", MapIndex = 1,
            FirstName = "Tontrool", NickName = "Tontrool", LastName = "Verea",
            AgeBiologicalTicks = 133366047L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 18, "Major"),
                ("Melee", 3, "None"),
                ("Construction", 16, "Minor"),
                ("Mining", 0, "None"),
                ("Cooking", 17, "Major"),
                ("Plants", 6, "None"),
                ("Animals", 9, "Minor"),
                ("Crafting", 1, "None"),
                ("Artistic", 0, "None"),
                ("Intellectual", 2, "None"),
            },
            DisabledSkills = new string[] { "Medicine", "Social" },
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Cleaning", "Construction", "Cooking", "Crafting", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring" },
            Traits = new[] { ("Transhumanist", 0) },
            ActiveGenes = new string[] { "Skin_Melanin3", "Hair_DarkBlack" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (9, "Enabled", false),
                (14, "Enabled", false),
                (8, "Enabled", false),
                (19, "Enabled", false),
                (47, "Enabled", false),
                (13, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (12, "Enabled", false),
                (50, "Enabled", false),
            },
        },
        new SamplePawn
        {
            Name = "Twiggy", ThingId = "Thing_Human1981069", MapIndex = 1,
            FirstName = "Taren", NickName = "Twiggy", LastName = "Mahoney",
            AgeBiologicalTicks = 142333631L,
            HasRangedWeapon = true,
            Skills = new[]
            {
                ("Shooting", 16, "Major"),
                ("Melee", 4, "None"),
                ("Construction", 16, "Minor"),
                ("Mining", 15, "Major"),
                ("Cooking", 0, "None"),
                ("Plants", 6, "None"),
                ("Animals", 9, "Major"),
                ("Crafting", 2, "None"),
                ("Artistic", 2, "None"),
                ("Medicine", 2, "None"),
                ("Intellectual", 2, "None"),
            },
            DisabledSkills = new string[] { "Social" },
            CapableWorkTypes = new string[] { "Art", "BasicWorker", "Cleaning", "Construction", "Cooking", "Crafting", "Doctor", "Firefighter", "Fishing", "Growing", "Handling", "Hauling", "Hunting", "Mining", "Patient", "PatientBedRest", "PlantCutting", "Research", "Smithing", "Tailoring" },
            Traits = new[] { ("NightOwl", 0) },
            ActiveGenes = new string[] { "Skin_Melanin8", "Hair_ReddishBrown" },
            AptitudeGenes = new (string, string, string, bool)[0],
            Assignments = new[]
            {
                (43, "Enabled", false),
                (1, "Enabled", false),
                (39, "Enabled", false),
                (38, "Enabled", false),
                (14, "Enabled", false),
                (8, "Enabled", false),
                (12, "Enabled", false),
                (19, "Enabled", false),
                (13, "Enabled", false),
                (26, "Enabled", false),
                (27, "Enabled", false),
                (50, "Enabled", false),
            },
        },
    };
}
