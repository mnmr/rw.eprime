---
title: MultiFloors
icon: verdict-yellow.png
---
<!-- l10n:verdict -->
| **结论** | ![部分兼容](verdict-yellow.png|16) 部分兼容 |
| **依据** | 源码分析 |
| **检查日期** | 2026-11-02 |
| **版本** | Implanner 1.2.8,MultiFloors 1.6.1.3,[创意工坊 3384660931](https://steamcommunity.com/sharedfiles/filedetails/?id=3384660931) |

<!-- l10n:summary -->
自动化无法跨楼层运送植入体或进行手术。

<!-- l10n:detail -->
规划、追踪和自动化均可正常使用,但医疗任务只会在患者所在的楼层寻找植入体;MultiFloors 的搬运只对生产任务有帮助,对医疗任务无效。如果已预留的植入体位于另一楼层,手术会一直等待,直到你将其移过来;总开关旁会显示相应警告(参见[自动化暂停的情况](topic:when-automation-stands-down))。要使用完整的自动化,请将植入体、手术台和患者放在同一楼层。

