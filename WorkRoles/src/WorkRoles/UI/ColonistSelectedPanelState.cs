using System;
using System.Collections.Generic;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    internal sealed class ColonistSelectedPanelSnapshot
    {
        internal ColonistSelectedPanelSnapshot(Pawn pawn,
            ColonistSelectedChromeSnapshot chrome,
            ColonistSelectedActivitySnapshot activity,
            ColonistSelectedTraitsSnapshot traits)
        {
            Pawn = pawn;
            Chrome = chrome;
            Activity = activity;
            Traits = traits;
        }

        internal Pawn Pawn { get; }
        internal ColonistSelectedChromeSnapshot Chrome { get; }
        internal ColonistSelectedActivitySnapshot Activity { get; }
        internal ColonistSelectedTraitsSnapshot Traits { get; }
    }

    internal sealed class ColonistSelectedChromeSnapshot
    {
        internal ColonistSelectedChromeSnapshot(string label, string shownLabel,
            float nameTagWidth, float nameTagHeight, Color nameColor, bool fitName)
        {
            Label = label;
            ShownLabel = shownLabel;
            NameTagWidth = nameTagWidth;
            NameTagHeight = nameTagHeight;
            NameColor = nameColor;
            FitName = fitName;
        }

        internal string Label { get; }
        internal string ShownLabel { get; }
        internal float NameTagWidth { get; }
        internal float NameTagHeight { get; }
        internal Color NameColor { get; }
        internal bool FitName { get; }

        internal bool ContentEquals(ColonistSelectedChromeSnapshot other) =>
            other != null
            && string.Equals(Label, other.Label, StringComparison.Ordinal)
            && string.Equals(ShownLabel, other.ShownLabel, StringComparison.Ordinal)
            && NameTagWidth == other.NameTagWidth
            && NameTagHeight == other.NameTagHeight
            && FitName == other.FitName
            && NameColor.r == other.NameColor.r
            && NameColor.g == other.NameColor.g
            && NameColor.b == other.NameColor.b
            && NameColor.a == other.NameColor.a;
    }

    internal sealed class ColonistSelectedActivitySnapshot
    {
        private readonly string tipText;

        internal ColonistSelectedActivitySnapshot(bool hasRole,
            RoleChipRenderData role, float roleWidth, string label,
            string tipText, StructuredTip? tooltip)
        {
            HasRole = hasRole;
            Role = role;
            RoleWidth = roleWidth;
            Label = label;
            this.tipText = tipText;
            Tooltip = tooltip;
        }

        internal bool HasRole { get; }
        internal RoleChipRenderData Role { get; }
        internal float RoleWidth { get; }
        internal string Label { get; }
        internal StructuredTip? Tooltip { get; }

        internal bool ContentEquals(ColonistSelectedActivitySnapshot other)
        {
            if (other == null || HasRole != other.HasRole
                || RoleWidth != other.RoleWidth
                || !string.Equals(Label, other.Label, StringComparison.Ordinal)
                || !string.Equals(tipText, other.tipText,
                    StringComparison.Ordinal))
                return false;
            return !HasRole || Role.ContentEquals(other.Role);
        }
    }

    internal sealed class ColonistSelectedTraitRowSnapshot
    {
        private readonly string tipText;

        // Trait Rarity Colors' default colours by TraitRarityTiers tier.
        private static readonly Color[] RarityColors =
        {
            new Color32(0xFF, 0x00, 0x00, 0xFF), // mystic
            new Color32(0xFF, 0x99, 0x00, 0xFF), // legendary
            new Color32(0xCC, 0x00, 0x99, 0xFF), // epic
            new Color32(0x00, 0x73, 0xE6, 0xFF), // rare
            new Color32(0x00, 0xFF, 0x00, 0xFF), // uncommon
            Color.white, // common
        };

        internal ColonistSelectedTraitRowSnapshot(string label, string tipText,
            StructuredTip? tooltip, int rarityTier)
        {
            Label = label;
            this.tipText = tipText;
            Tooltip = tooltip;
            // A label that carries its own rich-text colour (Trait Rarity
            // Colors writes colour tags into the trait definitions' labels)
            // is drawn untinted; otherwise the tier colour tints it.
            bool tagged = label.IndexOf("<color", StringComparison.Ordinal) >= 0;
            Tint = tagged ? Color.white : RarityColors[rarityTier - 1];
            RichLabel = tagged ? label : "<color=#"
                + ColorUtility.ToHtmlStringRGB(Tint) + ">" + label + "</color>";
        }

        internal string Label { get; }
        /// The label carrying its colour as a rich-text tag, for the
        /// wrapped list where one tint cannot colour each trait.
        internal string RichLabel { get; }
        internal StructuredTip? Tooltip { get; }
        internal Color Tint { get; }

        internal bool ContentEquals(ColonistSelectedTraitRowSnapshot other) =>
            other != null
            && string.Equals(Label, other.Label, StringComparison.Ordinal)
            && string.Equals(tipText, other.tipText, StringComparison.Ordinal);
    }

    internal sealed class ColonistSelectedTraitsSnapshot
    {
        private readonly List<ColonistSelectedTraitRowSnapshot> rows;

        internal ColonistSelectedTraitsSnapshot(
            List<ColonistSelectedTraitRowSnapshot> rows)
        {
            this.rows = rows;
            if (rows.Count > MaxRows)
            {
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < rows.Count; i++)
                {
                    if (i > 0) text.Append(", ");
                    text.Append(rows[i].RichLabel);
                }
                WrappedLabel = text.ToString();
            }
        }

        /// Traits shown one per row; more are drawn as one wrapped text.
        internal const int MaxRows = 4;

        internal int Count => rows.Count;
        /// All traits as one comma-separated rich text, or null when they
        /// fit one per row.
        internal string? WrappedLabel { get; }
        internal ColonistSelectedTraitRowSnapshot RowAt(int index) => rows[index];

        internal bool ContentEquals(ColonistSelectedTraitsSnapshot other)
        {
            if (other == null || rows.Count != other.rows.Count) return false;
            for (int i = 0; i < rows.Count; i++)
                if (!rows[i].ContentEquals(other.rows[i])) return false;
            return true;
        }
    }

    internal sealed class ColonistSelectedPanelState
    {
        internal const float NameTagPadding = 5f;

        private readonly ActivityState activityState;

        // Owner: Colonists window. Key: RoleStore and selected Pawn reference
        // identity. Value: one immutable panel snapshot composed from separately
        // invalidated chrome, activity, and trait snapshots; producer-owned trait
        // buffers never escape. The portrait is not part of the snapshot: the
        // draw pass fetches it from vanilla's per-frame PortraitsCache.
        // Dependencies: chrome and traits use the pawn's ExternalPawnFacts
        // revision and language. Mechanical chrome retains a fitted Small-font
        // label and measured line height; human chrome preserves its original
        // full label and 20px tag. Additionally keyed by
        // UI metrics and the portrait-width name-tag clamp/padding;
        // activity uses ActivityTracker.RevisionOf(pawn),
        // RuntimeForPawn and definition revision for claiming-role changes,
        // the active role's presentation revision, language, UI metrics and slot width. Refresh:
        // immediate on the next panel
        // read after a dependency changes; an activity-only change rebuilds only
        // the activity component. Equality: equal component rebuilds preserve
        // their identity and therefore the published panel identity. Teardown:
        // Release on reset, language invalidation, window close, or owner change.
        private RoleStore? owner;
        private Pawn? pawn;
        private int externalRevision = -1;
        private int activityRevision = -1;
        private int uiRevision = -1;
        private int rolePresentationRevision = -1;
        private int metricRevision = -1;
        private int definitionRevision = -1;
        private int languageRevision = -1;
        private float portraitSize = -1f;
        private float activityWidth = -1f;
        private ColonistSelectedChromeSnapshot? chrome;
        private ColonistSelectedActivitySnapshot? activity;
        private ColonistSelectedTraitsSnapshot? traits;
        private ColonistSelectedPanelSnapshot? published;

        internal ColonistSelectedPanelState(ActivityState activityState)
        {
            this.activityState = activityState;
        }

        internal ColonistSelectedPanelSnapshot? Snapshot(RoleStore store,
            Pawn selected, ColonistsRosterCatalogSnapshot catalog,
            float portraitSize, float activityWidth)
        {
            if (store == null || selected == null || catalog == null)
            {
                Release();
                return null;
            }

            bool ownerChanged = !ReferenceEquals(owner, store);
            bool pawnChanged = !ReferenceEquals(pawn, selected);
            int nextExternal = ExternalPawnFacts.Revisions.RevisionOf(selected);
            int nextActivity = ActivityTracker.RevisionOf(selected);
            int nextUi = UiVersion.RuntimeForPawn(selected);
            int nextRolePresentation = UiVersion.PresentationForRole(
                activityState.For(selected).RoleId);
            int nextDefinition = DefinitionReloadCoordinator.Revision;
            int nextLanguage = LanguageChangeCoordinator.Revision;

            ColonistSelectedChromeSnapshot? nextChrome = chrome;
            if (ownerChanged || pawnChanged || externalRevision != nextExternal
                || languageRevision != nextLanguage
                || metricRevision != RoleChipUI.MetricRevision
                || this.portraitSize != portraitSize)
            {
                nextChrome = BuildChrome(selected, portraitSize);
                if (!ownerChanged && !pawnChanged
                    && chrome != null && chrome.ContentEquals(nextChrome))
                    nextChrome = chrome;
            }

            ColonistSelectedTraitsSnapshot? nextTraits = traits;
            if (ownerChanged || pawnChanged || externalRevision != nextExternal
                || languageRevision != nextLanguage)
            {
                nextTraits = BuildTraits(selected);
                if (!ownerChanged && !pawnChanged
                    && traits != null && traits.ContentEquals(nextTraits))
                    nextTraits = traits;
            }

            ColonistSelectedActivitySnapshot? nextActivitySnapshot = activity;
            if (ownerChanged || pawnChanged || activityRevision != nextActivity
                || uiRevision != nextUi || languageRevision != nextLanguage
                || definitionRevision != nextDefinition
                || rolePresentationRevision != nextRolePresentation
                || metricRevision != RoleChipUI.MetricRevision
                || this.activityWidth != activityWidth)
            {
                nextActivitySnapshot = BuildActivity(store, selected, catalog,
                    nextActivity, nextUi, nextDefinition, activityWidth);
                if (!ownerChanged && !pawnChanged && activity != null
                    && activity.ContentEquals(nextActivitySnapshot))
                    nextActivitySnapshot = activity;
            }

            if (ownerChanged || pawnChanged || published == null
                || !ReferenceEquals(chrome, nextChrome)
                || !ReferenceEquals(activity, nextActivitySnapshot)
                || !ReferenceEquals(traits, nextTraits))
                // A null cached component forces its rebuild branch above, so
                // every next* is non-null here.
                published = new ColonistSelectedPanelSnapshot(selected,
                    nextChrome!, nextActivitySnapshot!, nextTraits!);

            owner = store;
            pawn = selected;
            externalRevision = nextExternal;
            activityRevision = nextActivity;
            uiRevision = nextUi;
            rolePresentationRevision = nextRolePresentation;
            metricRevision = RoleChipUI.MetricRevision;
            definitionRevision = nextDefinition;
            languageRevision = nextLanguage;
            this.portraitSize = portraitSize;
            this.activityWidth = activityWidth;
            chrome = nextChrome;
            activity = nextActivitySnapshot;
            traits = nextTraits;
            return published;
        }

        internal void Release()
        {
            owner = null;
            pawn = null;
            externalRevision = -1;
            activityRevision = -1;
            uiRevision = -1;
            definitionRevision = -1;
            languageRevision = -1;
            portraitSize = -1f;
            activityWidth = -1f;
            chrome = null;
            activity = null;
            traits = null;
            published = null;
        }

        private static ColonistSelectedChromeSnapshot BuildChrome(Pawn pawn,
            float portraitSize)
        {
            string label = pawn.LabelShortCap;
            bool fitName = MechanicalWorkers.IsMechanical(pawn);
            GameFont previousFont = Text.Font;
            string shownLabel;
            float nameTagWidth, nameTagHeight;
            try
            {
                Text.Font = GameFont.Small;
                nameTagWidth = Mathf.Clamp(
                    WrText.FitWidth(label) + 2f * NameTagPadding,
                    40f, portraitSize);
                shownLabel = fitName ? WrText.Truncate(label,
                    nameTagWidth - 2f * NameTagPadding) : label;
                // Retain the established human name-tag geometry exactly.
                nameTagHeight = fitName ? Mathf.Ceil(Text.LineHeight) : 20f;
            }
            finally
            {
                Text.Font = previousFont;
            }
            return new ColonistSelectedChromeSnapshot(
                label, shownLabel, nameTagWidth, nameTagHeight,
                pawn.IsSlave
                    ? PawnNameColorUtility.PawnNameColorOf(pawn)
                    : Color.white, fitName);
        }

        private ColonistSelectedActivitySnapshot BuildActivity(RoleStore store,
            Pawn pawn, ColonistsRosterCatalogSnapshot catalog,
            int activityRevision, int uiRevision, int definitionRevision,
            float slotWidth)
        {
            ActivitySnapshot resolved = activityState.For(pawn,
                activityRevision, uiRevision, definitionRevision, store);
            RoleChipRenderData role = default;
            bool hasRole = resolved.RoleId >= 0
                && catalog.TryGetChip(resolved.RoleId,
                    out role);
            float width = 0f;
            string label;
            if (hasRole)
            {
                GameFont previousFont = Text.Font;
                try
                {
                    Text.Font = GameFont.Small;
                    width = Mathf.Min(RoleChipUI.WidthFor(role,
                        showRemove: false), slotWidth);
                }
                finally
                {
                    Text.Font = previousFont;
                }
                label = role.Label;
            }
            else
                label = resolved.Label ?? "";

            // The report string can drift during one job without an event-backed
            // revision. Keep the published tooltip at the activity granularity
            // whose complete dependencies ActivityTracker and UiVersion cover.
            string tipText = label;
            StructuredTip? tip = BuildTextTip(
                "activity:" + pawn.thingIDNumber, tipText);
            return new ColonistSelectedActivitySnapshot(hasRole, role, width,
                label, tipText, tip);
        }

        private static ColonistSelectedTraitsSnapshot BuildTraits(Pawn pawn)
        {
            var rows = new List<ColonistSelectedTraitRowSnapshot>();
            List<Trait>? source = pawn.story?.traits?.allTraits;
            if (source == null) return new ColonistSelectedTraitsSnapshot(rows);
            for (int i = 0; i < source.Count; i++)
            {
                Trait trait = source[i];
                if (trait == null || trait.Suppressed) continue;
                string text = trait.TipString(pawn) ?? "";
                string key = "trait:" + pawn.thingIDNumber + ":"
                    + trait.def.defName + ":" + trait.Degree;
                rows.Add(new ColonistSelectedTraitRowSnapshot(trait.LabelCap,
                    text, BuildTextTip(key, text),
                    Core.TraitRarityTiers.Tier(Core.TraitRarityTiers.Commonality(
                        trait.def.GetGenderSpecificCommonality(Gender.None),
                        trait.def.GetGenderSpecificCommonality(Gender.Male),
                        trait.def.GetGenderSpecificCommonality(Gender.Female)))));
            }
            return new ColonistSelectedTraitsSnapshot(rows);
        }

        private static StructuredTip? BuildTextTip(string stableKey, string text)
        {
            if (text.NullOrEmpty()) return null;
            var model = new TipModel();
            model.AddSection().Text(text);
            return new StructuredTip(stableKey, model);
        }
    }
}
