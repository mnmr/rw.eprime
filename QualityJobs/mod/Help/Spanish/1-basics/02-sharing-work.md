---
title: Compartir trabajo sin terminar
---
<!-- l10n:sharing -->
Con **{k:QJ_SettingsShareWork}** activado en Opciones, otro trabajador apto puede continuar un objeto sin terminar en el que nadie esté trabajando, aunque su creador original haya muerto. El uso compartido funciona independientemente de **{k:QJ_ManageBill}**.

<!-- l10n:matching -->
El siguiente trabajador necesita una orden de producción utilizable para la misma receta. Los ingredientes del objeto deben estar permitidos por esa orden y su estilo debe coincidir. Además, el objeto debe ser accesible, estar permitido y estar disponible para su reserva. Compartir no permite eludir las asignaciones de trabajo habituales ni las restricciones de las órdenes de producción.

<!-- l10n:handoff -->
Un objeto en el que nadie está trabajando se desvincula de su trabajador y orden originales para que una orden compatible pueda retomarlo. Mientras el objeto sin terminar de esa orden esté esperando o siendo transportado, la orden no inicia otro igual. No se interrumpe un trabajo en curso solo para pasárselo a otra persona.

<!-- l10n:reserved -->
Compartir el trabajo inicial es distinto de reservar el toque final. Un objeto gestionado que se pausa justo antes de completarse muestra **Autor: reservado (Quality Jobs)**. Espera a un [trabajador que cumpla los requisitos de acabado](topic:finishers); un trabajador corriente no puede terminarlo mediante el uso compartido.

<!-- l10n:robot-work -->
Cuando **{k:QJ_SettingsAllowRobotPreparation}** está desactivado, el trabajo de un objeto compartido sin terminar sigue reservado a los colonos mientras su orden original siga existiendo y esté gestionada. Retomarlo mediante una orden no gestionada no permite que un mecanoide o robot se haga cargo de ese trabajo. Los mecanoides y robots aptos pueden seguir realizando las tareas de acabado asignadas por Quality Jobs.

<!-- l10n:off -->
Desactiva el trabajo compartido para que el trabajo ordinario sin terminar siga a cargo de su artesano original. Las órdenes gestionadas pueden seguir reservando el toque final y asignárselo a un trabajador apto para terminarlo. Desactivar **{k:QJ_ManageBill}** elimina los requisitos de acabado de esa orden y libera sus trabajos pendientes de terminar; no desactiva el trabajo compartido para la colonia.

