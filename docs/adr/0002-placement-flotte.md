# ADR 0002 : algorithme de placement de la flotte

## Statut et date

Accepté — 2026-09-15.

## Contexte

Le placement aléatoire de 5 navires (tailles 5/4/3/3/2, soit 17 cases) sur une grille 10×10 (100 cases) doit respecter : bornes de la grille, pas de chevauchement. Il faut un algorithme simple à expliquer et à tester.

## Options envisagées

1. **Essai-erreur borné** : pour chaque navire, tirer une origine et une orientation au hasard, vérifier via `Board.TryPlaceShip` (bornes + chevauchement), réessayer jusqu'à `maxAttemptsPerShip` (200) en cas d'échec ; lever une exception si la limite est atteinte.
2. **Backtracking déterministe** : construire un algorithme de placement avec retour arrière garantissant toujours une solution si une solution existe.

## Décision

Option 1. À cette échelle (17 cases utiles sur 100, ratio de remplissage ~17 %), la probabilité qu'un essai aléatoire échoue est faible et decroît vite pour les petits navires ; 200 tentatives par navire donne une marge très confortable sans complexité d'implémentation supplémentaire. `Board.TryPlaceShip` reste l'unique source de vérité pour les règles de placement (bornes + chevauchement), réutilisée à la fois par le placement aléatoire et par les tests qui posent une flotte fixe — pas de logique dupliquée entre "placement aléatoire" et "placement contrôlé".

## Conséquences

- Code simple, facile à expliquer et à tester (`PlaceFleetRandomly_ProducesExactlyStandardFleet_NoOverlap_NoOutOfBounds` sur de nombreuses graines).
- Risque théorique, non observé en pratique à cette taille de grille/flotte : dans une configuration extrêmement défavorable, les 200 tentatives pourraient s'épuiser et lever une `InvalidOperationException`. Accepté pour ce périmètre ; à revoir si la taille de grille ou le nombre de navires changent significativement.

## Vérification et réexamen

Test de non-régression sur N graines différentes vérifiant qu'aucune levée d'exception ne se produit et que la flotte posée correspond exactement à la spécification standard. À réexaminer si une variante de grille plus dense est ajoutée au backlog.

## Références

`BattleShip.Models/Domain/Board.cs` (`TryPlaceShip`, `PlaceFleetRandomly`).
