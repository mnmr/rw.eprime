using System;
using System.Collections.Generic;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// A lazily gathered tooltip rendered through the StructuredTip pipeline,
    /// so ordinary text tips get the same padding and placement as structured
    /// tips. Text gathers when the hover delay opens and freezes while the
    /// pointer stays (Pinned: kept until Reset; PerSession: leave and
    /// re-hover to regather; Mutable: the producer offers the text).
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

        internal static WrTip PerSession(string stableKey, int uniqueId, Func<string> gather,
            TooltipPriority priority = TooltipPriority.Default)
            => new WrTip(stableKey + ":" + uniqueId + ":" + (int)priority, gather,
                TipRefresh.PerSession);

        internal static WrTip Mutable(string stableKey)
            => new WrTip(stableKey, null, TipRefresh.Pinned);

        /// Call while drawing the owning control; the presenter gathers only
        /// when the hover delay opens. The steady offer path does not allocate.
        internal void Region(Rect rect)
        {
            StructuredTipPresenter.TipRegion(rect, this);
        }

        /// Offers a hover region while keeping the resulting tooltip window
        /// outside an adjacent interactive control.
        internal void Region(Rect rect, Rect exclusionRect)
        {
            StructuredTipPresenter.TipRegion(rect, exclusionRect, this);
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
            // Both branches leave text non-null.
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

    /// Shared translated, warning and runtime text tooltips, gathered lazily
    /// and rendered by the same presenter as structured tips.
    /// Owner: process (one registry per mod assembly). Key: translation key,
    /// optionally with one argument; runtime text by stable key or
    /// (scope, key). Value: one WrTip per key (stable identity).
    /// Dependencies: the host's registry revision (TipHost), observed on every
    /// access. Refresh policy: entries drop wholesale when it moves; runtime
    /// text invalidates only its own next display session. Equality: n/a.
    /// Teardown: Reset (world teardown) or a registry-revision change.
    internal static class WrTips
    {
        private static readonly Dictionary<string, WrTip> translated =
            new Dictionary<string, WrTip>();
        private static readonly Dictionary<(string key, string? arg), WrTip> withArg =
            new Dictionary<(string, string?), WrTip>();
        private static readonly Dictionary<string, WrTip> warnPlain =
            new Dictionary<string, WrTip>();
        private static readonly Dictionary<(string key, string arg), WrTip> warnWithArg =
            new Dictionary<(string, string), WrTip>();
        private static readonly Dictionary<string, WrTip> mutable =
            new Dictionary<string, WrTip>();
        private static readonly Dictionary<(string scope, string key), WrTip> scopedMutable =
            new Dictionary<(string, string), WrTip>();
        private static int observedRevision = -1;

        // Creation stays in separate methods: a lambda capturing a parameter
        // makes the compiler allocate its display class at method entry, so
        // an inline miss-branch lambda would allocate on every cache hit.

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

        internal static WrTip Key(string key, string? arg)
        {
            Observe();
            if (!withArg.TryGetValue((key, arg), out WrTip? tip))
                tip = CreateKeyed(key, arg);
            return tip;
        }

        private static WrTip CreateKeyed(string key, string? arg)
            => withArg[(key, arg)] = WrTip.Pinned(
                key + ":" + arg, () => key.Translate(arg).Resolve());

        /// Warning-styled translated tip (TipText.Warning formatting).
        internal static WrTip Warning(string key)
        {
            Observe();
            if (!warnPlain.TryGetValue(key, out WrTip? tip))
                tip = CreateWarning(key);
            return tip;
        }

        private static WrTip CreateWarning(string key)
            => warnPlain[key] = WrTip.Pinned("!" + key,
                () => TipText.Warning(key.Translate()));

        internal static WrTip Warning(string key, string arg)
        {
            Observe();
            if (!warnWithArg.TryGetValue((key, arg), out WrTip? tip))
                tip = CreateWarning(key, arg);
            return tip;
        }

        private static WrTip CreateWarning(string key, string arg)
            => warnWithArg[(key, arg)] = WrTip.Pinned("!" + key + ":" + arg,
                () => TipText.Warning(key.Translate(arg)));

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
            int current = TipHost.ObserveRegistryRevision();
            if (observedRevision == current) return;
            observedRevision = current;
            Clear();
        }

        internal static void Reset()
        {
            observedRevision = -1;
            Clear();
        }

        private static void Clear()
        {
            StructuredTipPresenter.Reset();
            translated.Clear();
            withArg.Clear();
            warnPlain.Clear();
            warnWithArg.Clear();
            mutable.Clear();
            scopedMutable.Clear();
        }
    }
}
