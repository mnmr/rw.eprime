using WorkRoles.Core;

namespace WorkRoles.Lab.Data;

/// All-DLC vanilla work catalog used by the offline recommendation CLI.
internal sealed class LabJobCatalog : IJobCatalog
{
    private static readonly string[] NoGivers = Array.Empty<string>();

    public IReadOnlyList<string> WorkGiversOf(string workTypeDefName) =>
        workTypeDefName != null
        && VanillaWorkOrder.GiversInOrder.TryGetValue(
            workTypeDefName, out string[] givers)
            ? givers
            : NoGivers;

    public string WorkTypeOf(string workGiverDefName) =>
        workGiverDefName != null
        && VanillaGiverBaseline.GiverWorkType.TryGetValue(
            workGiverDefName, out string workType)
            ? workType
            : null;

    public bool IsEmergency(string workGiverDefName) => false;
}
