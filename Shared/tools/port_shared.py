"""One-shot porter: copies files from the three mod repos into Shared/ with
namespace, using, doc-comment, and #nullable transforms. Safe to re-run."""
import os
import re
import sys

ROOT = r"D:\Code\RimWorld"
WR = os.path.join(ROOT, r"WorkRoles\src\WorkRoles.Core")
EPR = os.path.join(ROOT, r"Readouts\src\EPrimeReadouts.Core")
QJ = os.path.join(ROOT, r"QualityJobs\src\QualityJobs.Core")
WRT = os.path.join(ROOT, r"WorkRoles\tests\WorkRoles.Core.Tests")
EPRT = os.path.join(ROOT, r"Readouts\src\EPrimeReadouts.Core.Tests")
QJT = os.path.join(ROOT, r"QualityJobs\src\QualityJobs.Core.Tests")

COMMON = os.path.join(ROOT, r"Shared\Common")
TESTS = os.path.join(ROOT, r"Shared\Tests\RimShared.Common.Tests")

NS_SRC = "RimShared.Common"
NS_TEST = "RimShared.Common.Tests"

# (source root, relative source path, dest folder under Common, nullable directive)
SOURCES = [
    # Caching
    (QJ, "OwnerGenerationRegistry.cs", "Caching", "enable"),
    (EPR, "RevisionedCache.cs", "Caching", "disable"),
    (EPR, "RenderDataCache.cs", "Caching", "disable"),
    (EPR, "SelectedDisplayNameCache.cs", "Caching", "disable"),
    (WR, "SelectiveSnapshotCache.cs", "Caching", "disable"),
    (WR, "OwnerInvalidationRevisions.cs", "Caching", "disable"),
    (WR, "ManagedSnapshotCache.cs", "Caching", "disable"),
    (WR, "VersionedSnapshotCache.cs", "Caching", "disable"),
    (WR, "ExplicitSnapshotCache.cs", "Caching", "disable"),
    (WR, "ExplicitProjectionCache.cs", "Caching", "disable"),
    (WR, "DefinitionOwnedCache.cs", "Caching", "disable"),
    (WR, "MemoizedFactory.cs", "Caching", "disable"),
    (WR, "OwnerScopedTransferTable.cs", "Caching", "disable"),
    (QJ, "TickMemo.cs", "Caching", "enable"),
    (QJ, "RevisionMemo.cs", "Caching", "enable"),
    (QJ, "SnapshotPublication.cs", "Caching", "enable"),
    # Invalidation
    (QJ, "FixedTickBoundaryGate.cs", "Invalidation", "enable"),
    (EPR, "UiMetricRevision.cs", "Invalidation", "disable"),
    (WR, "ScopeCacheStamp.cs", "Invalidation", "disable"),
    (WR, "PawnListRevisionTracker.cs", "Invalidation", "disable"),
    (WR, "UiInvalidationBatch.cs", "Invalidation", "disable"),
    (WR, "DeferredInvalidationRevision.cs", "Invalidation", "disable"),
    (WR, "RevisionPairGate.cs", "Invalidation", "disable"),
    (QJ, "TrackedTargetIds.cs", "Invalidation", "enable"),
    # Lifecycle
    (WR, "PendingUpdate.cs", "Lifecycle", "disable"),
    (WR, "IdentityKeySweepPlanner.cs", "Lifecycle", "disable"),
    (WR, "ManagedDepartureTracker.cs", "Lifecycle", "disable"),
    (WR, "IdentitySelectionPreserver.cs", "Lifecycle", "disable"),
    (WR, "ContextualDrainQueue.cs", "Lifecycle", "disable"),
    (WR, "ParallelIndexGuard.cs", "Lifecycle", "disable"),
    # Layout
    (WR, "UniformViewportRange.cs", "Layout", "disable"),
    (WR, "VariableViewportLayout.cs", "Layout", "disable"),
    (WR, "InclinedLabelGeometry.cs", "Layout", "disable"),
    (WR, "ContentPassPolicy.cs", "Layout", "disable"),
    (EPR, "ColumnGrid.cs", "Layout", "disable"),
    (EPR, "RectF.cs", "Layout", "disable"),
    # Tips
    (QJ, "TipContinuity.cs", "Tips", "enable"),
    (QJ, "TipGatherPolicy.cs", "Tips", "enable"),
    (WR, "TooltipDisplayGate.cs", "Tips", "disable"),
    (WR, "TipBalancePolicy.cs", "Tips", "disable"),
    (EPR, "TipLayoutPolicy.cs", "Tips", "disable"),
    (QJ, "TooltipPlacement.cs", "Tips", "enable"),
    # Text
    (EPR, "CountFormat.cs", "Text", "disable"),
    (EPR, "SearchMatcher.cs", "Text", "disable"),
    (EPR, "TextHeightCache.cs", "Text", "disable"),
    (WR, "RoleAbbreviations.cs", "Text", "disable"),
    (WR, "InvariantDefName.cs", "Text", "disable"),
    (WR, "CatalogNameRules.cs", "Text", "disable"),
    # Collections
    (WR, "GroupEngine.cs", "Collections", "disable"),
    (WR, "ClipboardRules.cs", "Collections", "disable"),
    (WR, "SwatchPickPlanner.cs", "Collections", "disable"),
]

WRT_TESTS = [
    ("Caching", ["OwnerGenerationRegistryTests", "SelectiveSnapshotCacheTests",
                 "ManagedSnapshotCacheTests", "OwnerScopedTransferTableTests",
                 "VersionedSnapshotCacheTests", "ExplicitSnapshotCacheTests",
                 "ExplicitProjectionCacheTests", "MemoizedFactoryTests",
                 "DefinitionOwnedCacheTests"]),
    ("Invalidation", ["PawnListRevisionTrackerTests",
                      "DeferredInvalidationRevisionTests",
                      "RevisionPairGateTests", "ScopeCacheStampTests"]),
    ("Lifecycle", ["PendingUpdateTests", "IdentityKeySweepPlannerTests",
                   "ManagedDepartureTrackerTests", "IdentitySelectionPreserverTests",
                   "ContextualDrainQueueTests"]),
    ("UI", ["VariableViewportLayoutTests", "SwatchPickPlannerTests",
            "RoleAbbreviationsTests", "ParallelIndexGuardTests",
            "TooltipDisplayGateTests", "GroupEngineTests",
            "PaletteCoverageEnforcerTests", "ClipboardRulesTests",
            "ContentPassPolicyTests", "TipGatherPolicyTests"]),
    ("Roles", ["CatalogNameRulesTests", "GroupNameRulesTests",
               "InvariantDefNameTests"]),
]

# EPRT root files -> destination subfolder
EPRT_TESTS = {
    "RevisionedCacheTests": "Caching",
    "TextHeightCacheTests": "Text",
    "SelectedDisplayNameCacheTests": "Caching",
    "RenderDataCacheTests": "Caching",
    "CountFormatTests": "Text",
    "SearchMatcherTests": "Text",
    "ColumnGridTests": "Layout",
    "UiMetricRevisionTests": "Invalidation",
    "TipLayoutPolicyTests": "Tips",
    "TipTableAlignmentTests": "Tips",
    "UniformViewportRangeTests": "Layout",
    "TipContinuityTests": "Tips",
}

QJT_TESTS = {
    "TickMemoTests": "Caching",
    "RevisionMemoTests": "Caching",
    "TooltipPlacementTests": "Tips",
    "FixedTickBoundaryGateTests": "Invalidation",
    "SnapshotPublicationTests": "Caching",
    "TrackedTargetIdsTests": "Invalidation",
    "TipSupportTests": "Tips",
}

MOD_NS = r"(?:WorkRoles\.Core|EPrimeReadouts\.Core|QualityJobs\.Core)"
NS_DECL_RE = re.compile(
    r"^(\s*)namespace\s+" + MOD_NS + r"(?P<tests>\.Tests(?:\.[\w.]+)?)?(?P<tail>\s*[;{]?\s*)$")
USING_RE = re.compile(r"^(\s*)using\s+" + MOD_NS + r"\s*;\s*$")
DROP_RE = re.compile(r"Ported from|keep in lockstep")

created = []


def transform(src_path, dest_path, nullable, is_test):
    with open(src_path, "r", encoding="utf-8-sig", newline="") as f:
        text = f.read()
    newline = "\r\n" if "\r\n" in text else "\n"
    lines = text.split("\n")
    out = []
    for raw in lines:
        line = raw.rstrip("\r")
        if DROP_RE.search(line):
            continue
        m = NS_DECL_RE.match(line)
        if m:
            ns = NS_TEST if (is_test or m.group("tests")) else NS_SRC
            line = m.group(1) + "namespace " + ns + m.group("tail").rstrip()
            out.append(line)
            continue
        m = USING_RE.match(line)
        if m:
            out.append(m.group(1) + "using " + NS_SRC + ";")
            continue
        out.append(line)
    directive = "#nullable enable" if nullable == "enable" else "#nullable disable"
    body = "\n".join(out)
    result = directive + "\n" + body
    os.makedirs(os.path.dirname(dest_path), exist_ok=True)
    with open(dest_path, "w", encoding="utf-8", newline="") as f:
        f.write(result.replace("\n", newline) if newline == "\r\n" else result)
    created.append(dest_path)


def main():
    missing = []
    for root, rel, folder, nullable in SOURCES:
        src = os.path.join(root, rel)
        if not os.path.isfile(src):
            missing.append(src)
            continue
        transform(src, os.path.join(COMMON, folder, os.path.basename(rel)),
                  nullable, is_test=False)

    for folder, names in WRT_TESTS:
        for name in names:
            src = os.path.join(WRT, folder, name + ".cs")
            if not os.path.isfile(src):
                missing.append(src)
                continue
            transform(src, os.path.join(TESTS, folder, name + ".cs"),
                      "disable", is_test=True)

    for name, folder in EPRT_TESTS.items():
        src = os.path.join(EPRT, name + ".cs")
        if not os.path.isfile(src):
            missing.append(src)
            continue
        transform(src, os.path.join(TESTS, folder, name + ".cs"),
                  "disable", is_test=True)

    for name, folder in QJT_TESTS.items():
        src = os.path.join(QJT, name + ".cs")
        if not os.path.isfile(src):
            missing.append(src)
            continue
        transform(src, os.path.join(TESTS, folder, name + ".cs"),
                  "enable", is_test=True)

    print(f"created {len(created)} files")
    if missing:
        print("MISSING SOURCES:")
        for m in missing:
            print("  " + m)
        sys.exit(1)


if __name__ == "__main__":
    main()
