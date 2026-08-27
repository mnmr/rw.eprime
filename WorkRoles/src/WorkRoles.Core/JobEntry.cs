using System;

namespace WorkRoles.Core
{
    public enum JobEntryKind { WorkType, WorkGiver }

    public readonly struct JobEntry
    {
        public JobEntryKind Kind { get; }
        public string DefName { get; }

        public JobEntry(JobEntryKind kind, string defName)
        {
            Kind = kind;
            DefName = defName;
        }

        private const string WorkTypePrefix = "WorkType:";
        private const string WorkGiverPrefix = "WorkGiver:";

        public string Encode() =>
            (Kind == JobEntryKind.WorkType ? WorkTypePrefix : WorkGiverPrefix) + DefName;

        public static bool TryDecode(string encoded, out JobEntry entry)
        {
            if (encoded != null)
            {
                if (encoded.StartsWith(WorkTypePrefix, StringComparison.Ordinal))
                {
                    entry = new JobEntry(JobEntryKind.WorkType, encoded.Substring(WorkTypePrefix.Length));
                    return true;
                }
                if (encoded.StartsWith(WorkGiverPrefix, StringComparison.Ordinal))
                {
                    entry = new JobEntry(JobEntryKind.WorkGiver, encoded.Substring(WorkGiverPrefix.Length));
                    return true;
                }
            }
            entry = default;
            return false;
        }
    }
}
