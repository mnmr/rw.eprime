namespace WorkRoles.Core.Recs
{
    public enum RuleKind { Colony, PerPawn }

    /// One independent pipeline step. Colony rules see the whole context
    /// once; per-pawn rules run once per pawn. Irrelevant rules auto-skip.
    public abstract class RecRule
    {
        public abstract string Id { get; }
        public abstract RuleKind Kind { get; }
        public virtual bool Relevant(EngineContext context) => true;
        public virtual void Apply(EngineContext context) { }
        public virtual void Apply(EngineContext context, int pawnIndex) { }
    }
}
