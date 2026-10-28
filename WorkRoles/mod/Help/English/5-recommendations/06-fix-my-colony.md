---
title: Fix My Colony
---
<!-- l10n:b-77c09e98c74b -->
The button in the window's top-right corner reworks everyone at once:

<!-- l10n:b-5016dc0de5a8 -->
![The Fix My Colony button](fix-my-colony-btn.png)

<!-- l10n:b-413f172c1cb7 -->
Nothing happens blind: a preview lists every colonist's proposed row
first, with a checkbox per colonist to leave individuals out.

<!-- l10n:b-10360e9ae7c9 -->
![The Fix My Colony preview](fix-preview.png)

<!-- l10n:b-500c07033ccc -->
Apply performs exactly what the preview shows. Pinned assignments are
preserved, even when their role or a composite member is in
[Excluded roles](topic:recommendation-order). Unpinned excluded roles
are removed; blockers and rule-carrying roles are otherwise retained.

<!-- l10n:b-b836847ff6d2 -->
Colony planning is per location, so the button asks for a single
settlement or ship in the [location filter](topic:filters) when the table
currently mixes several.
