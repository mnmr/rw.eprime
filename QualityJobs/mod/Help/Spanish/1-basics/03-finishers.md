---
title: Elegir a los encargados del acabado
---
<!-- l10n:requirements -->
Usa estos controles en una orden de trabajo o un edificio. Otros trabajadores aptos pueden realizar las fases previas; solo quien dé el toque final debe cumplir tus requisitos.

<!-- l10n:controls -->
- **Habilidad para finalizar** establece un nivel mínimo de habilidad.
- **{k:QJ_AutoBest}** selecciona a los trabajadores según la calidad esperada en lugar del nivel mínimo de habilidad fijado manualmente. El control deslizante manual se conserva para cuando desactives esta opción.
- **{k:QJ_RequireInspired}** espera a que el trabajador tenga esa inspiración. Completar el trabajo la consume como de costumbre.
- **{k:QJ_RequireSpecialist}** requiere un rol de Ideology que otorgue una bonificación a la calidad de producción. Esta opción aparece con Ideology.

<!-- l10n:odds-image -->
![Requisitos de acabado y vista previa de las probabilidades de calidad](finisher-options.png)

<!-- l10n:best -->
La selección automática compara a los colonos aptos, los mecanoides de la colonia y los robots compatibles, y elige al trabajador con la mayor calidad esperada. Tiene en cuenta la habilidad, Creatividad inspirada, las bonificaciones de los especialistas en producción y las bonificaciones de calidad de los mods compatibles. Esto maximiza la calidad media; no garantiza el resultado de una tirada individual.

<!-- l10n:availability -->
Los trabajadores que cumplen los demás requisitos siguen contando en la comparación mientras están ausentes, derribados, dormidos, en crisis mental o recargándose, incluidos los robots en sus estaciones. Si ninguno de los trabajadores mejor clasificados puede finalizar en este mapa, el trabajo espera. Por ejemplo, un artesano mejor que esté en una caravana puede impedir que un trabajador peor clasificado que permanezca en la colonia lo finalice. No hay un plazo de espera tras el que se reduzca el requisito de calidad.

<!-- l10n:manual-threshold -->
Con **{k:QJ_AutoBest}** desactivado, cualquier trabajador capaz de realizar el trabajo que cumpla el nivel mínimo de habilidad fijado manualmente y los demás requisitos puede finalizarlo. Un trabajador mejor en otro lugar de la colonia no lo bloquea.

<!-- l10n:robot-finishers -->
Los mecanoides y los robots compatibles deben cumplir los mismos requisitos de acabado que los colonos. Exigir Creatividad inspirada o un rol de especialista en producción sigue excluyendo a los trabajadores que no los tengan. Desactivar **{k:QJ_SettingsAllowRobotPreparation}** reserva las fases previas del trabajo para los colonos; los mecanoides y robots aptos pueden seguir recibiendo tareas de acabado de Quality Jobs.

<!-- l10n:odds -->
En la tabla **{k:QJ_OddsHeader}** de una orden de trabajo, **{k:QJ_OddsColConfig}** estima las probabilidades según el nivel mínimo de habilidad para finalizar y las bonificaciones exigidas. Con **{k:QJ_AutoBest}** activado, si se encuentra un candidato apto entre todos los trabajadores de la colonia, muestra en su lugar las probabilidades de ese trabajador. Si no hay ningún candidato, usa tus ajustes manuales; esto no significa que haya un trabajador disponible para finalizar.

<!-- l10n:odds-best -->
**{k:QJ_OddsColBest}** muestra las probabilidades del trabajador mejor clasificado capaz de realizar el trabajo en este mapa, sin tener en cuenta tus requisitos adicionales de acabado. Por tanto, las dos columnas pueden corresponder a trabajadores distintos. Son probabilidades, no garantías; el resultado depende del trabajador que finalmente termine el trabajo.

<!-- l10n:eligible-finishers -->
**Aptos para terminar** enumera los trabajadores que pueden cumplir los requisitos en este mapa ahora mismo; la lista excluye a los trabajadores ausentes, derribados o con una crisis mental. En las órdenes, la lista usa los requisitos de la orden seleccionada, aunque los recuentos de la cola incluyan otras órdenes que fabrican el mismo producto en este mapa.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** da prioridad al trabajo de acabado asignado sobre las demás tareas, salvo apagar incendios, prestar atención médica, rescatar y guardar reposo en cama. No interrumpe la tarea actual. Con esta opción desactivada, el acabado tiene prioridad dentro de su tipo de trabajo, después de los tipos de trabajo con mayor prioridad. El trabajador debe poder realizar el tipo de trabajo habitual del objeto.

