using System;
using WorkRoles.Core.Signals;

namespace WorkRoles.Core.Recs
{
    /// Projects one immutable pawn signal snapshot into the mutable view used
    /// by a recommendation run. Keeping both target kinds here prevents game
    /// adapters and tests from drifting apart.
    public static class PawnSignalViewProjection
    {
        public static void Apply(PawnSignalSnapshot snapshot, PawnView view)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (view == null) throw new ArgumentNullException(nameof(view));

            view.SignalBuckets.Clear();
            foreach (SkillBucketSignal signal in snapshot.SkillBuckets.All)
                view.SignalBuckets[signal.SkillDefName] = signal.Bucket;

            if (snapshot.WorkTypeBuckets.All.Count == 0)
            {
                view.WorkTypeSignalBuckets?.Clear();
                return;
            }
            if (view.WorkTypeSignalBuckets == null)
                view.WorkTypeSignalBuckets =
                    new System.Collections.Generic.Dictionary<string, SignalBucket>(
                        StringComparer.Ordinal);
            else
                view.WorkTypeSignalBuckets.Clear();
            foreach (WorkTypeBucketSignal signal in snapshot.WorkTypeBuckets.All)
                view.WorkTypeSignalBuckets[signal.WorkTypeDefName] = signal.Bucket;
        }
    }
}
