using System;
using RimWorld;
using Verse;

namespace Implanner
{
    /// How an install surgery's worker treats the target part, the three
    /// behaviours Implanner's combination rules distinguish.
    public enum SurgeryWorkerBehavior
    {
        /// Like vanilla's Recipe_InstallImplant: mounts on a natural part
        /// and refuses a part that is, or sits under, an artificial part.
        InstallsImplant = 0,

        /// Like vanilla's Recipe_InstallArtificialBodyPart: restores the
        /// part and everything under it before adding its hediff.
        ClearsPart = 1,

        /// Carries no refusal of artificial parts, so it mounts ON a
        /// replacement (Bionic modularity's module worker). Implanner's
        /// assumption for any worker it cannot classify.
        MountsOnArtificialParts = 2,

        /// Refuses only duplicates, tag clashes and missing parts (Combat
        /// Bionics' Recipe_InstallImplantOverBionics): it mounts on an
        /// artificial part at its own record, but since the game marks
        /// every part under an added part missing (Hediff_AddedPart.PostAdd)
        /// it never reaches a part under one, like vanilla's implant worker.
        /// Natural parts take it too.
        MountsOnArtificialPart = 3,
    }

    /// Declares the behaviour of a modded surgery worker that derives from
    /// neither vanilla install worker, so Implanner cannot infer it from
    /// the class hierarchy (verified in each mod's source). Matched by full
    /// type name on the worker or any of its base types; no assembly
    /// reference, so an entry for an absent mod simply never matches.
    /// Supporting another mod's worker is one entry in Defs/SurgeryWorkers.xml.
    public class SurgeryWorkerDef : Def
    {
        public string workerClass = "";
        public SurgeryWorkerBehavior behavior;

        /// The behaviour of a recipe worker: vanilla ancestry first, then a
        /// declared entry, else MountsOnArtificialParts. Definition data
        /// only, so every multiplayer client answers alike. Catalog build
        /// path only (scans the def list).
        internal static SurgeryWorkerBehavior BehaviorOf(Type? worker)
        {
            if (worker == null) return SurgeryWorkerBehavior.MountsOnArtificialParts;
            if (typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(worker))
                return SurgeryWorkerBehavior.ClearsPart;
            if (typeof(Recipe_InstallImplant).IsAssignableFrom(worker))
                return SurgeryWorkerBehavior.InstallsImplant;
            var declared = DefDatabase<SurgeryWorkerDef>.AllDefsListForReading;
            for (Type? walk = worker; walk != null; walk = walk.BaseType)
                for (int i = 0; i < declared.Count; i++)
                    if (walk.FullName == declared[i].workerClass)
                        return declared[i].behavior;
            return SurgeryWorkerBehavior.MountsOnArtificialParts;
        }
    }
}
