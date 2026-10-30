---
title: Importar y exportar
---
<!-- l10n:b-63a50794a977 -->
Los botones de la esquina de Roles permiten llevar configuraciones entre partidas y amigos:

<!-- l10n:b-4f44ec3259f6 -->
![Importar, Exportar y Restaurar valores predeterminados](io-buttons.png)

<!-- l10n:b-fcca50ae80e8 -->
Exportar guarda la configuración completa de roles en un archivo o en el portapapeles: roles, grupos y su orden, iconos personalizados, colores de la paleta (incluidos los que no se usan), reglas, rutas de entrenamiento, ajustes de recomendaciones y opciones generales de la partida. También incluye las preferencias de visualización como una sección de importación opcional. Las asignaciones de colonos, los vínculos con órdenes de producción y el estado temporal de las ventanas permanecen en la partida o sesión actual.

<!-- l10n:b-0420bcf88983 -->
![Diálogo de exportación](export-dialog.png)

<!-- l10n:b-9a255dfe5c80 -->
Importar lee un archivo o el portapapeles:

<!-- l10n:b-8a2c8b9bcf40 -->
![Diálogo de importación](import-dialog.png)

<!-- l10n:b-c5c6fbb19706 -->
La vista previa ofrece Combinar o Sobrescribir por sección. Combinar añade y actualiza sin borrar; Sobrescribir iguala la configuración al archivo e indica exactamente qué roles se crearán, cambiarán o eliminarán antes de aplicar los cambios. Sobrescribir también restaura el orden de roles del archivo. Las opciones generales de la partida y las preferencias de visualización tienen casillas separadas; las preferencias de visualización están desmarcadas de forma predeterminada. Los archivos antiguos siguen siendo compatibles, y las secciones que falten en un archivo dejan intactos los ajustes correspondientes.

<!-- l10n:b-67c0a3e8f921 -->
Para reutilizar una configuración en futuras colonias, abre **Roles → Establecer valores predeterminados** y elige **Usar esta configuración**. Las nuevas partidas comienzan con esos roles y opciones; al cargar una partida existente, se conserva su propia configuración. Borrar la configuración predeterminada personal restaura la configuración inicial incluida con el mod para futuras partidas.

<!-- l10n:b-3fe7c915a062 -->
Puedes activar las actualizaciones automáticas al guardar una configuración predeterminada. Tras editar una configuración aprobada, los cambios se guardan como predeterminados al cerrar el editor. Al abrir el editor en una partida con una configuración distinta, se te pregunta si quieres usar la configuración de esa partida, conservar la predeterminada guardada o desactivar las actualizaciones automáticas. Conservar la predeterminada la protege de cambios posteriores en esa partida. Iniciar una nueva partida no reemplaza tus preferencias de visualización actuales.

<!-- l10n:b-b8d402e619ac -->
Los trabajos, habilidades e iconos de mods que no estén disponibles se conservan en las configuraciones importadas para poder exportarlos de nuevo. Si no se encuentra un asentamiento o una nave con el nombre indicado, la restricción de ubicación se conserva inactiva; nunca cambia automáticamente a Cualquier lugar. Modifica esa restricción en el editor de roles al trasladarte a una nueva colonia. Las importaciones con referencias inválidas a roles compuestos o de entrenamiento se rechazan antes de modificar la configuración.
