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
La sección **{k:QJ_StatusHeader}** cuenta los objetos sin terminar registrados para este producto entre todas las órdenes y mesas de trabajo de este mapa: trabajo compartido **Sin terminar**, objetos en **Espera** de un trabajador apto y objetos ya asignados que se están **Terminando**. La lista de trabajadores aptos y el aviso usan los requisitos de la orden seleccionada; otras órdenes del mismo producto pueden tener requisitos distintos. Pasa el cursor sobre el estado y la lista para ver más detalles. Consulta [cuando el trabajo espera](topic:waiting) si nada avanza.

<!-- l10n:disable -->
Desmarca **{k:QJ_ManageBill}** para que vuelva a completarse normalmente. Otros trabajadores podrán seguir realizando su trabajo compartido si la opción de compartir está activada.

