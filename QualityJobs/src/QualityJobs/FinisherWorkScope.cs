using Verse;

namespace QualityJobs
{
    /// Definition-owned, immutable scheduling metadata. Key: generated giver
    /// identity. Value: its original category reference. Dependency: the category
    /// chosen when the giver is first published. Refresh: created with a new
    /// giver; existing implied defs retain their original scope on hot reload.
    /// Equality: identity follows the owning def.
    /// Teardown: released with that def; retains no pawn, map, or save state.
    internal sealed class FinisherWorkScope : DefModExtension
    {
        internal readonly WorkTypeDef OriginalCategory;

        internal FinisherWorkScope(WorkTypeDef originalCategory)
        {
            OriginalCategory = originalCategory;
        }
    }
}
