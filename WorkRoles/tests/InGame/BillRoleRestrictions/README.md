# Bill role restrictions

WR1 regression: load the copied Fisso save with the real Complex Jobs mod.
The saved Cook role remembers butchering even though Complex Jobs moves that
work giver out of Cooking. Assert Cook and a composite containing it remain
selectable, direct Butcher and ordinary Cooking choices remain available,
and unrelated work is excluded. Click Cook in the actual worker menu and
verify the resulting bill admits an active holder, rejects a non-holder,
and observes paused assignment toggles.

Build WorkRoles for 1.7, the shared automation runtime, and `Probe.csproj`.
Then run from the repository root:

```powershell
pwsh WorkRoles/tests/InGame/BillRoleRestrictions/run.ps1 -ComplexJobsRoot <path-to-FSF_Complex_Jobs>
```

Use the author's unmodified Complex Jobs source. The regression was first
reproduced with commit `47e3c27fbe789e8ad16d032c36831162d279a4a3`.
The runner creates and removes its own managed run and retains logs,
manifests and captures under `WorkRoles/temp/bill-role-regression`.
