using System;
using System.Runtime.CompilerServices;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Read-only integration surface for other mods. Everything here is a pure
    /// query over current game state: no mutation, no multiplayer-visible
    /// effect, no revision bump. Callers bind by reflection and must treat a
    /// missing type or member as "Quality Jobs unavailable".
    ///
    /// Stability contract: members present at a given <see cref="ApiVersion"/>
    /// keep their signature and meaning. Additions bump the version; removals
    /// or semantic changes require a major-version bump.
    ///
    /// Cost: cache hits are allocation-free. Auto-best misses rank colonists
    /// behind the store's external-facts revision gate and must run on the game
    /// thread; manual answers depend only on their complete value key.
    public static class QualityJobsApi
    {
        /// Incremented whenever a member is added. Callers may gate on it.
        public const int ApiVersion = 1;

        /// The neutral answer: one run, no quality-driven rework.
        public const float NoRework = 1f;

        /// True while a save has Quality Jobs enabled. False in the main menu,
        /// and false for saves where the player disabled the mod.
        public static bool Active => QualityJobsStore.Active != null;

        /// Everything an expected-attempts answer depends on. Two bills of the
        /// same recipe under the same resume condition rank the same colonists
        /// and get the same answer, so they should only pay for it once.
        private readonly struct AttemptsKey : IEquatable<AttemptsKey>
        {
            private readonly RecipeDef? recipe;   // null = construction
            private readonly Map? map;
            private readonly int minSkill;
            private readonly bool inspired;
            private readonly bool specialist;
            private readonly bool autoBest;
            private readonly int target;

            public AttemptsKey(RecipeDef? recipe, Map? map,
                in ResumeCondition condition, bool autoBest, int target)
            {
                this.recipe = recipe;
                this.map = map;
                minSkill = condition.MinSkill;
                inspired = condition.RequireInspired;
                specialist = condition.RequireSpecialist;
                this.autoBest = autoBest;
                this.target = target;
            }

            public bool Equals(AttemptsKey other)
                => ReferenceEquals(recipe, other.recipe)
                   && ReferenceEquals(map, other.map)
                   && minSkill == other.minSkill
                   && inspired == other.inspired
                   && specialist == other.specialist
                   && autoBest == other.autoBest
                   && target == other.target;

            public override bool Equals(object? obj)
                => obj is AttemptsKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = recipe != null ? recipe.shortHash : 0;
                    hash = (hash * 397)
                        ^ (map != null ? RuntimeHelpers.GetHashCode(map) : 0);
                    hash = (hash * 397) ^ minSkill;
                    hash = (hash * 397) ^ target;
                    return (hash * 397)
                        ^ ((inspired ? 1 : 0) | (specialist ? 2 : 0) | (autoBest ? 4 : 0));
                }
            }
        }

        // Cache contract:
        // Owner: process, partitioned by the live QualityJobsStore below.
        // Key: AttemptsKey — recipe (or construction), map, resume condition,
        //      auto-best mode and quality target: the complete input set.
        // Value: expected attempts (float), immutable.
        // Dependencies: configuration is complete in the key; auto-best entries
        //      additionally depend on the external pawn-facts revision (skills,
        //      XP tie-breaks, inspiration, roles, work settings, and pawn scope).
        // Refresh policy: lazy, first miss after a key change or, for auto-best,
        //      after that revision moves; unrelated ticks reuse the same answer.
        //      Bill/construction and manual/auto use separate memo domains so an
        //      unrelated call cannot churn another domain's revision stamp.
        // Equality policy: n/a (one value per key).
        // Teardown: cleared when the owning store changes, so a previous save's
        //      answers can never be served; auto keys hold map identity only
        //      during that owning store's lifetime.
        private static readonly RevisionMemo<AttemptsKey, float> billManualAttemptsMemo =
            new RevisionMemo<AttemptsKey, float>();
        private static readonly RevisionMemo<AttemptsKey, float> billAutoAttemptsMemo =
            new RevisionMemo<AttemptsKey, float>();
        private static readonly RevisionMemo<AttemptsKey, float> constructionManualAttemptsMemo =
            new RevisionMemo<AttemptsKey, float>();
        private static readonly RevisionMemo<AttemptsKey, float> constructionAutoAttemptsMemo =
            new RevisionMemo<AttemptsKey, float>();
        private static QualityJobsStore? memoOwner;

        /// Drops memoised answers when the active save changes.
        private static void EnsureMemoOwner(QualityJobsStore store)
        {
            if (ReferenceEquals(memoOwner, store)) return;
            ClearMemos();
            memoOwner = store;
        }

        internal static void ReleaseMemoOwner(QualityJobsStore store)
        {
            if (!ReferenceEquals(memoOwner, store)) return;
            ClearMemos();
            memoOwner = null;
        }

        private static void ClearMemos()
        {
            billManualAttemptsMemo.Clear();
            billAutoAttemptsMemo.Clear();
            constructionManualAttemptsMemo.Clear();
            constructionAutoAttemptsMemo.Clear();
        }

        /// Expected number of production runs of <paramref name="bill"/> needed
        /// to yield one product at or above its quality target, read off the
        /// bill's configured gate.
        ///
        /// Returns <see cref="NoRework"/> (1) only when there is genuinely no
        /// rework to predict: Quality Jobs inactive, the bill unmanaged, or no
        /// quality target set. An unreachable target saturates at
        /// <see cref="ExpectedAttempts.Max"/> rather than reporting one run.
        public static float ExpectedAttemptsForBill(Bill? bill)
        {
            if (bill == null) return NoRework;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return NoRework;

            BillPresentationSnapshot presentation = store.BillPresentationFor(bill);
            int target = presentation.TargetQuality;
            if (target <= 0) return NoRework;

            BillConfig config = presentation.Config;
            if (!config.Managed) return NoRework;

            RecipeDef recipe = presentation.Recipe;

            EnsureMemoOwner(store);
            var key = new AttemptsKey(recipe,
                config.AutoBest ? presentation.Map : null,
                config.Condition, config.AutoBest, target);
            long revision = config.AutoBest
                ? (uint)store.ExternalPawnFactsRevision : 0L;
            RevisionMemo<AttemptsKey, float> memo = config.AutoBest
                ? billAutoAttemptsMemo : billManualAttemptsMemo;
            if (memo.TryGet(revision, key, out float cached))
                return cached;

            float attempts = AttemptsFor(config.Condition, config.AutoBest,
                recipe, target);
            memo.Store(revision, key, attempts);
            return attempts;
        }

        /// Expected number of build attempts for a quality-managed blueprint or
        /// frame, counting the deconstruct-and-rebuild cycles Quality Jobs runs
        /// when the rolled quality lands below the plan's target. Read off the
        /// plan's configured gate.
        ///
        /// Returns <see cref="NoRework"/> (1) only for things Quality Jobs is
        /// not managing and for plans with no quality target.
        public static float ExpectedAttemptsForConstructible(Thing? thing)
        {
            if (thing == null) return NoRework;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null
                || !store.TryGetPlanPresentation(thing.thingIDNumber,
                    out PlanPresentationSnapshot? plan)
                || plan == null || plan.MinQuality <= 0)
                return NoRework;

            EnsureMemoOwner(store);
            var key = new AttemptsKey(null,
                plan.AutoBest ? plan.Map : null,
                plan.Condition, plan.AutoBest, plan.MinQuality);
            long revision = plan.AutoBest
                ? (uint)store.ExternalPawnFactsRevision : 0L;
            RevisionMemo<AttemptsKey, float> memo = plan.AutoBest
                ? constructionAutoAttemptsMemo : constructionManualAttemptsMemo;
            if (memo.TryGet(revision, key, out float cached))
                return cached;

            float attempts = AttemptsFor(plan.Condition, plan.AutoBest,
                recipe: null, targetQuality: plan.MinQuality);
            memo.Store(revision, key, attempts);
            return attempts;
        }

        /// The gate decides the odds. A gate admits exactly the workers its
        /// resume condition describes, so the condition alone fixes the quality
        /// distribution — no live pawn is consulted, and "nobody available right
        /// now" is not an answer about how much rework a target implies.
        ///
        /// Auto-best is the one case where the gate's skill value is dynamic:
        /// it tracks the colony's best finisher. That resolution mirrors the
        /// plan and bill dialogs exactly, including their fallback to the
        /// configured threshold when no colonist resolves.
        private static float AttemptsFor(
            in ResumeCondition condition, bool autoBest,
            RecipeDef? recipe, int targetQuality)
        {
            if (!autoBest) return GateOdds.AttemptsFor(condition, targetQuality);

            // Auto mode ignores MinSkill; the filters still bound the pool.
            var poolCondition = new ResumeCondition(
                0, condition.RequireInspired, condition.RequireSpecialist);
            Pawn? best = Dispatcher.AutoBestForDisplay(recipe, poolCondition);
            if (best == null) return GateOdds.AttemptsFor(condition, targetQuality);

            return ExpectedAttempts.For(
                QualityOdds.Distribution(
                    recipe != null
                        ? Dispatcher.SkillOf(best, recipe)
                        : Dispatcher.ConstructionSkillOf(best),
                    best.InspirationDef == InspirationDefOf.Inspired_Creativity,
                    Dispatcher.RoleOffsetOf(best)),
                targetQuality);
        }

    }
}
