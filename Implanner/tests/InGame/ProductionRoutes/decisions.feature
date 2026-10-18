Feature: EPOE production decision cache
 Background:
  Given the screen is clear
  And the save "fisso" is loaded
  And EPOE crafting mode is 0
 @timeout:55
 Scenario: Production decisions observe bench and stock transitions
  Given an EPOE advanced arm production scene with no free base arm
  When production decisions refresh across bench and stock changes
  Then the production decision cache contract holds
