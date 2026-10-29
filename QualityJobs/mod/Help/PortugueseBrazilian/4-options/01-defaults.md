---
title: Opções e padrões
---
<!-- l10n:options-image -->
![A aba Opções com os padrões de ordens de produção e construção](options.png)

<!-- l10n:scope -->
Com uma partida carregada, Opções altera as configurações daquela colônia. No menu principal, as configurações do mod fornecem os padrões para novas partidas. **{k:QJ_SettingsShowToolbarButton}** é sua preferência pessoal de exibição; ocultá-lo não desativa o mod. Você ainda pode abrir as configurações do mod pelo menu Opções do jogo.

<!-- l10n:bill-defaults -->
As **{k:QJ_SettingsBillDefaults}** controlam as ordens que seguem os padrões. As configurações individuais de cada ordem têm precedência. Portanto, alterar um padrão pode afetar ordens existentes que não foram personalizadas. O limite de estoque de itens inacabados é compartilhado por produto, em vez de ser separado para cada ordem.

<!-- l10n:construction-defaults -->
As **{k:QJ_SettingsConstructionDefaults}** são aplicadas quando o gerenciamento de uma construção é ativado ou ocorre automaticamente. Construções já gerenciadas mantêm suas próprias configurações. A qualidade desejada para construção usa **{k:QJ_NoRetries}**; a de fabricação usa **{k:QJ_AnyQuality}**.

<!-- l10n:global -->
**{k:QJ_SettingsShareWork}** e **{k:QJ_SettingsHighPriorityFinish}** se aplicam a toda a colônia. Veja seus efeitos em [compartilhamento](topic:sharing-work) e [finalizadores](topic:finishers). **{k:QJ_DisableButton}** serve para a [remoção segura](topic:removing), não para pausar uma única ordem de produção.

<!-- l10n:robot-preparation -->
**{k:QJ_SettingsAllowRobotPreparation}** é salva para a colônia atual e começa ativada, inclusive em partidas salvas existentes. Desative-a para reservar aos colonos o trabalho em ordens de fabricação e projetos de construção gerenciados, para que eles recebam a experiência de habilidade desse trabalho. Isso também interrompe o trabalho de mecanoides e robôs que já estiverem nessas tarefas. Mecanoides elegíveis e robôs compatíveis ainda podem finalizar trabalhos atribuídos pelo Quality Jobs. Trabalhos comuns não gerenciados não são afetados; [itens inacabados compartilhados](topic:sharing-work) mantêm a restrição da ordem gerenciada original. Esta opção fica disponível com uma partida carregada e é sincronizada no Multiplayer.

