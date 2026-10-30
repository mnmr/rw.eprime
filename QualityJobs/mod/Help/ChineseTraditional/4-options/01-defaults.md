---
title: 選項與預設值
---
<!-- l10n:options-image -->
![包含工作單與建造預設值的「選項」分頁](options.png)

<!-- l10n:scope -->
載入存檔後，「選項」會變更該殖民地的設定。從主選單開啟的模組設定則提供新存檔的預設值。**{k:QJ_SettingsShowToolbarButton}**是個人的顯示偏好；隱藏按鈕不會停用模組。你仍可從遊戲的「選項」選單開啟模組設定。

<!-- l10n:bill-defaults -->
**{k:QJ_SettingsBillDefaults}**控制採用預設值的工作單。個別工作單的設定會覆寫預設值。**{k:QJ_SettingsManageNewBills}**會影響新工作單，以及仍採用該管理預設值的現有工作單。關閉此選項後，這些工作單中等待收尾的物品便可正常完工。其他工作單預設值也可能影響未針對該項設定另行覆寫的現有工作單。未完成品庫存上限依產品共用，各地圖分別計算。

<!-- l10n:construction-defaults -->
**{k:QJ_SettingsConstructionDefaults}**會在為建築啟用管理或自動納管時套用。已受管理的建築會保留各自的設定。建造的目標預設為 **{k:QJ_NoRetries}**；製作的目標預設為 **{k:QJ_AnyQuality}**。

<!-- l10n:global -->
**{k:QJ_SettingsShareWork}**與 **{k:QJ_SettingsHighPriorityFinish}**會套用至整個殖民地。相關效果請參閱[共用](topic:sharing-work)與[收尾者](topic:finishers)。**{k:QJ_DisableButton}**是供[安全移除模組](topic:removing)使用，並非用來暫停單張工作單。

<!-- l10n:robot-preparation -->
**{k:QJ_SettingsAllowRobotPreparation}**會儲存於目前殖民地的設定中，預設為啟用，現有存檔也一樣。關閉後，受管理的製作工作指令與建造計畫的工作會保留給殖民者，讓他們獲得工作的技能經驗。這也會停止機械族與機器人正在進行的這些工作。符合資格的機械族與受支援的機器人仍可完成 Quality Jobs 指派的收尾工作。一般未受管理的工作不受影響；[共用未完成品](topic:sharing-work)仍受原始受管理工作指令的限制。此選項須載入存檔後才能使用，並會在 Multiplayer 中同步。

