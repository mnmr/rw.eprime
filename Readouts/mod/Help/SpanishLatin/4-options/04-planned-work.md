---
title: Trabajo previsto
---
<!-- l10n:b-7aa1a59c7379 -->
Las opciones de trabajo previsto descuentan de las cantidades mostradas el costo estimado de los materiales necesarios para proyectos o planos.

<!-- l10n:b-ee40b81e925d -->
- Reservar recursos para proyectos descuenta los ingredientes necesarios para los proyectos de producción pendientes. Solo se cuenta un ingrediente si el proyecto permite exactamente un tipo de recurso para él. Por ejemplo, permitir cualquier cuero no reserva nada; permitir un solo tipo de cuero incluye el costo de ese cuero. «Hacer X veces» reserva X ejecuciones; «Hacer hasta tener X», las necesarias para el objetivo; «Hacer siempre», una ejecución. Los proyectos suspendidos no reservan nada.
- Reservar recursos para construcciones descuenta lo que aún necesitan planos y estructuras parciales. Los materiales entregados o recogidos para llevarlos se consideran gastados y no se descuentan de nuevo. Ignora planos prohibidos.
- Mostrar los déficits como números negativos indica cuánto falta si las necesidades superan tus existencias, en vez de detenerse en cero.
- Compatibilidad con Quality Jobs ajusta las reservas activadas para proyectos y construcciones a los objetivos de calidad administrados por Quality Jobs. Estima los materiales necesarios para los intentos adicionales y toma en cuenta los materiales recuperados al desmantelar construcciones. Activa la opción de reserva correspondiente de arriba para usarla. La fila queda desactivada si EPrime's Quality Jobs falta o es antiguo; el motivo aparece al pasar el cursor.

<!-- l10n:planned-work-tooltip-columns -->
Pasa el cursor sobre un recurso o conjunto de recursos con descuentos por trabajo previsto para ver la tabla Trabajo previsto. Trabajo restante indica las ejecuciones de recetas o construcciones pendientes. Costo por intento indica los materiales necesarios para un intento. Descontado es la cantidad que se resta del total mostrado, teniendo en cuenta los materiales ya entregados o en camino a la construcción y los reintentos de calidad estimados cuando está activada la compatibilidad con Quality Jobs.

<!-- l10n:b-a4ac1a2f64dc -->
Con los números negativos activados, las carencias del trabajo previsto se muestran en naranja:

<!-- l10n:b-b209bc9139c1 -->
![Materiales con números negativos activados](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Desactivados, las mismas casillas se detienen en cero:

<!-- l10n:b-a997f13279d8 -->
![Las mismas casillas sin números negativos](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
El trabajo previsto se actualiza junto con los recuentos de recursos cada pocos segundos de tiempo de juego. Los proyectos nuevos o modificados aparecen en la siguiente actualización; reanuda el juego para que avance el tiempo.
