---
title: Rendering fallback
---
Readouts uses cached panel images to reduce drawing work. If it detects a problem displaying them, the panel draws directly instead.

If buffered rendering is disabled after failures, the gear turns amber. Further repeated failures switch to the game's own readout until you load a game again. These fallback changes are recorded in the game log.
