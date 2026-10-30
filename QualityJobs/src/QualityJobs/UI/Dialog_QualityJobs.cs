using System.Collections.Generic;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// Toolbar options and help. The existing settings renderer continues to
    /// read published store settings and issue the same synchronized commands.
    public sealed class Dialog_QualityJobs : Window
    {
        private bool showingHelp;
        // Window's final queued GUI pass can arrive after PostClose, especially
        // on Waylith. Do not recreate resources that teardown just released.
        private bool closed;
        private readonly HelpTabView help = new HelpTabView(QualityJobsHelpHost.Instance);

        // Owner: window. Key: language revision. Value: tab records with cached
        // translated captions and window-owned delegates. Dependencies: language;
        // selection is read by the cached predicates. Refresh: immediate gated
        // rebuild. Equality: unchanged revision retains list/delegate identity.
        // Teardown: PostClose drops the list and releases Help's content/images.
        private List<TabRecord>? tabs;
        private int tabsLanguage = -1;

        public Dialog_QualityJobs()
        {
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1180f, Verse.UI.screenWidth - 16f),
            Mathf.Min(760f, Verse.UI.screenHeight - 16f));

        public override void PreOpen()
        {
            closed = false;
            base.PreOpen();
            help.Reset();
        }

        public override void WindowUpdate()
        {
            if (closed) return;
            base.WindowUpdate();
            UiRevision.ObserveCurrentMetrics();
            help.FlushPendingWrites();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (closed) return;
            using (GuiStateScope.Capture())
            {
                UiRevision.ObserveCurrentMetrics();
                if (tabs == null || tabsLanguage != UiRevision.LanguageCurrent)
                {
                    tabs = new List<TabRecord>
                    {
                        new TabRecord("QJ_TabOptions".Translate(), ShowOptions, OptionsSelected),
                        new TabRecord("QJ_TabHelp".Translate(), ShowHelp, HelpSelected),
                    };
                    tabsLanguage = UiRevision.LanguageCurrent;
                }
                var body = new Rect(inRect.x, inRect.y + TabStrip.TabHeight,
                    inRect.width, inRect.height - TabStrip.TabHeight - CloseButSize.y - 12f);
                Widgets.DrawMenuSection(body);
                for (int i = 0; i < tabs.Count; i++)
                    tabs[i].labelColor = i == (showingHelp ? 1 : 0)
                        ? TabStrip.ActiveLabelColor : (Color?)null;
                TabStrip.Draw(body, tabs, QualityJobsTex.TabAtlas);
                TabStrip.DrawActiveTabSeam(body, showingHelp ? 1 : 0, tabs.Count);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                body = body.ContractedBy(10f);
                if (showingHelp) help.Draw(body);
                else QualityJobsMod.Instance.DoSettingsWindowContents(body);
            }
        }

        private void ShowOptions() { showingHelp = false; StructuredTipPresenter.Reset(); }
        private void ShowHelp() { showingHelp = true; StructuredTipPresenter.Reset(); }
        private bool OptionsSelected() => !showingHelp;
        private bool HelpSelected() => showingHelp;

        public override void PostClose()
        {
            closed = true;
            try
            {
                help.FlushPendingWrites();
                QualityJobsMod.Instance.WriteSettings();
            }
            finally
            {
                help.ReleaseWindowData();
                StructuredTipPresenter.Reset();
                tabs = null;
                tabsLanguage = -1;
                base.PostClose();
            }
        }
    }
}
