using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace QualityJobs.Patches
{
    /// Replaces only the displayed author read; the invariant marker remains
    /// in save data, and vanilla still composes every other inspect-string part.
    [HarmonyPatch(typeof(UnfinishedThing), nameof(UnfinishedThing.GetInspectString))]
    internal static class Patch_ReservedAuthor
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var field = AccessTools.Field(typeof(UnfinishedThing), "creatorName");
            var display = AccessTools.Method(typeof(UftAuthor), nameof(UftAuthor.DisplayName));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.LoadsField(field))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = display;
                }
                yield return instruction;
            }
        }
    }
}
