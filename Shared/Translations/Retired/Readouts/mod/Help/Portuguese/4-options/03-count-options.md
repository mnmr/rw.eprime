---
title: Opções de contagem
---
As opções de contagem definem quais pilhas entram em todos os contadores. Ambas começam ligadas.

Contar apenas objetos armazenados inclui pilhas em zonas de armazenamento e construções como prateleiras. Objetos soltos pelo mapa ficam de fora:

![Recursos brutos contando apenas os armazenados](storage-only-on.png)

Desligada, a opção permite contar objetos em qualquer lugar do mapa:

![Os mesmos espaços sem essa restrição](storage-only-off.png)

Ignorar objetos proibidos exclui pilhas proibidas.

Objetos podres, em áreas não exploradas, carregados por colonos ou guardados em inventários e recipientes nunca são contados.

Como objetos carregados não entram, o contador normalmente cai quando um colono pega uma pilha. Material de projeto é exceção: com Reservar recursos para construções (veja [Trabalho planeado](topic:planned-work)), a necessidade já foi descontada ao colocar o projeto. Pegar e entregar o material não o desconta de novo.

As quantidades são atualizadas a cada poucos segundos do jogo. Mudar uma opção atualiza imediatamente, mesmo em pausa. Um recurso ou conjunto pode substituir ambas para todas as suas posições (veja [Opções do espaço](topic:slot-options)).
