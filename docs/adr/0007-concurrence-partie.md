# ADR 0007 : accès concurrent à une partie

## Statut et date

Accepté — 15/09/2026.

## Contexte

`InMemoryGameStore` est un singleton (`Program.cs`) qui expose des instances `Game` mutables, sans copie, à
chaque requête HTTP/gRPC qui connaît un `gameId` (voir `docs/adr/0006-stockage-etat-partie.md`). ASP.NET Core
traite les requêtes en parallèle par défaut : deux requêtes concurrentes sur le même `gameId` (deux onglets, un
double-clic, un client qui réessaie) s'exécutent sur deux threads du pool qui partagent la même instance `Game`
sans aucune synchronisation.

Un audit de revue de code a signalé le scénario concret : deux tirs concurrents sur la même case pourraient
tous deux passer `ComputerBoard.IsValidTarget(target)` avant que l'un des deux n'appelle `ReceiveShot` (qui fait
`_shotsReceived.Add(target)` sur un `HashSet<Coordinate>` non thread-safe). Conséquence possible : un coup
compté deux fois — ce qui violerait directement la règle non négociable « rejouer une case déjà jouée ne compte
pas comme un nouveau coup » (`CLAUDE.md`) — ou une corruption interne du `HashSet` sous accès concurrent non
synchronisé.

## Options envisagées

1. **Verrou par partie** (`Lock`/`lock` sur un champ privé de `Game`), englobant toute lecture et toute écriture
   de l'état d'une partie donnée.
2. **Verrou global** sur l'ensemble du store (`InMemoryGameStore`), sérialisant toutes les requêtes de toutes
   les parties entre elles.
3. **Structures de données thread-safe** (ex. `ConcurrentDictionary` pour les cases jouées) sans verrou explicite
   au niveau de `Game`.
4. **Ne rien faire** pour ce socle : accepter le risque, le documenter comme limite connue.

## Décision

Option 1. Un verrou par instance `Game` (`Lock _gate`, .NET 9+) sérialise les accès à une même partie sans
bloquer les autres parties entre elles — contrairement à l'option 2, qui pénaliserait inutilement des joueurs
sur des parties différentes. Contrairement à l'option 3, un verrou explicite couvrant toute la méthode
`PlayHumanShot` (lecture de `Status`, validation, tir joueur, riposte ordinateur, calcul du gagnant) garantit
l'atomicité de la séquence complète, pas seulement d'une opération élémentaire — remplacer uniquement le
`HashSet` par une collection concurrente n'aurait pas empêché deux threads de passer tous deux le contrôle
`IsValidTarget` avant que l'un des deux n'écrive.

`Game` expose aussi une méthode générique `Locked<TResult>(Func<TResult> func)`, utilisée par
`GameEndpoints`/`BattleshipGrpcService` pour les lectures (`GET`, `GetGameState` gRPC) et pour englober le
mapping DTO qui suit un tir accepté : sans cela, une requête concurrente pourrait encore lire un plateau à
moitié muté entre la fin de `PlayHumanShot` (qui relâche son propre verrou) et la construction de la réponse.
`Lock`/`lock` est réentrant pour un même thread, donc `PlayHumanShot` reste sûr à appeler seul (comme le font
les tests) tout en étant englobable dans un `Locked(...)` plus large côté endpoint sans risque d'interblocage.

Option 4 écartée : le scénario n'est pas hypothétique (deux onglets du même joueur suffisent), et le correctif
est peu coûteux à ce stade — l'écarter aurait laissé une violation démontrable d'une règle non négociable du
cours.

## Conséquences

- Les tirs sur une même partie s'exécutent désormais strictement en série ; aucun impact sur les parties
  distinctes (chacune a son propre verrou).
- Léger surcoût de synchronisation par requête, négligeable à l'échelle d'une démo/school project (pas de
  mesure de performance jugée nécessaire).
- N'introduit aucune dépendance externe ni changement de contrat public (signatures HTTP/gRPC inchangées).

## Vérification et réexamen

Test `GameTests.PlayHumanShot_ConcurrentCallsOnSameCell_OnlyOneIsAccepted` : 32 appels concurrents sur la même
case d'une partie fraîche, assertion qu'exactement un `Accepted` et 31 `Rejected(AlreadyPlayed)` sont produits.
Vérifié en désactivant temporairement le verrou (et en ajoutant un court délai artificiel entre la validation et
l'écriture pour forcer l'entrelacement) : le test échoue alors de façon reproductible, confirmant qu'il détecte
bien la violation avant correction (voir `REVUE-IA.md`). À réexaminer si le stockage évolue vers une base de
données ou un processus multi-instance (le verrou en mémoire ne protège qu'un seul processus API).

## Références

`BattleShip.Models/Domain/Game.cs` (`_gate`, `Locked`, `PlayHumanShot`), `BattleShip.API/Endpoints/GameEndpoints.cs`,
`BattleShip.API/Grpc/BattleshipGrpcService.cs`, `docs/adr/0006-stockage-etat-partie.md`.
