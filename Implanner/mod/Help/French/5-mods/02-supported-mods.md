---
title: Mods pris en charge
---
Tout mod d'implants utilisant les données d'opérations du jeu fonctionne sans configuration. Des joueurs ont confirmé Expanded Prosthetics and Organ Engineering et CeleTech, dont les implants sont lus comme ceux du jeu de base. Les mods ci-dessous ont été vérifiés individuellement dans leurs données.

![Souche de mécanites sélectionnée dans la liste Torse](mechanites-picked.png)

![Autres souches indiquant le remplacement de la souche choisie](mechanites-overrides.png)

**[FSF] Advanced Bionics Expansion** : les souches de mécanites s'excluent mutuellement. En injecter une retire toute autre souche du corps ; le sélecteur les traite donc comme un seul emplacement et nomme la souche remplacée. Les mécanites améliorant l'immunité ne sont pas une souche et coexistent avec chacune. La vessie bionique avancée et l'améliorateur d'hygiène avancé sont concernés par les options de compatibilité avec Dubs Bad Hygiene.

**Dubs Bad Hygiene** : la vessie bionique et l'améliorateur d'hygiène sont des implants du torse que le jeu autorise à combiner avec les versions avancées de [FSF] Advanced Bionics Expansion. Les deux options de compatibilité décident si Implanner autorise aussi ce cumul.

![Yeux bioniques simple et avancé indiquant le remplacement de l'œil modulaire sélectionné](modular-eye.png)

**Integrated Implants** : les bras supplémentaires sont limités à un par côté ; choisir un autre type du même côté remplace le premier. L'armure sous-cutanée et sa version de prestige, les tourelles d'épaule, les queues et les explosifs implantés forment chacun un groupe à choix unique. Les bioniques modulaires sont des améliorations sur place : choisir un œil modulaire fait poser un œil bionique si nécessaire, puis effectuer l'amélioration, qui consomme un composant sans second œil. Ces améliorations exigent EBSG Framework et ne sont pas proposées avec Medical System Expansion 2. Les modules eux-mêmes sont insérés depuis l'inventaire sans chirurgie et ne sont donc pas listés.

**Vanilla Genetics Expanded** : ses implants hybrides pour colons sont listés avec les autres. Ceux des animaux ne le sont pas, car Implanner ne planifie que les colons.

Certains hybrides remplacent une partie : le module d'hibernation remplace le cerveau. D'autres s'y posent : le renforcement neuronal se pose sur le cerveau. Des implants ordinaires peuvent être ajoutés à une partie portant un hybride, mais pas à une partie remplacée.

Poser n'importe quel hybride déloge tout ce qui est déjà installé sur cette partie. Les implants délogés tombent près du médecin.

- Implanner pose l'hybride d'abord, afin de ne rien retirer.
- Si le colon porte déjà des implants à cet endroit, Implanner les laisse tomber, les réserve à ce colon puis les repose juste après.
- Implanner ne pose jamais un hybride si un élément de cette partie devait être perdu définitivement.

Les hybrides tirent leur qualité du génocadre utilisé pour les fabriquer (voir [Qualité minimale](topic:minimum-quality)).

**Quality Bionics Remastered** : les bioniques ont des qualités ; une meilleure qualité améliore l'efficacité et les points de vie. L'infobulle montre l'efficacité d'horrible à légendaire, et chaque plan définit la qualité minimale à poser (voir [Qualité minimale](topic:minimum-quality)).

**Elite Bionics Framework** : les implants augmentant ou diminuant les points de vie d'une partie affichent ce changement dans leur infobulle.

![Demande de prérequis pour un module de jambe](requirement-prompt.png)

![Plan après ajout : jambe bionique et module sélectionnés ensemble](requirement-added.png)

**Bionic modularity** : chaque module occupe un emplacement sur sa partie. Un module de combat et un module de travail tiennent sur le même bras, mais pas deux modules de combat. Les modules de membres se posent sur un membre bionique et partagent la partie : un bras bionique prévu et son module peuvent coexister. Cocher un module sans bionique sur cette partie dans le plan ouvre une liste d'hôtes possibles, avec le moins avancé sélectionné par défaut. **Ajouter au plan** ajoute votre choix et le module ensemble. **Annuler** n'ajoute rien.
