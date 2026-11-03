---
title: Strata
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **판정** | ![완전 호환](verdict-blue.png|16) 완전 호환 (자체 지원) |
| **근거** | 소스 분석, 게임 내 자동 테스트 |
| **확인일** | 2026-10-30 |
| **버전** | Readouts 1.5.3, Strata 3.11.6, [Workshop 3762844292](https://steamcommunity.com/sharedfiles/filedetails/?id=3762844292), files 2026-10-27 |

<!-- l10n:summary -->
Strata의 자원 통합 설정에 따라 카운터가 집계하는 범위가 정해집니다.

<!-- l10n:combined -->
Strata의 자원 통합 설정이 켜져 있으면, Readouts는 화면에 보이는 층에서 닿을 수 있는 모든 층을 합산하며 모든 층이 같은 합계를 표시합니다. 칸 툴팁의 보관, 금지, 흩어진 자원 내역과 예정된 작업의 차감량도 같은 층들을 따르므로 카운터와 일치합니다.

<!-- l10n:per-level -->
설정이 꺼져 있으면 각 층에는 그 층에 있는 것만 표시됩니다.

<!-- l10n:limits -->
Readouts는 Strata의 자체 코드를 통해 해당 모드의 층 그래프를 읽습니다. Strata 업데이트로 이 구조가 바뀌면 Readouts는 경고를 한 번 기록하고, Readouts가 업데이트될 때까지 게임 기본 자원 카운터를 사용합니다.

