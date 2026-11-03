---
title: 機械體工作分頁
icon: verdict-yellow.png
---
<!-- l10n:verdict -->
| **結論** | ![部分相容](verdict-yellow.png|16) 部分相容 |
| **依據** | 原始碼分析、遊戲內自動化測試 |
| **檢查日期** | 2026-11-02 |
| **版本** | WorkRoles 1.7.9、Mech Work Tab [Workshop 3268299107](https://steamcommunity.com/sharedfiles/filedetails/?id=3268299107)、檔案日期 2026-11-01 |

<!-- l10n:summary -->
Mech Work Tab 會繼續管理所有未指派 WorkRoles 角色的機械體。

<!-- l10n:detail -->
Mech Work Tab 這類機械體優先順序模組,可以管理沒有 WorkRoles 指派的工作者。若啟用機器人與機械體支援,WorkRoles 也會接管已指派角色的機械體工作者的工作排序,此時分頁對這些機械體的優先順序寫入會被封鎖,並顯示一次性通知。在選項中關閉支援即可讓它們全部恢復原生排序;已儲存的角色會保持休眠。

