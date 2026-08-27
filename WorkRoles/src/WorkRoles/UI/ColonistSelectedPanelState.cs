using System;
using System.Collections.Generic;
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
        internal ColonistSelectedChromeSnapshot(Texture portrait, string label,
            float nameTagWidth, Color nameColor)
        {
            Portrait = portrait;
            Label = label;
            NameTagWidth = nameTagWidth;
            NameColor = nameColor;
        }

        internal Texture Portrait { get; }
        internal string Label { get; }
        internal float NameTagWidth { get; }
        internal Color NameColor { get; }

        internal bool ContentEquals(ColonistSelectedChromeSnapshot other) =>
            other != null && ReferenceEquals(Portrait, other.Portrait)
            && string.Equals(Label, other.Label, StringComparison.Ordinal)
            && NameTagWidth == other.NameTagWidth
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

        internal ColonistSelectedTraitRowSnapshot(string label, string tipText,
            StructuredTip? tooltip)
        {
            Label = label;
            this.tipText = tipText;
            Tooltip = tooltip;
        }

        internal string Label { get; }
        internal StructuredTip? Tooltip { get; }

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
        }

        internal int Count => rows.Count;
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
        private readonly ActivityState activityState;

        // Owner: Colonists window. Key: RoleStore and selected Pawn reference
        // identity. Value: one immutable panel snapshot composed from separately
        // invalidated chrome, activity, and trait snapshots; producer-owned trait
        // buffers never escape, while portrait textures are stable game-owned
        // assets that this cache never mutates or releases. Dependencies: chrome
        // and traits use the pawn's ExternalPawnFacts revision (plus language and
        // portrait dimensions); activity uses ActivityTracker.RevisionOf(pawn),
        // UiVersion.Current and definition revision for claiming-role changes,
        // the shared detached role catalog, language, and slot width. Refresh:
        // immediate on the next panel
        // read after a dependency changes; an activity-only change rebuilds only
        // the activity component. Equality: equal component rebuilds preserve
        // their identity and therefore the published panel identity. Teardown:
        // Release on reset, language invalidation, window close, or owner change.
        private RoleStore? owner;
        private Pawn? pawn;
        private ColonistsRosterCatalogSnapshot? activityCatalog;
        private int externalRevision = -1;
        private int activityRevision = -1;
        private int uiRevision = -1;
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
            int nextUi = UiVersion.Current;
            int nextDefinition = DefinitionReloadCoordinator.Revision;
            int nextLanguage = LanguageChangeCoordinator.Revision;

            ColonistSelectedChromeSnapshot? nextChrome = chrome;
            if (ownerChanged || pawnChanged || externalRevision != nextExternal
                || languageRevision != nextLanguage
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
                || !ReferenceEquals(activityCatalog, catalog)
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
            activityCatalog = catalog;
            externalRevision = nextExternal;
            activityRevision = nextActivity;
            uiRevision = nextUi;
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
            activityCatalog = null;
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
            GameFont previousFont = Text.Font;
            float nameTagWidth;
            try
            {
                Text.Font = GameFont.Small;
                nameTagWidth = Mathf.Clamp(WrText.FitWidth(label) + 10f,
                    40f, portraitSize);
            }
            finally
            {
                Text.Font = previousFont;
            }
            return new ColonistSelectedChromeSnapshot(
                PortraitsCache.Get(pawn,
                    new Vector2(portraitSize, portraitSize), Rot4.South),
                label, nameTagWidth,
                pawn.IsSlave
                    ? PawnNameColorUtility.PawnNameColorOf(pawn)
                    : Color.white);
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
                    text, BuildTextTip(key, text)));
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
