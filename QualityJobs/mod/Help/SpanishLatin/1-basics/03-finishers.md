---
title: Elegir quién finaliza el trabajo
---
<!-- l10n:requirements -->
Usa estos controles en una orden de trabajo o una construcción. Otros trabajadores aptos pueden realizar el trabajo previo; solo quien dé el toque final debe cumplir tus requisitos.

<!-- l10n:controls -->
- **Habilidad para terminar** establece un nivel mínimo de habilidad.
- **{k:QJ_AutoBest}** selecciona a los trabajadores según la calidad esperada en lugar del nivel mínimo de habilidad que fijaste manualmente. El control deslizante manual conserva su valor para cuando desactives esta opción.
- **{k:QJ_RequireInspired}** espera a que se active esa inspiración. Completar el trabajo la consume como de costumbre.
- **{k:QJ_RequireSpecialist}** exige un rol de Ideology que otorgue una bonificación de calidad de producción. Esta opción aparece con Ideology.

<!-- l10n:odds-image -->
![Requisitos para terminar y vista previa de las probabilidades de calidad](finisher-options.png)

<!-- l10n:best -->
La selección automática compara a los colonos aptos, los mecanoides de la colonia y los robots compatibles, y elige al trabajador con la mayor calidad esperada. Toma en cuenta la habilidad, la creatividad inspirada, las bonificaciones de los especialistas en producción y las bonificaciones de calidad de los mods compatibles. Esto maximiza la calidad promedio, pero no garantiza el resultado de una tirada individual.

<!-- l10n:availability -->
Los trabajadores que cumplen los demás requisitos siguen contando en la comparación mientras están ausentes, derribados, dormidos, en crisis mental o recargándose, incluidos los robots en sus estaciones. Si ninguno de los trabajadores mejor clasificados puede terminar el trabajo en este mapa, el trabajo espera. Por ejemplo, un artesano mejor que esté en una caravana puede impedir que un trabajador con una clasificación menor que se quedó en la colonia lo termine. No hay un plazo de espera después del cual se reduzca el requisito de calidad.

<!-- l10n:manual-threshold -->
Con **{k:QJ_AutoBest}** desactivado, cualquier trabajador capaz de realizar el trabajo que cumpla el nivel mínimo de habilidad que fijaste manualmente y los demás requisitos puede terminarlo. Un trabajador mejor en otra parte de la colonia no lo bloquea.

<!-- l10n:robot-finishers -->
Los mecanoides y los robots compatibles deben cumplir los mismos requisitos para terminar el trabajo que los colonos. Exigir creatividad inspirada o un rol de especialista en producción sigue excluyendo a los trabajadores que no los tengan. Desactivar **{k:QJ_SettingsAllowRobotPreparation}** reserva el trabajo previo para los colonos; los mecanoides y robots aptos aún pueden aceptar tareas de finalización de Quality Jobs.

<!-- l10n:odds -->
En la tabla **{k:QJ_OddsHeader}** de una orden de trabajo, **{k:QJ_OddsColConfig}** estima las probabilidades según el nivel mínimo de habilidad para terminar y las bonificaciones que exiges. Con **{k:QJ_AutoBest}** activado, si se encuentra un candidato apto entre todos los trabajadores de la colonia, muestra las probabilidades de ese trabajador. Si no hay ningún candidato, usa tus ajustes manuales; esto no significa que haya un trabajador disponible para terminar.

<!-- l10n:odds-best -->
**{k:QJ_OddsColBest}** muestra las probabilidades del trabajador mejor clasificado capaz de realizar el trabajo en este mapa, sin tener en cuenta tus requisitos adicionales para terminarlo. Por eso, las dos columnas pueden corresponder a trabajadores distintos. Son probabilidades, no garantías; el resultado depende de quién termine realmente el trabajo.

<!-- l10n:eligible-finishers -->
**Aptos para terminar** muestra a los trabajadores que pueden cumplir los requisitos en este mapa ahora; la lista excluye a los trabajadores ausentes, derribados o con una crisis mental. En las órdenes, la lista usa los requisitos de la orden seleccionada, aunque los recuentos de la cola incluyan otras órdenes que fabrican el mismo producto en este mapa.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** hace que el trabajo de finalización asignado tenga prioridad sobre otros trabajos, excepto apagar incendios, brindar atención médica, rescatar y descansar en cama. No interrumpe la tarea actual. Si se desactiva, la finalización tiene prioridad dentro de su tipo de trabajo, después de los tipos de trabajo con mayor prioridad. El trabajador debe poder realizar el tipo de trabajo habitual del objeto.

