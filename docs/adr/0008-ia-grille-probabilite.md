# ADR 0008 : IA adverse par grille de probabilité

## Statut et date

Accepté — 2026-09-15. Remplace `docs/adr/0004-strategie-adversaire.md`.

## Contexte

L'ADR 0004 avait retenu un tir aléatoire uniforme pour livrer le socle, et laissé la stratégie chasse/cible en
piste de backlog. TICKET-03 (`docs/ticket.md`) a été retenu par le binôme. Deux problèmes du code existant
devaient être réglés en même temps :
- `Game.PickComputerTarget` lisait directement `HumanBoard`, positions des navires comprises : rien n'empêchait
  une stratégie plus élaborée de tricher ;
- `Game.PlayHumanShot` utilisait `Random.Shared` en dur : aucune riposte n'était reproductible en test.

## Options envisagées

1. **Chasse/cible** : tir aléatoire jusqu'à une touche, puis cases adjacentes à la touche.
2. **Grille de probabilité** : pour chaque case non jouée, somme des placements encore possibles des navires
   non coulés qui la recouvrent ; les placements recouvrant une touche non coulée pèsent 100 fois plus.

## Décision

Option 2. Elle englobe l'option 1 (une touche fait dominer ses voisines) tout en exploitant aussi les ratés et
les tailles de navires restants en phase de recherche. Elle reste une fonction pure et testable
(`ProbabilityTargeting.ComputeDensity`).

La stratégie est extraite derrière `IComputerTargeting` (`BattleShip.Models/Ai`), injectée dans `Game` avec un
`Random` optionnel. Elle ne reçoit que `OpponentBoardDto`, produit par `BoardViewMapper` — le même chokepoint
anti-fuite que les réponses envoyées au joueur. La cible renvoyée est revérifiée par `Board.IsValidTarget` et une
cible invalide lève une exception plutôt que d'être corrigée silencieusement.

## Conséquences

- L'ordinateur devient nettement plus fort : 45,2 tirs en moyenne pour couler la flotte, contre 94,9 en
  aléatoire (30 graines fixes, mesure relevée pendant la vérification, voir `REVUE-IA.md`).
- Le domaine (`Game`) dépend désormais de `BattleShip.Models.Contracts` (même assembly). Assumé : c'est ce qui
  garantit que l'IA voit exactement ce que verrait le joueur.
- Le constat SOLID « `Game` mêle règles et stratégie IA », différé lors du tri des audits (`PROMPTS.md`), est levé.
- Contrat public REST/gRPC inchangé.

## Vérification et réexamen

`BattleShip.Tests/Engine/ProbabilityTargetingTests.cs` (cibles toujours valides, voisinage d'une touche,
densité nulle, tailles des navires coulés, comparaison à l'aléatoire) et
`GameTests.ComputerShot_SameVisibleHistory_DifferentHiddenFleets_SameTarget`, vérifié en neutralisant la règle.
À réexaminer si un niveau de difficulté réglable est ajouté au backlog.

## Références

`BattleShip.Models/Ai/ProbabilityTargeting.cs`, `BattleShip.Models/Domain/Game.cs` (`PlayComputerShot`),
`docs/ticket.md` (TICKET-03).
