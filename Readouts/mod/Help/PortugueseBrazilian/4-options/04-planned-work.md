---
title: Trabalho planejado
---
<!-- l10n:b-7aa1a59c7379 -->
As opções de trabalho planejado descontam das contagens exibidas os custos estimados de materiais para ordens de produção ou projetos.

<!-- l10n:b-ee40b81e925d -->
- Reservar recursos para ordens de produção desconta os ingredientes necessários para as ordens de produção pendentes. Um ingrediente só é contado quando a ordem permite exatamente um tipo de recurso para ele. Por exemplo, permitir qualquer couro não reserva nada; permitir apenas um tipo de couro inclui o custo desse couro. “Fazer X vezes” reserva X execuções; “Até ter X”, as necessárias para atingir o alvo; “Para sempre”, uma execução. Ordens suspensas não reservam nada.
- Reservar recursos para construções desconta o que projetos e estruturas parcialmente construídas ainda precisam. Materiais entregues ou já carregados por um colono para entrega contam como gastos, sem novo desconto. Projetos proibidos são ignorados.
- Mostrar déficits como números negativos exibe quanto falta quando o trabalho precisa de mais do que você tem, em vez de parar em zero.
- Suporte para Quality Jobs ajusta as reservas ativadas para ordens e construções aos objetivos de qualidade gerenciados pelo Quality Jobs. Estima os materiais necessários para tentativas adicionais e considera os materiais recuperados ao desconstruir. Ative a opção de reserva correspondente acima para usar este recurso. A linha fica desativada se EPrime's Quality Jobs não estiver instalado ou for antigo; a dica explica o motivo.

<!-- l10n:planned-work-tooltip-columns -->
Passe o cursor sobre um recurso ou conjunto de recursos com descontos de trabalho planejado para ver a tabela Trabalho planejado. Trabalho restante conta execuções de receitas ou construções a concluir. Custo por tentativa é o custo em materiais de uma tentativa. Descontado é a quantidade subtraída da contagem exibida, considerando os materiais já entregues ou sendo transportados para a construção e as novas tentativas estimadas para atingir a qualidade quando o suporte para Quality Jobs está ativado.

<!-- l10n:b-a4ac1a2f64dc -->
Com números negativos ligados, o déficit causado pelo trabalho planejado aparece em laranja:

<!-- l10n:b-b209bc9139c1 -->
![Materiais com números negativos ligados](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Desligados, os mesmos espaços param em zero:

<!-- l10n:b-a997f13279d8 -->
![Os mesmos espaços sem números negativos](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
O trabalho planejado é atualizado junto com as contagens de recursos a cada poucos segundos de tempo de jogo. Ordens novas ou alteradas aparecem na próxima atualização; retome o jogo para que o tempo avance.
