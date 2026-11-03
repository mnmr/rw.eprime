---
title: MultiFloors
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **结论** | ![完全兼容](verdict-blue.png|16) 完全兼容(定制支持) |
| **依据** | 源码分析、游戏内自动化测试 |
| **检查日期** | 2026-08-03 |
| **版本** | Readouts 1.0.4,MultiFloors 1.6.1.3,[Workshop 3384660931](https://steamcommunity.com/sharedfiles/filedetails/?id=3384660931),文件日期 2026-07-14 |

<!-- l10n:summary -->
同一层级组的各个楼层视为同一个殖民地。

<!-- l10n:counts -->
每个资源读数计数器都会汇总你当前查看的那组楼层中所有楼层的资源,且每个楼层显示的数字相同。阈值、计划工作和资源池也使用同样的整组总数。

<!-- l10n:limits -->
Readouts 通过 MultiFloors 自身的代码读取其楼层列表。如果 MultiFloors 更新后改变了该列表,Readouts 会记录一条警告,并在其更新之前退回到按各楼层单独计数。

