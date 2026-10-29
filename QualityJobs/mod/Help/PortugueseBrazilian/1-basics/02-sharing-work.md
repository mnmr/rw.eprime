---
title: Compartilhamento de trabalhos inacabados
---
<!-- l10n:sharing -->
Com **{k:QJ_SettingsShareWork}** ativado nas Opções, outro trabalhador apto pode continuar um item inacabado que esteja parado, mesmo que seu criador original tenha morrido. O compartilhamento funciona independentemente de **{k:QJ_ManageBill}**.

<!-- l10n:matching -->
O próximo trabalhador precisa de uma ordem de produção utilizável para a mesma receita. Os ingredientes do item devem ser permitidos por essa ordem, e o estilo deve corresponder. O item também precisa estar acessível, permitido e disponível para reserva. O compartilhamento não ignora as atribuições normais de trabalho nem as restrições das ordens de produção.

<!-- l10n:handoff -->
Um item inativo é desvinculado do trabalhador e da ordem de produção originais para que uma ordem compatível possa retomá-lo. Enquanto o item inacabado dessa ordem aguarda ou é transportado, ela não inicia outro igual. Um trabalho em andamento não é interrompido apenas para passá-lo a outra pessoa.

<!-- l10n:reserved -->
Compartilhar as etapas iniciais do trabalho é diferente de reservar o toque final. Um item gerenciado pausado pouco antes da conclusão exibe **Autor: reservado (Quality Jobs)**. Ele aguarda um [finalizador qualificado](topic:finishers); um trabalhador comum não pode concluí-lo por meio do compartilhamento.

<!-- l10n:robot-work -->
Quando **{k:QJ_SettingsAllowRobotPreparation}** está desativado, um item inacabado compartilhado permanece reservado para o trabalho de colonos enquanto sua ordem de produção original existir e for gerenciada. Retomar o item por meio de uma ordem não gerenciada não permite que um mecanoide ou robô assuma esse trabalho. Mecanoides e robôs aptos ainda podem executar tarefas de finalização atribuídas pelo Quality Jobs.

<!-- l10n:off -->
Desative o compartilhamento para que os trabalhos compartilhados voltem a pertencer a um único trabalhador. As ordens gerenciadas ainda podem reservar o toque final. Desativar **{k:QJ_ManageBill}** libera os trabalhos dessa ordem que aguardam os requisitos de qualidade; isso não desativa o compartilhamento na colônia.

