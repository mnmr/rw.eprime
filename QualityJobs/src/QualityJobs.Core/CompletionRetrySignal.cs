namespace QualityJobs.Core
{
    /// <summary>Single-slot transient signal joining product quality evaluation
    /// to the synchronous bill completion notification.</summary>
    public sealed class CompletionRetrySignal
    {
        private string? billId;
        private int tick = -1;

        public void Mark(string id, int gameTick)
        {
            if (billId == id && tick == gameTick) return;
            billId = id;
            tick = gameTick;
        }

        public bool Consume(string id, int gameTick)
        {
            bool matches = billId == id && tick == gameTick;
            billId = null;
            tick = -1;
            return matches;
        }
    }
}
