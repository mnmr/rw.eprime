---
title: 옵션 및 기본값
---
<!-- l10n:options-image -->
![작업 및 건설 기본값이 있는 옵션 탭](options.png)

<!-- l10n:scope -->
저장된 게임을 불러온 상태에서는 옵션에서 해당 정착지의 설정을 변경합니다. 메인 메뉴의 모드 설정에서는 새 게임에 사용할 기본값을 지정합니다. **{k:QJ_SettingsShowToolbarButton}**는 개인 표시 설정이며, 버튼을 숨겨도 모드는 비활성화되지 않습니다. 게임의 옵션 메뉴에서 계속 모드 설정을 열 수 있습니다.

<!-- l10n:bill-defaults -->
**{k:QJ_SettingsBillDefaults}**은 기본값을 따르는 작업에 적용됩니다. 개별 작업의 설정이 기본값보다 우선합니다. **{k:QJ_SettingsManageNewBills}**는 새 작업과 이 관리 기본값을 계속 따르는 기존 작업에 적용됩니다. 이 옵션을 끄면 해당 작업의 대기 중인 물품이 해제되어 정상적으로 완성될 수 있습니다. 다른 작업 기본값도 해당 설정을 별도로 지정하지 않은 기존 작업에 영향을 줄 수 있습니다. 미완성품 보유 한도는 생산품별로 공유하며, 지도마다 따로 집계합니다.

<!-- l10n:construction-defaults -->
**{k:QJ_SettingsConstructionDefaults}**은 건설에 품질 관리를 활성화하거나 자동 관리를 적용할 때 사용됩니다. 이미 관리 중인 건물은 각자의 설정을 유지합니다. 건설 목표에는 **{k:QJ_NoRetries}**을, 제작 목표에는 **{k:QJ_AnyQuality}**를 사용합니다.

<!-- l10n:global -->
**{k:QJ_SettingsShareWork}**와 **{k:QJ_SettingsHighPriorityFinish}**는 정착지 전체에 적용됩니다. 각 기능의 효과는 [공유](topic:sharing-work)와 [마무리 작업자](topic:finishers)를 참고하세요. **{k:QJ_DisableButton}**는 개별 작업을 일시 중지하는 기능이 아니라 [안전한 모드 제거](topic:removing)를 위한 기능입니다.

<!-- l10n:robot-preparation -->
**{k:QJ_SettingsAllowRobotPreparation}**는 현재 정착지에 저장되며, 기존 저장 게임에서도 기본적으로 켜져 있습니다. 이 옵션을 끄면 관리 중인 제작 작업 지시와 건설 계획의 작업을 정착민만 수행하여 해당 작업의 기술 경험치를 얻도록 할 수 있습니다. 이미 해당 작업을 수행 중인 메카노이드와 로봇도 작업을 중단합니다. 조건에 맞는 메카노이드와 지원되는 로봇은 계속 Quality Jobs가 배정한 마무리 작업을 수행할 수 있습니다. 일반적인 비관리 작업에는 영향을 주지 않으며, [공유 미완성품](topic:sharing-work)에는 원래 관리 대상 작업 지시의 제한이 유지됩니다. 이 옵션은 저장된 게임을 불러온 상태에서 사용할 수 있으며 Multiplayer에서 동기화됩니다.

