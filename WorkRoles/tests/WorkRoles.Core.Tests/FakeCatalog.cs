using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public sealed class FakeCatalog : IJobCatalog
{
    private readonly Dictionary<string, List<string>> typeToGivers = new();
    private readonly Dictionary<string, string> giverToType = new();
    private readonly HashSet<string> emergency = new();

    public FakeCatalog WithWorkType(string workType, params string[] givers)
    {
        typeToGivers[workType] = givers.ToList();
        foreach (var g in givers) giverToType[g] = workType;
        return this;
    }

    public FakeCatalog WithEmergency(params string[] givers)
    {
        foreach (var g in givers) emergency.Add(g);
        return this;
    }

    public bool WorkGiverExists(string workGiverDefName) => giverToType.ContainsKey(workGiverDefName);
    public bool WorkTypeExists(string workTypeDefName) => typeToGivers.ContainsKey(workTypeDefName);
    public IReadOnlyList<string> WorkGiversOf(string workTypeDefName) =>
        typeToGivers.TryGetValue(workTypeDefName, out var list) ? list : new List<string>();
    public string WorkTypeOf(string workGiverDefName) =>
        giverToType.TryGetValue(workGiverDefName, out var t) ? t : null;
    public bool IsEmergency(string workGiverDefName) => emergency.Contains(workGiverDefName);
}
