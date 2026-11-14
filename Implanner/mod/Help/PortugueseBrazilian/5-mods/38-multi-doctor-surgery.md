---
title: Multi-Doctor Surgery
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Veredito** | ![Totalmente compatível](verdict-blue.png|16) Totalmente compatível (suporte específico) |
| **Evidência** | Análise do código-fonte, teste automatizado no jogo |
| **Verificado** | 2026-11-14 |
| **Versões** | Implanner 1.2.9, Multi-Doctor Surgery [Workshop 3356249192](https://steamcommunity.com/sharedfiles/filedetails/?id=3356249192) |

<!-- l10n:summary -->
As operações agendadas pelo Implanner podem ser feitas por uma equipe cirúrgica.

<!-- l10n:teams -->
A opção **Equipes cirúrgicas** na aba Opções define quem realiza as operações
do Implanner. **Individual**, o padrão, faz um único médico operar, como
sem o Multi-Doctor Surgery. **Equipe padrão** usa a equipe cirúrgica padrão
configurada na aba Operações do Multi-Doctor Surgery. **Melhor equipe** faz o médico
disponível mais habilidoso liderar, auxiliado pelos médicos que mais aumentam a
chance de sucesso, até o limite de médicos do Multi-Doctor Surgery. Médicos mecanoides
podem entrar na equipe quando o Multi-Doctor Surgery os lista.

<!-- l10n:rules -->
O Implanner define a equipe ao agendar uma operação, e a
operação então aparece na aba Operações do Multi-Doctor Surgery. Uma operação
em equipe espera pelo cirurgião líder, que pode operar mesmo quando a escolha
automática de médicos exigiria uma habilidade maior. Quando o líder padrão não pode
operar aquele colono, por exemplo porque o líder é o paciente ou está
em outra colônia, um único médico realiza a operação sem equipe.
Operações excluídas nas configurações do Multi-Doctor Surgery continuam individuais. Equipes
não são usadas no multijogador.

<!-- l10n:manual -->
As operações que você adiciona manualmente continuam recebendo o diálogo de equipe e os
bônus do Multi-Doctor Surgery, inclusive em colonos que seguem um plano do Implanner.

