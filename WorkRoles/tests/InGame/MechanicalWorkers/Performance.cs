using System;
using System.Diagnostics;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles;

namespace WorkRolesMechanicalVerification
{
    public static partial class Probe
    {
        private static void VerifyWarmOrders(Pawn human)
        {
            const int reads = 100000;
            var allocationMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
            var allocations = allocationMethod == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
            var clock = new Stopwatch();
            // Measure warm steady state separately from first-call/JIT costs.
            // Use the identical measurement method for warmup and both modes.
            bool warmed = MeasureOrders(human, reads, allocations, clock, out long warmBytes, out double warmMs);
            Log.Message("[MechanicalProbe] PERF warmup stable=" + warmed + " allocated=" + warmBytes + " ms=" + warmMs);
            Check(warmed, "work-order warmup preserves identity");
            double[] humanTimes = new double[2];
            for (int mode = 0; mode < 2; mode++)
            {
                RoleCommands.SetMechanicalWorkersEnabled(mode == 1);
                bool stable = MeasureOrders(human, reads, allocations, clock, out long bytes, out double ms);
                humanTimes[mode] = ms;
                Log.Message("[MechanicalProbe] PERF human support=" + (mode == 1) + " stable=" + stable + " allocated=" + bytes + " ms=" + humanTimes[mode]);
                Check(stable && (allocations == null || bytes == 0), "unchanged human work-order reads preserve identity without allocations (support=" + (mode == 1) + ")");
            }
            foreach (Pawn worker in workers)
            {
                bool stable = MeasureOrders(worker, reads, allocations, clock, out long bytes, out double ms);
                Log.Message("[MechanicalProbe] PERF " + worker.def.defName + " reads=" + reads + " ms=" + ms + " allocated=" + bytes + " allocationCounter=" + (allocations != null));
                Check(stable && (allocations == null || bytes == 0), worker.def.defName + " warm work-order reads preserve identity without allocations");
            }
            Log.Message("[MechanicalProbe] PERF human reads=" + reads + " supportOffMs=" + humanTimes[0] + " supportOnMs=" + humanTimes[1]);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool MeasureOrders(Pawn pawn, int reads, Func<long> allocations, Stopwatch clock, out long bytes, out double ms)
        {
            List<WorkGiver> expected = CompiledJobOrders.NormalFor(pawn);
            long before = allocations?.Invoke() ?? 0;
            clock.Restart();
            bool stable = true;
            for (int i = 0; i < reads; i++) stable &= ReferenceEquals(expected, CompiledJobOrders.NormalFor(pawn));
            clock.Stop();
            bytes = (allocations?.Invoke() ?? 0) - before;
            ms = clock.Elapsed.TotalMilliseconds;
            return stable;
        }
    }
}
