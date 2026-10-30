---
title: Compartir grupos
---
<!-- l10n:b-a06a00109adf -->
Los cuatro botones superiores del editor permiten compartir, guardar o restablecer tu configuración:

<!-- l10n:b-3abef8172f5c -->
![Importar, Exportar, Establecer valores predeterminados y Restaurar valores predeterminados](editor-buttons.png)

<!-- l10n:b-d2699fb79187 -->
## Exportar una configuración

<!-- l10n:b-e4537f800fee -->
Exportar guarda tus grupos, conjuntos de recursos, umbrales, excepciones de recuento y ajustes del jugador. Elige una ubicación y un nombre de fichero y pulsa Guardar, o usa Copiar al portapapeles.

<!-- l10n:b-355662a5828e -->
![La vista previa de exportación y los controles para guardar](export-setup.png)

<!-- l10n:b-fe372ca48bad -->
Los ajustes del jugador incluyen los grupos activados y las opciones globales de visualización y recuento. Se excluyen el número actual de niveles visibles, el texto de búsqueda, las posiciones de desplazamiento y el estado temporal del editor.

<!-- l10n:b-cf6b7284c676 -->
## Elegir qué importar

<!-- l10n:b-5c19c2c6b9e4 -->
Importar lee un fichero guardado o el portapapeles y muestra una vista previa antes de cambiar nada. Selecciona las categorías que quieras sustituir. La casilla Ajustes del jugador y grupos activados empieza desmarcada para que puedas conservar tus preferencias de visualización.

<!-- l10n:b-537b3af44d4d -->
![La vista previa de importación con selección de categorías](import-selection.png)

<!-- l10n:b-1c0ab7a10c14 -->
Puedes seleccionar cualquier categoría presente en el fichero. Puedes cambiar las categorías relacionadas de una en una, aunque una selección intermedia esté incompleta. Importar permanece desactivado hasta que la configuración elegida tenga referencias válidas a conjuntos y grupos; pasa el cursor sobre el botón desactivado para ver qué falta.

<!-- l10n:import-dependency-recovery -->
Si los umbrales o las reglas de recuento de recursos necesitan un conjunto que falta, importa esos ajustes junto con sus Conjuntos de recursos, o deja esas categorías sin cambios. Si los ajustes del jugador necesitan un grupo que falta, importa **Grupos** y **Ajustes del jugador y grupos activados** juntos, o deja ambos sin cambios.

<!-- l10n:b-3081ef3ab31d -->
Las categorías ausentes de una exportación antigua permanecen sin cambios. Se omiten las entradas que requieren contenido de mods no disponible.

<!-- l10n:b-84361107e203 -->
En multijugador, los grupos, conjuntos, umbrales y excepciones de recuento importados cambian la configuración para todos. Los ajustes del jugador importados solo te afectan a ti.

<!-- l10n:b-f4e74689b23c -->
## Guardar o restaurar valores predeterminados

<!-- l10n:b-6181f2c14286 -->
Establecer valores predeterminados guarda una configuración personal para futuras colonias (consulta [Valores predeterminados para nuevas partidas](topic:new-game-defaults)). Restaurar valores predeterminados pide confirmación y, después, sustituye todos los grupos y conjuntos de recursos por los predeterminados del mod y borra todos los umbrales y excepciones de recuento. En multijugador, este restablecimiento afecta a la configuración compartida de todos.
