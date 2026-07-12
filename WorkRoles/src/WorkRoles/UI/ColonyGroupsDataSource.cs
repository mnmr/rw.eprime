using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Read-only data source for Colony Groups (TacticalGroups): the player's
    /// named pawn groups, resolved by reflection (no assembly reference).
    /// Membership can't be derived, only looked up, so a snapshot is captured
    /// lazily — first partition after the window opens or the grouping is
    /// switched — and held until invalidated.
    internal static class ColonyGroupsDataSource
    {
        private static bool resolved;
        private static PropertyInfo allPawnGroups; // static TacticUtils.AllPawnGroups
        private static FieldInfo groupId;          // ColonistGroup.groupID (persisted)
        private static FieldInfo curGroupName;     // ColonistGroup.curGroupName
        private static FieldInfo groupName;        // fallback name
        private static PropertyInfo activePawns;   // ColonistGroup.ActivePawns (filtered)

        private static List<MembershipGroup<Pawn>> snapshot;

        internal static bool Available
        {
            get
            {
                Resolve();
                return allPawnGroups != null;
            }
        }

        /// Drops the snapshot; the next Groups() call re-reads Colony Groups.
        internal static void InvalidateSnapshot() => snapshot = null;

        /// The player's named groups, in Colony Groups' own order.
        internal static List<MembershipGroup<Pawn>> Groups()
        {
            if (snapshot != null) return snapshot;
            snapshot = new List<MembershipGroup<Pawn>>();
            if (!Available) return snapshot;
            try
            {
                if (!(allPawnGroups.GetValue(null, null) is IEnumerable groups)) return snapshot;
                foreach (var group in groups)
                {
                    if (group == null) continue;
                    string name = curGroupName?.GetValue(group) as string;
                    if (name.NullOrEmpty()) name = groupName?.GetValue(group) as string;
                    var membership = new MembershipGroup<Pawn>
                    {
                        // groupID is persisted, so collapse state survives renames.
                        Key = "cg|" + (groupId != null ? groupId.GetValue(group) : (object)name),
                        Title = name ?? "?",
                    };
                    if (activePawns.GetValue(group, null) is IEnumerable pawns)
                        foreach (var p in pawns)
                            if (p is Pawn pawn)
                                membership.Members.Add(pawn);
                    snapshot.Add(membership);
                }
            }
            catch (Exception e)
            {
                Log.Error("[WorkRoles] Reading Colony Groups failed; its grouping is disabled: " + e.Message);
                allPawnGroups = null;
                snapshot = new List<MembershipGroup<Pawn>>();
            }
            return snapshot;
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try
            {
                var utils = GenTypes.GetTypeInAnyAssembly("TacticalGroups.TacticUtils");
                if (utils == null) return; // mod not loaded: silently unavailable
                const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var colonistGroup = GenTypes.GetTypeInAnyAssembly("TacticalGroups.ColonistGroup");
                allPawnGroups = utils.GetProperty("AllPawnGroups", Static);
                groupId = colonistGroup?.GetField("groupID", Inst);
                curGroupName = colonistGroup?.GetField("curGroupName", Inst);
                groupName = colonistGroup?.GetField("groupName", Inst);
                activePawns = colonistGroup?.GetProperty("ActivePawns", Inst);
                if (allPawnGroups == null || activePawns == null || (curGroupName == null && groupName == null))
                {
                    Log.Error("[WorkRoles] Colony Groups is loaded but its internals moved; grouping by its groups is disabled.");
                    allPawnGroups = null;
                }
            }
            catch (Exception e)
            {
                Log.Error("[WorkRoles] Colony Groups detection failed: " + e.Message);
                allPawnGroups = null;
            }
        }
    }
}
