using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("WorkRoles.Core.Tests")]
// Temporary: lets the temp/OrderingLab comparison harness drive the staged
// planner until RecsEngine.Plan exists (plan Task 9); remove afterwards.
[assembly: InternalsVisibleTo("OrderingLab")]
