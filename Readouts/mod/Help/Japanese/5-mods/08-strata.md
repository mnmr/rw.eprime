---
title: Strata
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **判定** | ![完全互換](verdict-blue.png|16) 完全互換(専用対応) |
| **根拠** | ソース解析、ゲーム内自動テスト |
| **確認日** | 2026-10-30 |
| **バージョン** | Readouts 1.5.3、Strata 3.11.6、[Workshop 3762844292](https://steamcommunity.com/sharedfiles/filedetails/?id=3762844292)、files 2026-10-27 |

<!-- l10n:summary -->
Strata の「資源を統合」設定によって、カウンターの集計範囲が決まります。

<!-- l10n:combined -->
Strata の「資源を統合」設定がオンの場合、Readouts は表示中のレベルから到達できるすべてのレベルを合算し、どのレベルでも同じ合計を表示します。スロットのツールチップにある保管・禁止・散在の内訳や、予定作業による差し引きも同じレベルを対象にするため、カウンターと一致します。

<!-- l10n:per-level -->
設定がオフの場合、各レベルにはそのレベルにあるものだけが表示されます。

<!-- l10n:limits -->
Readouts は Strata のレベルグラフを、そのMod自身のコードを通じて読み取ります。Strata のアップデートでこれが変わった場合、Readouts は警告を1回ログに記録し、対応するまでゲーム標準の資源カウンターを使用します。

