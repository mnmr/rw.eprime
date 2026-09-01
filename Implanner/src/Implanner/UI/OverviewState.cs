using System;
using System.Collections.Generic;
using Implanner.Core;
using RimShared.UiLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
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

    /// Colonist pipeline status for the State column, in sort order: active
    /// pipeline stages first, then the terminal and waiting-on-world states.
    internal enum ColonistStatus
    {
        /// Implants are missing but no item is reserved for this colonist yet.
        Waiting = 0,
        /// Collecting the batch: at least one item is reserved for them.
        Preparing = 1,
        /// Surgery scheduled: an Implanner operation bill is on their list.
        Operating = 2,
        /// Every planned implant is installed.
        Done = 3,
        /// Off traveling; automation waits.
        Away = 4,
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
        internal ColonistStatus State;
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
        /// Automation is actively moving this implant: reserved, ready, or
        /// scheduled (drawn in the blue active tone).
        internal bool Active;
        /// Header rows only: the label is a star-tier glyph run, drawn gold.
        internal bool Stars;
    }

    internal sealed class OverviewSnapshot
    {
        internal List<GroupingOption> GroupingOptions = new List<GroupingOption>();
        internal List<string> GroupingLabels = new List<string>();
        internal List<string> GroupByKeys = new List<string>();
        internal List<string> GroupByLabels = new List<string>();
        internal string GroupByLabel = "";
        internal List<OverviewRow> Rows = new List<OverviewRow>();
        /// Colony summary strip texts, three lines per column: the colony
        /// (name, colonist count, automation chip), production (item-stock
        /// coverage percent, free-stock/queued counts, one activity
        /// sentence), and surgery (installed percent, installed units, the
        /// active batch sentence). Empty sentence = nothing to report.
        internal string ColonyLabelText = "";
        internal string StatsText = "";
        internal bool AutomationOn;
        internal string AutomationText = "";
        internal string ProductionTitleText = "";
        internal string StockQueuedText = "";
        internal string ProductionSubText = "";
        /// Whether the production sentence reports active work (a crafting
        /// bill in progress) rather than a blocked ingredient.
        internal bool ProductionSubActive;
        internal string SurgeryTitleText = "";
        internal string UnitsText = "";
        /// The active batch sentence with the tier stars at the end, and a
        /// starless variant the strip falls back to when the full line
        /// would not fit its column (the line never wraps or clips).
        internal string SurgeryBatchText = "";
        internal string SurgeryBatchShortText = "";
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
        //   dependency moves or it reopens, and displays that snapshot. The
        //   strip's production column samples item stock, bench bills, and
        //   ingredient resource counts at rebuild the same way (bill
        //   creation/removal bumps store.Version, so automation activity
        //   still refreshes it promptly).
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
        private float detailWidth = -1f;

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

        /// Header line under the name: one sentence combining the
        /// colonist's pipeline status with the implants involved
        /// ("Surgery scheduled for bionic leg", "Collecting bionic arm x2"),
        /// or the bare status word when automation holds nothing for them.
        /// The height is the sentence's measured wrapped height at the
        /// panel's body width (measured on rebuild only, never clipped).
        internal string DetailStatusText { get; private set; } = "";
        internal float DetailStatusHeight { get; private set; } = 22f;

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

        /// width is the panel body width the header sentence wraps at; a
        /// width change re-measures (it is part of the cache key).
        internal IReadOnlyList<DetailRow> Details(
            OverviewSnapshot current, float width)
        {
            if (detailRows == null
                || !ReferenceEquals(detailSnapshot, current)
                || detailPawnId != SelectedPawnId
                || detailWidth != width)
            {
                detailRows = BuildDetails(current, width);
                detailSnapshot = current;
                detailPawnId = SelectedPawnId;
                detailWidth = width;
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
            Grouping = LocationGrouping.Revalidate(Grouping,
                result.GroupingOptions, ColonyScope.CurrentLocationId());
            for (int i = 0; i < result.GroupingOptions.Count; i++)
                result.GroupingLabels.Add(LabelOf(result.GroupingOptions[i]));
            var locationLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < infos.Count; i++)
                locationLabels[infos[i].Id] = infos[i].Label;

            BuildGroupByOptions(result);

            GroupingOption grouping = Grouping!;

            // Pawns in colonist-bar order (the Name column's default order).
            List<Pawn> pawns = InBarOrder(ColonyScope.AllPlanableColonists());

            var rows = new List<OverviewRow>();
            int complete = 0, total = 0;
            int unitsSatisfied = 0, unitsTotal = 0;

            // Pawns automation currently holds a reservation for, resolved
            // once for the whole pass (drives Waiting vs Preparing).
            var reservedPawns = new HashSet<int>();
            foreach (KeyValuePair<int, ItemReservation> pair
                in store.Model.Reservations)
                reservedPawns.Add(pair.Value.PawnId);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                string? pawnGroupingId = ColonyScope.GroupingIdOf(pawn);
                if (!LocationGrouping.Matches(grouping, pawnGroupingId))
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
                    PlanEvaluation evaluation = PawnProjection.Evaluate(pawn, goals, away);
                    row.Goals = goals;
                    row.PlanId = plan.Id;
                    row.PlanName = plan.Name;
                    row.Progress = evaluation.Progress;
                    row.ProgressText = evaluation.SatisfiedUnits + " / " + evaluation.TotalUnits;
                    row.State = StatusOf(evaluation.State,
                        pawn.thingIDNumber, store, reservedPawns);
                    row.StateText = StateText(row.State);
                    row.Evaluation = evaluation;

                    total++;
                    if (evaluation.State == PawnPlanState.Complete) complete++;
                    unitsSatisfied += evaluation.SatisfiedUnits;
                    unitsTotal += evaluation.TotalUnits;
                }
                rows.Add(row);
            }

            BuildSurgeryBatch(result, rows, store);
            BuildProductionSummary(result, rows, store, total);

            SortRows(rows);
            ApplyGrouping(result, rows, locationLabels);

            result.ColonyLabelText =
                "IMP_StripColony".Translate(LabelOf(Grouping!)).ToString();
            result.StatsText = "IMP_AggregateColonists".Translate(complete, total);
            // The chip reflects the effective master state: the player's
            // switch, and the level-mod stand-down that overrides it.
            result.AutomationOn = !store.Model.AutomationPaused
                && PlannerAutomation.Available;
            result.AutomationText = (result.AutomationOn
                ? "IMP_StripAutomationOn"
                : "IMP_StripAutomationOff").Translate().ToString();
            int percent = unitsTotal == 0 ? 0
                : unitsSatisfied * 100 / unitsTotal;
            result.SurgeryTitleText =
                "IMP_StripSurgery".Translate(percent).ToString();
            result.UnitsText = "IMP_StripInstalled".Translate(
                unitsSatisfied, unitsTotal).ToString();
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

        /// The strip's surgery activity line. Automation installs one batch
        /// into one colonist at a time: order all pending implant work with
        /// the reconciler's own dispatch comparer (iteration strategy and
        /// star tiers), take the first item's pawn as that colonist, and
        /// name them with the dispatch tier's star run at the end
        /// ("Implanting batch on Twiggy (★★★)"). Empty when no batch is
        /// pending or surgery automation is off (the master switch, or a
        /// level-mod stand-down — surgery has no separate toggle).
        private static void BuildSurgeryBatch(
            OverviewSnapshot result, List<OverviewRow> rows, ImplannerStore store)
        {
            if (store.Model.AutomationPaused || !PlannerAutomation.Available)
                return;
            var work = new List<SurgeryWorkItem>();
            var rowByPawn = new Dictionary<int, OverviewRow>();
            for (int i = 0; i < rows.Count; i++)
            {
                OverviewRow row = rows[i];
                if (row.Evaluation == null || row.Goals == null) continue;
                for (int g = 0; g < row.Evaluation.Implants.Length; g++)
                {
                    if (row.Evaluation.Implants[g].Missing <= 0) continue;
                    ImplantGoal goal = row.Goals[g];
                    work.Add(new SurgeryWorkItem(row.PawnId, row.Priority,
                        StarRanking.TierOf(
                            store.Model.ImplantStarsOf(goal.ImplantDefName)),
                        GoalKeys.GoalToken(goal)));
                    rowByPawn[row.PawnId] = row;
                }
            }
            if (work.Count == 0) return;
            SurgeryPlanner.Order(work, store.Model.Iteration);
            OverviewRow next = rowByPawn[work[0].PawnId];
            List<ImplantGoal> goals = next.Goals!;

            List<string> missing =
                PawnProjection.MissingImplantSlotKeys(next.Pawn, goals);
            List<string> batch = SurgeryPlanner.ComputeBatch(
                missing, store.Model, goals, store.Model.Iteration);
            if (batch.Count == 0) return;
            result.SurgeryBatchText = "IMP_StripBatchStars".Translate(
                next.Name, PlannerStyle.TierStars[work[0].Tier]).ToString();
            result.SurgeryBatchShortText =
                "IMP_StripBatch".Translate(next.Name).ToString();
        }

        /// The strip's production column: how much of the item shortfall
        /// behind the scope's missing implant slots is already covered by
        /// unforbidden stock (minus the player's hold-back reserves), plus
        /// one sentence about production — the best-ranked implant-item bill
        /// automation owns ("Making bionic arm"), intermediary-only chain
        /// work attributed to its implant ("Making materials for bionic
        /// arm"), or what the top
        /// uncovered item truly waits on — the shortfall walked down the
        /// production chain to the first ingredient automation cannot craft
        /// for itself ("Waiting for plasteel"). A short craftable
        /// intermediary is never reported while intermediary production is
        /// active: automation will queue it. Waits name only ingredients or
        /// bench capacity, never a final product; "Nothing to craft" covers
        /// full coverage and uncraftable remainders, and the line stays
        /// blank entirely while production automation is off.
        /// Stock, bench bills, and resource counts are sampled at rebuild
        /// like the Next section's readiness; ranking and blocking mirror
        /// PlannerProduction's own dispatch rules.
        private void BuildProductionSummary(OverviewSnapshot result,
            List<OverviewRow> rows, ImplannerStore store, int planAssigned)
        {
            PlannerModel model = store.Model;

            // Item shortfall per kind: every missing implant slot in scope
            // wants one item of the implant's removal def.
            var needed = new Dictionary<ThingDef, int>();
            int neededTotal = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                OverviewRow row = rows[i];
                if (row.Evaluation == null || row.Goals == null) continue;
                for (int g = 0; g < row.Evaluation.Implants.Length; g++)
                {
                    int missing = row.Evaluation.Implants[g].Missing;
                    if (missing <= 0) continue;
                    ThingDef? item = Catalogs.ImplantByDefName(
                        row.Goals[g].ImplantDefName)?.Def.spawnThingOnRemoved;
                    if (item == null) continue;
                    needed.TryGetValue(item, out int count);
                    needed[item] = count + missing;
                    neededTotal += missing;
                }
            }

            // Maps in scope: serviceable locations matching the grouping.
            var maps = new List<Map>();
            List<Map> allMaps = Find.Maps;
            for (int m = 0; m < allMaps.Count; m++)
            {
                string? locationId = ColonyScope.LocationId(allMaps[m]);
                if (locationId != null
                    && LocationGrouping.Matches(Grouping!, locationId))
                    maps.Add(allMaps[m]);
            }

            // Unforbidden stock of wanted items, less the player's hold-back
            // reserves (production keeps crafting past held-back items, so
            // they do not count as produced here either). Items automation
            // has reserved for a colonist's surgery count toward coverage —
            // they exist and need no production — but not toward the free
            // in-stock number.
            var stock = new Dictionary<ThingDef, int>();
            var reservedStock = new Dictionary<ThingDef, int>();
            for (int m = 0; m < maps.Count; m++)
            {
                List<Thing> haulables = maps[m].listerThings.ThingsInGroup(
                    ThingRequestGroup.HaulableEver);
                for (int i = 0; i < haulables.Count; i++)
                {
                    Thing thing = haulables[i];
                    if (!needed.ContainsKey(thing.def)
                        || thing.IsForbidden(RimWorld.Faction.OfPlayer))
                        continue;
                    stock.TryGetValue(thing.def, out int count);
                    stock[thing.def] = count + thing.stackCount;
                    if (model.Reservations.ContainsKey(thing.thingIDNumber))
                    {
                        reservedStock.TryGetValue(thing.def, out int reserved);
                        reservedStock[thing.def] = reserved + thing.stackCount;
                    }
                }
            }
            Dictionary<ThingDef, int> holdBack =
                PlannerSurgery.ImplantItemReserves(model);
            int covered = 0, freeStock = 0;
            foreach (KeyValuePair<ThingDef, int> pair in needed)
            {
                stock.TryGetValue(pair.Key, out int have);
                if (holdBack.TryGetValue(pair.Key, out int held))
                    have = System.Math.Max(0, have - held);
                stock[pair.Key] = have;
                int usable = System.Math.Min(pair.Value, have);
                covered += usable;
                reservedStock.TryGetValue(pair.Key, out int reserved);
                freeStock += System.Math.Max(0, usable - reserved);
            }

            int percent = neededTotal == 0
                ? (planAssigned > 0 ? 100 : 0)
                : covered * 100 / neededTotal;
            result.ProductionTitleText =
                "IMP_StripProduction".Translate(percent).ToString();
            result.StockQueuedText = "IMP_StripStock".Translate(
                freeStock, neededTotal - covered).ToString();

            // The activity line reports only what production automation is
            // actually doing; while it is off (master switch, production
            // option, or a level-mod stand-down) the line stays blank.
            bool producing = model.AutoProduction && !model.AutomationPaused
                && PlannerAutomation.Available;
            if (!producing) return;

            // Dispatch rank per item (best owning implant's star tier, then
            // the player-arranged tier position), mirroring PlannerProduction.
            var rankByItem = new Dictionary<ThingDef, (int Tier, int Order)>();
            IReadOnlyList<ImplantCatalogEntry> catalog = Catalogs.Implants();
            for (int i = 0; i < catalog.Count; i++)
            {
                ThingDef? produced = catalog[i].Def.spawnThingOnRemoved;
                if (produced == null) continue;
                string hediff = catalog[i].Def.defName;
                var rank = (StarRanking.TierOf(model.ImplantStarsOf(hediff)),
                    model.ImplantOrderOf(hediff));
                if (!rankByItem.TryGetValue(produced, out var existing)
                    || rank.CompareTo(existing) < 0)
                    rankByItem[produced] = rank;
            }

            // Uncovered implant items in dispatch order — the subjects the
            // activity line may talk about. Intermediaries never headline:
            // "Making" names implant items only, and chain work is
            // attributed to the implant item it serves.
            var uncovered = new List<ThingDef>();
            foreach (KeyValuePair<ThingDef, int> pair in needed)
            {
                stock.TryGetValue(pair.Key, out int have);
                if (have < pair.Value) uncovered.Add(pair.Key);
            }
            uncovered.Sort((a, b) =>
            {
                var maxRank = (int.MaxValue, int.MaxValue);
                if (!rankByItem.TryGetValue(a, out var rankA)) rankA = maxRank;
                if (!rankByItem.TryGetValue(b, out var rankB)) rankB = maxRank;
                int rank = rankA.CompareTo(rankB);
                if (rank != 0) return rank;
                return string.CompareOrdinal(a.defName, b.defName);
            });

            // Active work: the best-ranked owned bill producing an implant
            // item. Owned bills for anything else are intermediary chain
            // work (components, smelted steel) queued in service of an
            // implant bill that could not be paid yet.
            ThingDef? making = null;
            var makingRank = (int.MaxValue, int.MaxValue);
            bool chainWork = false;
            for (int m = 0; m < maps.Count; m++)
            {
                List<Building> buildings =
                    maps[m].listerBuildings.allBuildingsColonist;
                for (int b = 0; b < buildings.Count; b++)
                {
                    if (!(buildings[b] is Building_WorkTable bench)) continue;
                    BillStack bills = bench.BillStack;
                    for (int i = 0; i < bills.Count; i++)
                    {
                        if (!(bills[i] is Bill_Production bill)
                            || !model.OwnedProductionBills.ContainsKey(
                                bill.GetUniqueLoadID()))
                            continue;
                        ThingDef? produced = bill.recipe.ProducedThingDef;
                        if (produced == null) continue;
                        if (!needed.ContainsKey(produced))
                        {
                            chainWork = true;
                            continue;
                        }
                        if (!rankByItem.TryGetValue(produced, out var rank))
                            rank = (int.MaxValue, int.MaxValue);
                        if (making != null
                            && (rank.CompareTo(makingRank) > 0
                                || (rank.CompareTo(makingRank) == 0
                                    && string.CompareOrdinal(produced.defName,
                                        making.defName) >= 0)))
                            continue;
                        making = produced;
                        makingRank = rank;
                    }
                }
            }
            if (making != null)
            {
                result.ProductionSubText =
                    "IMP_StripMaking".Translate(making.label).CapitalizeFirst();
                result.ProductionSubActive = true;
                return;
            }
            if (chainWork)
            {
                // Only intermediary bills are running: attribute them to the
                // implant item they bootstrap ("Making materials for bionic
                // arm"). No craftable target left means the demand behind
                // them vanished; the next pass deletes the bills.
                ThingDef? target = FirstCraftableUncovered(uncovered, needed,
                    stock);
                if (target != null)
                {
                    result.ProductionSubText = "IMP_StripMakingFor"
                        .Translate(target.label).CapitalizeFirst();
                    result.ProductionSubActive = true;
                }
                return;
            }

            if (covered >= neededTotal)
            {
                result.ProductionSubText =
                    "IMP_StripNothingToCraft".Translate().ToString();
                return;
            }

            // Nothing crafting yet: derive the wait from the best-ranked
            // uncovered item production can actually craft. Production only
            // ever waits on ingredients or bench capacity, never on the
            // final product — a blocked fixed ingredient names the wait,
            // otherwise the bill simply has no bench yet. When no uncovered
            // item is craftable at all, production has nothing to do.
            for (int i = 0; i < uncovered.Count; i++)
            {
                ThingDef item = uncovered[i];
                RecipeDef? recipe = PlannerProduction.ProductionRecipeFor(item);
                if (recipe == null || !recipe.AvailableNow) continue;
                stock.TryGetValue(item, out int have);
                int crafts = ProductionMath.CraftsNeeded(needed[item], have, 0,
                    PlannerProduction.OutputCount(recipe, item));
                if (crafts <= 0) continue;
                var visited = new HashSet<ThingDef> { item };
                ThingDef? blocker = FindBlockingIngredient(model, maps,
                    recipe, crafts, model.AllowIntermediaries, visited, 0);
                result.ProductionSubText = blocker != null
                    ? "IMP_StripWaiting".Translate(blocker.label)
                        .CapitalizeFirst()
                    : "IMP_StripWaitingBench".Translate().ToString();
                return;
            }
            result.ProductionSubText =
                "IMP_StripNothingToCraft".Translate().ToString();
        }

        /// The best-ranked uncovered implant item with an available recipe
        /// and crafts outstanding; null when nothing craftable remains.
        private static ThingDef? FirstCraftableUncovered(
            List<ThingDef> uncovered, Dictionary<ThingDef, int> needed,
            Dictionary<ThingDef, int> stock)
        {
            for (int i = 0; i < uncovered.Count; i++)
            {
                ThingDef item = uncovered[i];
                RecipeDef? recipe = PlannerProduction.ProductionRecipeFor(item);
                if (recipe == null || !recipe.AvailableNow) continue;
                stock.TryGetValue(item, out int have);
                if (ProductionMath.CraftsNeeded(needed[item], have, 0,
                        PlannerProduction.OutputCount(recipe, item)) > 0)
                    return item;
            }
            return null;
        }

        /// The first fixed ingredient down the recipe's production chain
        /// that blocks crafting and that automation cannot resolve itself:
        /// short of its reserve after the bill's cost, and either outside
        /// intermediary production or not a craftable manufactured item
        /// (the dispatcher's whitelist — raw resources are always a real
        /// wait). With intermediary production active, a short craftable
        /// manufactured ingredient recurses into its own recipe instead
        /// (once per resource, depth-capped like the dispatcher's own
        /// expansion); null when automation will handle every shortfall on
        /// its own.
        private static ThingDef? FindBlockingIngredient(PlannerModel model,
            List<Map> maps, RecipeDef recipe, int crafts,
            bool intermediaries, HashSet<ThingDef> visited, int depth)
        {
            const int MaxDepth = 8;
            List<IngredientCount>? ingredients = recipe.ingredients;
            if (ingredients == null) return null;
            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredient = ingredients[i];
                if (!ingredient.IsFixedIngredient) continue;
                ThingDef def = ingredient.FixedIngredient;
                int cost = (int)System.Math.Ceiling(
                    ingredient.GetBaseCount() * crafts);
                int available = 0;
                for (int m = 0; m < maps.Count; m++)
                    available += maps[m].resourceCounter.GetCount(def);
                int reserve = model.ResourceReserveOf(def.defName);
                if (available - cost >= reserve) continue;
                RecipeDef? subRecipe =
                    intermediaries && depth < MaxDepth
                    && PlannerProduction.IsManufactured(def)
                    && visited.Add(def)
                        ? PlannerProduction.ProductionRecipeFor(def)
                        : null;
                if (subRecipe == null) return def;
                int output = PlannerProduction.OutputCount(subRecipe, def);
                if (output <= 0) return def;
                int shortfall = cost + reserve - available;
                ThingDef? blocker = FindBlockingIngredient(model, maps,
                    subRecipe, (shortfall + output - 1) / output,
                    intermediaries, visited, depth + 1);
                if (blocker != null) return blocker;
            }
            return null;
        }

        /// The header sentence: what automation is doing for the colonist
        /// right now (scheduled implants while Operating, reserved implants
        /// while Preparing), or the bare status word.
        private static string DetailStatus(OverviewRow selected,
            List<ImplantGoal> goals, ImplannerStore store,
            Dictionary<string, bool> reservedByGoal)
        {
            switch (selected.State)
            {
                case ColonistStatus.Operating:
                {
                    Dictionary<string, string>? bills =
                        store.Model.OwnedBillsFor(selected.PawnId);
                    if (bills != null && bills.Count > 0)
                        return "IMP_NextScheduled".Translate(
                            KindList(goals, bills.Keys)).CapitalizeFirst();
                    break;
                }
                case ColonistStatus.Preparing:
                    if (reservedByGoal.Count > 0)
                        return "IMP_DetailCollecting".Translate(
                            KindList(goals, reservedByGoal.Keys)).CapitalizeFirst();
                    break;
            }
            return StateText(selected.State);
        }

        /// Deterministic lowercase "label, label x2" list over goal-slot keys.
        private static string KindList(
            List<ImplantGoal> goals, IEnumerable<string> keys)
        {
            var sorted = new List<string>(keys);
            sorted.Sort(StringComparer.Ordinal);
            var order = new List<string>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!GoalKeys.TryResolveImplantSlot(goals, sorted[i],
                        out ImplantGoal goal, out _))
                    continue;
                if (counts.TryGetValue(goal.ImplantDefName, out int n))
                    counts[goal.ImplantDefName] = n + 1;
                else
                {
                    counts[goal.ImplantDefName] = 1;
                    order.Add(goal.ImplantDefName);
                }
            }
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < order.Count; i++)
            {
                if (i > 0) text.Append(", ");
                ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(order[i]);
                text.Append(entry?.Def.label ?? order[i]);
                if (counts[order[i]] > 1)
                    text.Append(" x").Append(counts[order[i]]);
            }
            return text.ToString();
        }

        private List<DetailRow> BuildDetails(OverviewSnapshot current, float width)
        {
            var rows = new List<DetailRow>();
            DetailTitle = "";
            DetailPercent = "";
            DetailStatusText = "";
            DetailStatusHeight = 22f;
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

            // Reservations for this colonist, with readiness (item present on
            // the pawn's map and unforbidden → ready for surgery).
            var reservedByGoal = new Dictionary<string, bool>(StringComparer.Ordinal);
            BuildReservationStates(selected.Pawn, store!, reservedByGoal);

            DetailStatusText = DetailStatus(
                selected, goals, store!, reservedByGoal);
            // The sentence never clips: the header band grows to its
            // wrapped height (measured here, on rebuild only).
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                DetailStatusHeight = Mathf.Max(22f,
                    Mathf.Ceil(Text.CalcHeight(DetailStatusText, width)));
            }

            // Implants, with their surgery-pipeline position (awaiting
            // batch, scheduled, recovering, blocked by doctor floor) derived
            // by the reconciler's own logic.
            Dictionary<string, PlannerSurgery.SlotStatus>? surgery = null;
            int effectiveFloor = 0;
            if (goals.Count > 0)
                surgery = PlannerSurgery.PresentationFor(
                    store!.Model, selected.Pawn, plan, reservedByGoal,
                    out effectiveFloor);
            // Per-goal rows with their grouping keys resolved once. The
            // panel reads in the order automation delivers: tier iteration
            // groups by star tier with the player-arranged position within,
            // colonist iteration groups by anatomy region with labels A-Z.
            PlannerModel model = store!.Model;
            bool byTier = model.Iteration == IterationStrategy.ImplantTier;
            var entries = new List<DetailEntry>(evaluation.Implants.Length);
            for (int g = 0; g < evaluation.Implants.Length; g++)
            {
                GoalResult goal = evaluation.Implants[g];
                // A wholly impossible pick (no applicable anatomy on this
                // body) is outside the colonist's target: not listed.
                if (goal.Requested == 0) continue;
                ImplantGoal implantGoal = goals[g];
                var pipeline = PlannerSurgery.SlotStatus.None;
                string? reservedKey = null;
                for (int j = 0; j < implantGoal.SlotOrdinals.Count; j++)
                {
                    string key = GoalKeys.ImplantSlot(implantGoal, implantGoal.SlotOrdinals[j]);
                    if (reservedKey == null && reservedByGoal.ContainsKey(key))
                        reservedKey = key;
                    if (surgery != null
                        && surgery.TryGetValue(key, out PlannerSurgery.SlotStatus slot)
                        && slot > pipeline)
                        pipeline = slot;
                }
                string statusText =
                    goal.IsComplete || pipeline == PlannerSurgery.SlotStatus.None
                    ? GoalStatusText(goal, reservedByGoal, reservedKey)
                    : SurgeryStatusText(pipeline, effectiveFloor);
                ImplantCatalogEntry? entry =
                    Catalogs.ImplantByDefName(implantGoal.ImplantDefName);
                entries.Add(new DetailEntry
                {
                    Row = new DetailRow
                    {
                        Label = ImplantLabel(implantGoal),
                        StatusText = statusText,
                        Satisfied = goal.IsComplete,
                        Blocked = pipeline
                            == PlannerSurgery.SlotStatus.BlockedByFloor,
                        Active = !goal.IsComplete
                            && (reservedKey != null
                                || (pipeline != PlannerSurgery.SlotStatus.None
                                    && pipeline != PlannerSurgery.SlotStatus
                                        .BlockedByFloor)),
                    },
                    Satisfied = goal.Satisfied,
                    Requested = goal.Requested,
                    Group = byTier
                        ? StarRanking.TierOf(
                            model.ImplantStarsOf(implantGoal.ImplantDefName))
                        : (int)(entry?.Region ?? ImplantRegion.Torso),
                    SortLabel = entry?.Label ?? implantGoal.ImplantDefName,
                    DefName = implantGoal.ImplantDefName,
                    Order = model.ImplantOrderOf(implantGoal.ImplantDefName),
                });
            }
            entries.Sort(byTier ? ByTierThenPosition : ByRegionThenLabel);

            string[] groupLabels =
                byTier ? PlannerStyle.TierStars : RegionGroupLabels();
            int group = -1, headerIndex = -1, sat = 0, total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                DetailEntry entry = entries[i];
                if (entry.Group != group)
                {
                    if (headerIndex >= 0)
                        rows[headerIndex].StatusText = sat + " / " + total;
                    group = entry.Group;
                    sat = 0;
                    total = 0;
                    headerIndex = rows.Count;
                    rows.Add(new DetailRow
                    {
                        Header = true,
                        Label = groupLabels[group],
                        Stars = byTier,
                    });
                }
                sat += entry.Satisfied;
                total += entry.Requested;
                rows.Add(entry.Row);
            }
            if (headerIndex >= 0)
                rows[headerIndex].StatusText = sat + " / " + total;

            return rows;
        }

        /// One computed detail row plus the keys the strategy-dependent
        /// grouping and ordering need.
        private struct DetailEntry
        {
            public DetailRow Row;
            public int Satisfied;
            public int Requested;
            public int Group;        // tier index or ImplantRegion ordinal
            public string SortLabel;
            public string DefName;
            public int Order;        // player-arranged tier position
        }

        private static readonly Comparison<DetailEntry> ByRegionThenLabel = (a, b) =>
        {
            int c = a.Group.CompareTo(b.Group);
            if (c != 0) return c;
            c = string.Compare(a.SortLabel, b.SortLabel,
                StringComparison.OrdinalIgnoreCase);
            return c != 0 ? c : string.CompareOrdinal(a.DefName, b.DefName);
        };

        private static readonly Comparison<DetailEntry> ByTierThenPosition = (a, b) =>
        {
            int c = a.Group.CompareTo(b.Group);
            if (c != 0) return c;
            c = a.Order.CompareTo(b.Order);
            return c != 0 ? c : string.CompareOrdinal(a.DefName, b.DefName);
        };

        private static string[] RegionGroupLabels() => new[]
        {
            "IMP_RegionLimbs".Translate().ToString(),
            "IMP_RegionTorso".Translate().ToString(),
            "IMP_RegionHead".Translate().ToString(),
        };


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
            if (goalKey != null && reservedByGoal.TryGetValue(goalKey, out bool ready))
                return (ready ? "IMP_GoalReady" : "IMP_GoalReserved").Translate();
            return goal.Satisfied > 0
                ? "IMP_GoalPartial".Translate(goal.Satisfied, goal.Requested)
                : "IMP_GoalMissing".Translate();
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
                    // AwaitingBatch: the item is in stock and allocated to
                    // this colonist; surgery waits for the rest of the
                    // batch. To the player that is simply Reserved.
                    return "IMP_GoalReserved".Translate();
            }
        }

        /// Maps evaluation state plus the authoritative reservation and
        /// owned-bill bookkeeping onto the colonist's pipeline status.
        private static ColonistStatus StatusOf(PawnPlanState state,
            int pawnId, ImplannerStore store, HashSet<int> reservedPawns)
        {
            switch (state)
            {
                case PawnPlanState.Complete: return ColonistStatus.Done;
                case PawnPlanState.Away: return ColonistStatus.Away;
            }
            Dictionary<string, string>? bills = store.Model.OwnedBillsFor(pawnId);
            if (bills != null && bills.Count > 0) return ColonistStatus.Operating;
            return reservedPawns.Contains(pawnId)
                ? ColonistStatus.Preparing
                : ColonistStatus.Waiting;
        }

        private static string StateText(ColonistStatus status)
        {
            switch (status)
            {
                case ColonistStatus.Preparing: return "IMP_StatePreparing".Translate();
                case ColonistStatus.Operating: return "IMP_StateOperating".Translate();
                case ColonistStatus.Done: return "IMP_StateDone".Translate();
                case ColonistStatus.Away: return "IMP_StateAway".Translate();
                default: return "IMP_StateWaiting".Translate();
            }
        }

        private static string LabelOf(GroupingOption option)
        {
            if (option.Kind == GroupingKind.All) return "IMP_ScopeAll".Translate();
            return option.Label ?? "";
        }
    }
}
