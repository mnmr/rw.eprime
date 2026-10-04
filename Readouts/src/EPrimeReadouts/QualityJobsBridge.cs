using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Soft-bound Quality Jobs integration. The foreign snapshot is projected
    /// into an EPrime-owned map-indexed snapshot only when QJA publishes a new
    /// object.
    internal static class QualityJobsBridge
    {
        private const string PackageId = "EPrime.QualityJobs";

        private static bool resolved;
        private static bool installed;

        // getManagedJobs is the availability gate; the remaining accessors are
        // assigned together when binding succeeds and are only invoked behind
        // that gate, so they are declared non-null with late assignment.
        private static Func<object>? getManagedJobs;
        private static Func<object, object> getJobs = null!;
        private static Func<object, int> getJobCount = null!;
        private static Func<object, int, object> getJobAt = null!;
        private static Func<object, Map> getMap = null!;
        private static Func<object, double> getProbability = null!;
        private static Func<object, Bill_Production> getBill = null!;
        private static Func<object, RecipeDef> getRecipe = null!;
        private static Func<object, ThingDef> getProduct = null!;
        private static Func<object, int> getRemainingIterations = null!;
        private static Func<object, ThingDef> getBuildableDef = null!;
        private static Func<object, ThingDef> getStuff = null!;
        private static Func<object, object> getTargets = null!;
        private static Func<object, int> getTargetCount = null!;
        private static Func<object, int, Thing> getTargetAt = null!;
        private static Type billJobType = null!;
        private static Type constructionJobType = null!;

        private static readonly Func<object, QualityJobsPlannedWorkSnapshot>
            buildSnapshot = BuildSnapshot;
        private static readonly Comparison<Map> compareMaps = CompareMaps;

        // Cache contract:
        // Owner: the active world/store lifecycle, behind process-scoped API
        //        binding.
        // Key: QJA's published snapshot object, by reference identity.
        // Value: immutable per-map managed bills, targets and job handles.
        // Dependencies: Map, Bill, Recipe, Product,
        //               RemainingAcceptedIterations and probability for bills;
        //               Map, BuildableDef, Stuff, Targets and probability for
        //               construction. UFTs and settings are intentionally not
        //               consumed independently. Live material state is
        //               deliberately not a dependency: the count pass reads it
        //               through the job handles.
        // Refresh policy: immediate when QJA publishes a new snapshot reference;
        //                 unchanged source references are allocation-free.
        //                 A runtime API/projection failure disables the bridge
        //                 for the process and publishes the empty fallback.
        // Equality policy: equal rebuilt jobs preserve projection identity.
        // Teardown: Reset on map removal and world teardown releases all QJA,
        //           map, bill and target references.
        private static readonly ReferenceProjectionCache<object,
            QualityJobsPlannedWorkSnapshot> snapshotCache =
            new ReferenceProjectionCache<object, QualityJobsPlannedWorkSnapshot>(
                buildSnapshot);

        internal static bool Installed
        {
            get { Resolve(); return installed; }
        }

        internal static bool Available
        {
            get { Resolve(); return getManagedJobs != null; }
        }

        internal static QualityJobsPlannedWorkSnapshot Current()
        {
            Resolve();
            if (getManagedJobs == null) return QualityJobsPlannedWorkSnapshot.Empty;
            try
            {
                object source = getManagedJobs();
                return source == null
                    ? QualityJobsPlannedWorkSnapshot.Empty
                    : snapshotCache.Get(source);
            }
            catch (Exception exception)
            {
                getManagedJobs = null;
                snapshotCache.Clear();
                Log.Warning("[EPrimeReadouts] Quality Jobs runtime API failed; "
                    + "quality rework is disabled for this process: "
                    + exception.GetType().Name + ": " + exception.Message);
                return QualityJobsPlannedWorkSnapshot.Empty;
            }
        }

        internal static void Reset()
        {
            // Binding is process-scoped. Only the world-owned source and
            // projection references must be released at teardown.
            snapshotCache.Clear();
        }

        /// Groups QJA's jobs by map, maps in uniqueID order and jobs in QJA's
        /// own order, so the projection is deterministic.
        private static QualityJobsPlannedWorkSnapshot BuildSnapshot(object source)
        {
            object jobs = getJobs(source);
            int count = getJobCount(jobs);
            var bills = new Dictionary<Map, List<ManagedBillJob>>(
                ReferenceIdentityComparer<Map>.Instance);
            var construction = new Dictionary<Map, List<ManagedConstructionJob>>(
                ReferenceIdentityComparer<Map>.Instance);
            var maps = new List<Map>();
            for (int i = 0; i < count; i++)
            {
                object job = getJobAt(jobs, i);
                if (billJobType.IsInstanceOfType(job))
                {
                    Map billMap = getMap(job);
                    ListFor(bills, billMap, maps).Add(new ManagedBillJob(
                        billMap, getBill(job), getRecipe(job),
                        getProduct(job), getRemainingIterations(job),
                        getProbability(job)));
                    continue;
                }
                if (!constructionJobType.IsInstanceOfType(job)) continue;

                object targets = getTargets(job);
                int targetCount = getTargetCount(targets);
                var copiedTargets = new Thing[targetCount];
                for (int target = 0; target < targetCount; target++)
                    copiedTargets[target] = getTargetAt(targets, target);
                Map constructionMap = getMap(job);
                ListFor(construction, constructionMap, maps).Add(
                    new ManagedConstructionJob(
                        constructionMap, getBuildableDef(job), getStuff(job),
                        copiedTargets, getProbability(job)));
            }

            if (maps.Count == 0) return QualityJobsPlannedWorkSnapshot.Empty;
            maps.Sort(compareMaps);
            var projected = new QualityJobsMapWorkSnapshot[maps.Count];
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                projected[i] = new QualityJobsMapWorkSnapshot(map,
                    bills.TryGetValue(map, out var mapBills)
                        ? mapBills.ToArray()
                        : Array.Empty<ManagedBillJob>(),
                    construction.TryGetValue(map, out var mapConstruction)
                        ? mapConstruction.ToArray()
                        : Array.Empty<ManagedConstructionJob>());
            }
            return new QualityJobsPlannedWorkSnapshot(projected);
        }

        private static List<T> ListFor<T>(
            Dictionary<Map, List<T>> byMap, Map map, List<Map> maps)
        {
            if (byMap.TryGetValue(map, out List<T> list)) return list;
            list = new List<T>();
            byMap.Add(map, list);
            if (!maps.Contains(map)) maps.Add(map);
            return list;
        }

        private static int CompareMaps(Map left, Map right)
            => left.uniqueID.CompareTo(right.uniqueID);

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            installed = ModLister.GetActiveModWithIdentifier(
                PackageId, ignorePostfix: true) != null;
            if (!installed) return;

            Type api = GenTypes.GetTypeInAnyAssembly("QualityJobs.QualityJobsApi");
            if (api == null)
            {
                WarnChangedApi();
                return;
            }

            try
            {
                const BindingFlags flags = BindingFlags.Public
                    | BindingFlags.Static;
                MethodInfo get = api.GetMethod("GetManagedJobs", flags);
                if (get == null || get.GetParameters().Length != 0
                    || get.ReturnType == typeof(void))
                    throw new MissingMemberException("GetManagedJobs");

                Type snapshotType = get.ReturnType;
                PropertyInfo jobsProperty = Property(snapshotType, "Jobs");
                Type jobsType = jobsProperty.PropertyType;
                PropertyInfo jobCountProperty = Property(jobsType, "Count");
                PropertyInfo jobItemProperty = Indexer(jobsType);
                Type jobType = jobItemProperty.PropertyType;

                Assembly assembly = api.Assembly;
                billJobType = assembly.GetType("QualityJobs.ManagedBillJob", true);
                constructionJobType = assembly.GetType(
                    "QualityJobs.ManagedConstructionJob", true);
                if (!jobType.IsAssignableFrom(billJobType)
                    || !jobType.IsAssignableFrom(constructionJobType))
                    throw new MissingMemberException("managed job types");

                PropertyInfo mapProperty = Property(jobType, "Map");
                PropertyInfo probabilityProperty = Property(
                    jobType, "ProbabilityAtOrAboveTarget");
                PropertyInfo billProperty = Property(billJobType, "Bill");
                PropertyInfo recipeProperty = Property(billJobType, "Recipe");
                PropertyInfo productProperty = Property(billJobType, "Product");
                PropertyInfo remainingProperty = Property(
                    billJobType, "RemainingAcceptedIterations");
                PropertyInfo buildableProperty = Property(
                    constructionJobType, "BuildableDef");
                PropertyInfo stuffProperty = Property(constructionJobType, "Stuff");
                PropertyInfo targetsProperty = Property(
                    constructionJobType, "Targets");
                Type targetsType = targetsProperty.PropertyType;
                PropertyInfo targetCountProperty = Property(targetsType, "Count");
                PropertyInfo targetItemProperty = Indexer(targetsType);

                RequireAssignable(mapProperty, typeof(Map));
                RequireExact(probabilityProperty, typeof(double));
                RequireAssignable(billProperty, typeof(Bill_Production));
                RequireAssignable(recipeProperty, typeof(RecipeDef));
                RequireAssignable(productProperty, typeof(ThingDef));
                RequireExact(remainingProperty, typeof(int));
                RequireAssignable(buildableProperty, typeof(ThingDef));
                RequireAssignable(stuffProperty, typeof(ThingDef));
                RequireExact(jobCountProperty, typeof(int));
                RequireExact(targetCountProperty, typeof(int));
                RequireAssignable(targetItemProperty, typeof(Thing));

                getManagedJobs = CompileStaticObject(get);
                getJobs = CompileProperty<object>(jobsProperty);
                getJobCount = CompileProperty<int>(jobCountProperty);
                getJobAt = CompileObjectIndexer(jobItemProperty);
                getMap = CompileProperty<Map>(mapProperty);
                getProbability = CompileProperty<double>(probabilityProperty);
                getBill = CompileProperty<Bill_Production>(billProperty);
                getRecipe = CompileProperty<RecipeDef>(recipeProperty);
                getProduct = CompileProperty<ThingDef>(productProperty);
                getRemainingIterations = CompileProperty<int>(remainingProperty);
                getBuildableDef = CompileProperty<ThingDef>(buildableProperty);
                getStuff = CompileProperty<ThingDef>(stuffProperty);
                getTargets = CompileProperty<object>(targetsProperty);
                getTargetCount = CompileProperty<int>(targetCountProperty);
                getTargetAt = CompileIndexer<Thing>(targetItemProperty);
            }
            catch (Exception exception)
            {
                getManagedJobs = null;
                snapshotCache.Clear();
                Log.Warning("[EPrimeReadouts] Quality Jobs integration API "
                    + "binding failed; quality rework is unavailable: "
                    + exception.Message);
            }
        }

        private static PropertyInfo Property(Type owner, string name)
        {
            PropertyInfo property = owner.GetProperty(
                name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null) return property;
            Type[] inherited = owner.GetInterfaces();
            for (int i = 0; i < inherited.Length; i++)
            {
                property = inherited[i].GetProperty(
                    name, BindingFlags.Instance | BindingFlags.Public);
                if (property != null) return property;
            }
            throw new MissingMemberException(owner.FullName, name);
        }

        private static PropertyInfo Indexer(Type owner)
        {
            PropertyInfo property = owner.GetProperty(
                "Item", BindingFlags.Instance | BindingFlags.Public);
            if (property == null || property.GetIndexParameters().Length != 1
                || property.GetIndexParameters()[0].ParameterType != typeof(int))
                throw new MissingMemberException(owner.FullName, "Item[int]");
            return property;
        }

        private static void RequireExact(PropertyInfo property, Type expected)
        {
            if (property.PropertyType != expected)
                throw new MissingMemberException(
                    property.DeclaringType?.FullName, property.Name);
        }

        private static void RequireAssignable(PropertyInfo property, Type expected)
        {
            if (!expected.IsAssignableFrom(property.PropertyType))
                throw new MissingMemberException(
                    property.DeclaringType?.FullName, property.Name);
        }

        private static Func<object> CompileStaticObject(MethodInfo method)
            => Expression.Lambda<Func<object>>(
                Expression.Convert(Expression.Call(method), typeof(object)))
                .Compile();

        private static Func<object, T> CompileProperty<T>(PropertyInfo property)
        {
            ParameterExpression instance = Expression.Parameter(
                typeof(object), "instance");
            return Expression.Lambda<Func<object, T>>(
                Expression.Convert(
                    Expression.Property(
                        Expression.Convert(instance, property.DeclaringType),
                        property),
                    typeof(T)),
                instance).Compile();
        }

        private static Func<object, int, object> CompileObjectIndexer(
            PropertyInfo property) => CompileIndexer<object>(property);

        private static Func<object, int, T> CompileIndexer<T>(PropertyInfo property)
        {
            ParameterExpression instance = Expression.Parameter(
                typeof(object), "instance");
            ParameterExpression index = Expression.Parameter(typeof(int), "index");
            return Expression.Lambda<Func<object, int, T>>(
                Expression.Convert(
                    Expression.Property(
                        Expression.Convert(instance, property.DeclaringType),
                        property, index),
                    typeof(T)),
                instance, index).Compile();
        }

        private static void WarnChangedApi()
        {
            Log.Warning("[EPrimeReadouts] Quality Jobs is active but its "
                + "managed-jobs API is unavailable; quality rework is disabled.");
        }

        internal readonly struct ManagedBillJob : IEquatable<ManagedBillJob>
        {
            internal ManagedBillJob(
                Map map,
                Bill_Production bill,
                RecipeDef recipe,
                ThingDef product,
                int remainingAcceptedIterations,
                double probability)
            {
                Map = map;
                Bill = bill;
                Recipe = recipe;
                Product = product;
                RemainingAcceptedIterations = remainingAcceptedIterations;
                Probability = probability;
            }

            internal readonly Map Map;
            internal readonly Bill_Production Bill;
            internal readonly RecipeDef Recipe;
            internal readonly ThingDef Product;
            internal readonly int RemainingAcceptedIterations;
            internal readonly double Probability;

            public bool Equals(ManagedBillJob other)
                => ReferenceEquals(Map, other.Map)
                   && ReferenceEquals(Bill, other.Bill)
                   && ReferenceEquals(Recipe, other.Recipe)
                   && ReferenceEquals(Product, other.Product)
                   && RemainingAcceptedIterations
                   == other.RemainingAcceptedIterations
                   && Probability.Equals(other.Probability);
        }

        internal readonly struct ManagedConstructionJob
            : IEquatable<ManagedConstructionJob>
        {
            internal ManagedConstructionJob(
                Map map,
                ThingDef buildableDef,
                ThingDef stuff,
                Thing[] targets,
                double probability)
            {
                Map = map;
                BuildableDef = buildableDef;
                Stuff = stuff;
                Targets = targets;
                Probability = probability;
            }

            internal readonly Map Map;
            internal readonly ThingDef BuildableDef;
            internal readonly ThingDef Stuff;
            internal readonly Thing[] Targets;
            internal readonly double Probability;

            public bool Equals(ManagedConstructionJob other)
            {
                if (!ReferenceEquals(Map, other.Map)
                    || !ReferenceEquals(BuildableDef, other.BuildableDef)
                    || !ReferenceEquals(Stuff, other.Stuff)
                    || !Probability.Equals(other.Probability)
                    || Targets.Length != other.Targets.Length)
                    return false;
                for (int i = 0; i < Targets.Length; i++)
                    if (!ReferenceEquals(Targets[i], other.Targets[i])) return false;
                return true;
            }
        }
    }
}
