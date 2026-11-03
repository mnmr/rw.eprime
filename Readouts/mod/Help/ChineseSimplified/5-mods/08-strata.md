---
title: Strata
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **结论** | ![完全兼容](verdict-blue.png|16) 完全兼容(定制支持) |
| **依据** | 源码分析、游戏内自动化测试 |
| **检查日期** | 2026-10-30 |
| **版本** | Readouts 1.5.3,Strata 3.11.6,[Workshop 3762844292](https://steamcommunity.com/sharedfiles/filedetails/?id=3762844292),文件日期 2026-10-27 |

<!-- l10n:summary -->
Strata 的合并资源设置决定计数器的统计范围。

<!-- l10n:combined -->
开启 Strata 的合并资源设置后,Readouts 会汇总从当前画面所在层级可到达的所有层级,且每个层级显示的总数相同。资源格提示中的已存放、已禁止和散落明细,以及计划工作的扣减,都遵循同样的层级范围,因此与计数器一致。

<!-- l10n:per-level -->
关闭该设置后,每个层级只显示本层的内容。

<!-- l10n:limits -->
Readouts 通过 Strata 自身的代码读取其层级图。如果 Strata 更新后改变了它,Readouts 会记录一条警告,并在其更新之前使用游戏自带的资源计数器。

