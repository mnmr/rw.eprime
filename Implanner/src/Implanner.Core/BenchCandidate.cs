namespace Implanner.Core
{
    /// One bench that can work a production recipe, as the dispatch pass
    /// sees it: its stable thing id, how many Implanner bills it holds, and
    /// whether any of its other bills currently wants work.
    public readonly struct BenchCandidate
    {
        public BenchCandidate(int id, int implannerBills, bool hasOtherWork)
        {
            Id = id;
            ImplannerBills = implannerBills;
            HasOtherWork = hasOtherWork;
        }

        public int Id { get; }
        public int ImplannerBills { get; }
        public bool HasOtherWork { get; }
    }
}
