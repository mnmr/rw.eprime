---
title: Opciones y valores predeterminados
---
<!-- l10n:options-image -->
![La pestaña Opciones con los valores predeterminados de órdenes y construcción](options.png)

<!-- l10n:scope -->
Con una partida cargada, Opciones modifica los ajustes de esa colonia. Desde el menú principal, los ajustes del mod proporcionan los valores predeterminados para las partidas nuevas. **{k:QJ_SettingsShowToolbarButton}** es una preferencia personal de visualización; ocultarlo no desactiva el mod. Puedes seguir abriendo los ajustes del mod desde el menú Opciones del juego.

<!-- l10n:bill-defaults -->
Las **{k:QJ_SettingsBillDefaults}** controlan las órdenes que usan los valores predeterminados. Los ajustes individuales de cada orden tienen prioridad sobre ellas. **{k:QJ_SettingsManageNewBills}** afecta a las órdenes nuevas y a las existentes que aún usan ese valor predeterminado de gestión. Desactivarlo libera los objetos en espera de esas órdenes para que puedan terminarse normalmente. Los demás valores predeterminados también pueden afectar a las órdenes existentes que no tengan un ajuste personalizado para esa opción. El límite de existencias sin terminar se comparte por producto y se calcula por separado en cada mapa.

<!-- l10n:construction-defaults -->
Las **{k:QJ_SettingsConstructionDefaults}** se aplican al activar la gestión de una construcción o cuando esta se gestiona automáticamente. Las construcciones que ya están bajo gestión conservan sus propios ajustes. El objetivo de construcción usa **{k:QJ_NoRetries}**; el de fabricación usa **{k:QJ_AnyQuality}**.

<!-- l10n:global -->
**{k:QJ_SettingsShareWork}** y **{k:QJ_SettingsHighPriorityFinish}** se aplican a toda la colonia. Consulta [trabajo compartido](topic:sharing-work) y [quienes finalizan](topic:finishers) para conocer sus efectos. **{k:QJ_DisableButton}** está pensado para [desinstalar el mod de forma segura](topic:removing), no para pausar una orden.

<!-- l10n:robot-preparation -->
**{k:QJ_SettingsAllowRobotPreparation}** se guarda para la colonia actual y está activado inicialmente, incluso en partidas existentes. Desactívalo para reservar a los colonos el trabajo en las órdenes de fabricación y los planos de construcción gestionados, de modo que reciban la experiencia de habilidad correspondiente. Esto también detiene a los mecanoides y robots que ya estén realizando ese trabajo. Los mecanoides aptos y los robots compatibles pueden seguir finalizando trabajos asignados por Quality Jobs. Los trabajos normales sin gestionar no se ven afectados; los [objetos sin terminar compartidos](topic:sharing-work) mantienen la restricción de la orden gestionada original. Esta opción está disponible con una partida cargada y se sincroniza en Multiplayer.

