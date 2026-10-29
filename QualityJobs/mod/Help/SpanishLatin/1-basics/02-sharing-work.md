---
title: Compartir trabajos sin terminar
---
<!-- l10n:sharing -->
Con **{k:QJ_SettingsShareWork}** activado en Opciones, otro trabajador apto puede continuar un objeto sin terminar en el que nadie esté trabajando, incluso si su creador original ha muerto. El uso compartido funciona independientemente de **{k:QJ_ManageBill}**.

<!-- l10n:matching -->
El siguiente trabajador necesita una orden de producción que pueda usar y que tenga la misma receta. Los ingredientes del objeto deben estar permitidos por esa orden y su estilo debe coincidir. Además, el objeto debe ser accesible, estar permitido y estar disponible para reservarlo. Compartir no omite las asignaciones de trabajo habituales ni las restricciones de las órdenes.

<!-- l10n:handoff -->
Un objeto sin terminar en el que nadie está trabajando se desvincula de su trabajador y orden originales para que una orden compatible pueda retomarlo. Mientras el objeto sin terminar de esa orden esté esperando o siendo transportado, la orden no inicia un duplicado. El trabajo en curso no se interrumpe solo para pasárselo a otra persona.

<!-- l10n:reserved -->
Compartir el trabajo inicial es distinto de reservar el toque final. Un objeto gestionado que se pausa justo antes de completarse muestra **Autor: reservado (Quality Jobs)**. Espera a un [trabajador que cumpla los requisitos para terminarlo](topic:finishers); un trabajador común no puede terminarlo mediante el uso compartido.

<!-- l10n:robot-work -->
Cuando **{k:QJ_SettingsAllowRobotPreparation}** está desactivado, un objeto compartido sin terminar sigue reservado para el trabajo de los colonos mientras su orden original exista y esté gestionada. Retomarlo mediante una orden no gestionada no permite que un mecanoide o robot se haga cargo de ese trabajo. Los mecanoides y robots aptos aún pueden realizar las tareas de finalización asignadas por Quality Jobs.

<!-- l10n:off -->
Desactiva el uso compartido para que cada trabajo compartido vuelva a pertenecer a un solo trabajador. Las órdenes gestionadas aún pueden reservar el toque final. Desactivar **{k:QJ_ManageBill}** libera los trabajos de esa orden que esperan un acabado de calidad; no desactiva el uso compartido para la colonia.

