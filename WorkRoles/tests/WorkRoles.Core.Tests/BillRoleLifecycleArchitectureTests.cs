namespace WorkRoles.Core.Tests;

public class BillRoleLifecycleArchitectureTests
{
    [Test]
    public async Task BillRoleDictionaryUsesReferenceIdentityBeforeCrossRefsResolve()
    {
        string store = Source("RoleStore.cs");
        string expose = Method(store, "public override void ExposeData()");
        string factory = Method(store,
            "private static Dictionary<Bill, int> NewBillRoleDictionary()");
        string ensure = Method(store,
            "private void EnsureBillRoleIdentityComparer()");

        await Assert.That(store).Contains(
            "public Dictionary<Bill, int> billRoles = NewBillRoleDictionary();");
        await Assert.That(factory).Contains(
            "ReferenceIdentityComparer<Bill>.Instance");
        await Assert.That(ensure).Contains("billRoles == null");
        await Assert.That(ensure).Contains("ReferenceEquals(billRoles.Comparer,");
        await Assert.That(ensure).Contains("ReferenceIdentityComparer<Bill>.Instance");

        int look = expose.IndexOf("Scribe_Collections.Look(ref billRoles",
            StringComparison.Ordinal);
        int nextScribe = expose.IndexOf("Scribe_Values.Look(ref nextPathId", look,
            StringComparison.Ordinal);
        string postLookWindow = look >= 0 && nextScribe > look
            ? expose.Substring(look, nextScribe - look)
            : "";
        int loadingBranch = postLookWindow.IndexOf(
            "if (Scribe.mode == LoadSaveMode.LoadingVars)", StringComparison.Ordinal);
        int identityAssignment = postLookWindow.IndexOf(
            "billRoles = NewBillRoleDictionary();", StringComparison.Ordinal);
        int postLoad = expose.IndexOf("EnsureBillRoleIdentityComparer()",
            nextScribe + 1, StringComparison.Ordinal);

        await Assert.That(look >= 0 && nextScribe > look
            && loadingBranch >= 0 && identityAssignment > loadingBranch).IsTrue()
            .Because("LoadingVars must replace Scribe's still-empty default dictionary before ResolvingCrossRefs fills it");
        await Assert.That(postLoad > nextScribe).IsTrue()
            .Because("PostLoadInit must also repair null or non-identity dictionaries");
    }

    [Test]
    public async Task LegacyAllRoleMigratesBeforeMissingBillRoleSanitation()
    {
        string expose = Method(Source("RoleStore.cs"),
            "public override void ExposeData()");
        int postLoad = expose.IndexOf(
            "if (Scribe.mode == LoadSaveMode.PostLoadInit)", StringComparison.Ordinal);
        int deadBillSanitation = expose.IndexOf(
            "billRoles.RemoveAll(kv => kv.Key == null || kv.Key.deleted)",
            postLoad, StringComparison.Ordinal);
        int migration = expose.IndexOf("if (allRole != null)", postLoad,
            StringComparison.Ordinal);
        int missingRoleSanitation = expose.IndexOf(
            "billRoles.RemoveAll(kv => RoleById(kv.Value) == null)",
            postLoad, StringComparison.Ordinal);

        await Assert.That(postLoad >= 0 && deadBillSanitation > postLoad
            && migration > deadBillSanitation
            && missingRoleSanitation > migration).IsTrue()
            .Because("allRole-backed bill mappings are valid only after the legacy role joins roles");
    }

    [Test]
    public async Task BillRoleMutationsRouteThroughIdempotentStoreHelpers()
    {
        string store = Source("RoleStore.cs");
        string commands = Source("RoleCommands.cs");
        string billRoles = Source("BillRoles.cs");
        string patches = Source("Patches", "Patch_BillRoles.cs");

        await Assert.That(store).Contains("internal bool RemoveBillRole(Bill bill)");
        await Assert.That(store).Contains("internal bool SetBillRole(Bill bill, int roleId)");
        await Assert.That(store).Contains("internal int RemoveBillRolesForStack(BillStack stack)");
        await Assert.That(store).Contains("internal int RemoveBillRolesForRole(int roleId)");
        await Assert.That(store).Contains(
            "internal int SweepBillRoles(IEnumerable<Bill> liveBills)");
        await Assert.That(Method(commands,
            "public static void SetBillRole(Bill bill, int roleId)"))
            .Contains("Store.SetBillRole(bill, roleId)");
        await Assert.That(Method(commands, "public static void DeleteRole(int roleId)"))
            .Contains("Store.RemoveBillRolesForRole(roleId)");
        await Assert.That(Method(billRoles, "public static Role RestrictionFor(Bill bill)"))
            .DoesNotContain("billRoles.Remove");
        await Assert.That(Method(patches, "public static class Patch_Bill_Clone"))
            .DoesNotContain("billRoles");
    }

    [Test]
    public async Task CloneStagesOwnerScopedTransferWithoutMutatingScribedMappings()
    {
        string transferPath = Path.Combine(RepoRoot(), "src", "WorkRoles",
            "BillRoleTransfer.cs");
        string transfer = File.Exists(transferPath)
            ? File.ReadAllText(transferPath)
            : "";
        string transferTable = File.ReadAllText(Path.Combine(RepoRoot(), "src",
            "WorkRoles.Core", "OwnerScopedTransferTable.cs"));
        string patches = Source("Patches", "Patch_BillRoles.cs");
        string clone = Method(patches, "public static class Patch_Bill_Clone");
        string saveSweep = Method(Source("RoleStore.cs"),
            "private void SweepBillRolesBeforeSave()");

        await Assert.That(transfer).Contains(
            "OwnerScopedTransferTable<Bill, RoleStore, int>");
        await Assert.That(transferTable).Contains("ConditionalWeakTable<TKey, Entry>");
        await Assert.That(transferTable).Contains("WeakReference<TOwner>");
        await Assert.That(clone).Contains("BillRoleTransfer.PropagateClone");
        await Assert.That(clone).DoesNotContain("billRoles[");
        await Assert.That(saveSweep).DoesNotContain("BillRoleTransfer");
        await Assert.That(transfer).DoesNotContain("WorldComponentTick");
    }

    [Test]
    public async Task BillSerializationRoutesOptionalTransientMarker()
    {
        string source = Source("Patches", "Patch_BillRoles.cs");
        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(Bill), nameof(Bill.ExposeData))]");
        string expose = Method(source,
            "public static class Patch_Bill_ExposeData");

        await Assert.That(expose).Contains("BillRoleTransfer.RoleIdForScribe");
        await Assert.That(expose).Contains(
            "Scribe_Values.Look(ref roleId, \"workRoles_billRoleId\", -1)");
        await Assert.That(expose).Contains("LoadSaveMode.LoadingVars");
        await Assert.That(expose).Contains("BillRoleTransfer.RestoreFromScribe");
    }

    [Test]
    public async Task AddBillPromotesOnlyAfterExactMembershipAndConsumesTransfer()
    {
        string source = Source("Patches", "Patch_BillRoles.cs");
        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill), typeof(Bill))]");
        string add = Method(source, "public static class Patch_BillStack_AddBill");
        int membership = add.IndexOf("RoleStore.BillStackContainsReference",
            StringComparison.Ordinal);
        int consume = add.IndexOf("BillRoleTransfer.TryConsume",
            StringComparison.Ordinal);
        int promote = add.IndexOf("store.SetBillRole", StringComparison.Ordinal);

        await Assert.That(add).Contains("public static void Postfix");
        await Assert.That(membership >= 0 && consume > membership
            && promote > consume).IsTrue();
        await Assert.That(add).DoesNotContain("BillRoleTransfer.TryGet");
        await Assert.That(add).DoesNotContain("store.billRoles[");
    }

    [Test]
    public async Task ExactBillLifecycleTargetsRemoveOnlyAfterTheActionOccurred()
    {
        string source = Source("Patches", "Patch_BillRoles.cs");

        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(BillStack), nameof(BillStack.Delete), typeof(Bill))]");
        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(BillStack), nameof(BillStack.Clear))]");
        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(BillStack), nameof(BillStack.RemoveIncompletableBills))]");
        await Assert.That(source).Contains(
            "[HarmonyPatch(typeof(Thing), nameof(Thing.Destroy), typeof(DestroyMode))]");

        string delete = Method(source, "public static class Patch_BillStack_Delete");
        await Assert.That(delete).Contains("public static void Postfix");
        await Assert.That(delete).Contains(
            "bill.deleted || !RoleStore.BillStackContainsReference");
        await Assert.That(delete).Contains("RemoveBillRole(bill)");

        string clear = Method(source, "public static class Patch_BillStack_Clear");
        await Assert.That(clear).Contains("CaptureBillRolesForStack");
        await Assert.That(clear).Contains("public static void Postfix");
        await Assert.That(clear).Contains("RemoveCapturedBillRolesMissingFromStack");
        await Assert.That(clear).DoesNotContain("new List<Bill>");

        string incompletable = Method(source,
            "public static class Patch_BillStack_RemoveIncompletableBills");
        await Assert.That(incompletable).Contains("onlyIncompletable: true");
        await Assert.That(incompletable).Contains(
            "RemoveCapturedBillRolesMissingFromStack");
        await Assert.That(incompletable).DoesNotContain("new List<Bill>");

        string destroy = Method(source, "public static class Patch_Thing_DestroyBillRoles");
        await Assert.That(destroy).Contains("public static void Postfix");
        await Assert.That(destroy).Contains("__instance.Destroyed");
        await Assert.That(destroy).Contains("RemoveBillRolesForStack(__state)");
    }

    [Test]
    public async Task SavingSweepsReferenceDeduplicatedLiveOwnerInventoryWithoutReadMutation()
    {
        string store = Source("RoleStore.cs");
        string billRoles = Source("BillRoles.cs");
        string expose = Method(store, "public override void ExposeData()");
        string sweepMethod = Method(store, "private void SweepBillRolesBeforeSave()");

        int sweep = expose.IndexOf("SweepBillRolesBeforeSave()", StringComparison.Ordinal);
        int scribe = expose.IndexOf("Scribe_Collections.Look(ref billRoles",
            StringComparison.Ordinal);
        await Assert.That(sweep >= 0 && scribe > sweep).IsTrue()
            .Because("live-bill cleanup must happen before billRoles is scribed");
        await Assert.That(sweepMethod).Contains("Find.Maps");
        await Assert.That(sweepMethod).Contains("map.listerThings.AllThings");
        await Assert.That(sweepMethod).Contains("PawnsFinder.All_AliveOrDead");
        await Assert.That(sweepMethod).Contains("ReferenceIdentityComparer<Bill>.Instance");
        await Assert.That(sweepMethod).Contains("IsAttachedToLiveOwner");
        await Assert.That(expose).DoesNotContain("kv.Key.DeletedOrDereferenced");
        await Assert.That(Method(billRoles, "public static Role RestrictionFor(Bill bill)"))
            .DoesNotContain(".Remove(");
    }

    [Test]
    public async Task IdentityPlannerReusesOnlyItsAuditedReferenceHashSet()
    {
        string planner = File.ReadAllText(Path.Combine(RepoRoot(), "src",
            "WorkRoles.Core", "IdentityKeySweepPlanner.cs"));

        await Assert.That(planner).Contains("liveKeys is HashSet<TKey> candidate");
        await Assert.That(planner).Contains(
            "ReferenceEquals(candidate.Comparer, ReferenceIdentityComparer<TKey>.Instance)");
        await Assert.That(planner).Contains("StaleKeysAgainstSet(storedKeys, candidate)");
    }

    private static string Method(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return "";
        int open = source.IndexOf('{', start);
        if (open < 0) return "";
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(start, i - start + 1);
        }
        return "";
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "WorkRoles" }
            .Concat(parts).ToArray()));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
