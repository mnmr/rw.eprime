using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Validates portable references against the final selected configuration.
    /// The resolver returns the imported role, a retained destination role, or
    /// null when a dependency is neither selected nor already present.
    public static class RoleFileDependencies
    {
        private static readonly Func<FileRole, FileRole?> identity = role => role;
        public static string? Validate(RoleFileDocument document) => Validate(document, document.roles, identity);

        public static string? Validate(RoleFileDocument document, IEnumerable<FileRole> selected,
            Func<FileRole, FileRole?> effective)
        {
            foreach (FileRole role in selected)
            {
                foreach (FileRoleReference reference in role.members)
                {
                    FileRole? source = RoleFile.ResolveRole(document, reference.fileId, reference.label);
                    FileRole? member = source == null ? null : effective(source);
                    if (member == null) return Missing(role, reference);
                    if (ReferenceEquals(source, role) || member.composite
                        || member.activeHours != FileRole.AllHours || member.locations.Count != 0)
                        return "Invalid composite member: " + role.label + " → " + reference.label;
                }
                if (role.training.Count == 0) continue;
                var seen = new HashSet<FileRole>();
                var mins = new List<int>();
                var maxes = new List<int>();
                foreach (FileTrainingPathEntry entry in role.training)
                {
                    FileRole? member = RoleFile.ResolveRole(document, entry.role.fileId, entry.role.label);
                    if (member == null || effective(member) == null) return Missing(role, entry.role);
                    if (!seen.Add(member)) return "Duplicate training role: " + role.label;
                    mins.Add(entry.min); maxes.Add(entry.max);
                }
                if (!seen.Contains(role) || !SkillProgressionMath.Validate(seen.Count, mins, maxes))
                    return "Invalid training path: " + role.label;
            }
            return null;
        }

        private static string Missing(FileRole owner, FileRoleReference reference) =>
            "Missing role dependency: " + owner.label + " → " + reference.label;
    }
}
