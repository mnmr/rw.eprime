---
title: Targets and stock caps
---
<!-- l10n:target -->
**{k:QJ_MinQualityLabel}** asks a fixed-count bill to keep trying until it produces enough items at that quality or better. A below-target item is kept, but does not use up one of the bill's remaining repetitions. Each extra attempt uses more materials. **{k:QJ_AnyQuality}** accepts every result.

<!-- l10n:repeat-modes -->
This retry behavior applies to **Do X times**. For **Do until you have X**, use the normal bill's quality range to decide what counts toward the stock target. **Do forever** continues as usual. A quality target does not itself choose a better finisher; set the finisher requirements too.

<!-- l10n:stock -->
**Stock cap** limits new work on a product on this map. Its unfinished items count together across bills and benches, but only managed bills enforce the cap. **0 means unlimited.** Finished items in storage do not count.

<!-- l10n:cap-behavior -->
At the cap, managed bills stop starting new items. Resuming or finishing existing items is still allowed, so the queue can drain. If no finisher qualifies, lowering the requirements or restoring an eligible worker can unblock the queue.
