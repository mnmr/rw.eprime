using Multiplayer.API;
using QualityJobs.Core;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// <summary>One-time host prompt for bills quarantined when Quality Jobs is
    /// first added to an existing save. All text is captured at construction;
    /// the render path reads only immutable dialog-owned strings and scalars.</summary>
    public sealed class Dialog_ExistingBillMigration : Window
    {
        private const float Width = 560f;
        private const float Height = 300f;
        private const float TitleHeight = 36f;
        private const float BodyHeight = 92f;
        private const float OptionHeight = 44f;
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 35f;

        // Owner: dialog. Key: language and captured bill count. Value: immutable
        // translated display strings. Dependencies: language/count at open.
        // Refresh/equality: rebuilt with each dialog instance. Teardown: window close.
        private readonly string title;
        private readonly string body;
        private readonly string optionLabel;
        private readonly string optionTip;
        private readonly string continueLabel;

        private bool enableQualityJobs = true;
        private bool resolved;

        public override Vector2 InitialSize => new Vector2(Width, Height);

        private Dialog_ExistingBillMigration(int billCount)
        {
            title = "QJ_MigrationTitle".Translate();
            body = "QJ_MigrationBody".Translate(billCount);
            optionLabel = "QJ_MigrationEnableExisting".Translate();
            optionTip = "QJ_MigrationEnableExistingTip".Translate();
            continueLabel = "QJ_MigrationContinue".Translate();

            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            draggable = true;
        }

        internal static void QueueIfNeeded(QualityJobsStore store)
        {
            if (!ExistingBillMigrationPolicy.ShouldShowDialog(
                    store.pendingExistingBillMigrationIds.Count)) return;
            if (MP.IsInMultiplayer && !MP.IsHosting) return;

            int billCount = store.pendingExistingBillMigrationIds.Count;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                if (!ReferenceEquals(QualityJobsStore.Active, store)) return;
                if (!ExistingBillMigrationPolicy.ShouldShowDialog(
                        store.pendingExistingBillMigrationIds.Count)) return;
                Find.WindowStack.Add(new Dialog_ExistingBillMigration(billCount));
            });
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, inRect.width, TitleHeight), title);
            }
            finally
            {
                Text.Font = previousFont;
            }

            float y = TitleHeight;
            Widgets.Label(new Rect(0f, y, inRect.width, BodyHeight), body);
            y += BodyHeight + 8f;

            Rect optionRect = new Rect(0f, y, inRect.width, OptionHeight);
            Widgets.CheckboxLabeled(optionRect, optionLabel, ref enableQualityJobs);
            TooltipHandler.TipRegion(optionRect, optionTip);

            Rect buttonRect = new Rect(
                inRect.width - ButtonWidth,
                inRect.height - ButtonHeight,
                ButtonWidth,
                ButtonHeight);
            if (Widgets.ButtonText(buttonRect, continueLabel))
                ResolveAndClose();
        }

        public override void PreClose()
        {
            if (resolved) return;
            resolved = true;
            Commands.ResolveExistingBillMigration(enableQualityJobs: false);
        }

        private void ResolveAndClose()
        {
            if (resolved) return;
            resolved = true;
            Commands.ResolveExistingBillMigration(enableQualityJobs);
            Close();
        }
    }
}
