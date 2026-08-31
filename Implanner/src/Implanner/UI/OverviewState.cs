using System;
using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    internal enum OverviewColumn
    {
        Name = 0,
        Plan = 1,
        Progress = 2,
        Shooting = 3,
        Melee = 4,
        Priority = 5,
        State = 6,
    }

    /// One colonist row (or grouping header) fully resolved for drawing. The
    /// Pawn reference is observed for identity and click targets only, never
    /// mutated. WeaponDef/UtilityDef are display-only equipped gear — no
    /// Implanner logic is tied to them.
    internal sealed class OverviewRow
    {
        internal bool Header;
        internal string SectionKey = "";     // header rows: collapse identity
        internal ThingDef? WeaponDef;        // equipped weapon, observed only
        internal ThingDef? UtilityDef;       // worn belt-slot item, observed only
        internal Pawn Pawn = null!;
        internal int PawnId;
        internal string Name = "";
        internal int PlanId;
        internal string PlanName = "";
        internal float Progress;
        internal string ProgressText = "";
        internal PawnPlanState State;
        internal string StateText = "";
        internal string? GroupingId;
        internal PlanEvaluation? Evaluation;

        /// The plan's effective goals (own + inherited), parallel to
        /// Evaluation.Implants by index.
        internal List<ImplantGoal>? Goals;
        internal int Shooting;
        internal int Melee;
        internal string ShootingText = "";
        internal string MeleeText = "";
        internal int Priority = PlannerModel.PriorityNormal;
        internal string PriorityText = "";
        internal int BarIndex;
    }

    /// One line in the colonist details panel: a category header carrying its
    /// progress, or an itemized goal beneath one.
    internal sealed class DetailRow
    {
        internal bool Header;
        internal string Label = "";
        internal string StatusText = "";   // items: status; headers: progress
        internal bool Satisfied;
        internal bool Blocked;
        internal bool Muted;
    }

    internal sealed class OverviewSnapshot
    {
        internal List<GroupingOption> GroupingOptions = new List<GroupingOption>();
        internal List<string> GroupingLabels = new List<string>();
        internal List<string> GroupByKeys = new List<string>();
        internal List<string> GroupByLabels = new List<string>();
        internal string GroupByLabel = "";
        internal List<OverviewRow> Rows = new List<OverviewRow>();
        internal string PercentText = "";
        internal string StatsText = "";
        internal string BlockersText = "";
        internal List<string> NextWork = new List<string>();
    }

    /// Overview tab presentation state. Owned by the dialog; dies with it.
    internal sealed class OverviewState
    {
        // Cache contract:
        // Owner: the Implanner dialog window.
        // Key: UiVersion.Current, store identity and Version,
        //   ExternalPawnFacts.Revision, ColonyScope.LocationRevision, the
        //   current map id, the selected grouping id, the group-by key, and
        //   the sort column/direction/name-order selections.
        // Value: an immutable overview snapshot (rows with grouping headers,
        //   selector options, aggregates); Pawn references are observed,
        //   never mutated.
        // Dependencies: plans, assignments, and priorities (store revisions),
        //   worn gear, hediffs, and roster membership (ExternalPawnFacts),
        //   serviceable locations and caravans (LocationRevision + facts
        //   revision), colonist-bar order (observed at rebuild; a pure bar
        //   reorder becomes visible on the next facts or metric revision),
        //   view selections, and UI metrics for labels. The informational
        //   Shooting/Melee columns are SAMPLED at rebuild by design (no
        //   skill-change seam exists): the window collects its data when a
        //   dependency moves or it reopens, and displays that snapshot.
        // Refresh policy: immediate on the next Repaint read after any key
        //   component moves; structural edits are visible while paused
        //   because commands bump store revisions immediately.
        // Equality policy: rebuilds replace the snapshot; the gate bounds
        //   rebuild frequency to actual dependency movement.
        // Teardown: Release() drops the snapshot and detail rows.
        private OverviewSnapshot? snapshot;
        private int uiStamp = -1;
        private ImplannerStore? owner;
        private int storeStamp = -1;
        private int factsStamp = -1;
        private int locationStamp = -1;
        private int mapStamp = -1;
        private GroupingKind groupingKindStamp = (GroupingKind)(-1);
        private string? groupingLocationStamp;
        private string? groupByStamp;
        private OverviewColumn sortStamp;
        private bool sortDescendingStamp;
        private bool nameAlphabeticalStamp;

        // Cache contract:
        // Owner: the Implanner dialog window.
        // Key: the overview snapshot identity plus the selected pawn id.
        // Value: immutable detail rows plus the panel title/percent.
        // Dependencies: the selected row's evaluation (carried by the
        //   snapshot) and the assigned plan content.
        // Refresh policy: immediate when the snapshot or selection changes.
        // Equality policy: rebuilds replace the list.
        // Teardown: Release() clears it.
        private List<DetailRow>? detailRows;
        private OverviewSnapshot? detailSnapshot;
        private int detailPawnId = -1;

        internal GroupingOption? Grouping;
        internal int SelectedPawnId = -1;

        // Session view state: sorting and grouping selections.
        internal OverviewColumn SortColumn = OverviewColumn.Name;
        internal bool SortDescending;
        /// Name column toggles between colonist-bar order (default) and A-Z.
        internal bool NameAlphabetical;
        internal string GroupByKey = "none";

        /// Session view state: collapsed table sections (group-by headers).
        internal readonly HashSet<string> CollapsedTableGroups =
            new HashSet<string>(StringComparer.Ordinal);

        internal string DetailTitle { get; private set; } = "";
        internal string DetailPercent { get; private set; } = "";

        /// The selected colonist has regressed goals: the details panel
        /// offers the explicit re-enlist command.
        internal bool DetailHasRegression { get; private set; }

        internal void Release()
        {
            snapshot = null;
            owner = null;
            detailRows = null;
            detailSnapshot = null;
            detailPawnId = -1;
            uiStamp = -1;
            storeStamp = -1;
            factsStamp = -1;
            locationStamp = -1;
            mapStamp = -1;
            groupingKindStamp = (GroupingKind)(-1);
            groupingLocationStamp = null;
            groupByStamp = null;
        }

        /// Column-header click: first click selects (with the column's natural
        /// direction), a repeat click toggles. Name toggles bar order vs A-Z.
        internal void ClickColumn(OverviewColumn column)
        {
            if (SortColumn != column)
            {
                SortColumn = column;
                SortDescending = column == OverviewColumn.Shooting
                    || column == OverviewColumn.Melee
                    || column == OverviewColumn.Progress;
                if (column == OverviewColumn.Name) NameAlphabetical = false;
                return;
            }
            if (column == OverviewColumn.Name)
                NameAlphabetical = !NameAlphabetical;
            else
                SortDescending = !SortDescending;
        }

        /// Called on the Repaint pass only.
        internal OverviewSnapshot Current(ImplannerStore store)
        {
            int mapId = Find.CurrentMap?.uniqueID ?? -1;
            // Two raw stamps instead of a composite key string: the gate
            // runs on every Repaint and a cache hit must not allocate.
            GroupingKind groupingKind = Grouping?.Kind ?? (GroupingKind)(-1);
            string? groupingLocation =
                Grouping?.Kind == GroupingKind.Location ? Grouping.LocationId : null;
            if (snapshot == null
                || uiStamp != UiVersion.Current
                || !ReferenceEquals(owner, store)
                || storeStamp != store.Version
                || factsStamp != ExternalPawnFacts.Revision
                || locationStamp != ColonyScope.LocationRevision
                || mapStamp != mapId
                || groupingKindStamp != groupingKind
                || !string.Equals(groupingLocationStamp, groupingLocation,
                    StringComparison.Ordinal)
                || !string.Equals(groupByStamp, GroupByKey, StringComparison.Ordinal)
                || sortStamp != SortColumn
                || sortDescendingStamp != SortDescending
                || nameAlphabeticalStamp != NameAlphabetical)
            {
                snapshot = Build(store);
                uiStamp = UiVersion.Current;
                owner = store;
                storeStamp = store.Version;
                factsStamp = ExternalPawnFacts.Revision;
                locationStamp = ColonyScope.LocationRevision;
                mapStamp = mapId;
                // Re-read: Build revalidates the grouping selection.
                groupingKindStamp = Grouping?.Kind ?? (GroupingKind)(-1);
                groupingLocationStamp =
                    Grouping?.Kind == GroupingKind.Location ? Grouping.LocationId : null;
                groupByStamp = GroupByKey;
                sortStamp = SortColumn;
                sortDescendingStamp = SortDescending;
                nameAlphabeticalStamp = NameAlphabetical;
            }
            return snapshot;
        }

        internal IReadOnlyList<DetailRow> Details(OverviewSnapshot current)
        {
            if (detailRows == null
                || !ReferenceEquals(detailSnapshot, current)
                || detailPawnId != SelectedPawnId)
            {
                detailRows = BuildDetails(current);
                detailSnapshot = current;
                detailPawnId = SelectedPawnId;
            }
            return detailRows;
        }

        private OverviewSnapshot Build(ImplannerStore store)
        {
            var result = new OverviewSnapshot();

            // Grouping catalog: serviceable locations plus live caravans.
            var infos = new List<GroupingInfo>(ColonyScope.Locations());
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (caravan.Faction != ColonyScope.ViewFaction) continue;
                infos.Add(new GroupingInfo(
                    LocationGrouping.CaravanPrefix + caravan.ID.ToStringCached(),
                    caravan.LabelCap, isShip: false, isCaravan: true));
            }
            result.GroupingOptions = LocationGrouping.BuildOptions(infos);
            Grouping = LocationGrouping.Revalidate(Grouping, result.GroupingOptions);
            for (int i = 0; i < result.GroupingOptions.Count; i++)
                result.GroupingLabels.Add(LabelOf(result.GroupingOptions[i]));
            var locationLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < infos.Count; i++)
                locationLabels[infos[i].Id] = infos[i].Label;

            BuildGroupByOptions(result);

            string currentId = ColonyScope.CurrentLocationId();
            GroupingOption grouping = Grouping!;

            // Pawns in colonist-bar order (the Name column's default order).
            List<Pawn> pawns = InBarOrder(ColonyScope.AllPlanableColonists());

            var rows = new List<OverviewRow>();
            int complete = 0, total = 0, blockedGoals = 0;
            int unitsSatisfied = 0, unitsTotal = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                string? pawnGroupingId = ColonyScope.GroupingIdOf(pawn);
                if (!LocationGrouping.Matches(grouping, pawnGroupingId, currentId))
                    continue;

                var row = new OverviewRow
                {
                    Pawn = pawn,
                    PawnId = pawn.thingIDNumber,
                    Name = pawn.LabelShortCap,
                    GroupingId = pawnGroupingId,
                    BarIndex = i,
                    Shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0,
                    Melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0,
                    Priority = store.Model.PriorityOf(pawn.thingIDNumber),
                    WeaponDef = pawn.equipment?.Primary?.def,
                    UtilityDef = WornBeltDef(pawn),
                };
                row.ShootingText = row.Shooting.ToStringCached();
                row.MeleeText = row.Melee.ToStringCached();
                row.PriorityText = PlannerLabels.PriorityLabel(row.Priority);
                Plan? plan = store.Model.AssignedPlan(pawn.thingIDNumber);
                if (plan == null)
                {
                    row.PlanId = 0;
                    row.PlanName = "IMP_NoPlan".Translate();
                }
                else
                {
                    bool away = !ColonyScope.PlaceOf(pawn).IsServiceable;
                    List<ImplantGoal> goals = store.Model.EffectiveImplants(plan);
                    PlanEvaluation evaluation = PawnProjection.Evaluate(pawn, goals, away,
                        store.Model.LatchesFor(pawn.thingIDNumber));
                    row.Goals = goals;
                    row.PlanId = plan.Id;
                    row.PlanName = plan.Name;
                    row.Progress = evaluation.Progress;
                    row.ProgressText = evaluation.SatisfiedUnits + " / " + evaluation.TotalUnits;
                    row.State = evaluation.State;
                    row.StateText = StateText(evaluation.State);
                    row.Evaluation = evaluation;

                    total++;
                    if (evaluation.State == PawnPlanState.Complete) complete++;
                    blockedGoals += CountBlockers(evaluation);
                    unitsSatisfied += evaluation.SatisfiedUnits;
                    unitsTotal += evaluation.TotalUnits;
                }
                rows.Add(row);
            }

            // Next-work mirrors the reconciler's dispatch order: implant work
            // in the configured traversal order across pawns.
            var byPriority = new List<OverviewRow>(rows);
            byPriority.Sort(ByPriorityThenBar);
            AddImplantNextWork(result.NextWork, byPriority, store);

            SortRows(rows);
            ApplyGrouping(result, rows, locationLabels);

            result.PercentText = unitsTotal == 0 ? ""
                : (unitsSatisfied * 100 / unitsTotal) + "%";
            result.StatsText = "IMP_AggregateColonists".Translate(complete, total);
            result.BlockersText = "IMP_AggregateBlockers".Translate(blockedGoals);
            return result;
        }

        private static List<Pawn> InBarOrder(List<Pawn> listed)
        {
            List<Pawn>? bar = Find.ColonistBar?.GetColonistsInOrder();
            if (bar == null) return listed;
            var pool = new HashSet<Pawn>(listed);
            var ordered = new List<Pawn>(listed.Count);
            for (int i = 0; i < bar.Count; i++)
                if (pool.Remove(bar[i]))
                    ordered.Add(bar[i]);
            for (int i = 0; i < listed.Count; i++)
                if (pool.Contains(listed[i]))
                    ordered.Add(listed[i]);
            return ordered;
        }

        private void SortRows(List<OverviewRow> rows)
        {
            switch (SortColumn)
            {
                case OverviewColumn.Name when NameAlphabetical:
                    rows.Sort(static (a, b) => CompareLabels(a, b));
                    break;
                case OverviewColumn.Name:
                    break; // bar order is the build order
                case OverviewColumn.Plan:
                    Sort(rows, static (a, b) => string.Compare(
                        a.PlanName, b.PlanName, StringComparison.OrdinalIgnoreCase));
                    break;
                case OverviewColumn.Progress:
                    Sort(rows, static (a, b) => a.Progress.CompareTo(b.Progress));
                    break;
                case OverviewColumn.Shooting:
                    Sort(rows, static (a, b) => a.Shooting.CompareTo(b.Shooting));
                    break;
                case OverviewColumn.Melee:
                    Sort(rows, static (a, b) => a.Melee.CompareTo(b.Melee));
                    break;
                case OverviewColumn.Priority:
                    Sort(rows, static (a, b) => a.Priority.CompareTo(b.Priority));
                    break;
                default:
                    Sort(rows, static (a, b) => ((int)a.State).CompareTo((int)b.State));
                    break;
            }
        }

        private void Sort(List<OverviewRow> rows, Comparison<OverviewRow> compare)
        {
            bool descending = SortDescending;
            rows.Sort((a, b) =>
            {
                int result = compare(a, b);
                if (result != 0) return descending ? -result : result;
                return a.BarIndex.CompareTo(b.BarIndex);
            });
        }

        private static int CompareLabels(OverviewRow a, OverviewRow b)
        {
            int result = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return result != 0 ? result : a.BarIndex.CompareTo(b.BarIndex);
        }

        private static readonly Comparison<OverviewRow> ByPriorityThenBar = (a, b) =>
        {
            int result = a.Priority.CompareTo(b.Priority);
            return result != 0 ? result : a.BarIndex.CompareTo(b.BarIndex);
        };

        private void BuildGroupByOptions(OverviewSnapshot result)
        {
            result.GroupByKeys.Add("none");
            result.GroupByLabels.Add("IMP_GroupByNone".Translate());
            result.GroupByKeys.Add("location");
            result.GroupByLabels.Add("IMP_GroupByLocation".Translate());
            result.GroupByKeys.Add("faction");
            result.GroupByLabels.Add("IMP_GroupByFaction".Translate());
            result.GroupByKeys.Add("gender");
            result.GroupByLabels.Add("IMP_GroupByGender".Translate());
            if (ModsConfig.BiotechActive)
            {
                result.GroupByKeys.Add("xenotype");
                result.GroupByLabels.Add("IMP_GroupByXenotype".Translate());
            }
            if (ModsConfig.IdeologyActive)
            {
                result.GroupByKeys.Add("ideo");
                result.GroupByLabels.Add("IMP_GroupByIdeo".Translate());
            }
            int index = result.GroupByKeys.IndexOf(GroupByKey);
            if (index < 0)
            {
                GroupByKey = "none";
                index = 0;
            }
            result.GroupByLabel = result.GroupByLabels[index];
        }

        /// Sections the sorted rows by the group-by selection, inserting one
        /// header row per section (A-Z by section key). "none" leaves the
        /// flat list. (Classification ported from WorkRoles' GroupSources.)
        private void ApplyGrouping(OverviewSnapshot result,
            List<OverviewRow> rows, Dictionary<string, string> locationLabels)
        {
            if (GroupByKey == "none" || rows.Count == 0)
            {
                result.Rows = rows;
                return;
            }
            var sectionKeys = new List<string>();
            var sections = new Dictionary<string, List<OverviewRow>>(StringComparer.Ordinal);
            var titles = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                (string key, string title) = Classify(rows[i], locationLabels);
                if (!sections.TryGetValue(key, out List<OverviewRow> bucket))
                {
                    bucket = new List<OverviewRow>();
                    sections.Add(key, bucket);
                    sectionKeys.Add(key);
                    titles.Add(key, title);
                }
                bucket.Add(rows[i]);
            }
            sectionKeys.Sort(StringComparer.OrdinalIgnoreCase);
            var grouped = new List<OverviewRow>();
            for (int s = 0; s < sectionKeys.Count; s++)
            {
                grouped.Add(new OverviewRow
                {
                    Header = true,
                    Name = titles[sectionKeys[s]],
                    SectionKey = sectionKeys[s],
                });
                grouped.AddRange(sections[sectionKeys[s]]);
            }
            result.Rows = grouped;
        }

        private (string, string) Classify(
            OverviewRow row, Dictionary<string, string> locationLabels)
        {
            Pawn pawn = row.Pawn;
            switch (GroupByKey)
            {
                case "location":
                {
                    string? id = row.GroupingId;
                    if (id != null && locationLabels.TryGetValue(id, out string label))
                        return ("location|" + label, label);
                    return ("location|~", "IMP_StateAway".Translate().ToString());
                }
                case "faction":
                {
                    Faction? faction = pawn.HomeFaction ?? pawn.Faction;
                    string name = faction?.Name ?? pawn.kindDef.race.LabelCap.ToString();
                    return ("faction|" + name, name);
                }
                case "gender":
                    return ("gender|" + pawn.gender,
                        pawn.gender.GetLabel().CapitalizeFirst());
                case "xenotype":
                {
                    string? name = pawn.genes?.XenotypeLabelCap.ToString();
                    if (name.NullOrEmpty()) name = "?";
                    return ("xenotype|" + name, name!);
                }
                default:
                    return ("ideo|" + (pawn.Ideo?.name ?? "?"), pawn.Ideo?.name ?? "?");
            }
        }

        private static int CountBlockers(PlanEvaluation evaluation)
        {
            int blocked = 0;
            for (int g = 0; g < evaluation.Implants.Length; g++)
                blocked += evaluation.Implants[g].Blocked;
            return blocked;
        }

        private const int MaxNextWork = 3;

        /// Implant work ordered by the configured iteration strategy and star
        /// tiers across pawns — the reconciler's own dispatch comparer, so
        /// the list never diverges from automation.
        private static void AddImplantNextWork(
            List<string> next, List<OverviewRow> byPriority, ImplannerStore store)
        {
            var work = new List<SurgeryWorkItem>();
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < byPriority.Count; i++)
            {
                OverviewRow row = byPriority[i];
                if (row.Evaluation == null || row.Goals == null) continue;
                for (int g = 0; g < row.Evaluation.Implants.Length; g++)
                {
                    if (row.Evaluation.Implants[g].Missing <= 0) continue;
                    ImplantGoal goal = row.Goals[g];
                    string key = "i" + goal.Id;
                    work.Add(new SurgeryWorkItem(row.PawnId, row.Priority,
                        StarRanking.TierOf(
                            store.Model.ImplantStarsOf(goal.ImplantDefName)),
                        key));
                    labels[row.PawnId + "|" + key] = row.Name + ": " + ImplantLabel(goal);
                }
            }
            SurgeryPlanner.Order(work, store.Model.Iteration);
            for (int i = 0; i < work.Count && next.Count < MaxNextWork; i++)
                next.Add(labels[work[i].PawnId + "|" + work[i].GoalKey]);
        }

        private List<DetailRow> BuildDetails(OverviewSnapshot current)
        {
            var rows = new List<DetailRow>();
            DetailTitle = "";
            DetailPercent = "";
            DetailHasRegression = false;
            OverviewRow? selected = null;
            for (int i = 0; i < current.Rows.Count; i++)
                if (!current.Rows[i].Header && current.Rows[i].PawnId == SelectedPawnId)
                {
                    selected = current.Rows[i];
                    break;
                }
            if (selected?.Evaluation == null || selected.Goals == null) return rows;
            ImplannerStore? store = ImplannerStore.Current;
            Plan? plan = store?.Model.PlanById(selected.PlanId);
            if (plan == null) return rows;
            List<ImplantGoal> goals = selected.Goals;

            PlanEvaluation evaluation = selected.Evaluation;
            DetailTitle = selected.Name;
            DetailPercent = (int)(evaluation.Progress * 100f) + "%";
            DetailHasRegression = selected.State == PawnPlanState.Regressed
                || HasAnyRegression(evaluation);

            // Reservations for this colonist, with readiness (item present on
            // the pawn's map and unforbidden → ready for surgery).
            var reservedByGoal = new Dictionary<string, bool>(StringComparer.Ordinal);
            BuildReservationStates(selected.Pawn, store!, reservedByGoal);

            // Implants, with their surgery-pipeline position (awaiting
            // batch, scheduled, recovering, blocked by doctor floor) derived
            // by the reconciler's own logic.
            Dictionary<string, PlannerSurgery.SlotStatus>? surgery = null;
            int effectiveFloor = 0;
            if (goals.Count > 0)
                surgery = PlannerSurgery.PresentationFor(
                    store!.Model, selected.Pawn, plan, reservedByGoal,
                    out effectiveFloor);
            int implantSatisfied = 0, implantTotal = 0, implantHeader = -1;
            for (int g = 0; g < evaluation.Implants.Length; g++)
            {
                GoalResult goal = evaluation.Implants[g];
                if (implantHeader < 0)
                {
                    implantHeader = rows.Count;
                    rows.Add(new DetailRow
                    {
                        Header = true,
                        Label = "IMP_GroupImplants".Translate(),
                    });
                }
                implantSatisfied += goal.Satisfied;
                implantTotal += goal.Requested;
                ImplantGoal implantGoal = goals[g];
                var pipeline = PlannerSurgery.SlotStatus.None;
                string? reservedKey = null;
                for (int j = 0; j < implantGoal.SlotOrdinals.Count; j++)
                {
                    string key = GoalKeys.ImplantSlot(implantGoal.Id, implantGoal.SlotOrdinals[j]);
                    if (reservedKey == null && reservedByGoal.ContainsKey(key))
                        reservedKey = key;
                    if (surgery != null
                        && surgery.TryGetValue(key, out PlannerSurgery.SlotStatus slot)
                        && slot > pipeline)
                        pipeline = slot;
                }
                string statusText =
                    goal.IsComplete || (goal.Regressed > 0 && goal.Missing == 0)
                    || pipeline == PlannerSurgery.SlotStatus.None
                    ? GoalStatusText(goal, reservedByGoal, reservedKey)
                    : SurgeryStatusText(pipeline, effectiveFloor);
                rows.Add(new DetailRow
                {
                    Label = ImplantLabel(implantGoal),
                    StatusText = statusText,
                    Satisfied = goal.IsComplete,
                    Blocked = goal.Blocked > 0
                        || pipeline == PlannerSurgery.SlotStatus.BlockedByFloor,
                    Muted = goal.Regressed > 0,
                });
            }
            if (implantHeader >= 0)
                rows[implantHeader].StatusText = implantSatisfied + " / " + implantTotal;

            return rows;
        }

        private static bool HasAnyRegression(PlanEvaluation evaluation)
        {
            for (int i = 0; i < evaluation.Implants.Length; i++)
                if (evaluation.Implants[i].Regressed > 0)
                    return true;
            return false;
        }

        /// goalKey → the reserved item is ready (spawned somewhere on the
        /// pawn's colony map stack, unforbidden) — the same collectability
        /// gate ScheduleOperations enforces, so the panel never reports a
        /// stranded reservation as schedulable. One pass over the stack's
        /// haulables resolves every reservation; the panel never scans per
        /// reservation.
        private static void BuildReservationStates(
            Pawn pawn, ImplannerStore store, Dictionary<string, bool> reservedByGoal)
        {
            Dictionary<int, string>? reservedItems = null;
            foreach (KeyValuePair<int, ItemReservation> pair in store.Model.Reservations)
            {
                if (pair.Value.PawnId != pawn.thingIDNumber) continue;
                (reservedItems ??= new Dictionary<int, string>())[pair.Key] =
                    pair.Value.GoalKey;
                reservedByGoal[pair.Value.GoalKey] = false;
            }
            if (reservedItems == null) return;
            Map? canonical = FloorMaps.Canonical(pawn.MapHeld);
            if (canonical == null) return;
            List<Map> maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                if (FloorMaps.Canonical(maps[m]) != canonical) continue;
                List<Thing> haulables = maps[m].listerThings.ThingsInGroup(
                    ThingRequestGroup.HaulableEver);
                for (int i = 0; i < haulables.Count; i++)
                    if (reservedItems.TryGetValue(
                            haulables[i].thingIDNumber, out string goalKey))
                        reservedByGoal[goalKey] =
                            !haulables[i].IsForbidden(RimWorld.Faction.OfPlayer);
            }
        }

        internal static ThingDef? WornBeltDef(Pawn pawn)
        {
            List<Apparel>? worn = pawn.apparel?.WornApparel;
            if (worn == null) return null;
            for (int i = 0; i < worn.Count; i++)
                if (worn[i].def.apparel?.LastLayer == ApparelLayerDefOf.Belt)
                    return worn[i].def;
            return null;
        }

        private static string ImplantLabel(ImplantGoal goal)
        {
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(goal.ImplantDefName);
            string label = entry?.Label ?? goal.ImplantDefName;
            return goal.Count > 1 ? label + " x" + goal.Count : label;
        }

        private static string GoalStatusText(GoalResult goal,
            Dictionary<string, bool> reservedByGoal, string? goalKey)
        {
            if (goal.IsComplete) return "IMP_GoalComplete".Translate();
            if (goal.Regressed > 0 && goal.Missing == 0)
                return "IMP_GoalRegressed".Translate();
            if (goalKey != null && reservedByGoal.TryGetValue(goalKey, out bool ready))
                return (ready ? "IMP_GoalReady" : "IMP_GoalReserved").Translate();
            switch (goal.Blocker)
            {
                case GoalBlocker.Anatomy:
                    return "IMP_GoalBlockedAnatomy".Translate();
                default:
                    return goal.Satisfied > 0
                        ? "IMP_GoalPartial".Translate(goal.Satisfied, goal.Requested)
                        : "IMP_GoalMissing".Translate();
            }
        }

        private static string SurgeryStatusText(
            PlannerSurgery.SlotStatus status, int effectiveFloor)
        {
            switch (status)
            {
                case PlannerSurgery.SlotStatus.BlockedByFloor:
                    return "IMP_GoalBlockedFloor".Translate(effectiveFloor);
                case PlannerSurgery.SlotStatus.Scheduled:
                    return "IMP_GoalScheduled".Translate();
                case PlannerSurgery.SlotStatus.Recovering:
                    return "IMP_GoalRecovering".Translate();
                default:
                    return "IMP_GoalAwaitingBatch".Translate();
            }
        }

        private static string StateText(PawnPlanState state)
        {
            switch (state)
            {
                case PawnPlanState.Complete: return "IMP_StateComplete".Translate();
                case PawnPlanState.Blocked: return "IMP_StateBlocked".Translate();
                case PawnPlanState.Away: return "IMP_StateAway".Translate();
                case PawnPlanState.Regressed: return "IMP_StateRegressed".Translate();
                default: return "IMP_StateActive".Translate();
            }
        }

        private static string LabelOf(GroupingOption option)
        {
            if (option.Kind == GroupingKind.All) return "IMP_ScopeAll".Translate();
            if (option.Kind != GroupingKind.CurrentLocation) return option.Label ?? "";
            return "IMP_ScopeCurrent".Translate();
        }
    }
}
