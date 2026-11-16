---
title: Opções de produção
---
<!-- l10n:b-bdd2b459773e -->
Com **Adicionar automaticamente tarefas de fabricação para implantes em falta** ativado, Implanner coloca tarefas nas bancadas capazes de fabricar os implantes necessários e remove suas próprias tarefas quando deixam de ser necessárias. Tarefas criadas por você nunca são removidas. Cada tarefa fabrica uma vez, seguindo a ordem de instalação nas cirurgias: com Conjuntos completos, as pernas de um colono vêm antes dos braços se tiverem prioridade maior na classificação compartilhada de implantes.

<!-- l10n:b-8effc9e9b828 -->
Quando várias receitas produzem o mesmo implante, Implanner prefere uma rota disponível que possa pagar agora. Pode melhorar um implante básico livre ou fabricar diretamente o avançado com materiais. Se só a melhoria estiver habilitada, a produção intermediária pode fazer a base primeiro. Receitas precisam estar pesquisadas e ter uma bancada elegível.

<!-- l10n:b-0aa3ded1d655 -->
Para dedicar uma bancada à fabricação de implantes, marque **Bancada Implanner** na aba Tarefas da bancada. A caixa só aparece em bancadas que podem fabricar implantes. As tarefas já existentes na bancada são suspensas, e o Implanner coloca nela suas tarefas de fabricação antes de qualquer outra bancada.

<!-- l10n:queue -->
Enquanto a caixa estiver marcada, a aba Tarefas mostra a fila de produção do Implanner em vez da lista de tarefas habitual da bancada. Cada linha é uma tarefa colocada na fila pelo Implanner: o item, se um fabricante já a iniciou, o plano ao qual pertence, o colono que é o próximo na fila para recebê-lo e há quanto tempo está na fila. Uma tarefa para um ingrediente indica o implante para o qual ele é necessário. **Detalhes** abre as configurações do jogo para essa tarefa. A linha no final conta as suas próprias tarefas que estão pausadas. Você não pode adicionar tarefas à bancada enquanto não desmarcar a caixa.

<!-- l10n:b-a4d04c1bceb2 -->
![Bancada de fabricação entregue ao Implanner](implanner-bench.png)

<!-- l10n:b-a89a880dcac5 -->
Desmarque para recuperá-la. Tarefas já suspensas antes da designação continuam assim; as demais retomam. Se remover o mod enquanto a bancada estiver designada, ela continua funcional, mas você precisa retomar suas tarefas manualmente.

<!-- l10n:b-c9f0dad1a5c5 -->
![Opções de produção](production-options.png)

<!-- l10n:b-f6f358878993 -->
- **Bancadas que podem receber tarefas** limita quantas trabalham com implantes por colônia. Cada uma mantém até duas tarefas, deixando a próxima pronta ao terminar a primeira. Bancadas dedicadas contam no limite.
- **Criar tarefas apenas em bancadas desocupadas** evita bancadas com trabalho pendente e preserva a prioridade das suas filas.
- **Usar apenas bancadas Implanner** impede produção em colônias sem bancada dedicada. Desligada, outras são usadas quando todas as dedicadas já têm duas tarefas.
- **Habilidade de fabricação exigida** protege materiais caros de fabricantes pouco habilidosos.
- **Permitir tarefas de produção de intermediários em falta** segue a cadeia: se falta um componente avançado para a próxima perna biônica, ele é fabricado primeiro. Enquanto um implante aguarda matéria-prima, como plastaço, bancadas livres fazem os componentes avançados necessários. Produtos manufaturados, como componentes e implantes básicos, são elegíveis. Matérias-primas como aço nunca são fabricadas, mesmo quando há receita de fundição para obtê-las; você precisa coletá-las.

<!-- l10n:b-24089426e9a9 -->
Todas as tarefas respeitam os limites de estoque (veja [Preservar estoque](topic:keeping-stock)).
