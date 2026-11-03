---
title: Strata
icon: verdict-yellow.png
---
<!-- l10n:verdict -->
| **结论** | ![部分兼容](verdict-yellow.png|16) 部分兼容 |
| **依据** | 源码分析、游戏内自动化测试 |
| **检查日期** | 2026-11-02 |
| **版本** | Implanner 1.2.8,Strata 3.11.6,[创意工坊 3762844292](https://steamcommunity.com/sharedfiles/filedetails/?id=3762844292) |

<!-- l10n:summary -->
自动化无法跨层运送植入体或进行手术。

<!-- l10n:detail -->
规划、追踪和自动化均可正常使用,但医疗任务只会在患者所在的层寻找植入体;Strata 的缺料搬运只服务于生产工作台,不服务于医疗任务。如果已预留的植入体位于另一层,手术会一直等待,直到你将其移过来;总开关旁会显示相应警告(参见[自动化暂停的情况](topic:when-automation-stands-down))。要使用完整的自动化,请将植入体、手术台和患者放在同一层。

