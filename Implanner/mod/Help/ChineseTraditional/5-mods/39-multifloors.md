---
title: MultiFloors
icon: verdict-yellow.png
---
<!-- l10n:verdict -->
| **判定** | ![部分相容](verdict-yellow.png|16) 部分相容 |
| **依據** | 原始碼分析 |
| **檢查日期** | 2026-11-02 |
| **版本** | Implanner 1.2.8、MultiFloors 1.6.1.3、[工作坊 3384660931](https://steamcommunity.com/sharedfiles/filedetails/?id=3384660931) |

<!-- l10n:summary -->
自動化無法跨樓層搬運植入物或進行手術。

<!-- l10n:detail -->
規劃、追蹤與自動化都能正常運作,但醫療單只會在病患所在的樓層尋找植入物;MultiFloors 的搬運功能只對生產單有幫助,對醫療單無效。若已預留的植入物位於另一個樓層,手術會等到你把它搬過來才進行,主開關旁也會顯示警告(參見[自動化暫停時](topic:when-automation-stands-down))。若要完整使用自動化,請將植入物、手術台與病患放在同一樓層。

