---
title: Como a automatização funciona
---
A automatização examina a colónia algumas vezes por hora do jogo, e cada edição provoca uma verificação imediata extra:

1. Conta o que ainda falta aos colonos vinculados.
2. Cria ordens dos implantes que a colónia pode fabricar (veja [Opções de produção](topic:production-options)).
3. Reserva objetos prontos das existências para colonos específicos, conforme prioridades e níveis.
4. Quando tudo necessário para a próxima etapa estiver reservado e no local, acrescenta as operações em bloco à lista do colono, depois das ordens existentes.

O resumo acima da Visão geral mostra se a automatização está ligada, o que está a ser fabricado ou aguardado, quantos implantes há em armazém e na fila e quem será o próximo operado. Passe o cursor sobre produção ou cirurgia para ver detalhes por implante e o motivo de espera de cada objeto.

![Resumo da colónia com produção e cirurgia](colony-next.png)

Os lotes adiam a cirurgia até que vários implantes possam entrar na mesma anestesia. Um lote contém a próxima etapa: o nível atual ou o plano completo, conforme a estratégia (veja [Opções de cirurgia](topic:surgery-options)). Uma estratégia ignora lotes e agenda cada implante assim que estiver reservado e no local.

As operações esperam o colono estar em condições. Colónias nunca tomam objetos umas das outras: as reservas e instalações ocorrem onde o colono vive.
