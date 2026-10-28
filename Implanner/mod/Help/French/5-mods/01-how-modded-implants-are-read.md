---
title: Lecture des implants de mods
---
<!-- l10n:b-935816ca622e -->
Implanner n'a pas besoin d'une liste de mods compatibles pour lire leurs implants. Il lit toutes les opérations chargées par le jeu, ses extensions et les mods. Toute opération posant un implant sur une partie du corps devient une entrée du sélecteur. Les mêmes données lui indiquent quels implants peuvent cohabiter :

<!-- l10n:b-d02011c283d2 -->
- **Un remplacement par partie** : un bras bionique remplace l'épaule. Deux remplacements ne partagent donc jamais une partie ; un remplacement efface tous les implants montés sur elle ou ses parties inférieures.
- **Implants sur une même partie** : ils coexistent sauf si l'opération les déclare incompatibles. C'est ainsi que les glandes cutanées du jeu de base s'excluent, et que les mods définissent les emplacements de modules.
- **Retrait à la pose** : certains implants retirent un rival où qu'il soit sur le corps. Deux implants qui se retirent mutuellement ne peuvent jamais coexister.
- **Opérations vidant une partie** : certaines retirent tout d'une partie avant d'ajouter un implant qui ne la remplace pas. Les implants posés ensuite restent : Implanner pose donc cet implant d'abord et remet ceux qu'il déloge.
- **Modules montés sur les bioniques** : une opération conçue pour une partie artificielle partage cette partie avec le bionique au lieu de le remplacer.
- **Améliorations sur place** : une opération retirant un implant pour en ajouter un autre améliore la partie installée. La version améliorée est listée séparément.
- **Objets uniquement disponibles à l'achat** : un implant qu'aucun établi ne fabrique reste masqué jusqu'à l'activation de l'option correspondante.

<!-- l10n:b-ea92c69a5624 -->
Deux éléments restent invisibles : les modules insérés depuis l'inventaire sans opération, et les règles présentes seulement dans le code d'un mod, sans données correspondantes. Tout ce qu'un mod exprime dans ses opérations fonctionne sans modifier Implanner.
