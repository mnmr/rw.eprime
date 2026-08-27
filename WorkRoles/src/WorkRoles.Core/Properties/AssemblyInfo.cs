using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("WorkRoles.Core.Tests")]
// WorkRoles.Lab renders internal planner facts alongside
// the published plan; remove this friend when those diagnostics are retired.
[assembly: InternalsVisibleTo("WorkRoles.Lab")]
