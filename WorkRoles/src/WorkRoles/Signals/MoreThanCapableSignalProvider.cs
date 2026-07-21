using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    /// Optional adapter for More Than Capable's public work-aversion API.
    /// Binding is resolved once; the API is queried only while a pawn signal
    /// snapshot is being rebuilt.
    internal sealed class MoreThanCapableSignalProvider : ISignalProvider
    {
        private const string ApiTypeName =
            "MoreThanCapable.MoreThanCapableMod";
        private const string ApiMethodName = "IsBadWork";

        private static bool initialized;
        private static Func<Pawn, WorkTypeDef, bool> isBadWork;
        private static bool warned;

        internal static bool Available
        {
            get
            {
                EnsureInitialized();
                return isBadWork != null;
            }
        }

        public IEnumerable<PawnSignal> Collect(Pawn pawn)
        {
            EnsureInitialized();
            Func<Pawn, WorkTypeDef, bool> query = isBadWork;
            if (pawn == null || query == null)
                return Array.Empty<PawnSignal>();

            try
            {
                List<WorkTypeDef> workTypes =
                    DefDatabase<WorkTypeDef>.AllDefsListForReading;
                var signals = new List<PawnSignal>();
                for (int i = 0; i < workTypes.Count; i++)
                {
                    WorkTypeDef workType = workTypes[i];
                    if (workType == null || !query(pawn, workType)) continue;
                    string label = "WR_MtcHatedWorkSignalLabel"
                        .Translate(workType.labelShort).ToString();
                    signals.Add(MoreThanCapableSignal.Create(
                        workType.defName,
                        label,
                        "WR_MtcHatedWorkSignalDescription".Translate().ToString()));
                }
                return signals;
            }
            catch (Exception exception)
            {
                // An optional integration must not repeatedly throw while
                // rebuilding snapshots if the other mod changes its behavior.
                isBadWork = null;
                WarnOnce(exception);
                return Array.Empty<PawnSignal>();
            }
        }

        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            Type apiType = AccessTools.TypeByName(ApiTypeName);
            if (apiType == null) return;
            try
            {
                MethodInfo method = AccessTools.Method(
                    apiType,
                    ApiMethodName,
                    new[] { typeof(Pawn), typeof(WorkTypeDef) });
                if (method == null || !method.IsStatic
                    || method.ReturnType != typeof(bool))
                    throw new MissingMethodException(
                        ApiTypeName,
                        ApiMethodName + "(Pawn, WorkTypeDef) -> bool");
                isBadWork = (Func<Pawn, WorkTypeDef, bool>)
                    Delegate.CreateDelegate(
                        typeof(Func<Pawn, WorkTypeDef, bool>), method);
            }
            catch (Exception exception)
            {
                WarnOnce(exception);
                isBadWork = null;
            }
        }

        private static void WarnOnce(Exception exception)
        {
            if (warned) return;
            warned = true;
            Log.Warning("[WorkRoles] More Than Capable signal integration disabled: "
                + exception.Message);
        }
    }
}
