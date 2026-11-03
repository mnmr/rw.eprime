---
title: MultiFloors
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **結論** | ![完全相容](verdict-blue.png|16) 完全相容(專門支援) |
| **依據** | 原始碼分析、遊戲內自動化測試 |
| **檢查日期** | 2026-08-03 |
| **版本** | Readouts 1.0.4、MultiFloors 1.6.1.3、[Workshop 3384660931](https://steamcommunity.com/sharedfiles/filedetails/?id=3384660931)、檔案 2026-07-14 |

<!-- l10n:summary -->
同一層級堆疊的各樓層會視為同一個殖民地。

<!-- l10n:counts -->
每個資源讀數計數器都會加總你目前查看的樓層堆疊中所有樓層的資源,且每個樓層顯示相同的數字。門檻、計畫中的工作與資源池也使用相同的堆疊總數。

<!-- l10n:limits -->
Readouts 透過 MultiFloors 本身的程式碼讀取其樓層清單。若 MultiFloors 更新後更動了該清單,Readouts 會記錄一則警告,並改為各樓層分開計算,直到 Readouts 更新為止。

