---
title: Strata
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **結論** | ![完全相容](verdict-blue.png|16) 完全相容(專門支援) |
| **依據** | 原始碼分析、遊戲內自動化測試 |
| **檢查日期** | 2026-10-30 |
| **版本** | Readouts 1.5.3、Strata 3.11.6、[Workshop 3762844292](https://steamcommunity.com/sharedfiles/filedetails/?id=3762844292)、檔案 2026-10-27 |

<!-- l10n:summary -->
計數器涵蓋的範圍由 Strata 的合併資源設定決定。

<!-- l10n:combined -->
開啟 Strata 的合併資源設定後,Readouts 會加總從目前畫面上的層級可到達的所有層級,且每個層級顯示相同的總數。欄位提示中的已儲存、已禁止與散落明細,以及計畫中工作的扣除量,也都依相同的層級計算,因此與計數器一致。

<!-- l10n:per-level -->
關閉該設定後,每個層級只顯示該層級上的資源。

<!-- l10n:limits -->
Readouts 透過 Strata 本身的程式碼讀取其層級圖。若 Strata 更新後更動了該圖,Readouts 會記錄一則警告,並改用遊戲本身的資源計數器,直到 Readouts 更新為止。

