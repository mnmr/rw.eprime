using System.Linq;
using LudeonTK;
using Verse;

namespace WorkRoles.Dev
{
    public static class DebugActions
    {
        [DebugAction("WorkRoles", "Dump compiled order",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpCompiledOrder(Pawn p)
        {
            if (p.workSettings == null)
            {
                Log.Message($"[WorkRoles] {p} has no work settings");
                Log.TryOpenLogWindow();
                return;
            }
            // DebugString prints both ordered giver lists via the (patched) getters.
            Log.Message($"[WorkRoles] {p.LabelShort} managed={RoleStore.Current?.IsManaged(p)}\n{p.workSettings.DebugString()}");
            Log.TryOpenLogWindow();
        }

        [DebugAction("WorkRoles", "Assign role...",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AssignRole(Pawn p)
        {
            var store = RoleStore.Current;
            if (store == null) return;
            var options = store.roles
                .Select(role => new DebugMenuOption(role.label, DebugMenuOptionMode.Action,
                    () => RoleCommands.AssignRole(p, role.id)))
                .ToList();
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction("WorkRoles", "Remove role...",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RemoveRole(Pawn p)
        {
            var store = RoleStore.Current;
            if (store == null || !store.pawnSets.TryGetValue(p, out var set)) return;
            var options = set.assignments
                .Select(a => store.RoleById(a.roleId))
                .Where(r => r != null)
                .Select(role => new DebugMenuOption(role.label, DebugMenuOptionMode.Action,
                    () => RoleCommands.RemoveRoleFromPawn(p, role.id)))
                .ToList();
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction("WorkRoles", "Toggle role globally...",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleRoleGlobal()
        {
            var store = RoleStore.Current;
            if (store == null) return;
            var options = store.roles
                .Select(role => new DebugMenuOption($"{role.label} ({(role.enabled ? "on" : "off")})",
                    DebugMenuOptionMode.Action,
                    () => RoleCommands.ToggleRoleGlobal(role.id)))
                .ToList();
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }
    }
}
