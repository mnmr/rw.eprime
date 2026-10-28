---
title: Opções de produção
---
<!-- l10n:b-bdd2b459773e -->
**Adicionar automaticamente ordens de fabricação para implantes em falta** coloca ordens nas bancadas capazes e remove as próprias quando deixam de ser necessárias. Ordens criadas por você nunca são removidas. Cada ordem fabrica uma vez, seguindo a instalação: com Conjuntos completos, as pernas vêm antes dos braços se estiverem acima no plano.

<!-- l10n:b-8effc9e9b828 -->
Quando várias receitas produzem o mesmo implante, Implanner prefere uma rota disponível que possa pagar agora. Pode melhorar um implante básico livre ou fabricar diretamente o avançado com materiais. Se só a melhoria estiver habilitada, a produção intermediária pode fazer a base primeiro. Receitas precisam estar pesquisadas e ter uma bancada elegível.

<!-- l10n:b-0aa3ded1d655 -->
Para dedicar uma bancada, marque **Bancada Implanner** na aba Tarefas. A caixa só aparece onde é possível fabricar implantes. As ordens antigas são suspensas e o Implanner prioriza essa bancada. Enquanto marcada, não é possível adicionar, colar, suspender ou retomar ordens.

<!-- l10n:b-a4d04c1bceb2 -->
![Bancada de fabricação entregue ao Implanner](implanner-bench.png)

<!-- l10n:b-a89a880dcac5 -->
Desmarque para recuperá-la. Ordens já suspensas antes da designação continuam assim; as demais retomam. Se remover o mod enquanto a bancada estiver designada, ela continua funcional, mas você precisa retomar suas ordens manualmente.

<!-- l10n:b-c9f0dad1a5c5 -->
![Opções de produção](production-options.png)

<!-- l10n:b-f6f358878993 -->
- **Bancadas que podem receber ordens** limita quantas trabalham com implantes por colônia. Cada uma mantém até duas ordens, deixando a próxima pronta ao terminar a primeira. Bancadas dedicadas contam no limite.
- **Criar ordens apenas em bancadas desocupadas** evita bancadas com trabalho pendente e preserva a prioridade das suas filas.
- **Usar apenas bancadas Implanner** impede produção em colônias sem bancada dedicada. Desligada, outras são usadas quando todas as dedicadas já têm duas ordens.
- **Habilidade de fabricação exigida** protege materiais caros de fabricantes pouco habilidosos.
- **Permitir ordens de produção de intermediários em falta** segue a cadeia: se falta um componente avançado para a próxima perna biônica, ele é fabricado primeiro. Enquanto um implante aguarda matéria-prima, como plastaço, bancadas livres fazem os componentes avançados necessários. Produtos manufaturados, como componentes e implantes básicos, são elegíveis. Matérias-primas como aço nunca são fabricadas, mesmo quando há receita de fundição para obtê-las; você precisa coletá-las.

<!-- l10n:b-24089426e9a9 -->
Todas as ordens respeitam os limites de estoque (veja [Preservar estoque](topic:keeping-stock)).
