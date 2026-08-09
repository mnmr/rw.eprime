namespace WorkRoles.Core
{
    /// How a role's assignment strategy fills holders, independent of the
    /// numeric band values. Skilled uses the signal machinery (Strong+ surplus);
    /// Unskilled assigns every capable pawn except hard vetoes; Never assigns
    /// none and carries no numerics.
    public enum ScaleMode
    {
        Skilled = 0,
        Unskilled = 1,
        Never = 2,
    }
}
