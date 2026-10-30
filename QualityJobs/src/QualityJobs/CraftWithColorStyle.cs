using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs
{
    [StaticConstructorOnStartup]
    internal static class CraftWithColorStyle
    {
        // Owner: process. Key: optional mod's loaded assembly. Value: immutable
        // API delegates, never bill/world references. Dependencies: assembly API
        // identity (fixed for process lifetime). Refresh: startup only. Equality:
        // delegates retained for process lifetime. Teardown: process exit; no
        // game-owned resources. Read calls allocate nothing. Apply runs only
        // when constructing a finishing bill, using CWC's native addition.
        private static readonly Func<Bill, bool>? active;
        private static readonly Func<Bill, ThingStyleDef>? style;
        private static readonly Func<Bill, bool>? random;
        private static readonly Action<Bill_Production, ThingStyleDef?, bool>? apply;

        static CraftWithColorStyle()
        {
            Type? state = AccessTools.TypeByName("CraftWithColor.State");
            if (state == null) return;
            MethodInfo? addition = AccessTools.Method(state, "TryGetAddition");
            MethodInfo? activeFor = AccessTools.Method(state, "StyleActiveFor");
            MethodInfo? styleFor = AccessTools.Method(state, "StyleFor");
            MethodInfo? getAddition = AccessTools.Method(state, "GetAddition");
            if (addition == null || activeFor == null || styleFor == null || getAddition == null) return;
            Type additionType = addition.ReturnType;
            MethodInfo? hasRandom = AccessTools.PropertyGetter(additionType, "HasRandomStyle");
            MethodInfo? choose = AccessTools.PropertySetter(additionType, "StyleSelection");
            MethodInfo? fromId = AccessTools.Method(additionType, "StyleFromIdDef",
                new[] { typeof(int), typeof(ThingStyleDef) });
            FieldInfo? styleActive = AccessTools.Field(additionType, "styleActive");
            if (hasRandom == null || choose == null || fromId == null || styleActive == null) return;
            active = Bind<bool>(addition, activeFor);
            style = Bind<ThingStyleDef>(addition, styleFor);
            ParameterExpression bill = Expression.Parameter(typeof(Bill), "bill");
            random = Expression.Lambda<Func<Bill, bool>>(
                Expression.Call(Expression.Call(addition, bill), hasRandom), bill).Compile();

            ParameterExpression target = Expression.Parameter(typeof(Bill_Production), "bill");
            ParameterExpression selected = Expression.Parameter(typeof(ThingStyleDef), "style");
            ParameterExpression isRandom = Expression.Parameter(typeof(bool), "random");
            ParameterExpression add = Expression.Variable(additionType, "addition");
            // Native IDs: Basic=0, Explicit=1, Random=2. Resolve the native
            // option for this product and use its setter, retaining CWC's
            // completion-time random selection and explicit Basic override.
            Expression id = Expression.Condition(isRandom, Expression.Constant(2),
                Expression.Condition(Expression.Equal(selected, Expression.Constant(null, typeof(ThingStyleDef))),
                    Expression.Constant(0), Expression.Constant(1)));
            apply = Expression.Lambda<Action<Bill_Production, ThingStyleDef?, bool>>(
                Expression.Block(new[] { add },
                    Expression.Assign(add, Expression.Call(getAddition, target)),
                    Expression.Assign(Expression.Field(add, styleActive), Expression.Constant(true)),
                    Expression.Call(add, choose, Expression.Call(add, fromId, id, selected))),
                target, selected, isRandom).Compile();
        }

        private static Func<Bill, T> Bind<T>(MethodInfo addition, MethodInfo read)
        {
            ParameterExpression bill = Expression.Parameter(typeof(Bill), "bill");
            return Expression.Lambda<Func<Bill, T>>(
                Expression.Call(read, Expression.Call(addition, bill)), bill).Compile();
        }

        internal static bool TryGet(Bill bill, out ThingStyleDef? result, out bool isRandom)
        {
            result = null;
            isRandom = false;
            if (active == null || style == null || random == null || !active(bill)) return false;
            isRandom = random(bill);
            result = isRandom ? null : style(bill);
            return true; // null is CWC's explicit basic (unstyled) selection.
        }

        internal static void Apply(Bill_Production bill, StyleSnapshot snapshot)
        {
            if (snapshot.customStyle) apply?.Invoke(bill, snapshot.style, snapshot.randomStyle);
        }
    }
}
