namespace WorkRoles.Core.Tests;

/// Locates the shipped mod folder (the one holding 1.6/Defs) by walking up
/// from the test output directory.
internal static class ShippedMod
{
    private static readonly Lazy<string> root = new(FindRoot);

    internal static string Root => root.Value;

    /// A path inside the shipped mod folder, e.g. Path("1.6", "Defs", "Roles.xml").
    internal static string Path(params string[] parts) =>
        System.IO.Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string mod = System.IO.Path.Combine(dir.FullName, "mod");
            if (Directory.Exists(System.IO.Path.Combine(mod, "1.6", "Defs")))
                return mod;
        }
        throw new DirectoryNotFoundException(
            "mod/1.6/Defs not found above " + AppContext.BaseDirectory);
    }
}
