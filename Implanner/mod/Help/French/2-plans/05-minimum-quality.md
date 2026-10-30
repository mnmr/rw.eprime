---
title: Qualité minimale
---
<!-- l10n:b-d8fd3884b5cc -->
Avec Quality Bionics Remastered ou Vanilla Genetics Expanded, les implants possèdent une qualité allant d'horrible à légendaire, qui change leur efficacité. Un bouton **Qualité minimale** apparaît alors à côté de **Supprimer le plan**.

<!-- l10n:b-118873404542 -->
![En-tête du plan avec le bouton Qualité minimale](plan-min-quality.png)

<!-- l10n:b-84db128338c2 -->
Le minimum s’applique aux nouvelles poses de ce plan, y compris aux choix hérités. La fabrication automatique vise cette qualité et peut nécessiter plusieurs tentatives pour l’atteindre. Les implants sans qualité ne sont pas concernés. Un nouveau plan qui en complète un autre commence avec le même minimum.

<!-- l10n:b-93af25d8591d -->
Implanner ne remplace jamais une partie saine par une moins bonne. Un bras bionique horrible fonctionne moins bien qu'un bras naturel : il attend donc un meilleur objet, sauf si le bras manque ou est gravement endommagé. Lorsque plusieurs objets conviennent, le meilleur va au colon dont le tour arrive en premier.

<!-- l10n:b-0e7cf20ad9dc -->
Un implant déjà posé du type prévu compte toujours comme terminé, même sous la qualité minimale. Activez **Donner les meilleurs implants aux colons prioritaires** pour autoriser son remplacement par un exemplaire de meilleure qualité, comme expliqué ci-dessous.

<!-- l10n:b-b163fd8a6054 -->
## Meilleurs implants pour les colons prioritaires

<!-- l10n:b-0c6c2d002995 -->
![Option dans l'onglet Automatisation](upgrade-by-priority.png)

<!-- l10n:b-8937cc3e8047 -->
Avec **Donner les meilleurs implants aux colons prioritaires**, dans les options de chirurgie de l'onglet Automatisation, la qualité suit la priorité des colons :

<!-- l10n:b-0d3ec0233e11 -->
- Les nouvelles opérations utilisent la plus basse qualité acceptée, laissant les meilleurs objets libres.
- Un meilleur objet va au colon le plus prioritaire portant un implant moins bon du même type. L'ancien est retiré, le meilleur est posé, puis l'ancien passe au colon suivant.
- Un objet déjà réservé pour un colon moins prioritaire est aussi transféré tant que son opération n'est pas encore programmée.

<!-- l10n:b-e246867de4e8 -->
Remplacer un implant déjà posé par un autre de meilleure qualité nécessite deux opérations : le retrait et la pose. Un remplacement ne commence que si aucune autre opération Implanner n'est en attente pour ce colon et si la limite d'opérations simultanées le permet. Un implant n'est jamais échangé si son retrait enlèverait autre chose de la partie du corps.

<!-- l10n:b-37015122025b -->
Quand Implanner fabrique un implant pour le plan :

<!-- l10n:b-51f50276fd20 -->
- Avec Quality Jobs, l'ordre vise le minimum ; la finition revient donc à un artisan capable de l'atteindre.
- Un implant hybride de Vanilla Genetics Expanded tire sa qualité du génocadre. L'ordre n'utilise que des génocadres au minimum requis ou mieux.
- Sans l'un ni l'autre, tout artisan peut terminer l'implant ; un résultat inférieur au minimum est fabriqué à nouveau. Les stocks minimaux (voir [Conserver du stock](topic:keeping-stock)) arrêtent ce processus avant d'épuiser les matériaux.
