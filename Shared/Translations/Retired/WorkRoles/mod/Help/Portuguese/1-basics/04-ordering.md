---
title: Ordem das atribuições
---
O colono percorre a linha da esquerda para a direita, considerando só funções ligadas:

![Etiquetas ordenadas de um colono](colonist-row.png)

1. Todos os trabalhos das funções anteriores têm prioridade sobre os posteriores.
2. Dentro de uma função, os primeiros trabalhos vêm antes.
3. Um trabalho presente em várias funções usa a sua primeira posição.

Colonos só executam trabalhos das funções atribuídas. Não atribuir equivale a deixar a prioridade original sem número.

Arraste para mudar a prioridade:

@demo:chip-drag

Também pode arrastar para outro colono e transferir a atribuição. Clique no caixote do lixo da etiqueta para removê-la da linha.

RimWorld mantém uma lista separada de emergências: apagar incêndios, tratar urgências e deitar para tratamento urgente. Elas interrompem sono, refeições e qualquer trabalho. Por predefinição, só entram nessa lista se cobertas por uma das cinco primeiras funções ligadas. Nas posteriores, executam na posição normal. Com Urgências primeiro e Médico sexto, o colono interrompe tudo por incêndios, mas trata urgências na posição normal de Medicina. Mover Urgências além do quinto também torna apagar incêndios um trabalho comum.

Desligue “Só as primeiras 5 funções interrompem o trabalho em emergências” em Opções para cancelar esse limite. Todas as emergências cobertas por funções ligadas passam a usar a lista especial, seja qual for a posição.
