---
title: Qualidade mínima
---
<!-- l10n:b-d8fd3884b5cc -->
Com Quality Bionics Remastered ou Vanilla Genetics Expanded, os implantes têm qualidades de horrível a lendária, afetando seu funcionamento. O plano passa a mostrar **Qualidade mínima** ao lado de **Excluir plano**.

<!-- l10n:b-118873404542 -->
![Cabeçalho com Qualidade mínima](plan-min-quality.png)

<!-- l10n:b-84db128338c2 -->
O mínimo se aplica às novas instalações deste plano, incluindo as escolhas herdadas. A fabricação automática busca essa qualidade e pode precisar de várias tentativas para alcançá-la. Implantes sem qualidade não são afetados. Um novo plano derivado começa com o mesmo mínimo do plano base.

<!-- l10n:b-93af25d8591d -->
Implanner nunca troca uma parte saudável por outra pior. Um braço biônico horrível funciona pior que um natural e aguarda um item melhor, a menos que o braço esteja ausente ou muito danificado. Entre vários itens aceitáveis, o melhor vai para quem chegar primeiro na ordem.

<!-- l10n:b-0e7cf20ad9dc -->
Um implante já instalado do tipo planejado continua contando como concluído, mesmo abaixo da qualidade mínima. Ative **Dar os melhores implantes aos colonos mais prioritários** para permitir a substituição por exemplares de qualidade superior, como explicado abaixo.

<!-- l10n:b-b163fd8a6054 -->
## Melhores implantes para colonos prioritários

<!-- l10n:b-0c6c2d002995 -->
![Opção na aba Automação](upgrade-by-priority.png)

<!-- l10n:b-8937cc3e8047 -->
Com **Dar os melhores implantes aos colonos mais prioritários**, nas opções de cirurgia, a qualidade segue a prioridade:

<!-- l10n:b-0d3ec0233e11 -->
- Cirurgias novas usam a menor qualidade aceita, deixando os itens melhores livres.
- Um item melhor vai ao colono de maior prioridade com um implante pior do mesmo tipo. O antigo é retirado, o melhor é instalado e o retirado passa ao próximo colono.
- Itens reservados para alguém de menor prioridade também podem ser transferidos, desde que sua cirurgia ainda não esteja agendada.

<!-- l10n:b-e246867de4e8 -->
Substituir um implante instalado por outro de qualidade superior exige duas operações: remoção e instalação. A troca só começa quando não há outra cirurgia de Implanner esperando pelo colono e existe vaga no limite de cirurgias simultâneas. Um implante nunca é trocado se sua remoção também retiraria outra coisa da parte do corpo.

<!-- l10n:b-37015122025b -->
Ao fabricar para o plano:

<!-- l10n:b-51f50276fd20 -->
- Com Quality Jobs, a ordem busca a qualidade mínima, entregando o acabamento a um fabricante capaz de alcançá-la.
- Híbridos de Vanilla Genetics Expanded recebem a qualidade da estrutura genética. Só são usadas estruturas na qualidade mínima ou acima.
- Sem esses mecanismos, qualquer fabricante pode terminar. Um resultado abaixo do mínimo é fabricado novamente. Os limites de estoque (veja [Preservar estoque](topic:keeping-stock)) impedem que os materiais se esgotem.
