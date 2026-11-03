---
title: MultiFloors
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **판정** | ![완전 호환](verdict-blue.png|16) 완전 호환 (자체 지원) |
| **근거** | 소스 분석, 게임 내 자동 테스트 |
| **확인일** | 2026-08-03 |
| **버전** | Readouts 1.0.4, MultiFloors 1.6.1.3, [Workshop 3384660931](https://steamcommunity.com/sharedfiles/filedetails/?id=3384660931), files 2026-07-14 |

<!-- l10n:summary -->
같은 층 묶음에 속한 층들은 하나의 정착지로 집계됩니다.

<!-- l10n:counts -->
모든 현황판 카운터는 지금 보고 있는 층 묶음의 모든 층에 있는 자원을 합산하며, 모든 층이 같은 수치를 표시합니다. 기준치, 예정된 작업, 자원 풀도 같은 묶음 합계를 사용합니다.

<!-- l10n:limits -->
Readouts는 MultiFloors의 자체 코드를 통해 해당 모드의 층 목록을 읽습니다. MultiFloors 업데이트로 이 구조가 바뀌면 Readouts는 경고를 한 번 기록하고, Readouts가 업데이트될 때까지 각 층을 따로 집계합니다.

