---
title: Opções de produção
---
**Adicionar automaticamente ordens de fabrico para implantes em falta** coloca ordens nas bancadas capazes e remove as próprias quando deixam de ser necessárias. Ordens criadas por si nunca são removidas. Cada ordem fabrica uma vez, seguindo a instalação: com Conjuntos completos, as pernas vêm antes dos braços se estiverem acima no plano.

Quando várias receitas produzem o mesmo implante, Implanner prefere uma rota disponível que possa pagar agora. Pode melhorar um implante básico livre ou fabricar diretamente o avançado com materiais. Se só a melhoria estiver habilitada, a produção intermédia pode fazer a base primeiro. Receitas precisam de estar investigadas e ter uma bancada elegível.

Para dedicar uma bancada, marque **Bancada Implanner** no separador Ordens. A caixa só aparece onde é possível fabricar implantes. As ordens antigas são suspensas e o Implanner prioriza essa bancada. Enquanto marcada, não é possível adicionar, colar, suspender ou retomar ordens.

![Bancada de fabrico entregue ao Implanner](implanner-bench.png)

Desmarque para recuperá-la. Ordens já suspensas antes da designação continuam assim; as restantes retomam. Se remover o mod enquanto a bancada estiver designada, ela continua funcional, mas precisa de retomar as suas ordens manualmente.

![Opções de produção](production-options.png)

- **Bancadas que podem receber ordens** limita quantas trabalham com implantes por colónia. Cada uma mantém até duas ordens, deixando a próxima pronta ao terminar a primeira. Bancadas dedicadas contam no limite.
- **Criar ordens apenas em bancadas desocupadas** evita bancadas com trabalho pendente e preserva a prioridade das suas filas.
- **Usar apenas bancadas Implanner** impede produção em colónias sem bancada dedicada. Desligada, outras são usadas quando todas as dedicadas já têm duas ordens.
- **Competência de fabrico exigida** protege materiais caros de fabricantes pouco habilidosos.
- **Permitir ordens de produção de intermédios em falta** segue a cadeia: se falta um componente avançado para a próxima perna biónica, ele é fabricado primeiro. Enquanto um implante aguarda matéria-prima, como plastaço, bancadas livres fazem os componentes avançados necessários. Produtos manufaturados, como componentes e implantes básicos, são elegíveis. Matérias-primas como aço nunca são fabricadas, mesmo quando há receita de fundição para obtê-las; precisa de recolhê-las.

Todas as ordens respeitam os limites de existências (veja [Preservar existências](topic:keeping-stock)).
