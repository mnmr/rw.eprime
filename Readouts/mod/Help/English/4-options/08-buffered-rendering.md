---
title: Buffered rendering
---
Buffered panel rendering draws the panel from images the mod keeps ready, so a panel that does not change costs almost nothing per frame. It is on by default and looks the same as drawing directly.

Turn it off if the panel shows up blank or garbled on your system. The panel is then drawn directly every frame, and the gear at the top of the panel turns amber so you can tell which way the panel is being drawn, even from a screenshot.

The same switch sits in the game's own mod settings for EPrime's Readouts, so you can reach it even when the panel is not visible (see [Panel position and size](topic:panel-position-and-size)).

If the panel keeps running into errors, it falls back automatically: first to direct drawing, then to the game's own readout until you load a game again. Each step is written to the game log.
