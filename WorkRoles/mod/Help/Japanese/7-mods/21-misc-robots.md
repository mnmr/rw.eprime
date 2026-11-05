---
title: Misc. Robots と Misc. Robots++
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **判定** | ![完全互換](verdict-blue.png|16) 完全互換(専用対応) |
| **根拠** | ソース解析、ゲーム内自動テスト |
| **確認日** | 2026-11-05 |
| **バージョン** | WorkRoles 1.8.0、Misc. Robots 1.7.0、[Workshop 724602224](https://steamcommunity.com/sharedfiles/filedetails/?id=724602224)、ファイル 2026-10-28、Misc. Robots++ [Workshop 3253308913](https://steamcommunity.com/sharedfiles/filedetails/?id=3253308913)、ファイル 2026-10-28 |

<!-- l10n:summary -->
ロボットも入植者と同様にロールを持てます。

<!-- l10n:mechanical-workers -->
**ロボットとメカを入植者に表示**はセーブごとのオプションで、初期状態ではオフです。
オンにすると、Biotech のメカ、Misc. Robots、Misc. Robots++ が既存の表に追加されます。
ロールはそこで割り当て、制御グループ、エリア、起動、充電にはそれぞれ標準のタブを使います。
ロールが並べるのは、そのワーカーが実際に実行できる作業だけです。人型のアンドロイドは、
通常の入植者のルールがそのまま適用されます。

<!-- l10n:declared-work -->
ロールを持つロボットは、モデルが対応するすべての作業タイプを実行できます。モデル自体では無効になっている作業も含まれます。Researcher ロールを持つ Misc. Robots++ の Crafter または Omni ボットは、研究とスキャナーの操作を行います。炎を叩き消せるロボットなら、モデルを問わず Firefighter ロールも持てます。

<!-- l10n:recommendations -->
推奨は最も適合するものが優先されるため、単機能のロボットには Grunt ではなく
Hauler や Cleaner が割り当てられ、適性の星も入植者と同じくスキルに基づきます。

<!-- l10n:mechanical-recharge -->
充電はこれまでどおりの標準動作で、充電ロールは不要です。Biotech では各制御グループで
充電の上限を設定できます。Misc. Robots は独自の自動帰還しきい値を使い、ロボットタブの
充電率をクリックすると即時充電を要求できます。WorkRoles はこれらのしきい値を変更しません。

