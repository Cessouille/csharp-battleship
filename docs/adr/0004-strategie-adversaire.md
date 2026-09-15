# ADR 0004 : stratégie de l'adversaire (ordinateur)

## Statut et date

Accepté — 2026-09-15.

## Contexte

Le cours exige un adversaire jouable qui respecte les mêmes règles de validité que le joueur ; la difficulté et la sophistication de sa stratégie relèvent du backlog (diapo 38, diapo 60).

## Options envisagées

1. **Tir aléatoire uniforme** parmi les cases non encore jouées du plateau humain.
2. **Stratégie chasse/cible** : tir aléatoire tant qu'aucun tir n'a touché, puis ciblage des cases adjacentes à un dernier tir touché jusqu'à couler le navire.

## Décision

Option 1 pour ce premier socle. Objectif de cette itération : livrer une partie complète et démontrable de bout en bout (création → placement → tirs alternés → fin → nouvelle partie) ; une stratégie plus élaborée n'est pas nécessaire pour valider ce parcours et ajoute une surface de test et de bugs potentiels (gestion de la pile de cases candidates, cas limites en bord de grille) qui n'est pas justifiée avant que le socle soit stable.

## Conséquences

- L'ordinateur est un adversaire faible (aucune mémoire des tirs touchés pour cibler ensuite). Assumé et documenté comme limite connue du socle dans le README.
- La stratégie chasse/cible est retenue comme piste de backlog explicite : elle s'intégrerait dans `Game.PickComputerTarget` sans changer le contrat public (`Game.PlayHumanShot` / `TurnResult`), donc sans impact sur l'API ni sur l'App.
- La garantie « l'IA respecte les mêmes règles que le joueur » ne dépend pas de la stratégie choisie : elle vient du fait que `PickComputerTarget` ne pioche que dans les cases valides de `HumanBoard` et que la résolution passe par le même `Board.ReceiveShot` que le joueur (voir ADR 0001).

## Vérification et réexamen

Tests `ComputerAutoShot_OnlyTargetsUntriedCellsOnHumanBoard` et `ComputerAutoShot_NeverTargetsOutOfGridCoordinate`. À réexaminer si le backlog priorise un adversaire plus difficile.

## Références

`BattleShip.Models/Domain/Game.cs` (`PickComputerTarget`), Referentiel.md diapo 38.
