using System.Collections.Generic;

namespace WorkRoles.Core
{
    public interface IJobCatalog
    {
        bool WorkGiverExists(string workGiverDefName);
        bool WorkTypeExists(string workTypeDefName);
        /// <summary>Givers of a work type, ordered by priorityInType descending (vanilla scan order).</summary>
        IReadOnlyList<string> WorkGiversOf(string workTypeDefName);
        /// <summary>Owning work type defName, or null if the giver is unknown.</summary>
        string WorkTypeOf(string workGiverDefName);
        bool IsEmergency(string workGiverDefName);
    }
}
