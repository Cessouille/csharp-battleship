# ADR 0012 : armes spéciales (torpille, frappe aérienne)

## Statut et date

Accepté — 2026-09-15.

## Contexte

TICKET-04 : chaque camp dispose d'une torpille et d'une frappe aérienne de 3 cases, chacune remplaçant tout le
tour. Règles actées dans `docs/ticket.md`. La règle de la frappe sur cases déjà jouées a été déduite de celle de
la torpille (cases ignorées, refus si toutes sont jouées) et reste à confirmer par le binôme. Un coup peut
désormais toucher de 0 à 10 cases, alors que `Board.ReceiveShot` (ADR 0003) n'en résout qu'une.

## Options envisagées

1. Faire évoluer `ReceiveShot` pour gérer des zones d'effet.
2. Garder `ReceiveShot` tel quel et exprimer une arme comme une liste ordonnée de cases (`WeaponRules.Cells`),
   résolues une à une par `Board.ReceiveWeapon`.

## Décision

Option 2. La règle de résolution d'une case reste unique et déjà testée ; seules les règles propres aux armes
sont ajoutées : trajectoire de la torpille depuis un bord, arrêt au premier navire touché, cases jouées
traversées, arrêt dès que la flotte est coulée.

- Munitions : `Arsenal` par joueur dans `Game`, décrémenté uniquement sous le verrou de la partie (ADR 0007).
- Validation : `Game.ValidateWeapon` (`WeaponsDisabled`, `NoAmmoLeft`, `OutOfGrid`, `AlreadyPlayed`) est
  **commune au joueur et à l'ordinateur**.
- IA : `IComputerTargeting.PickWeapon`, avec une implémentation par défaut qui n'utilise aucune arme.
  `ProbabilityTargeting` applique une heuristique fondée sur la grille de densité : frappe pour achever un navire
  touché, torpille dans le couloir le plus probable en phase de recherche. Volontairement simple pour rester
  explicable ; ce n'est pas une stratégie optimale.
- API : deux endpoints (`/torpedoes`, `/airstrikes`) avec chacun son DTO et son validateur, plutôt qu'un DTO
  unique à champs optionnels dont la validation dépendrait de l'arme choisie.
- Les munitions adverses sont exposées : elles se déduisent de toute façon des armes déjà utilisées.

## Conséquences

- En pratique, l'ordinateur lance sa torpille dès le premier tour (aucune touche encore) : prévisible, assumé.
- `TurnResult` / `TurnResultDto` portent l'arme utilisée par chaque camp.

## Vérification et réexamen

`Engine/WeaponTests.cs` et `Api/WeaponEndpointsTests.cs`. Contrôle des munitions neutralisé : 4 tests échouent,
dont `ComputerWeapon_IsSubjectToSameAmmoRules_AsPlayer`. À réexaminer si la frappe doit finalement refuser les
cases déjà jouées.

## Références

`BattleShip.Models/Domain/Weapons.cs`, `Board.ReceiveWeapon`, `Game.PlayHumanWeapon`, `Game.ValidateWeapon`,
`BattleShip.Models/Ai/ProbabilityTargeting.cs` (`PickWeapon`).
