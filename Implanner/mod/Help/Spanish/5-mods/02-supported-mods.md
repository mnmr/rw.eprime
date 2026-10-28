---
title: Mods compatibles
---
<!-- l10n:b-3d8630b508cc -->
Cualquier mod de implantes que use los datos de cirugía funciona sin configuración. Los jugadores han confirmado Expanded Prosthetics and Organ Engineering y CeleTech, leídos como contenido original. Los siguientes se comprobaron individualmente contra sus datos.

<!-- l10n:b-fc34121549e7 -->
![Una cepa de mecanitos seleccionada en Torso](mechanites-picked.png)

<!-- l10n:b-8e0b09ad2325 -->
![Otras cepas avisan de que sustituyen a la seleccionada](mechanites-overrides.png)

<!-- l10n:b-8115e58333ac -->
**[FSF] Advanced Bionics Expansion**: las cepas de mecanitos se excluyen. Inyectar una elimina cualquier otra del cuerpo; el selector las trata como una casilla y nombra la sustituida. Los mecanitos inmunitarios no son una cepa y coexisten con todas. La vejiga biónica avanzada y el potenciador avanzado de higiene están cubiertos por las opciones de compatibilidad junto con Dubs Bad Hygiene.

<!-- l10n:b-951da050479a -->
**Dubs Bad Hygiene**: la vejiga biónica y el potenciador de higiene son implantes de torso que el juego permite combinar con las versiones avanzadas de [FSF] Advanced Bionics Expansion. Las dos opciones de compatibilidad deciden si Implanner también lo permite.

<!-- l10n:b-41bdd507c8fd -->
![Los ojos básico y avanzado avisan de que sustituyen al modular seleccionado](modular-eye.png)

<!-- l10n:b-d5d8d7431467 -->
**Integrated Implants**: permite un brazo adicional por lado; elegir otro tipo en el mismo lado sustituye al anterior. La armadura subdérmica y su versión de prestigio, las torretas de hombro, las colas y los explosivos implantados forman cada uno un grupo que admite solo uno. Las partes modulares se mejoran sobre la base instalada: al elegir un ojo biónico modular, se instala antes el ojo básico si falta y después se mejora consumiendo un componente, sin otro ojo. Estas mejoras requieren EBSG Framework y no se ofrecen con Medical System Expansion 2 cargado. Los módulos se insertan desde el inventario sin cirugía y no se muestran.

<!-- l10n:b-261a620cdad5 -->
**Vanilla Genetics Expanded**: sus híbridos para colonos aparecen con los demás. Los implantes animales no, porque Implanner solo planifica colonos.

<!-- l10n:b-8b2ffcc18549 -->
Algunos híbridos reemplazan la parte: el módulo de hibernación sustituye el cerebro. Otros se colocan sobre ella, como el refuerzo neuronal. Los implantes normales pueden instalarse en una parte que lleva un híbrido encima, pero no si el híbrido la ha sustituido.

<!-- l10n:b-cd881c381961 -->
Instalar cualquier híbrido expulsa lo ya instalado en esa parte. Los implantes expulsados caen junto al médico.

<!-- l10n:b-ad1a61d0da90 -->
- Implanner instala primero el híbrido para no tener que sacar nada.
- Si ya había implantes, permite que caigan, los reserva para ese colono y los reinstala justo después.
- Nunca instala un híbrido si algo de esa parte se perdería permanentemente.

<!-- l10n:b-c282558e897d -->
La calidad del híbrido procede de la estructura genética utilizada (consulta [Calidad mínima](topic:minimum-quality)).

<!-- l10n:b-721ad339d2c2 -->
**Quality Bionics Remastered**: las partes biónicas tienen calidad; las mejores funcionan mejor y tienen más salud. La descripción muestra la eficiencia desde horrible hasta legendaria, y cada plan establece su calidad mínima (consulta [Calidad mínima](topic:minimum-quality)).

<!-- l10n:b-7a2e3799838c -->
**Elite Bionics Framework**: los implantes que modifican la salud de una parte muestran ese cambio en su descripción.

<!-- l10n:b-c7cd0a958354 -->
![Requisitos de un módulo de pierna](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![Plan tras añadirlo: pierna biónica y módulo seleccionados](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
**Bionic modularity**: cada módulo ocupa una casilla de su parte; un módulo de combate y otro laboral caben en el mismo brazo, pero dos de combate no. Los módulos de extremidades se instalan sobre una parte biónica y la comparten, así que el brazo y su módulo pueden coexistir en el plan. Marcar un módulo sin la parte biónica abre un diálogo con las bases compatibles, seleccionando por defecto la menos avanzada. **Añadir al plan** añade ambos; **Cancelar** no añade nada.
