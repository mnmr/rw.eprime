---
title: Escolhendo finalizadores
---
<!-- l10n:requirements -->
Use estes controles em uma ordem de produção ou construção. Outros trabalhadores aptos podem fazer as etapas anteriores; apenas o toque final precisa atender aos seus requisitos.

<!-- l10n:controls -->
- **Habilidade do finalizador** define um nível mínimo de habilidade.
- **{k:QJ_AutoBest}** escolhe o melhor trabalhador apto automaticamente. O valor do controle deslizante manual é mantido para quando você desativar esta opção.
- **{k:QJ_RequireInspired}** aguarda essa inspiração. Concluir o trabalho a consome normalmente.
- **{k:QJ_RequireSpecialist}** exige um papel de Ideology que conceda um bônus de qualidade de produção. Esta opção aparece com Ideology.

<!-- l10n:odds-image -->
![Requisitos do finalizador e prévia das chances de qualidade](finisher-options.png)

<!-- l10n:best -->
O ajuste automático compara os colonos aptos, os mecanoides da colônia e os robôs compatíveis e escolhe o trabalhador com a maior qualidade esperada. A habilidade, Criatividade inspirada, os bônus de especialistas em produção e os bônus de qualidade de mods compatíveis são considerados. Isso maximiza a qualidade média, mas não garante o resultado de uma rolagem individual.

<!-- l10n:availability -->
Trabalhadores que atendem aos demais requisitos continuam na comparação enquanto estão ausentes, caídos, dormindo, em crise mental ou recarregando, incluindo robôs em suas estações. Se nenhum dos trabalhadores mais bem classificados puder finalizar neste mapa, o trabalho espera. Por exemplo, um artesão melhor que esteja em uma caravana pode impedir que um trabalhador com classificação inferior na colônia finalize. Não há um prazo de espera após o qual o requisito de qualidade seja reduzido.

<!-- l10n:manual-threshold -->
Com o ajuste automático desativado, qualquer trabalhador capaz que atenda ao nível mínimo de habilidade definido manualmente e aos demais requisitos pode finalizar. Um trabalhador melhor em outro lugar da colônia não o bloqueia.

<!-- l10n:robot-finishers -->
Mecanoides e robôs compatíveis devem atender aos mesmos requisitos de finalização que os colonos. Exigir Criatividade inspirada ou um papel de especialista em produção continua excluindo trabalhadores sem essa inspiração ou esse papel. Desativar **{k:QJ_SettingsAllowRobotPreparation}** reserva as etapas anteriores aos colonos; mecanoides e robôs aptos ainda podem receber tarefas de finalização do Quality Jobs.

<!-- l10n:odds -->
**{k:QJ_OddsHeader}** compara sua **{k:QJ_OddsColConfig}** com o **{k:QJ_OddsColBest}** candidato atual. São probabilidades, não garantias. **Finalizadores aptos** lista os trabalhadores que podem atender aos requisitos neste mapa agora; trabalhadores ausentes, derrubados ou em surto mental ficam de fora dessa lista.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** faz com que o trabalho de finalização atribuído tenha prioridade sobre outros trabalhos, exceto combate a incêndios, cuidados médicos, resgate e repouso na cama. Isso não interrompe a tarefa atual. Com essa opção desativada, a finalização tem prioridade dentro do seu tipo de trabalho, depois dos tipos de trabalho com prioridade maior. O trabalhador deve ser capaz de executar o tipo de trabalho normal do item.

