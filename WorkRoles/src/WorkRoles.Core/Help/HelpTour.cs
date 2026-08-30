namespace WorkRoles.Core.Help
{
    /// <summary>
    /// The guided tour: essential topic slugs in reading order. Lives in
    /// Core so the shipped-content test can verify every slug exists.
    /// </summary>
    public static class HelpTour
    {
        public static readonly string[] Slugs =
        {
            "roles",
            "assigning",
            "states",
            "ordering",
            "selected-panel",
            "role-tree",
            "role-editor",
            "picking-jobs",
            "role-types",
            "recommendations",
            "verdicts",
            "pinning",
        };
    }
}
