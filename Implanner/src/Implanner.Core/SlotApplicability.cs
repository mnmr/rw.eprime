using System;
using System.Collections.Generic;

namespace Implanner.Core
{
    /// Why a planned slot is outside one pawn's target. A plan applies to
    /// colonists of every race, so a slot is judged against the pawn's own
    /// body when the pawn is evaluated: a slot the body lacks, or one the
    /// game will never let the surgery reach, neither completes nor counts
    /// as missing.
    public enum SlotBlock : byte
    {
        None = 0,

        /// The body has no such part.
        NoAnatomy = 1,

        /// An artificial part is installed on a part above the slot, and
        /// the implant's surgery refuses parts under one (vanilla's
        /// Recipe_InstallImplant and Recipe_InstallArtificialBodyPart):
        /// an elbow blade under a bionic arm.
        UnderArtificialPart = 2,

        /// Another goal of the same plan replaces a part above the slot on
        /// this body: once that replacement is in, the slot is under an
        /// artificial part. Plans are validated on a reference body, so a
        /// race whose body nests the parts differently reaches this.
        UnderPlannedReplacement = 3,

        /// Every surgery of the kind needs a mutant the pawn is not
        /// (Anomaly's ghoul kit on an ordinary colonist).
        RequiresMutant = 4,

        /// Every surgery of the kind refuses the pawn's mutant kind
        /// (revenant vertebrae on a ghoul).
        RefusesMutant = 5,
    }

    /// An artificial part installed on the pawn: its anatomy record and
    /// the installed implant kind.
    public readonly struct ArtificialPart
    {
        public ArtificialPart(int record, string defName)
        {
            Record = record;
            DefName = defName;
        }

        public int Record { get; }
        public string DefName { get; }
    }

    /// Per-pawn slot applicability, mirroring the surgery workers' own part
    /// refusal (verified in RimWorld source: a part that sits under a
    /// directly added part is refused unless the worker mounts on
    /// artificial parts). Pure: the caller extracts the facts on the
    /// pawn's body and the installed artificial parts.
    public static class SlotApplicability
    {
        /// facts[g][j]: goal g's slot j on the pawn's body (null where the
        /// body lacks the part). Fills blocks and blockers, parallel to
        /// facts; a blocker is the implant kind the block comes from (the
        /// nearest one up the body), null where the slot is not blocked
        /// by an implant. An installed artificial part outranks a planned
        /// replacement, since the planned one is itself unreachable then.
        /// pawnMutant is the pawn's mutant kind ("" = none): a kind whose
        /// surgeries the game refuses for that kind of pawn (mutant
        /// prerequisite or blacklist) is outside the target on every slot.
        public static void Resolve(
            IReadOnlyList<IReadOnlyList<PlannedSlotFacts?>> facts,
            IReadOnlyList<ArtificialPart> artificialParts,
            SlotBlock[][] blocks, string?[][] blockers, string pawnMutant = "")
        {
            for (int g = 0; g < facts.Count; g++)
            {
                IReadOnlyList<PlannedSlotFacts?> goal = facts[g];
                for (int j = 0; j < goal.Count; j++)
                {
                    PlannedSlotFacts? slot = goal[j];
                    blockers[g][j] = null;
                    if (slot == null)
                    {
                        blocks[g][j] = SlotBlock.NoAnatomy;
                        continue;
                    }
                    if (!slot.AcceptsMutant(pawnMutant))
                    {
                        // A listed mutant is refused outright; otherwise
                        // the kind needs a mutant this pawn is not.
                        blocks[g][j] = slot.RefusesMutant(pawnMutant)
                            ? SlotBlock.RefusesMutant
                            : SlotBlock.RequiresMutant;
                        continue;
                    }
                    blocks[g][j] = SlotBlock.None;
                    if (slot.MountsUnderArtificialParts) continue;
                    IReadOnlyList<int> ancestors = slot.SlotAncestors;
                    for (int a = 0; a < ancestors.Count && blocks[g][j] == SlotBlock.None; a++)
                        for (int p = 0; p < artificialParts.Count; p++)
                            if (artificialParts[p].Record == ancestors[a])
                            {
                                blocks[g][j] = SlotBlock.UnderArtificialPart;
                                blockers[g][j] = artificialParts[p].DefName;
                                break;
                            }
                    if (blocks[g][j] != SlotBlock.None) continue;
                    for (int a = 0; a < ancestors.Count && blocks[g][j] == SlotBlock.None; a++)
                        for (int o = 0; o < facts.Count && blocks[g][j] == SlotBlock.None; o++)
                        {
                            if (o == g) continue;
                            IReadOnlyList<PlannedSlotFacts?> other = facts[o];
                            for (int k = 0; k < other.Count; k++)
                            {
                                PlannedSlotFacts? candidate = other[k];
                                if (candidate == null || !candidate.IsReplacement
                                    || candidate.SlotRecord != ancestors[a])
                                    continue;
                                blocks[g][j] = SlotBlock.UnderPlannedReplacement;
                                blockers[g][j] = candidate.DefName;
                                break;
                            }
                        }
                }
            }
        }
    }
}
