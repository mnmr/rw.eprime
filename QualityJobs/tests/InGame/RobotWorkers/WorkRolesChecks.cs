using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace RobotWorkersVerification
{
    public static partial class Probe
    {
        private static void AssignWorkRoles(Pawn pawn, bool enable)
        {
            Type commands = AccessTools.TypeByName("WorkRoles.RoleCommands");
            if (commands == null) return;
            Type assignment = AccessTools.TypeByName("WorkRoles.RoleAssignment");
            var values = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(assignment));
            if (enable)
            {
                AccessTools.Method(commands, "SetMechanicalWorkersEnabled").Invoke(null, new object[] { true });
                Type storeType = AccessTools.TypeByName("WorkRoles.RoleStore");
                object roleStore = AccessTools.Property(storeType, "Current").GetValue(null);
                foreach (string template in new[] { "WS_Tailor", "WS_Builder" })
                {
                    object role = AccessTools.Method(storeType, "RoleByTemplate").Invoke(roleStore, new object[] { template });
                    Require(role != null, "WorkRoles native test role exists: " + template);
                    object value = Activator.CreateInstance(assignment);
                    AccessTools.Field(assignment, "roleId").SetValue(value, AccessTools.Field(role.GetType(), "id").GetValue(role));
                    values.Add(value);
                }
            }
            AccessTools.Method(commands, "PasteRoleSet").Invoke(null, new object[] { pawn, values });
            if (enable) Check(true, pawn.def.defName + " executes QJ scenario with WorkRoles Tailor and Builder assignments");
        }
    }
}
