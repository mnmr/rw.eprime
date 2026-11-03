using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimShared.GameLib
{
    /// Which mech work modes make a mech work. Vanilla has one (Work), but
    /// mods add their own: Smart Work Mode's "SmartWork" branch works like
    /// Work and self-shuts down when idle. Comparing against
    /// MechWorkModeDefOf.Work by identity silently excludes such mechs, so
    /// the rule is read from the data the game itself runs: a mode works
    /// when the Mechanoid think tree's branch for it (after every mod's
    /// patches) contains a JobGiver_Work node.
    internal static class MechWorkModes
    {
        // Cache contract:
        // Owner: process (one per mod assembly).
        // Key: none (single set).
        // Value: immutable set of MechWorkModeDefs whose think-tree branch
        //   contains JobGiver_Work; defs are observed, never mutated.
        // Dependencies: the loaded definition set (the Mechanoid ThinkTreeDef
        //   and MechWorkModeDefs), static per session; the mod's definition
        //   reload path calls Reset.
        // Refresh policy: built on the first query after defs are loaded.
        // Equality policy: never changes within a session.
        // Teardown: Reset drops the set; nothing else is owned.
        private static HashSet<MechWorkModeDef>? working;

        internal static void Reset() => working = null;

        /// Whether the pawn's control group is in a working mode. False for
        /// mechs without a control group (no overseer).
        internal static bool IsWorking(Pawn pawn) => IsWorking(pawn.GetMechWorkMode());

        internal static bool IsWorking(MechWorkModeDef? mode)
        {
            if (mode == null) return false;
            working ??= Build();
            return working.Contains(mode);
        }

        private static HashSet<MechWorkModeDef> Build()
        {
            var result = new HashSet<MechWorkModeDef>();
            ThinkTreeDef? tree = DefDatabase<ThinkTreeDef>.GetNamedSilentFail("Mechanoid");
            if (tree?.thinkRoot != null) Collect(tree.thinkRoot, result);
            // A tree another mod rewrote beyond recognition still leaves the
            // vanilla mode working, so the behaviour never regresses below
            // the old identity comparison.
            result.Add(MechWorkModeDefOf.Work);
            return result;
        }

        private static void Collect(ThinkNode node, HashSet<MechWorkModeDef> result)
        {
            if (node is ThinkNode_ConditionalWorkMode conditional && conditional.workMode != null
                && ContainsWork(node))
                result.Add(conditional.workMode);
            List<ThinkNode> children = node.subNodes;
            for (int i = 0; i < children.Count; i++) Collect(children[i], result);
        }

        private static bool ContainsWork(ThinkNode node)
        {
            if (node is JobGiver_Work) return true;
            List<ThinkNode> children = node.subNodes;
            for (int i = 0; i < children.Count; i++)
                if (ContainsWork(children[i])) return true;
            return false;
        }
    }
}
