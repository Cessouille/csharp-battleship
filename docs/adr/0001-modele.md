# ADR 0001 : représentation de l'état de partie

## Statut et date

Accepté — 2026-09-15.

## Contexte

Le moteur de jeu doit être vérifiable indépendamment de HTTP/JSON/gRPC (spécification 1 du référentiel). Il faut choisir comment représenter une partie (deux grilles, flotte, tirs, tour) et comment résoudre un tir du joueur suivi de la riposte de l'ordinateur, sans introduire d'état superflu.

## Options envisagées

1. **Machine à état explicite "à qui le tour"** (`CurrentTurn: PlayerId`, un endpoint par joueur) — nécessaire dans un contexte multijoueur avec latence réseau entre les deux joueurs, où chaque camp agit de façon asynchrone.
2. **Résolution synchrone** : un seul point d'entrée (`Game.PlayHumanShot`) qui résout le tir du joueur puis, si la partie continue, tire immédiatement pour l'ordinateur dans le même appel — pas de champ "tour" persisté.

## Décision

Option 2. Il n'y a qu'un seul joueur humain face à un ordinateur qui répond instantanément dans le même processus : aucune latence réseau à arbitrer entre les deux camps, donc aucune machine à état "à qui le tour" n'apporte de garantie supplémentaire — elle ajouterait un état mutable de plus à maintenir en cohérence sans bénéfice.

`Game` est l'agrégat racine : il possède `HumanBoard` et `ComputerBoard` (deux `Board`), et expose `Winner`/`Status` calculés (`Status` dérive de `Winner is null`, pas un champ indépendant qui pourrait diverger).

## Conséquences

- Le contrat public de l'API est plus simple : un seul endpoint de tir renvoie à la fois le résultat du joueur et celui de l'ordinateur.
- Cette représentation ne convient pas telle quelle à une extension multijoueur (backlog) : elle suppose que la riposte adverse peut être calculée sans attendre d'action réseau. Une extension multijoueur devra réintroduire une notion de tour explicite — c'est un renoncement assumé pour ce périmètre, pas un oubli.
- `Board` est réutilisé à l'identique pour les deux joueurs (même méthode `ReceiveShot`, mêmes règles de validité), ce qui garantit structurellement que l'ordinateur ne peut pas enfreindre une règle que le joueur humain devrait respecter.

## Vérification et réexamen

Vérifié par les tests `GameTests` (notamment `PlayHumanShot_SinkingLastEnemyShip_EndsGame_NoComputerCounterShot` et `ComputerAutoShot_OnlyTargetsUntriedCellsOnHumanBoard`). À revoir si le backlog multijoueur est retenu : cette ADR serait alors remplacée par une nouvelle décision introduisant un état de tour.

## Références

`BattleShip.Models/Domain/Game.cs`, `BattleShip.Models/Domain/Board.cs`, Referentiel.md diapo 36-38.
