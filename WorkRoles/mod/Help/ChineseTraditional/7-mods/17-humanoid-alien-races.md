---
title: Humanoid Alien Races
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **結論** | ![完全相容](verdict-blue.png|16) 完全相容(專門支援) |
| **依據** | 原始碼分析、遊戲內自動化測試 |
| **檢查日期** | 2026-10-19 |
| **版本** | WorkRoles 1.7.1、Humanoid Alien Races [工作坊 839005762](https://steamcommunity.com/sharedfiles/filedetails/?id=839005762)、檔案 2026-10-15 |

<!-- l10n:summary -->
種族的工作限制直接從框架本身讀取。

<!-- l10n:detail -->
對於 RimWorld 1.6 和 1.7 上經過審查的 Humanoid Alien Races 版本,
建議會採用該框架實際的工作分配器與配方限制,
因此所有使用這些規則的種族都能受益。被禁用的配方
不會讓角色中其他可用的配方一併失效,
也不會被視為訓練該技能的途徑。

<!-- l10n:limits -->
未知的框架版本會沿用現有的資格判斷,並回報其規則不受支援。
隱藏在任意工作內部的限制無法被安全地偵測。
未經驗證的種族專屬規則(例如 Ratkin 的)會沿用
現有行為;不會依據種族或異種族名稱去猜測限制。

