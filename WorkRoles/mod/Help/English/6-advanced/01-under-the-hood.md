---
title: Under the hood
---
Each colonist's ordered roles compile into one strict job order, fed to
the game through the same lists vanilla job selection already reads. Pawn
AI behaves exactly as it would with a hand-tuned priority grid, no tick
patches, no per-frame work.

Everything recomputes when assignments or roles change, then sits in a
cache. Conditional roles additionally refresh at hour boundaries and when
a colonist changes location. Emergency jobs like firefighting go through
the game's emergency work pass when one of the colonist's first five
enabled roles covers them; the rest run in their normal position. With
"Only the first 5 roles interrupt for emergencies" off, every covered
emergency job uses that pass.

WorkRoles keeps vanilla's priority storage populated from your roles
(mapped onto the familiar 0 to 4 range while the vanilla-range option is
on, the default), which is why removing the mod is safe: the vanilla
Work tab returns, populated with your roles converted to priorities.
Adding the mod to an existing save converts the other way.
