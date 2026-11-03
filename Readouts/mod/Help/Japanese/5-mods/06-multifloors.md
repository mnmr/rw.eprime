---
title: MultiFloors
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **判定** | ![完全互換](verdict-blue.png|16) 完全互換(専用対応) |
| **根拠** | ソース解析、ゲーム内自動テスト |
| **確認日** | 2026-08-03 |
| **バージョン** | Readouts 1.0.4、MultiFloors 1.6.1.3、[Workshop 3384660931](https://steamcommunity.com/sharedfiles/filedetails/?id=3384660931)、files 2026-07-14 |

<!-- l10n:summary -->
同じレベルスタックのフロアは、1つのコロニーとして集計されます。

<!-- l10n:counts -->
すべてのリードアウトのカウンターは、表示中のスタックにある全フロアの資源を合算し、どのフロアでも同じ数値を表示します。しきい値、予定作業、資源プールにも同じスタック合計が使われます。

<!-- l10n:limits -->
Readouts は MultiFloors のレベル一覧を、そのMod自身のコードを通じて読み取ります。MultiFloors のアップデートでこれが変わった場合、Readouts は警告を1回ログに記録し、対応するまで各フロアを個別に集計する方式に切り替えます。

