---
title: Metas e limites de estoque
---
<!-- l10n:target -->
A **{k:QJ_MinQualityLabel}** faz uma ordem com quantidade fixa continuar tentando até produzir itens suficientes dessa qualidade ou superior. Um item abaixo da meta é mantido, mas não consome uma das repetições restantes da ordem. Cada tentativa extra usa mais materiais. **{k:QJ_AnyQuality}** aceita todos os resultados.

<!-- l10n:repeat-modes -->
Esse comportamento de novas tentativas se aplica a **Fazer X vezes**. Para **Fazer até ter X**, use a faixa de qualidade da ordem normal para definir o que conta para a meta de estoque. **Fazer para sempre** continua como de costume. Uma meta de qualidade, por si só, não seleciona um finalizador melhor; defina também os requisitos do finalizador.

<!-- l10n:stock -->
O **Limite de estoque** restringe o início de novos itens de um produto neste mapa. Seus itens inacabados são contados em conjunto entre ordens e bancadas, mas apenas as ordens gerenciadas aplicam o limite. **0 significa ilimitado.** Itens finalizados armazenados não entram na contagem.

<!-- l10n:cap-behavior -->
Ao atingir o limite, as ordens gerenciadas deixam de iniciar novos itens. Ainda é permitido retomar ou finalizar itens existentes, permitindo esvaziar a fila. Se nenhum finalizador atender aos requisitos, reduzi-los ou disponibilizar novamente um trabalhador apto pode desbloquear a fila.

