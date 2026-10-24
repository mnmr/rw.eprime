---
title: Cómo se leen implantes de mods
---
Implanner no necesita una lista de mods compatibles. Lee todas las cirugías cargadas del juego, expansiones y mods; cualquier operación que instale un implante en una parte corporal aparece en el selector. Esos datos también determinan la coexistencia:

- **Una sustitución por parte**: un brazo biónico reemplaza el hombro; dos sustituciones nunca comparten parte. Una sustitución elimina los implantes de esa parte y de las partes inferiores.
- **Implantes en una misma parte**: coexisten salvo incompatibilidad declarada por la cirugía. Así se excluyen las glándulas cutáneas originales y se definen las casillas de módulos de mods.
- **Retirada al instalar**: algunos implantes eliminan a un rival en cualquier parte del cuerpo al instalarse. Dos que se eliminan mutuamente nunca coexisten.
- **Cirugías que vacían una parte**: algunas retiran todo antes de añadir un implante que no sustituye la parte. Lo instalado después permanece, así que Implanner instala este primero y repone lo exhaz clic endo.
- **Módulos sobre partes biónicas**: si la cirugía está programada para instalar sobre una parte artificial, el módulo la comparte con la biónica en vez de sustituirla.
- **Mejoras sin reemplazar la base**: una operación que quita un implante y añade otro mejora la parte instalada. La mejora aparece como implante propio.
- **Solo adquiribles**: si ningún banco fabrica su objeto, se ocultan hasta activar la opción correspondiente.

Hay dos límites: nunca se muestran módulos insertados desde el inventario sin cirugía, y no pueden leerse reglas que existan solo en código y no en datos. Todo lo que un mod exprese mediante sus cirugías funciona sin modificar Implanner.
