---
title: Configurar una orden
---
<!-- l10n:open -->
Abre **Detalles** en una orden de trabajo para un objeto con calidad. El panel de **{k:QJ_QualityPanelTitle}** aparece junto a los controles habituales de la orden en las recetas compatibles que dejan un objeto sin terminar. Las recetas sin calidad o sin trabajo sin terminar conservan su funcionamiento habitual.

<!-- l10n:bill-image -->
![Una orden de trabajo en una mesa de trabajo con su panel de Quality Jobs](crafting-bill.png)

<!-- l10n:setup -->
Activa **{k:QJ_ManageBill}** y elige los [requisitos para finalizar](topic:finishers). Los trabajadores comunes pueden realizar la mayor parte de la fabricación. Si el trabajador actual cumple los requisitos, termina el objeto directamente; de lo contrario, el objeto casi terminado se deja a un lado y la orden puede iniciar más objetos dentro de su límite de existencias.

<!-- l10n:dispatch -->
Cuando un trabajador cumple los requisitos para finalizar, Quality Jobs crea una orden temporal **Finalizar …** y se la asigna. El trabajador recoge el objeto sin terminar y lo completa. La orden temporal se elimina después automáticamente. Los objetos en espera se pueden transportar y almacenar como otros objetos sin terminar.

<!-- l10n:finisher-bill-restrictions -->
El nivel máximo de habilidad y la asignación a un trabajador específico de la orden original se aplican hasta que el objeto se deja a un lado para su finalización. Puedes asignar el trabajo a un aprendiz mientras un colono más capacitado, un mecanoide de la colonia o un robot compatible se encarga de la orden dedicada a finalizarlo. Quienes finalicen el trabajo deben poder realizar la receta y cumplir los requisitos de Quality Jobs.

<!-- l10n:status -->
La sección **{k:QJ_StatusHeader}** muestra el trabajo compartido **Sin terminar**, los objetos **En espera** de alguien que los termine y los objetos ya asignados para su **Finalización**. Pasa el cursor sobre el estado y la lista de trabajadores que pueden terminar los objetos para ver más detalles. Consulta [cuando el trabajo queda en espera](topic:waiting) si nada avanza.

<!-- l10n:disable -->
Desmarca **{k:QJ_ManageBill}** para que vuelva a completarse normalmente. Otros trabajadores podrán seguir realizando su trabajo compartido si la opción de compartir está activada.

