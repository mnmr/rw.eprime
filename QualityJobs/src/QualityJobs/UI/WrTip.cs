using System;
using System.Collections.Generic;
using QualityJobs.Core;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// A lazily gathered tooltip rendered through the StructuredTip pipeline,
    /// so it gets the same inner padding as every structured tip. Text
    /// gathers when the hover delay opens and freezes while the pointer stays
    /// (Pinned: kept until Reset; PerSession: leave and re-hover to regather).
    /// (Ported from WorkRoles; keep in lockstep.)
    internal sealed class WrTip : IStructuredTipSource
    {
        private readonly string stableKey;
        private readonly Func<string> gather;
        private readonly TipRefresh refresh;
        private string? text;
        private int lastFrame;
        private StructuredTip? structured;

        private WrTip(string stableKey, Func<string> gather, TipRefresh refresh)
        {
            this.stableKey = stableKey;
            this.gather = gather;
            this.refresh = refresh;
        }

        internal static WrTip Pinned(string stableKey, Func<string> gather)
            => new WrTip(stableKey, gather, TipRefresh.Pinned);

        internal static WrTip PerSession(string stableKey, int uniqueId, Func<string> gather)
            => new WrTip(stableKey + ":" + uniqueId, gather, TipRefresh.PerSession);

        /// Call while drawing the owning control; the presenter gathers only
        /// when the hover delay opens. The steady offer path does not allocate.
        internal void Region(Rect rect)
        {
            StructuredTipPresenter.TipRegion(rect, this);
        }

        string IStructuredTipSource.StableKey => stableKey;

        StructuredTip? IStructuredTipSource.Resolve()
        {
            int frame = Time.frameCount;
            if (TipGatherPolicy.ShouldGather(refresh, text != null, frame, lastFrame))
            {
                text = gather() ?? "";
                structured = null;
            }
            lastFrame = frame;
            if (text!.Length == 0) return null;
            if (structured == null)
            {
                var model = new TipModel();
                model.AddSection().Text(text);
                structured = new StructuredTip(stableKey, model);
            }
            return structured;
        }

        /// Drops gathered text so the next hover regathers (language change).
        internal void Reset()
        {
            text = null;
            structured = null;
        }
    }

    /// Shared translated tooltips, gathered lazily on first hover.
    /// Owner: process. Key: translation key, optionally composed with one
    /// argument. Value: pinned WrTip (immutable identity). Dependencies:
    /// UiVersion.Current (covers language change), observed on every access.
    /// Refresh policy: entries drop wholesale on a UI-metric change; content
    /// is otherwise static. Equality: n/a (single writer, stable identity per
    /// key). Teardown: bounded by the mod's translation-key set; a UI-metric
    /// change clears.
    internal static class WrTips
    {
        private static readonly Dictionary<string, WrTip> plain
            = new Dictionary<string, WrTip>();
        private static readonly Dictionary<(string key, string arg), WrTip> withArg
            = new Dictionary<(string, string), WrTip>();
        private static int observedUiVersion = -1;

        // Creation stays in separate methods: a lambda capturing a parameter
        // makes the compiler allocate its display class at method entry, so
        // an inline miss-branch lambda would allocate on every cache hit.

        internal static WrTip Key(string key)
        {
            Observe();
            if (!plain.TryGetValue(key, out WrTip? tip))
                tip = CreateKeyed(key);
            return tip;
        }

        private static WrTip CreateKeyed(string key)
            => plain[key] = WrTip.Pinned(key, () => key.Translate().Resolve());

        internal static WrTip Key(string key, string arg)
        {
            Observe();
            if (!withArg.TryGetValue((key, arg), out WrTip? tip))
                tip = CreateKeyed(key, arg);
            return tip;
        }

        private static WrTip CreateKeyed(string key, string arg)
            => withArg[(key, arg)] = WrTip.Pinned(key + ":" + arg,
                () => key.Translate(arg).Resolve());

        private static void Observe()
        {
            UiVersion.ObserveCurrentMetrics();
            int current = UiVersion.Current;
            if (observedUiVersion == current) return;
            observedUiVersion = current;
            StructuredTipPresenter.Reset();
            plain.Clear();
            withArg.Clear();
        }

        internal static void Reset()
        {
            observedUiVersion = -1;
            StructuredTipPresenter.Reset();
            plain.Clear();
            withArg.Clear();
        }
    }
}
