---
title: Como a automação funciona
---
<!-- l10n:b-44e89be1e85f -->
A automação examina a colônia aproximadamente duas vezes por hora do jogo. Após uma edição, faz outra verificação assim que o tempo do jogo avança. Retome o jogo para aplicar as alterações da automação. Cada verificação segue estas etapas:

<!-- l10n:b-6eb19bb9f96f -->
1. Conta o que ainda falta aos colonos vinculados.
2. Cria ordens dos implantes que a colônia pode fabricar (veja [Opções de produção](topic:production-options)).
3. Reserva itens prontos do estoque para colonos específicos, conforme prioridades e níveis.
4. Quando tudo necessário para a próxima etapa estiver reservado e no local, acrescenta as operações em bloco à lista do colono, depois das ordens existentes.

<!-- l10n:b-a747349bea45 -->
O resumo da colônia no topo da aba Visão geral mostra se a automação está ligada, o que está sendo fabricado ou aguardado, quantos implantes há em estoque e ainda precisam ser produzidos e quem será o próximo operado. Passe o cursor sobre os números de produção ou cirurgia para ver detalhes por implante e o motivo de espera de cada item.

<!-- l10n:production-counts -->
Na dica de produção, **Ainda a produzir** conta os implantes necessários ainda não cobertos pelo estoque, incluindo trabalho sem ordem de fabricação. **Em ordens de produção** conta a produção esperada das ordens existentes do Implanner, estejam elas aguardando ou em andamento.

<!-- l10n:b-8fea0b171c65 -->
![Resumo da colônia com produção e cirurgia](colony-next.png)

<!-- l10n:b-26d1852e16fa -->
Cada implante ainda exige sua própria operação. Os lotes adiam a cirurgia até que vários implantes possam ser instalados sob uma única anestesia, reduzindo o número de períodos separados sob anestesia. Um lote contém tudo de que o colono precisa na próxima etapa: o nível atual ou o plano completo, conforme a estratégia de iteração (veja [Opções de cirurgia](topic:surgery-options)). Uma estratégia ignora lotes e agenda cada implante assim que estiver reservado e no local.

<!-- l10n:b-6b9f966652bc -->
As operações esperam o colono estar em condições. Colônias nunca tomam itens umas das outras: as reservas e instalações ocorrem onde o colono vive.
