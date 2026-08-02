using HarmonyLib;
using QualityJobs.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs
{
    public class QualityJobsMod : Mod
    {
        // Initialized by the Mod constructor before any game code can run.
        // RimWorld constructs Mod subclasses during the earliest loading phase,
        // so Settings is always non-null by the time patches or game components execute.
        public static QualityJobsSettings Settings = null!;

        public QualityJobsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<QualityJobsSettings>();
            new Harmony("EPrime.QualityJobs").PatchAll();
        }

        public override string SettingsCategory() => "EPrime's Quality Jobs";

        // Header panel height and gap constants (mirroring Dialog_ReadoutConfig idiom).
        private const float PanelH = 56f;
        private const float PanelGap = 8f;

        // EprStyle color values replicated from EPrimeReadouts\src\EPrimeReadouts\UI\EprStyle.cs
        // (lines 29-31). Do not reference the other mod; values copied verbatim.
        private static readonly Color PanelBackground = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        private static readonly Color PanelOutline    = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color HeaderText      = new Color(0.85f, 0.85f, 0.85f);

        public override void DoSettingsWindowContents(Rect inRect)
        {
            SettingsLabels.Ensure();

            // ── Header panel ─────────────────────────────────────────────────────────
            // Full-width, PanelH tall, drawn above the listing body.
            var panelRect = new Rect(inRect.x, inRect.y, inRect.width, PanelH);
            Widgets.DrawBoxSolidWithOutline(panelRect, PanelBackground, PanelOutline);

            // Mod icon — 40x40 at 8px left padding, vertically centred.
            var iconRect = new Rect(panelRect.x + 8f, panelRect.y + 8f, 40f, 40f);
            GUI.DrawTexture(iconRect, QualityJobsTex.ModIcon);

            // Title — Small font, MiddleLeft, header-text color.
            GameFont prevFont   = Text.Font;
            TextAnchor prevAnch = Text.Anchor;
            Color prevColor     = GUI.color;
            Text.Font   = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color   = HeaderText;
            Widgets.Label(
                new Rect(iconRect.xMax + 8f, panelRect.y,
                    panelRect.width - iconRect.xMax - 8f - 158f, PanelH),
                SettingsLabels.Title!);
            GUI.color   = prevColor;
            Text.Anchor = prevAnch;
            Text.Font   = prevFont;

            // Right-side button: Disable / Enable (only when a game is loaded).
            // Multi-pass safety: Widgets.ButtonText returns true only on the
            // MouseUp event pass; dialog open is therefore per-click, not per-frame.
            if (Current.Game != null)
            {
                float btnY   = panelRect.y + (PanelH - 28f) / 2f;
                var   btnRect = new Rect(panelRect.xMax - 158f, btnY, 150f, 28f);
                QualityJobsStore? store = QualityJobsStore.Active;
                if (store != null)
                {
                    // Disable button: opens confirmation dialog.
                    if (Widgets.ButtonText(btnRect, SettingsLabels.DisableButton!))
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            SettingsLabels.DisableWarning!,
                            Commands.Disable));
                    }
                    TooltipHandler.TipRegion(btnRect, SettingsLabels.EnabledNote!);
                }
                else
                {
                    // Enable button: seeds store from current defaults.
                    if (Widgets.ButtonText(btnRect, SettingsLabels.EnableButton!))
                    {
                        Commands.RequestEnable();
                    }
                    TooltipHandler.TipRegion(btnRect, SettingsLabels.EnabledNote!);
                }
            }

            // ── Body listing (below panel + gap) ──────────────────────────────────
            var bodyRect = new Rect(inRect.x, inRect.y + PanelH + PanelGap,
                inRect.width, inRect.height - PanelH - PanelGap);

            var listing = new Listing_Standard();
            listing.Begin(bodyRect);

            // I4: constant labels are cached by language; interpolated labels by value.

            // ── Defaults for new saves section ───────────────────────────────────
            // MiniHeader: group-relative x=0 (listing.Begin opened a GUI group).
            float headerY = listing.CurHeight;
            QjUi.MiniHeader(0f, headerY, bodyRect.width, SettingsLabels.Defaults!);
            // Advance listing past the 30f consumed by MiniHeader.
            listing.GetRect(30f);

            listing.CheckboxLabeled(SettingsLabels.ManageNewBills!, ref Settings.defaultManageNewBills);

            // I4: rebuild interpolated min-skill label only when the displayed value changes.
            if (Settings.defaultMinSkill != SettingsLabels.MinSkillValue)
            {
                SettingsLabels.MinSkillLabel = "QJ_FinisherSkill".Translate(Settings.defaultMinSkill);
                SettingsLabels.MinSkillValue = Settings.defaultMinSkill;
            }
            Settings.defaultMinSkill = (int)listing.SliderLabeled(
                SettingsLabels.MinSkillLabel!, Settings.defaultMinSkill, 0f, 20f);

            listing.CheckboxLabeled(SettingsLabels.RequireInspired!, ref Settings.defaultRequireInspired);
            if (ModsConfig.IdeologyActive)
                listing.CheckboxLabeled(SettingsLabels.RequireSpecialist!, ref Settings.defaultRequireSpecialist);

            // I4: rebuild interpolated cap label only when the displayed value changes.
            if (Settings.defaultProductCap != SettingsLabels.DefaultCapValue)
            {
                SettingsLabels.DefaultCapLabel = SettingsLabels.DefaultCapPrefix! + ": " + Settings.defaultProductCap;
                SettingsLabels.DefaultCapValue = Settings.defaultProductCap;
            }
            Settings.defaultProductCap = (int)listing.SliderLabeled(
                SettingsLabels.DefaultCapLabel!, Settings.defaultProductCap, 0f, 50f);

            // Share/notification toggles: show only when no game is loaded.
            // When a game is loaded, the per-save (store-backed) versions render below.
            if (Current.Game == null)
            {
                listing.CheckboxLabeled(SettingsLabels.ShareWork!, ref Settings.defaultShareUnfinishedWork);
                listing.CheckboxLabeled(SettingsLabels.DispatchLetter!, ref Settings.dispatchLetter);
            }

            listing.GapLine();

            // ── Per-save section ─────────────────────────────────────────────────
            if (Current.Game == null)
            {
                listing.Label(SettingsLabels.NoGameLoaded!);
            }
            else
            {
                QualityJobsStore? store = QualityJobsStore.Active;
                if (store != null)
                {
                    // Per-save share/notification toggles (synced).
                    bool share = store.shareUnfinishedWork;
                    listing.CheckboxLabeled(SettingsLabels.ShareWork!, ref share);
                    if (share != store.shareUnfinishedWork)
                        Commands.SetShareUnfinishedWork(share);

                    bool letter = store.dispatchLetter;
                    listing.CheckboxLabeled(SettingsLabels.DispatchLetter!, ref letter);
                    if (letter != store.dispatchLetter)
                        Commands.SetDispatchLetter(letter);
                }
                // If store is null the mod is disabled for this save;
                // the Enable button in the header panel handles re-enabling.
            }

            listing.End();
        }

        /// I4: language-keyed label cache for the settings window. Constant labels
        /// are rebuilt when the active language changes; interpolated labels are
        /// keyed by value and rebuilt only when the displayed value changes.
        ///
        /// Owner: process. Key: LanguageDatabase.activeLanguage (constant labels);
        /// value integer (interpolated labels). Dependencies: language change;
        /// value change. Teardown: none (process-static, language-safe).
        private static class SettingsLabels
        {
            private static LoadedLanguage? _builtForLanguage;

            // Constant labels (rebuilt on language change).
            public static string? Title;
            public static string? Defaults;
            public static string? ManageNewBills;
            public static string? RequireInspired;
            public static string? RequireSpecialist;
            public static string? DefaultCapPrefix;
            public static string? ShareWork;
            public static string? DispatchLetter;
            public static string? NoGameLoaded;
            public static string? EnabledNote;
            public static string? DisableWarning;
            public static string? DisableButton;
            public static string? EnableButton;

            // Interpolated labels (rebuilt when value changes).
            public static string? MinSkillLabel;
            public static int MinSkillValue = -1;
            public static string? DefaultCapLabel;
            public static int DefaultCapValue = -1;

            public static void Ensure()
            {
                if (LanguageDatabase.activeLanguage == _builtForLanguage) return;
                _builtForLanguage = LanguageDatabase.activeLanguage;
                Title          = "EPrime's Quality Jobs"; // SettingsCategory() string — not translated
                Defaults       = "QJ_SettingsDefaults".Translate();
                ManageNewBills = "QJ_SettingsManageNewBills".Translate();
                RequireInspired   = "QJ_RequireInspired".Translate();
                RequireSpecialist = "QJ_RequireSpecialist".Translate();
                DefaultCapPrefix  = "QJ_SettingsDefaultCap".Translate();
                ShareWork      = "QJ_SettingsShareWork".Translate();
                DispatchLetter = "QJ_SettingsDispatchLetter".Translate();
                NoGameLoaded   = "QJ_NoGameLoaded".Translate();
                EnabledNote    = "QJ_SettingsEnabledNote".Translate();
                DisableWarning = "QJ_DisableWarning".Translate();
                DisableButton  = "QJ_DisableButton".Translate();
                EnableButton   = "QJ_EnableButton".Translate();
                // Force rebuild of interpolated labels at new language.
                MinSkillValue  = -1;
                DefaultCapValue = -1;
            }
        }
    }
}
