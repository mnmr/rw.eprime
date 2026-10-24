---
title: Preservar estoque
---
Duas opções impedem que a automação esvazie seus depósitos.

**Manter em estoque** define mínimos de recursos para produção. Uma ordem só entra se for possível pagar por ela, pelos materiais ainda necessários às ordens na fila e manter o mínimo de cada ingrediente. Aço em 2000 significa que a produção de implantes nunca o reduz abaixo de 2000. Se você gastar materiais e uma ordem ainda não iniciada deixar de caber no orçamento, ela é removida e retorna quando o estoque permitir.

![Mínimos de recursos mantidos em estoque](keep-in-stock.png)

**Implantes separados** guarda implantes prontos para uso manual. Separar um braço biônico faz a automação usar apenas os excedentes, deixando um para emergências ou comércio. A produção trata o item separado como indisponível para atender à demanda e repõe o estoque.
Ordens de melhoria também não usam implantes separados nem os necessários para cirurgias planejadas. Só os excedentes viram ingredientes.

![Um braço biônico separado para uso manual](implant-reservations.png)
