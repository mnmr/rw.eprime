Feature: EPOE production routes
 Background:
  Given the screen is clear
  And the save "fisso" is loaded
  And EPOE crafting mode is 0
 @timeout:55
 Scenario: The game consumes Implanner production bills
  Given an EPOE advanced arm production scene with no free base arm
  When a crafter works Implanner production for 2550 ticks
  Then the advanced arm was crafted using an active recipe



