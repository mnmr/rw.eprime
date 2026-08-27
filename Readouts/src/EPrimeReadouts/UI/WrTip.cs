using System;
using System.Collections.Generic;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// A lazily gathered tooltip rendered through the StructuredTip pipeline,
    /// so ordinary text tips use the same padding and placement as structured
    /// resource tips. Text freezes while the pointer remains over its region.
    /// (Ported from WorkRoles/QualityJobs; keep the shared behavior in lockstep.)
    internal sealed class WrTip : IStructuredTipSource
    {
        private readonly string stableKey;
        private readonly Func<string>? gather;
        private readonly TipRefresh refresh;
        private string? offeredText;
        private string? text;
        private int lastFrame;
        private StructuredTip? structured;

        private WrTip(string stableKey, Func<string>? gather, TipRefresh refresh)
        {
            this.stableKey = stableKey;
            this.gather = gather;
            this.refresh = refresh;
        }

        internal static WrTip Pinned(string stableKey, Func<string> gather)
            => new WrTip(stableKey, gather, TipRefresh.Pinned);

        internal static WrTip PerSession(string stableKey, int uniqueId, Func<string> gather)
            => new WrTip(stableKey + ":" + uniqueId, gather, TipRefresh.PerSession);

        internal static WrTip Mutable(string stableKey)
            => new WrTip(stableKey, null, TipRefresh.Pinned);

        /// Call while drawing the owning control; the presenter gathers only
        /// when the hover delay opens. The steady offer path does not allocate.
        internal void Region(Rect rect)
        {
            StructuredTipPresenter.TipRegion(rect, this);
        }

        internal void Offer(string? value)
        {
            value ??= "";
            if (offeredText == value) return;
            offeredText = value;
            text = null;
            structured = null;
        }

        string IStructuredTipSource.StableKey => stableKey;

        StructuredTip? IStructuredTipSource.Resolve()
        {
            int frame = Time.frameCount;
            if (gather == null)
            {
                text ??= offeredText ?? "";
            }
            else if (TipGatherPolicy.ShouldGather(
                         refresh, text != null, frame, lastFrame))
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

    /// Shared translated and runtime text tooltips, gathered lazily and
    /// rendered by the same presenter as structured resource tips.
    internal static class WrTips
    {
        private static readonly Dictionary<string, WrTip> translated =
            new Dictionary<string, WrTip>();
        private static readonly Dictionary<(string key, string arg), WrTip> withArg =
            new Dictionary<(string, string), WrTip>();
        private static readonly Dictionary<string, WrTip> mutable =
            new Dictionary<string, WrTip>();
        private static readonly Dictionary<(string scope, string key), WrTip> scopedMutable =
            new Dictionary<(string, string), WrTip>();
        private static int observedUiVersion = -1;

        internal static WrTip Key(string key)
        {
            Observe();
            if (!translated.TryGetValue(key, out WrTip? tip))
                tip = CreateKeyed(key);
            return tip;
        }

        private static WrTip CreateKeyed(string key)
            => translated[key] = WrTip.Pinned(
                key, () => key.Translate().Resolve());

        internal static WrTip Key(string key, string arg)
        {
            Observe();
            if (!withArg.TryGetValue((key, arg), out WrTip? tip))
                tip = CreateKeyed(key, arg);
            return tip;
        }

        private static WrTip CreateKeyed(string key, string arg)
            => withArg[(key, arg)] = WrTip.Pinned(
                key + ":" + arg, () => key.Translate(arg).Resolve());

        /// Returns one stable source for runtime text owned by stableKey.
        /// Updating the offered text invalidates only its next display session.
        internal static WrTip Text(string stableKey, string? text)
        {
            Observe();
            if (!mutable.TryGetValue(stableKey, out WrTip? tip))
            {
                tip = WrTip.Mutable(stableKey);
                mutable.Add(stableKey, tip);
            }
            tip.Offer(text);
            return tip;
        }

        /// Keeps producer identity separate without concatenating a cache key
        /// on every repaint; the combined presenter key is built only on miss.
        internal static WrTip Text(string scope, string key, string? text)
        {
            Observe();
            if (!scopedMutable.TryGetValue((scope, key), out WrTip? tip))
            {
                tip = WrTip.Mutable(scope + ":" + key);
                scopedMutable.Add((scope, key), tip);
            }
            tip.Offer(text);
            return tip;
        }

        private static void Observe()
        {
            UiVersion.ObserveCurrentMetrics();
            int current = UiVersion.Current;
            if (observedUiVersion == current) return;
            observedUiVersion = current;
            StructuredTipPresenter.Reset();
            translated.Clear();
            withArg.Clear();
            mutable.Clear();
            scopedMutable.Clear();
        }

        internal static void Reset()
        {
            observedUiVersion = -1;
            StructuredTipPresenter.Reset();
            translated.Clear();
            withArg.Clear();
            mutable.Clear();
            scopedMutable.Clear();
        }
    }
}
