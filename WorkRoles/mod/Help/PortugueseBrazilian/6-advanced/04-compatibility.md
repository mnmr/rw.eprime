---
title: Compatibilidade com mods
---
<!-- l10n:b-6110204fbb19 -->
Trabalhos de outros mods são detectados automaticamente. Algumas integrações vão além: especializações de Vanilla Skills Expanded e Alpha Skills alimentam sinais; trabalho odiado de More Than Capable vira sinal Péssimo; Colony Groups acrescenta agrupamento; mods de andares tratam pisos empilhados como um local.

<!-- l10n:b-c7669aaaba7d -->
Mods opcionais de trabalhos e habilidades podem fornecer perfis adicionais, identificando habilidades usadas e treinadas e refinando a principal para recomendações quando apropriado. WorkRoles deixa elegibilidade, designações e execução para os mods responsáveis. Essas integrações não acrescentam dependências obrigatórias.

<!-- l10n:b-27d73501db9c -->
Recomendações também leem etiquetas de trabalho, capacidades exigidas, oposição de preceitos e recusas verificadas à mineração comum. Em versões auditadas de Humanoid Alien Races para RimWorld 1.6 e 1.7, usam as restrições reais de provedores de trabalho e receitas. Todas as raças que usam essas regras se beneficiam. Uma receita bloqueada não remove outras utilizáveis da função nem conta como forma de treinar sua habilidade.

<!-- l10n:b-15011508cb81 -->
Versões desconhecidas preservam a elegibilidade existente e informam que suas regras não são suportadas. Restrições escondidas em trabalhos arbitrários não podem ser detectadas com segurança tentando executá-los durante a coleta de recomendações. Versões compatíveis de Vivi fornecem velocidade, rendimento e elegibilidade de coleta dependentes do equipamento. Versões desconhecidas de Vivi, regras personalizadas e regras específicas de Ratkin não verificadas mantêm o comportamento existente. Nenhuma restrição é presumida pelo nome da raça ou xenótipo.

<!-- l10n:b-cc8167cf5cfc -->
Complementos que modificam provedores existentes usam as mesmas funções e ordem. Por exemplo, manejo de animais continua usando Animal nas recomendações. Experiência condicional em outra habilidade depende das regras do complemento e não cria uma rota geral de formação para todos os atribuídos.

<!-- l10n:b-f6d4800368e1 -->
Duas famílias entram em conflito. Outros substitutos da aba Trabalho, como variantes de Work Tab e Better Work Tab, disputam a interface. Mods que definem prioridades, como Free Will e PriorityMaster, disputam os dados: WorkRoles controla os colonos gerenciados e bloqueia essas gravações. Um aviso único identifica o mod e sugere como obter efeito semelhante com funções.

<!-- l10n:b-3e9c0a143130 -->
Mods de prioridades de mecanoides, como Mech Work Tab, podem gerenciar trabalhadores sem atribuições no WorkRoles. Se você ativar o suporte a robôs e mecanoides, WorkRoles também controla a ordem dos trabalhos dos trabalhadores mecânicos com funções atribuídas. Desative o suporte em Opções para restaurar a ordem nativa de todos eles; suas funções salvas ficam inativas.

<!-- l10n:mechanical-workers -->
**Mostrar robôs e mecanoides em Colonos** é uma opção por partida salva, desativada por padrão. Ela adiciona mecanoides de Biotech, Misc. Robots e Misc. Robots++ à tabela existente. Atribua funções nessa tabela; use as abas nativas para grupos de controle, áreas, ativação e recarga. As funções apenas ordenam os trabalhos que o trabalhador realmente pode executar. Androides humanoides continuam seguindo as regras comuns dos colonos.

<!-- l10n:mechanical-recharge -->
A recarga continua seguindo o comportamento nativo e não exige uma função de Recarga. Biotech oferece limites de carga em cada grupo de controle. Misc. Robots usa seus próprios limites de retorno automático; clicar na porcentagem de carga na aba Robôs solicita uma recarga imediata. WorkRoles não altera esses limites.

<!-- l10n:b-3df0d9a43535 -->
Alguns mods exigem prioridades de 0 a 4. Em caso de problemas, ative “Comunicar prioridades de 0 a 4 aos outros mods”.

<!-- l10n:b-99382dfb0e0e -->
Mods que apenas leem prioridades funcionam normalmente: enxergam os valores calculados por WorkRoles.
