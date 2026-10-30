---
title: Configurando uma ordem de produção
---
<!-- l10n:open -->
Abra **Detalhes** em uma ordem de produção de um item com qualidade. O painel **{k:QJ_QualityPanelTitle}** aparece ao lado dos controles normais da ordem para receitas compatíveis que deixam um item inacabado. Receitas sem qualidade ou trabalho inacabado mantêm seu comportamento normal.

<!-- l10n:bill-image -->
![Uma ordem de produção de uma bancada com seu painel do Quality Jobs](crafting-bill.png)

<!-- l10n:setup -->
Ative **{k:QJ_ManageBill}** e escolha os [requisitos do finalizador](topic:finishers). Trabalhadores comuns podem fazer a maior parte da fabricação. Se o trabalhador atual atender aos requisitos, ele conclui o item diretamente; caso contrário, o item quase concluído é separado, e a ordem pode iniciar mais itens dentro do seu limite de estoque.

<!-- l10n:dispatch -->
Quando um finalizador atende aos requisitos, o Quality Jobs cria uma ordem temporária **Finalizar …** atribuída a ele. Ele recolhe o item inacabado e o conclui. A ordem temporária é removida automaticamente em seguida. Os itens em espera podem ser transportados e armazenados como outros itens inacabados.

<!-- l10n:finisher-bill-restrictions -->
O nível máximo de habilidade e a atribuição a um trabalhador específico da ordem de produção original se aplicam até que o item seja separado para finalização. Você pode atribuir o trabalho a um aprendiz enquanto um colono mais habilidoso, um mecanoide da colônia ou um robô compatível assume a ordem exclusiva de finalização. Os finalizadores ainda precisam ser capazes de executar a receita e atender aos requisitos do Quality Jobs.

<!-- l10n:status -->
A seção **{k:QJ_StatusHeader}** conta os itens inacabados rastreados deste produto entre todas as ordens e bancadas deste mapa: trabalho compartilhado **Inacab.**, itens em **Espera** de um finalizador e itens já atribuídos com o estado **Finalizando**. A lista de finalizadores e o aviso usam os requisitos da ordem selecionada; outras ordens do mesmo produto podem ter requisitos diferentes. Passe o cursor sobre o estado e a lista para ver detalhes. Veja [quando o trabalho espera](topic:waiting) se nada estiver avançando.

<!-- l10n:disable -->
Desmarque **{k:QJ_ManageBill}** para que ela volte a ser concluída normalmente. Seu trabalho compartilhado ainda pode ser continuado por outros trabalhadores quando o compartilhamento estiver habilitado.

