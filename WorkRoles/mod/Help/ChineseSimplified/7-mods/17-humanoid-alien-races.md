---
title: Humanoid Alien Races
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **结论** | ![完全兼容](verdict-blue.png|16) 完全兼容(定制支持) |
| **依据** | 源码分析、游戏内自动化测试 |
| **检查日期** | 2026-10-19 |
| **版本** | WorkRoles 1.7.1,Humanoid Alien Races [创意工坊 839005762](https://steamcommunity.com/sharedfiles/filedetails/?id=839005762),文件日期 2026-10-15 |

<!-- l10n:summary -->
种族的工作限制直接从该框架中读取。

<!-- l10n:detail -->
对于 RimWorld 1.6 和 1.7 上经过审核的 Humanoid Alien Races 版本,推荐会采用该框架实际的工作分配器与配方限制,因此所有使用这些规则的种族都能受益。被禁用的配方不会让角色中其他可用的配方失效,也不会被视为训练该技能的途径。

<!-- l10n:limits -->
未知的框架版本会保持现有的资格判断,并提示其规则不受支持。隐藏在任意工作内部的限制无法被安全地检测。未经验证的种族专属规则(例如鼠人 Ratkin 的规则)会保持现有行为;不会根据种族或异种名称去猜测限制。

