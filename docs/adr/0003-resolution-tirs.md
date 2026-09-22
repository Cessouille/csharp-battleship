# ADR 0003 : structure de résolution des tirs

## Statut et date

Accepté — 15/09/2026.

## Contexte

`Board.ReceiveShot` doit, pour une coordonnée donnée, déterminer si elle appartient à un navire et, si oui, enregistrer le tir sur ce navire et savoir s'il est désormais coulé. Il faut choisir la structure de données qui porte cette recherche.

## Options envisagées

1. **Parcours linéaire** de `List<Ship>` (`_ships.FirstOrDefault(s => s.Occupies(target))`), chaque `Ship.Occupies` testant l'appartenance parmi ses propres cases.
2. **Index `Dictionary<Coordinate, Ship>`** construit au placement, offrant une recherche en O(1) par case.

## Décision

Option 1. À l'échelle de ce projet (10×10 cases, 5 navires, 17 cases occupées au maximum), le coût d'un parcours linéaire par tir est négligeable (au plus 5 comparaisons de navire, chacune sur au plus 5 cases) : aucune mesure de performance ne justifie la complexité et le risque de désynchronisation d'un index supplémentaire à maintenir en cohérence avec `_ships`. Le code reste plus simple à expliquer et à relire par le binôme (critère explicite du cours).

## Conséquences

- Complexité en O(nombre de navires × taille moyenne) par tir, largement suffisante à cette échelle.
- Si la taille de grille ou le nombre de navires devaient significativement augmenter (hors périmètre de ce socle), cette décision serait à réexaminer en faveur d'un index.

## Vérification et réexamen

Couvert par l'ensemble des tests `BoardTests`/`GameTests` qui exercent `ReceiveShot` (aucune régression de comportement observée, seule la structure interne est concernée). Pas de test de performance dédié : non justifié à cette échelle.

## Références

`BattleShip.Models/Domain/Board.cs` (`ReceiveShot`).
