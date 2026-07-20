using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Signals;
using PawnSignal = WorkRoles.Core.Signals.Signal;

namespace WorkRoles.Signals
{
    internal static class SkillSignalPresentation
    {
        private static readonly Dictionary<string, Texture2D> IconCache =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly HashSet<string> WarnedOfficialIcons =
            new HashSet<string>(StringComparer.Ordinal);

        // Mirrors the skills grid's low-skill grey.
        private static readonly Color TableGrey = new Color(0.65f, 0.65f, 0.65f);
        // Passive rows: dimmer than active, brighter than descriptions.
        private static readonly Color PassiveGrey = new Color(0.8f, 0.8f, 0.8f);

        internal static List<Texture2D> ResolveIcons(SkillSignalView view)
        {
            var result = new List<Texture2D>(view.IconCandidates.Count);
            foreach (PawnSignal signal in view.IconCandidates)
            {
                Texture2D texture = ResolveIcon(signal.Ui.IconKey);
                if (texture != null)
                    result.Add(texture);
                else
                    WarnOfficialMissing(signal, signal.Ui.IconKey);
            }

            foreach (PawnSignal signal in view.OfficialMissingIcons)
                WarnOfficialMissing(signal, null);
            return result;
        }

        private static Texture2D ResolveIcon(string iconKey)
        {
            if (string.IsNullOrWhiteSpace(iconKey)) return null;
            if (!IconCache.TryGetValue(iconKey, out Texture2D texture))
            {
                texture = ContentFinder<Texture2D>.Get(iconKey, false);
                IconCache[iconKey] = texture;
            }
            return texture;
        }

        internal static StructuredTip CreateTooltip(
            Pawn pawn,
            string skillDefName,
            string skillLabel,
            string valueText,
            Color valueColor,
            SkillSignalView view,
            SignalBucket? bucket)
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

            table.Gap(UI.WrTipUI.TableInset);
            table.Columns(new[]
            {
                "WR_SignalColSignal".Translate().ToString(),
                "WR_SignalColEffects".Translate().ToString(),
                "WR_SignalCondition".Translate().ToString(),
                "WR_SignalSource".Translate().ToString(),
            }, TableGrey);
            table.Rule();
            foreach (PawnSignal signal in view.ActiveSignals)
                AddSignalRows(table, signal, null, pawn);
            foreach (PawnSignal signal in view.PassiveSignals)
                AddSignalRows(table, signal, PassiveGrey, pawn);

            return new StructuredTip(
                $"skill-signal:{pawn.thingIDNumber}:{skillDefName}", model);
        }

        private static void AddSignalRows(TipSection table, PawnSignal signal, Color? rowColor, Pawn pawn)
        {
            string name = string.IsNullOrWhiteSpace(signal.Ui.Label)
                ? Humanize(signal.Source.DefName)
                : signal.Ui.Label;
            if (signal.Relation == SignalRelation.Spillover)
                name += " " + "WR_SignalFromSkill".Translate(Humanize(signal.OriginSkillDefName));
            Texture2D icon = ResolveIcon(signal.Ui.IconKey);
            string source = signal.Ui.SourceDisplayName;

            if (signal.Effects.Count == 0)
            {
                table.Columns(new[] { name, null, null, source }, rowColor, icon);
            }
            else
            {
                for (int i = 0; i < signal.Effects.Count; i++)
                {
                    SignalEffect effect = signal.Effects[i];
                    table.Columns(i == 0
                        ? new[] { name, EffectText(effect), ConditionText(effect), source }
                        : new[] { null, EffectText(effect), ConditionText(effect), null },
                        rowColor,
                        i == 0 ? icon : null,
                        tight: i > 0);
                }
            }

            if (!string.IsNullOrWhiteSpace(signal.Ui.Description))
                table.Span(ResolveDescription(signal.Ui.Description, pawn), alignColumn: 1);
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

        /// Generic stat modifiers surface the stat itself; named kinds carry
        /// enough meaning on their own now that targets are no longer shown.
        private static string EffectText(SignalEffect effect)
        {
            string label = effect.Kind == SignalEffectKind.StatModifier
                && !string.IsNullOrWhiteSpace(effect.TargetDefName)
                ? Humanize(effect.TargetDefName)
                : Humanize(effect.Kind.ToString());
            return label + " " + EffectValue(effect);
        }

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
                    value = factor ? Percent(1f + displayedMagnitude.Value) : Signed(displayedMagnitude);
                    break;
                case SignalOperation.Multiply:
                    value = factor ? Percent(displayedMagnitude.Value) : "×" + Number(displayedMagnitude);
                    break;
                case SignalOperation.Set:
                    value = "= " + Number(displayedMagnitude);
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
                value += " " + Humanize(effect.Unit.ToString()).ToLowerInvariant();

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
            return text;
        }

        private static string ModDisplayName(string packageId)
        {
            string name = ModLister.GetModWithIdentifier(packageId)?.Name;
            if (!string.IsNullOrWhiteSpace(name)) return name;
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

        private static string Humanize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "?";
            var result = new StringBuilder(value.Length + 8);
            char previous = '\0';
            foreach (char current in value)
            {
                if (current == '_' || current == '-')
                {
                    if (result.Length > 0 && result[result.Length - 1] != ' ')
                        result.Append(' ');
                }
                else
                {
                    if (result.Length > 0 && char.IsUpper(current)
                        && (char.IsLower(previous) || char.IsDigit(previous)))
                        result.Append(' ');
                    result.Append(current);
                }
                previous = current;
            }
            return result.ToString();
        }

        private static void WarnOfficialMissing(PawnSignal signal, string iconKey)
        {
            if (!Prefs.DevMode || !SignalPresentationPolicy.IsOfficialPackage(signal.Source.PackageId))
                return;
            string identity = signal.Source.PackageId + ":" + signal.Source.Kind + ":" + signal.Source.DefName;
            if (!WarnedOfficialIcons.Add(identity)) return;
            string detail = string.IsNullOrWhiteSpace(iconKey)
                ? "has no icon"
                : "has unresolved icon '" + iconKey + "'";
            Log.Warning("[WorkRoles] official signal " + identity + " " + detail
                + "; add an icon or choose another decorator before shipping.");
        }
    }
}
