---
title: Setting up a bill
---
<!-- l10n:open -->
Open **Details** on a bill for a quality item. The **{k:QJ_QualityPanelTitle}** panel appears beside the normal bill controls for supported recipes that leave an unfinished item. Recipes without quality or unfinished work keep their normal behavior.

<!-- l10n:bill-image -->
![A workbench bill with its Quality Jobs panel](crafting-bill.png)

<!-- l10n:setup -->
Enable **{k:QJ_ManageBill}**, then choose the [finisher requirements](topic:finishers). Ordinary workers can do most of the crafting. If the current worker qualifies, they finish directly; otherwise the nearly finished item is set aside, and the bill can prepare more work within its stock cap.

<!-- l10n:dispatch -->
When a finisher qualifies, Quality Jobs creates a temporary **Finish …** bill assigned to them. They collect the unfinished item and complete it. The temporary bill removes itself afterward. Waiting items can be hauled and stored like other unfinished items.

<!-- l10n:status -->
The **{k:QJ_StatusHeader}** section shows shared **Unfinished** work, items **Waiting** for a finisher, and items already assigned for **Finishing**. Hover the status and finisher list for more detail. See [when work waits](topic:waiting) if nothing is progressing.

<!-- l10n:disable -->
Clear **{k:QJ_ManageBill}** to let it complete normally again. Its shared work can still be continued by other workers when sharing is enabled.
