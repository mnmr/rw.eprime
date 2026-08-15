namespace QualityJobs.Core
{
    /// <summary>Content equality used when publishing immutable snapshots.</summary>
    public interface IContentSnapshot<in T>
    {
        bool HasSameContent(T other);
    }

    /// <summary>
    /// Preserves the current reference when a rebuild has equal observable
    /// content; otherwise publishes the candidate.
    /// </summary>
    public static class SnapshotPublication
    {
        public static T Publish<T>(T current, T candidate)
            where T : class, IContentSnapshot<T>
            => current.HasSameContent(candidate) ? current : candidate;
    }
}
