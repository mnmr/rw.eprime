namespace WorkRoles.Core.Tests;

/// Vanilla work catalog in VANILLA ORDER, generated from RimWorld 1.6.4871 Data
/// XML (Core + all DLC, inheritance-resolved). Do not hand-edit; regenerate from
/// game data when the game updates, cross-checking against VanillaGiverBaseline.
/// Work types are in naturalPriority-descending order; givers within a type are in
/// priorityInType-descending order (ties broken by def load order), i.e. the order
/// the game runs them.
public static class VanillaWorkOrder
{
    /// workType -> naturalPriority, every vanilla + DLC work type.
    public static readonly Dictionary<string, int> NaturalPriority = new()
    {
        { "Firefighter", 1400 },
        { "Patient", 1350 },
        { "Doctor", 1300 },
        { "PatientBedRest", 1200 },
        { "Childcare", 1175 },
        { "BasicWorker", 1150 },
        { "Warden", 1100 },
        { "Handling", 1050 },
        { "Cooking", 1000 },
        { "Hunting", 950 },
        { "Construction", 900 },
        { "Growing", 700 },
        { "Mining", 600 },
        { "PlantCutting", 500 },
        { "Smithing", 470 },
        { "Tailoring", 450 },
        { "Art", 430 },
        { "Crafting", 400 },
        { "Fishing", 350 },
        { "Hauling", 300 },
        { "Cleaning", 200 },
        { "DarkStudy", 150 },
        { "Research", 100 },
    };

    /// workType -> its givers in vanilla order.
    public static readonly Dictionary<string, string[]> GiversInOrder = new()
    {
        // naturalPriority 1400
        { "Firefighter", new[] {
            "FightFires",  // priorityInType 0
        } },
        // naturalPriority 1350
        { "Patient", new[] {
            "PatientGoToBedEmergencyTreatment",  // priorityInType 0
            "PatientGoToBedTreatment",  // priorityInType 0
        } },
        // naturalPriority 1300
        { "Doctor", new[] {
            "DoctorTendEmergency",  // priorityInType 110
            "DoctorTendToHumanlikes",  // priorityInType 100
            "DoctorTendToSelfEmergency",  // priorityInType 95
            "DoctorTendToEntities",  // priorityInType 95  [Anomaly]
            "DoctorTendToSelf",  // priorityInType 90
            "DoctorFeedHumanlikes",  // priorityInType 80
            "DoBillsMedicalHumanOperation",  // priorityInType 70
            "FeedHemogen",  // priorityInType 65  [Biotech]
            "DoctorRescue",  // priorityInType 60
            "DoctorTendToAnimals",  // priorityInType 50
            "DoctorFeedAnimals",  // priorityInType 40
            "DoBillsMedicalAnimalOperation",  // priorityInType 30
            "TakeToBedToOperate",  // priorityInType 20
            "VisitSickPawn",  // priorityInType 10
            "ExtractBioferrite",  // priorityInType 10  [Anomaly]
        } },
        // naturalPriority 1200
        { "PatientBedRest", new[] {
            "PatientGoToBedRecuperate",  // priorityInType 0
        } },
        // naturalPriority 1175
        { "Childcare", new[] {
            "ChildcarerTeach",  // priorityInType 9999  [Biotech]
            "BringBabyToSafety",  // priorityInType 200  [Biotech]
            "BreastfeedBaby",  // priorityInType 80  [Biotech]
            "PlayWithBaby",  // priorityInType 80  [Biotech]
            "BottleFeedBaby",  // priorityInType 80  [Biotech]
            "CarryToBreastfeed",  // priorityInType 80  [Biotech]
        } },
        // naturalPriority 1150
        { "BasicWorker", new[] {
            "Flick",  // priorityInType 500
            "BasicReleasePrisoner",  // priorityInType 100
            "Open",  // priorityInType 50
            "EjectFuel",  // priorityInType 50
            "ChangeTreeMode",  // priorityInType 30  [Ideology]
            "ExtractSkull",  // priorityInType 20
        } },
        // naturalPriority 1100
        { "Warden", new[] {
            "InterrogatePrisoner",  // priorityInType 120  [Anomaly]
            "ExecuteEntity",  // priorityInType 115  [Anomaly]
            "DoExecution",  // priorityInType 110
            "ExecuteGuiltyColonist",  // priorityInType 110
            "ExecuteSlave",  // priorityInType 110  [Ideology]
            "EmancipateSlave",  // priorityInType 105  [Ideology]
            "ReleasePrisoner",  // priorityInType 100
            "EnslavePrisoner",  // priorityInType 95  [Ideology]
            "ActivitySuppression",  // priorityInType 95  [Anomaly]
            "TakePrisonerToBed",  // priorityInType 90
            "FeedPrisoner",  // priorityInType 80
            "ImprisonSlave",  // priorityInType 75  [Ideology]
            "ConvertPrisoner",  // priorityInType 72  [Ideology]
            "DeliverHemogenToPrisoner",  // priorityInType 72  [Biotech]
            "DeliverFoodToPrisoner",  // priorityInType 70
            "SuppressSlave",  // priorityInType 65  [Ideology]
            "ReleaseEntity",  // priorityInType 65  [Anomaly]
            "ChatWithPrisoner",  // priorityInType 60
        } },
        // naturalPriority 1050
        { "Handling", new[] {
            "TakeRoamingAnimalsToPen",  // priorityInType 160
            "HandlingFeedPatientAnimals",  // priorityInType 150
            "TakeToPen",  // priorityInType 130
            "Slaughter",  // priorityInType 100
            "ReleaseToWild",  // priorityInType 100
            "Milk",  // priorityInType 90
            "Shear",  // priorityInType 85
            "Tame",  // priorityInType 80
            "Train",  // priorityInType 70
            "RebalanceAnimalsInPens",  // priorityInType 60
        } },
        // naturalPriority 1000
        { "Cooking", new[] {
            "DoBillsCook",  // priorityInType 100
            "DoBillsCookCampfire",  // priorityInType 97
            "DoBillsButcherFlesh",  // priorityInType 90
            "DoBillsBrew",  // priorityInType 30
        } },
        // naturalPriority 950
        { "Hunting", new[] {
            "HunterHunt",  // priorityInType 100
        } },
        // naturalPriority 900
        { "Construction", new[] {
            "FixBrokenDownBuilding",  // priorityInType 120
            "Uninstall",  // priorityInType 110
            "BuildRoofs",  // priorityInType 100
            "RemoveRoofs",  // priorityInType 90
            "DeconstructForBlueprint",  // priorityInType 85
            "ConstructFinishFrames",  // priorityInType 80
            "ConstructDeliverResourcesToFrames",  // priorityInType 70
            "ConstructDeliverResourcesToBlueprints",  // priorityInType 60
            "FillIn",  // priorityInType 51
            "Deconstruct",  // priorityInType 50
            "Repair",  // priorityInType 40
            "ConstructRemoveFloors",  // priorityInType 30
            "ConstructRemoveFoundations",  // priorityInType 30
            "ConstructSmoothFloors",  // priorityInType 20
            "ConstructSmoothWalls",  // priorityInType 10
        } },
        // naturalPriority 700
        { "Growing", new[] {
            "GrowerHarvest",  // priorityInType 100
            "PlantSeed",  // priorityInType 80
            "Replant",  // priorityInType 60
            "GrowerSow",  // priorityInType 50
        } },
        // naturalPriority 600
        { "Mining", new[] {
            "Mine",  // priorityInType 100
            "Drill",  // priorityInType 50
        } },
        // naturalPriority 500
        { "PlantCutting", new[] {
            "ExtractTree",  // priorityInType 110
            "PruneGauranlenTree",  // priorityInType 25  [Ideology]
            "PlantsCut",  // priorityInType 0
        } },
        // naturalPriority 470
        { "Smithing", new[] {
            "DoBillsSubcoreEncoder",  // priorityInType 220  [Biotech]
            "DoBillsMechGestator",  // priorityInType 210  [Biotech]
            "RepairMech",  // priorityInType 200  [Biotech]
            "DoBillsMakeWeapons",  // priorityInType 115
            "DoBillsMachiningTable",  // priorityInType 75
            "DoBillsBioferriteShaper",  // priorityInType 75  [Anomaly]
            "DoBillsFabricationBench",  // priorityInType 50
        } },
        // naturalPriority 450
        { "Tailoring", new[] {
            "DoBillsMakeApparel",  // priorityInType 110
        } },
        // naturalPriority 430
        { "Art", new[] {
            "RemovePaintBuilding",  // priorityInType 202
            "RemovePaintFloor",  // priorityInType 202
            "PaintBuilding",  // priorityInType 200
            "PaintFloor",  // priorityInType 200
            "DoBillsSculpt",  // priorityInType 100
        } },
        // naturalPriority 400
        { "Crafting", new[] {
            "DoBillsUseCraftingSpot",  // priorityInType 100
            "DoBillsRefinery",  // priorityInType 97
            "DoBillsProduceDrugs",  // priorityInType 95
            "DoBillsStonecut",  // priorityInType 90
            "DoBillsSmelter",  // priorityInType 80
            "DoBillsSerumCentrifuge",  // priorityInType 75  [Anomaly]
        } },
        // naturalPriority 350
        { "Fishing", new[] {
            "Fish",  // priorityInType 50  [Odyssey]
        } },
        // naturalPriority 300
        { "Hauling", new[] {
            "TakeEntityToHoldingPlatform",  // priorityInType 300  [Anomaly]
            "RearmTurrets",  // priorityInType 150
            "Refuel",  // priorityInType 140
            "UnloadCarriers",  // priorityInType 130
            "HelpGatheringItemsForCaravan",  // priorityInType 120
            "HaulToGeneBank",  // priorityInType 111  [Biotech]
            "LoadTransporters",  // priorityInType 110
            "EmptyWasteContainer",  // priorityInType 110  [Biotech]
            "HaulToBiosculpterPod",  // priorityInType 109  [Ideology]
            "HaulToGrowthVat",  // priorityInType 109  [Biotech]
            "HaulToPortal",  // priorityInType 105
            "Strip",  // priorityInType 100
            "HaulCorpses",  // priorityInType 90
            "CarryToGeneExtractor",  // priorityInType 90  [Biotech]
            "CarryToGrowthVat",  // priorityInType 90  [Biotech]
            "HaulMechsToCharger",  // priorityInType 90  [Biotech]
            "CarryToSubcoreScanner",  // priorityInType 90  [Biotech]
            "HaulToCarrier",  // priorityInType 90  [Biotech]
            "HaulToSubcoreScanner",  // priorityInType 90  [Biotech]
            "HaulToWastepackAtomizer",  // priorityInType 90  [Biotech]
            "TransferEntity",  // priorityInType 64  [Anomaly]
            "CookFillHopper",  // priorityInType 50
            "DoBillsCremate",  // priorityInType 40
            "DoBillsHaulCampfire",  // priorityInType 30
            "TakeBeerOutOfFermentingBarrel",  // priorityInType 20
            "EmptyEggBox",  // priorityInType 20
            "TakeBioferriteOutOfHarvester",  // priorityInType 20  [Anomaly]
            "FillFermentingBarrel",  // priorityInType 19
            "HaulGeneral",  // priorityInType 15
            "DeliverResourcesToFrames",  // priorityInType 10
            "DeliverResourcesToBlueprints",  // priorityInType 9
            "HaulMerge",  // priorityInType 5
        } },
        // naturalPriority 200
        { "Cleaning", new[] {
            "CleanClearSnow",  // priorityInType 10
            "CleanFilth",  // priorityInType 5
            "CleanClearPollution",  // priorityInType 0  [Biotech]
        } },
        // naturalPriority 150
        { "DarkStudy", new[] {
            "StudyInteract",  // priorityInType 110  [Anomaly]
        } },
        // naturalPriority 100
        { "Research", new[] {
            "Hack",  // priorityInType 130
            "CreateXenogerm",  // priorityInType 120  [Biotech]
            "StudyArchotechStructures",  // priorityInType 110
            "Research",  // priorityInType 100
            "LongRangeScan",  // priorityInType 50
            "GroundPenetratingScan",  // priorityInType 50
        } },
    };
}
