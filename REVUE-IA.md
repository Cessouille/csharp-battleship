# Revues de propositions IA

Trois revues argumentées minimum. Aucune erreur n'est exigée ; chaque conclusion doit être étayée.

## Revue : le test « coup déjà joué » détecte-t-il vraiment la règle qu'il prétend couvrir ?

**Proposition examinée**
`GameTests.PlayHumanShot_OnAlreadyPlayedCell_IsRejected_AndDoesNotMutateState` (`BattleShip.Tests/Engine/GameTests.cs`), écrit pour couvrir la règle non négociable « rejouer une case déjà jouée ne compte pas comme un nouveau coup » (`CLAUDE.md`).

**Hypothèse à vérifier**
Le test échoue réellement si la garde `!ComputerBoard.IsValidTarget(target)` est retirée de `Game.PlayHumanShot` — pas seulement « il passe au vert », qui ne prouve rien sur ce qu'il détecte.

**Expérience**
Commenter temporairement les deux lignes de la garde dans `Game.cs`, relancer `dotnet test --filter "FullyQualifiedName~PlayHumanShot_OnAlreadyPlayedCell"`. Résultat attendu avant exécution : le test doit échouer (le second tir sur la même case serait accepté au lieu d'être rejeté). Erreur que ce contrôle serait capable de détecter : une régression qui réintroduirait la possibilité de rejouer une case déjà tirée.

**Observation**
Le test a échoué comme attendu (`1 échec` en sortie de `dotnet test`) une fois la garde retirée, puis est repassé au vert après restauration de la garde et nouvelle exécution de la suite complète (25/25 à ce stade du développement).

**Décision et justification**
Test conservé tel quel : il détecte effectivement la violation qu'il prétend couvrir, ce n'est pas un test qui passerait de toute façon.

**Preuves et limites**
Commit `eae51cc` (contient le test et l'implémentation restaurée) ; l'expérience de désactivation elle-même n'a pas été committée (modification locale temporaire, annulée avant tout commit). Limite : un seul des ~15 tests moteur a été soumis à cet exercice de « mutation manuelle » ; les autres s'appuient sur le même raisonnement (Theory avec des cas qui échoueraient sans la règle) sans avoir été individuellement vérifiés par désactivation.

---

## Revue : une directive `@using Grpc.Core` généré par l'IA compile-t-elle telle quelle ?

**Proposition examinée**
Premier jet de `BattleShip.App/Pages/Game.razor` et `GrpcDemo.razor`, contenant `@using Grpc.Core` pour accéder à `RpcException`/`StatusCode`.

**Hypothèse à vérifier**
La directive compile sans erreur dans le contexte du projet `BattleShip.App`.

**Scénario**
`dotnet build BattleShip.App`. Résultat attendu avant exécution : compilation réussie (le code est syntaxiquement standard). Erreur que ce contrôle serait capable de détecter : toute erreur de résolution de type/namespace à la compilation.

**Résultat réellement observé**
Échec de compilation : `CS0234 — Le nom de type ou d'espace de noms 'Core' n'existe pas dans l'espace de noms 'BattleShip.Grpc'`. Cause identifiée après lecture de l'erreur : le namespace racine du projet (`BattleShip`) coïncide avec le premier segment du namespace généré par protobuf (`BattleShip.Grpc`), et la résolution de `using` à l'intérieur d'un namespace Razor généré (`BattleShip.App.Pages`) remonte les namespaces englobants — `Grpc.Core` était donc résolu comme `BattleShip.Grpc.Core`, qui n'existe pas.

**Décision et justification**
Proposition initiale rejetée telle quelle, adaptée : `@using Grpc.Core` remplacé par `@using global::Grpc.Core` (et de même pour `@using BattleShip.Grpc` → `@using global::BattleShip.Grpc`), qui force la résolution depuis la racine globale et contourne la remontée d'espaces de noms englobants.

**Preuves et limites**
Avant/après : build en échec (`CS0234`, deux occurrences) puis build réussi (`0 Avertissement(s), 0 Erreur(s)`) après le correctif, sur les deux fichiers concernés. Commit `21af4e2`. Limite : ce correctif traite le symptôme dans les fichiers concernés ; il n'a pas été vérifié si d'autres futurs fichiers du projet pourraient reproduire le même piège avec d'autres espaces de noms démarrant par `BattleShip`.

---

## Revue : le code HTTP 503 observé dans l'onglet réseau du navigateur signale-t-il une vraie erreur serveur ?

**Proposition examinée**
Démonstration manuelle de l'échange gRPC-Web d'erreur depuis `/verifier-partie` (id de partie inconnu → `NotFound` attendu), vérifiée via les outils réseau du navigateur.

**Hypothèse à vérifier**
Le statut HTTP `503 Service Unavailable` visible dans le journal réseau du navigateur pour l'appel `POST /battleship.Battleship/GetGameState` correspond à une vraie erreur côté serveur (exception non gérée, service indisponible), et non au comportement gRPC-Web attendu pour un statut `NotFound`.

**Scénario**
Consultation des logs du processus `BattleShip.API` (sortie standard de `dotnet run`) immédiatement après l'appel ayant produit le `503` dans le navigateur. Résultat attendu avant exécution : si l'hypothèse d'une vraie erreur serveur est vraie, une trace d'exception ou un log d'erreur générique apparaît ; si le `503` n'est qu'un artefact d'affichage du gRPC-Web par l'outil de développement, seul un log `Grpc.AspNetCore.Server` normal apparaît. Erreur que ce contrôle serait capable de détecter : une exception non gérée masquée par un message d'erreur générique côté client.

**Résultat réellement observé**
Log serveur : `info: Grpc.AspNetCore.Server.ServerCallHandler[7] — Error status code 'NotFound' with detail 'Partie introuvable.' raised.` — aucune exception, aucun log d'erreur. L'interface Blazor affichait par ailleurs correctement « Erreur gRPC-Web : NotFound — Partie introuvable. », cohérent avec le comportement attendu et avec le test d'intégration `GetGameState_UnknownId_ThrowsNotFoundRpcException`.

**Décision et justification**
Hypothèse rejetée : le `503` est un artefact de la façon dont l'outil d'inspection réseau du navigateur représente une réponse gRPC-Web « trailers-only » (statut gRPC encodé dans les trailers plutôt que dans le code HTTP), pas une erreur serveur réelle. Aucune correction de code nécessaire.

**Preuves et limites**
Log serveur reproductible en relançant l'appel depuis `/verifier-partie` avec un GUID inconnu. Limite : l'origine exacte du `503` dans l'outil d'inspection réseau n'a pas été investiguée plus loin (code source de l'outil non examiné) — la conclusion s'appuie sur l'absence d'erreur côté serveur et la cohérence du comportement observé côté client, pas sur une explication confirmée du mécanisme d'affichage.

---

## Revue : le correctif de concurrence sur `Game` empêche-t-il vraiment le scénario décrit par l'audit ?

**Proposition examinée**
Constat de l'audit `audit-bugs-lint` (rapport du 2026-09-15, commit `cc9908a`) : `InMemoryGameStore` expose un `Game` mutable sans verrou, si bien que deux requêtes concurrentes sur le même `gameId` pourraient toutes deux passer `ComputerBoard.IsValidTarget(target)` avant que l'une des deux n'appelle `ReceiveShot`, produisant un coup compté deux fois. Correctif proposé (par l'audit) : verrouiller autour de toute la méthode `PlayHumanShot`.

**Hypothèse à vérifier**
1) Le scénario décrit est réellement reproductible sans correctif (pas seulement plausible en théorie). 2) Un verrou par instance `Game` (`Lock _gate`, méthode `Locked<T>`) suffit à l'empêcher, sans introduire d'interblocage malgré son usage imbriqué (`PlayHumanShot` verrouille en interne, les endpoints ré-englobent l'appel dans `Locked(...)`).

**Scénario**
Test `GameTests.PlayHumanShot_ConcurrentCallsOnSameCell_OnlyOneIsAccepted` : 32 tâches lancées en parallèle (démarrage synchronisé par un `ManualResetEventSlim`) appellent toutes `PlayHumanShot` sur la même coordonnée d'une partie fraîche. Résultat attendu avant exécution : exactement un `MoveResult.Accepted`, les 31 autres `Rejected(AlreadyPlayed)`. Erreur que ce contrôle serait capable de détecter : plus d'un `Accepted` (double-comptage), ou une exception issue d'une corruption du `HashSet<Coordinate>` sous-jacent.

**Résultat réellement observé**
Avec le verrou en place : test vert de façon reproductible (plusieurs exécutions). Pour vérifier que le test détecte vraiment la régression et ne passe pas « par hasard » : verrou temporairement retiré et délai artificiel de 5 ms inséré entre la validation et l'écriture (pour forcer l'entrelacement, la fenêtre de course naturelle étant trop étroite pour se déclencher de façon fiable sur 32 tâches sans cette aide). Résultat : échec reproductible du test, confirmant à la fois que le scénario de l'audit est réel et que le test le détecte.

**Décision et justification**
Correctif de l'audit accepté, adapté dans sa portée : verrou par instance de `Game` (pas un verrou global sur le store, qui aurait pénalisé des parties sans rapport) et méthode `Locked<T>` réentrante exposée pour englober aussi les lectures côté `GameEndpoints`/`BattleshipGrpcService` — l'audit ne mentionnait que l'écriture, mais une lecture concurrente à une écriture en cours pose le même risque de lecture d'un état à moitié muté.

**Preuves et limites**
Test restauré et vert dans la suite complète (43/43) après restauration du verrou ; commit `9d3c501`, détail du raisonnement dans `docs/adr/0007-concurrence-partie.md`. Limite explicitement documentée dans l'ADR : ce verrou ne protège qu'un seul processus API ; il ne couvrirait pas un futur déploiement multi-instance avec le stockage en mémoire actuel.
