---
title: Funcionamento interno
---
As funções ordenadas viram uma ordem estrita de trabalhos, fornecida pelas mesmas listas já usadas pelo jogo. O comportamento de trabalho corresponde ao de uma tabela ajustada manualmente, sem patches por tick ou trabalho por quadro.

Tudo é recalculado quando funções ou atribuições mudam e depois fica em cache. Condicionais também atualizam nas viradas de hora e mudanças de local. Emergências usam a etapa especial do jogo se cobertas por uma das cinco primeiras funções ligadas; as demais seguem sua posição normal. Desligar “Só as primeiras 5 funções interrompem o trabalho em emergências” põe todas as emergências cobertas nessa etapa.

WorkRoles mantém o armazenamento original de prioridades preenchido pelas funções, mapeando para 0–4 com a opção correspondente ligada, como ocorre por padrão. Por isso remover o mod é seguro: a aba Trabalho volta com as funções convertidas em prioridades. Adicionar a uma partida existente faz a conversão inversa.
