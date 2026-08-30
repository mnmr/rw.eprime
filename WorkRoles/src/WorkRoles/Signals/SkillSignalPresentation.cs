using System;
using System.Collections.Generic;
using System.Text;
using RimShared.Common;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    internal static class SkillSignalPresentation
    {
        private static readonly Dictionary<string, Texture2D?> IconCache =
            new Dictionary<string, Texture2D?>(StringComparer.Ordinal);

        // Mirrors the skills grid's low-skill grey.
        private static readonly Color TableGrey = new Color(0.65f, 0.65f, 0.65f);
        // Passive rows: dimmer than active, brighter than descriptions.
        private static readonly Color PassiveGrey = new Color(0.8f, 0.8f, 0.8f);
        // Vertical space between the head facts and the signals table.
        private const float TableGap = 16f;

        // TODO: known official signals without icons (they render text-only):
        // Ludeon.RimWorld:Trait:ShootingAccuracy and Ludeon.RimWorld:Trait:Nimble.
        // Add icons or pick another decorator; view.OfficialMissingIcons still
        // reports the full set when revisiting this.
        internal static List<Texture2D> ResolveIcons(SkillSignalView view)
        {
            var result = new List<Texture2D>(view.IconCandidates.Count);
            foreach (PawnSignal signal in view.IconCandidates)
            {
                Texture2D? texture = ResolveIcon(signal.Ui.IconKey);
                if (texture != null)
                    result.Add(texture);
            }
            return result;
        }

        private static Texture2D? ResolveIcon(string? iconKey)
        {
            if (iconKey is not { Length: > 0 }) return null;
            if (!IconCache.TryGetValue(iconKey, out Texture2D? texture))
            {
                texture = ContentFinder<Texture2D>.Get(iconKey, false);
                IconCache[iconKey] = texture;
            }
            return texture;
        }

        internal static StructuredTip? CreateTooltip(
            Pawn pawn,
            string skillDefName,
            string skillLabel,
            string valueText,
            Color valueColor,
            SkillSignalView view,
            SignalBucket? bucket,
            SignalBucket? baseBucket,
            int skillLevel,
            int promotionThreshold)
        {
            if (view == null || !view.HasTooltip) return null;

            // Single section: head facts share one label column so level and
            // verdict align, and no separator is drawn before the table.
            var model = new TipModel();
            var table = model.AddSection();
            table.Fact(skillLabel, valueText, valueColor, labelColor: Color.white);
            if (bucket.HasValue)
            {
                table.Fact(
                    "WR_SignalVerdict".Translate(),
                    BucketLabel(bucket.Value),
                    VerdictColor(bucket.Value),
                    labelColor: Color.white);
            }

            bool promoted = bucket.HasValue
                && baseBucket.HasValue
                && bucket.Value > baseBucket.Value
                && promotionThreshold >= 0;
            if (promoted
                || view.ActiveSignals.Count > 0
                || view.PassiveSignals.Count > 0)
            {
                table.Gap(TableGap);
                table.Columns(new[]
                {
                    "WR_SignalColSignal".Translate().ToString(),
                    "WR_SignalColEffects".Translate().ToString(),
                    "WR_SignalCondition".Translate().ToString(),
                    "WR_SignalSource".Translate().ToString(),
                }, TableGrey);
                table.Rule();
                if (promoted)
                    table.Columns(new[]
                    {
                        "WR_SignalSkillPromotion".Translate().ToString(),
                        "WR_SignalSkillPromotionEffect".Translate(
                            BucketLabel(baseBucket!.Value), // promoted implies both buckets
                            BucketLabel(bucket!.Value)).ToString(),
                        "WR_SignalSkillPromotionCondition".Translate(
                            skillLevel, promotionThreshold).ToString(),
                        "WorkRoles",
                    });
                foreach (PawnSignal signal in view.ActiveSignals)
                    AddSignalRows(table, signal, null, pawn);
                foreach (PawnSignal signal in view.PassiveSignals)
                    AddSignalRows(table, signal, PassiveGrey, pawn);
            }

            return new StructuredTip(
                $"skill-signal:{pawn.thingIDNumber}:{skillDefName}", model);
        }

        private static void AddSignalRows(TipSection table, PawnSignal signal, Color? rowColor, Pawn pawn)
        {
            string name = signal.Ui.Label is { Length: > 0 } label
                ? label
                : InvariantDefName.Humanize(signal.Source.DefName);
            if (signal.Relation == SignalRelation.Spillover)
                name += " " + "WR_SignalFromSkill".Translate(
                    InvariantDefName.Humanize(signal.OriginSkillDefName));
            Texture2D? icon = ResolveIcon(signal.Ui.IconKey);
            string source = signal.Ui.SourceDisplayName ?? "";

            if (signal.Effects.Count == 0)
            {
                // Null cells render as empty columns; TipColumnsRow tolerates them.
                table.Columns(new string[] { name, null!, null!, source }, rowColor, icon!);
            }
            else
            {
                for (int i = 0; i < signal.Effects.Count; i++)
                {
                    SignalEffect effect = signal.Effects[i];
                    table.Columns(i == 0
                        ? new string[] { name, EffectText(signal, effect), ConditionText(effect), source }
                        : new string[] { null!, EffectText(signal, effect), ConditionText(effect), null! },
                        rowColor,
                        (i == 0 ? icon : null)!,
                        tight: i > 0);
                }
            }

            if (signal.Ui.Description is { Length: > 0 } description)
                table.Span(ResolveDescription(description, pawn), alignColumn: 1);
        }

        /// Trait/gene descriptions carry grammar tokens meant for pawn context:
        /// [PAWN_xxx] (grammar) and legacy {PAWN_xxx}; unresolvable text stays raw.
        private static string ResolveDescription(string text, Pawn pawn)
        {
            if (pawn == null || string.IsNullOrWhiteSpace(text)) return text;
            try
            {
                return text.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn).Resolve();
            }
            catch (Exception)
            {
                return text;
            }
        }

        /// Generic stat modifiers surface the stat itself; capability and
        /// descriptive effects surface their target too, since the kind alone
        /// says nothing about WHAT is disabled or preferred.
        private static string EffectText(PawnSignal signal, SignalEffect effect)
        {
            if (effect.Kind == SignalEffectKind.Capability)
            {
                string subject = SentenceCase(InvariantDefName.Humanize(effect.TargetDefName));
                // With More Than Capable loaded, "incapable" work is merely
                // hated (treated as Awful), never blocked outright.
                return MoreThanCapableSignalProvider.Available
                    ? "WR_SignalCapabilityAwful".Translate(subject).ToString()
                    : "WR_SignalCapabilityOff".Translate(subject).ToString();
            }
            if (effect.Operation == SignalOperation.Descriptive)
            {
                string label = InvariantDefName.Humanize(effect.Kind.ToString());
                string? subject = DescriptiveSubject(signal, effect);
                return subject == null ? label : label + ": " + subject.ToLowerInvariant();
            }
            string head = effect.Kind == SignalEffectKind.StatModifier
                && !string.IsNullOrWhiteSpace(effect.TargetDefName)
                ? InvariantDefName.Humanize(effect.TargetDefName)
                : InvariantDefName.Humanize(effect.Kind.ToString());
            return head + " " + EffectValue(effect);
        }

        /// The descriptive effect's subject; suppressed when it would repeat
        /// the tooltip's own skill (or the CurrentSkill placeholder).
        private static string? DescriptiveSubject(PawnSignal signal, SignalEffect effect)
        {
            string? target = effect.TargetDefName;
            if (string.IsNullOrWhiteSpace(target) || target == "CurrentSkill"
                || string.Equals(target, signal.SkillDefName, StringComparison.Ordinal))
                return null;
            return InvariantDefName.Humanize(target);
        }

        private static string SentenceCase(string text) =>
            string.IsNullOrEmpty(text) ? text
            : char.ToUpperInvariant(text[0]) + text.Substring(1).ToLowerInvariant();

        private static string ConditionText(SignalEffect effect)
        {
            var sb = new StringBuilder();
            foreach (SignalCondition condition in effect.Conditions)
            {
                if (sb.Length > 0) sb.Append("; ");
                sb.Append(ConditionDisplay(condition));
            }
            if (effect.AlreadyReflected)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("WR_SignalAlreadyReflected".Translate());
            }
            return sb.ToString();
        }

        internal static string BucketLabel(SignalBucket bucket)
        {
            switch (bucket)
            {
                case SignalBucket.Awful: return "WR_VerdictAwful".Translate();
                case SignalBucket.Poor: return "WR_VerdictPoor".Translate();
                case SignalBucket.Strong: return "WR_VerdictStrong".Translate();
                case SignalBucket.Great: return "WR_VerdictGreat".Translate();
                case SignalBucket.Exceptional: return "WR_VerdictExceptional".Translate();
                default: return "WR_VerdictNeutral".Translate();
            }
        }

        // Star stack tints: on chips, unearned stars are a semi-transparent
        // black overlay, rendering as a darkened patch of the chip's own
        // color; on the dark stats panel they need a fixed solid grey
        // brighter than the background instead. Gold echoes the vanilla
        // Excellent work box outline; red marks the bad verdicts.
        private static readonly Color StarGrey = new Color(0f, 0f, 0f, 0.4f);
        private static readonly Color PanelStarGrey = new Color(0.35f, 0.35f, 0.35f);
        private static readonly Color StarWhite = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color StarGold = new Color(1f, 210f / 255f, 0f);
        private static readonly Color StarRed = new Color(0.9f, 0.35f, 0.3f);

        /// Verdict badge for role chips, earned bottom-up: Awful two red,
        /// Poor one red, Neutral all grey, Strong one white, Great two white,
        /// Exceptional two gold.
        internal static UI.RoleChipVerdict VerdictBadge(SignalBucket bucket) =>
            Badge(bucket, StarGrey);

        /// The same regime for the stats panel's per-skill star pair.
        internal static UI.RoleChipVerdict VerdictBadgePanel(SignalBucket bucket) =>
            Badge(bucket, PanelStarGrey);

        private static UI.RoleChipVerdict Badge(SignalBucket bucket, Color unearned)
        {
            switch (bucket)
            {
                case SignalBucket.Exceptional:
                    return new UI.RoleChipVerdict(StarGold, StarGold);
                case SignalBucket.Great:
                    return new UI.RoleChipVerdict(StarWhite, StarWhite);
                case SignalBucket.Strong:
                    return new UI.RoleChipVerdict(StarWhite, unearned);
                case SignalBucket.Poor:
                    return new UI.RoleChipVerdict(StarRed, unearned);
                case SignalBucket.Awful:
                    return new UI.RoleChipVerdict(StarRed, StarRed);
                default:
                    return new UI.RoleChipVerdict(unearned, unearned);
            }
        }

        internal static Color VerdictColor(SignalBucket bucket)
        {
            switch (bucket)
            {
                case SignalBucket.Awful: return new Color(0.9f, 0.35f, 0.3f);
                case SignalBucket.Poor: return new Color(0.85f, 0.78f, 0.35f);
                case SignalBucket.Strong: return new Color(0.55f, 0.8f, 0.45f);
                case SignalBucket.Great: return new Color(0.35f, 0.85f, 0.4f);
                case SignalBucket.Exceptional: return new Color(0.15f, 0.9f, 0.4f);
                default: return TableGrey;
            }
        }

        private static string EffectValue(SignalEffect effect)
        {
            float? displayedMagnitude = effect.ResolvedMagnitude ?? effect.Magnitude;
            string value;
            // Factor units render as the resulting percentage: a multiply IS the
            // factor (×0.5 -> 50%), an add offsets a base-1 factor stat
            // (-0.75 -> 25%). Non-factor units keep their literal notation.
            bool factor = effect.Unit == SignalValueUnit.Factor && displayedMagnitude.HasValue;
            switch (effect.Operation)
            {
                case SignalOperation.Add:
                    // factor implies displayedMagnitude.HasValue.
                    value = factor ? Percent(1f + displayedMagnitude!.Value) : Signed(displayedMagnitude);
                    break;
                case SignalOperation.Multiply:
                    value = factor ? Percent(displayedMagnitude!.Value) : "×" + Number(displayedMagnitude);
                    break;
                case SignalOperation.Set:
                    value = "= " + (factor
                        ? Percent(displayedMagnitude!.Value) : Number(displayedMagnitude));
                    break;
                case SignalOperation.Disable:
                    value = "WR_SignalDisabled".Translate();
                    break;
                default:
                    value = "WR_SignalDescriptive".Translate();
                    break;
            }

            if (effect.Unit != SignalValueUnit.None && effect.Unit != SignalValueUnit.Factor
                && displayedMagnitude.HasValue)
                value += " " + InvariantDefName.Humanize(
                    effect.Unit.ToString()).ToLowerInvariant();

            if (effect.ScaleKind == SignalScaleKind.ExpertiseLevel)
            {
                value += " (" + "WR_SignalExpertiseScale".Translate(
                    Number(effect.Magnitude),
                    Number(effect.CurrentScale),
                    Number(effect.ResolvedMagnitude)) + ")";
            }

            return value;
        }

        private static string ConditionDisplay(SignalCondition condition)
        {
            string text = string.IsNullOrWhiteSpace(condition.Description)
                ? condition.Key
                : condition.Description;
            const string prefix = "package:";
            if (text != null && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return ModDisplayName(text.Substring(prefix.Length));
            return text!; // non-null per SignalCondition contract; check above is defensive
        }

        private static string ModDisplayName(string packageId)
        {
            string? name = ModLister.GetModWithIdentifier(packageId)?.Name;
            if (!string.IsNullOrWhiteSpace(name)) return name!; // net472: no NotNullWhen on IsNullOrWhiteSpace
            int dot = packageId.LastIndexOf('.');
            return dot >= 0 ? packageId.Substring(dot + 1) : packageId;
        }

        private static string Percent(float value) => (value * 100f).ToString("0.#") + "%";

        private static string Signed(float? value)
        {
            if (!value.HasValue) return "?";
            return value.Value >= 0f ? "+" + Number(value) : Number(value);
        }

        private static string Number(float? value) =>
            value.HasValue ? value.Value.ToString("0.##") : "?";
    }
}
