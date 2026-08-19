using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using EPrimeReadouts.Core;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Soft-bound Quality Jobs integration. The foreign snapshot is projected
    /// into EPrime-owned immutable arrays only when QJA publishes a new object.
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

        private static readonly Func<object, ManagedJobsSnapshot> buildSnapshot =
            BuildSnapshot;
        private static readonly IEqualityComparer<ManagedJobsSnapshot>
            distinctSourceComparer = new DistinctSourceComparer();

        // Cache contract:
        // Owner: the active QJA store/world, behind process-scoped API binding.
        // Key: QJA's published snapshot object, by reference identity.
        // Value: immutable EPrime-owned bill/construction handle projection.
        // Dependencies: Map, Bill, Recipe, Product,
        //               RemainingAcceptedIterations and probability for bills;
        //               Map, BuildableDef, Stuff, Targets and probability for
        //               construction. UFTs and settings are intentionally not
        //               consumed independently.
        // Refresh policy: immediate on a new QJA snapshot reference.
        //                 A runtime API/projection failure disables the bridge
        //                 for the process and publishes the empty fallback.
        // Equality policy: every distinct QJA source produces a distinct bridge
        //                  projection so live handles are reread downstream;
        //                  the resource projection preserves identity only
        //                  after its complete rendered contents compare equal.
        // Teardown: Reset releases source and projected world/game references.
        private static readonly ReferenceProjectionCache<object, ManagedJobsSnapshot>
            snapshotCache = new ReferenceProjectionCache<object, ManagedJobsSnapshot>(
                buildSnapshot, distinctSourceComparer);

        internal static bool Installed
        {
            get { Resolve(); return installed; }
        }

        internal static bool Available
        {
            get { Resolve(); return getManagedJobs != null; }
        }

        internal static ManagedJobsSnapshot GetManagedJobs()
        {
            Resolve();
            if (getManagedJobs == null) return ManagedJobsSnapshot.Empty;
            try
            {
                object source = getManagedJobs();
                return source == null
                    ? ManagedJobsSnapshot.Empty
                    : snapshotCache.Get(source);
            }
            catch (Exception exception)
            {
                getManagedJobs = null;
                snapshotCache.Clear();
                Log.Warning("[EPrimeReadouts] Quality Jobs runtime API failed; "
                    + "quality rework is disabled for this process: "
                    + exception.GetType().Name + ": " + exception.Message);
                return ManagedJobsSnapshot.Empty;
            }
        }

        internal static void Reset()
        {
            // Binding is process-scoped. Only the world-owned source and
            // projection references must be released at teardown.
            snapshotCache.Clear();
        }

        private static ManagedJobsSnapshot BuildSnapshot(object source)
        {
            object jobs = getJobs(source);
            int count = getJobCount(jobs);
            List<ManagedBillJob>? bills = null;
            List<ManagedConstructionJob>? construction = null;
            for (int i = 0; i < count; i++)
            {
                object job = getJobAt(jobs, i);
                if (billJobType.IsInstanceOfType(job))
                {
                    if (bills == null) bills = new List<ManagedBillJob>();
                    bills.Add(new ManagedBillJob(
                        getMap(job), getBill(job), getRecipe(job),
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
                if (construction == null)
                    construction = new List<ManagedConstructionJob>();
                construction.Add(new ManagedConstructionJob(
                    getMap(job), getBuildableDef(job), getStuff(job),
                    copiedTargets, getProbability(job)));
            }

            if (bills == null && construction == null)
                return ManagedJobsSnapshot.Empty;
            return new ManagedJobsSnapshot(
                bills != null ? bills.ToArray() : Array.Empty<ManagedBillJob>(),
                construction != null
                    ? construction.ToArray()
                    : Array.Empty<ManagedConstructionJob>());
        }

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

        private sealed class DistinctSourceComparer
            : IEqualityComparer<ManagedJobsSnapshot>
        {
            public bool Equals(
                ManagedJobsSnapshot left, ManagedJobsSnapshot right) => false;

            public int GetHashCode(ManagedJobsSnapshot value) => 0;
        }

        internal sealed class ManagedJobsSnapshot
            : IEquatable<ManagedJobsSnapshot>
        {
            internal static readonly ManagedJobsSnapshot Empty =
                new ManagedJobsSnapshot(
                    Array.Empty<ManagedBillJob>(),
                    Array.Empty<ManagedConstructionJob>());

            internal ManagedJobsSnapshot(
                ManagedBillJob[] bills,
                ManagedConstructionJob[] construction)
            {
                Bills = bills;
                Construction = construction;
            }

            internal readonly ManagedBillJob[] Bills;
            internal readonly ManagedConstructionJob[] Construction;

            public bool Equals(ManagedJobsSnapshot? other)
            {
                if (other == null
                    || Bills.Length != other.Bills.Length
                    || Construction.Length != other.Construction.Length)
                    return false;
                for (int i = 0; i < Bills.Length; i++)
                    if (!Bills[i].Equals(other.Bills[i])) return false;
                for (int i = 0; i < Construction.Length; i++)
                    if (!Construction[i].Equals(other.Construction[i])) return false;
                return true;
            }

            public override bool Equals(object obj)
                => Equals(obj as ManagedJobsSnapshot);

            public override int GetHashCode()
                => (Bills.Length * 397) ^ Construction.Length;
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
