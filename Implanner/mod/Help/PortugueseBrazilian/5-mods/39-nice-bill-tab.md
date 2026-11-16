---
title: Nice Bill Tab
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Veredito** | ![Totalmente compatível](verdict-blue.png|16) Totalmente compatível (suporte personalizado) |
| **Evidência** | Análise do código-fonte, teste automatizado no jogo |
| **Verificado** | 2026-11-16 |
| **Versões** | Implanner 1.3.0, Nice Bill Tab [Workshop 3520130671](https://steamcommunity.com/sharedfiles/filedetails/?id=3520130671), arquivos de 2026-11-13 |

<!-- l10n:summary -->
Uma bancada Implanner mostra sua fila de produção, e a caixa de seleção da bancada tem uma linha própria.

<!-- l10n:toggle -->
Em uma bancada que pode fabricar implantes, a caixa de seleção **Bancada Implanner** fica em uma linha própria no topo da lista de tarefas do Nice Bill Tab, acima do campo de busca. Bancadas que não fabricam implantes mostram o Nice Bill Tab sem alterações.

<!-- l10n:vanilla-tab -->
Uma bancada marcada como bancada Implanner mostra a fila de produção do Implanner em vez do layout do Nice Bill Tab, a mesma aba que ela mostra sem o Nice Bill Tab (veja [opções de produção](topic:production-options)). O interruptor e o botão de configurações do próprio Nice Bill Tab não aparecem nessa aba. Desmarque a caixa de seleção e o layout do Nice Bill Tab volta para essa bancada.

<!-- l10n:stray-bills -->
O Nice Bill Tab pode criar uma tarefa em uma bancada disponível para um ingrediente em falta. As bancadas Implanner são ignoradas, então a tarefa vai para a outra bancada mais próxima capaz de fabricar o item. Quando só bancadas Implanner podem fabricá-lo, nenhuma tarefa é criada.
