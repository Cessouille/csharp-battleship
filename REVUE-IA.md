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

---

## Revue : l'IA probabiliste proposée respecte-t-elle vraiment la règle « pas d'information cachée » ?

**Proposition examinée**
Implémentation de TICKET-03 générée par l'IA : `ProbabilityTargeting` (`BattleShip.Models/Ai`) alimentée uniquement par `HumanBoard.ToOpponentBoardDto()`, avec l'argument que l'anti-triche est « structurel » puisque l'interface `IComputerTargeting` ne reçoit jamais de `Board`.

**Hypothèse à vérifier**
1) L'argument de type ne suffit pas à lui seul : `Game.PlayComputerShot` pourrait très bien construire une vue enrichie des vraies positions avant de la passer à la stratégie. Il faut un test qui échoue dans ce cas. 2) La stratégie est réellement meilleure que l'ancien tir aléatoire (sinon l'ADR 0004 n'a aucune raison d'être remplacée).

**Scénario**
Test `GameTests.ComputerShot_SameVisibleHistory_DifferentHiddenFleets_SameTarget` : deux parties à graine identique, même historique visible sur le plateau humain (une touche, un raté), croiseur caché horizontal dans l'une et vertical dans l'autre. Résultat attendu : même cible de riposte. Erreur détectable : une riposte qui dépend des positions cachées. Pour la qualité : `ProbabilityTargeting_SinksFleetInFewerShotsThanRandom_OnFixedSeeds` sur 30 graines fixes.

**Résultat réellement observé**
Test vert avec le code livré. Neutralisation : `PlayComputerShot` modifié temporairement pour ajouter toutes les cases de navires non coulés aux `Hits` de la vue transmise → le test échoue (`Assert.Equal() Failure: Values differ`), de même que `ComputerAutoShot_OnlyTargetsUntriedCellsOnHumanBoard_AcrossFullGame`. Code restauré, suite verte. Mesure relevée pendant la vérification : 45,2 tirs en moyenne pour couler la flotte contre 94,9 pour la référence aléatoire. Le test `ComputeDensity_IgnoresSunkShipSizes` a aussi été neutralisé (tailles des navires coulés non retirées) : il échoue, puis repasse une fois la règle restaurée.

**Décision et justification**
Proposition acceptée, complétée : l'argument de type a été conservé mais doublé d'un test comportemental au niveau de `Game`, là où la triche serait réellement introduite. Le seuil du benchmark reste volontairement « strictement meilleur que l'aléatoire » plutôt qu'une valeur chiffrée, pour ne pas figer un détail d'algorithme dans un test.

**Preuves et limites**
`BattleShip.Tests/Engine/ProbabilityTargetingTests.cs`, `BattleShip.Tests/Engine/GameTests.cs`, `docs/adr/0008-ia-grille-probabilite.md`. Limite : le test anti-triche compare deux configurations cachées précises ; il ne prouve pas l'absence de toute dépendance cachée possible, seulement qu'une fuite des positions dans la vue est détectée.

---

## Revue : un paramètre de corps nullable rend-il vraiment le corps facultatif sur `POST /api/games` ?

**Proposition examinée**
Plan d'implémentation généré par l'IA pour TICKET-00 : ajouter un paramètre `CreateGameRequestDto?` à l'endpoint de création pour que « sans corps → partie classique », en affirmant que les tests existants, qui postent sans corps, le prouveraient. Le plan marquait lui-même ce point « à vérifier ».

**Hypothèse à vérifier**
Une requête `POST /api/games` sans corps ni `Content-Type` atteint l'endpoint et reçoit `201 Created`.

**Scénario**
Suite de tests existante (`GameEndpointsTests`, `GrpcGameStateTests` postent `null`), puis API lancée localement et interrogée avec `curl` : sans corps, avec `Content-Type: application/json` sans corps, avec `{}`, avec `{"radar":"oui"}`. Journalisation `Microsoft.AspNetCore` passée en `Debug` pour lire la décision du routage.

**Résultat réellement observé**
10 tests en échec. `curl` sans corps : `404`. Journal : « Request did not match any endpoints » — seul le point de terminaison gRPC « Unimplemented service » est évalué, l'endpoint Minimal API n'est même pas candidat. Avec `Content-Type: application/json` sans corps : `201` ; avec `{}` : `201` ; avec un booléen mal typé : `400`.

**Décision et justification**
Proposition rejetée dans sa forme : le corps devient **obligatoire** (`{}` = partie classique), plutôt que de lire le corps à la main pour contourner le routage (plus de code, et perte du `ValidationFilter<T>` générique). Tests, App et `.http` envoient désormais un corps JSON. Décision documentée dans `docs/adr/0009-options-de-partie.md`.

**Preuves et limites**
Suite complète verte après correction. Limite : la raison précise du rejet par le routage (politique de correspondance sur le type de contenu) est déduite du journal, pas confirmée dans le code source d'ASP.NET Core.

---

## Revue : l'architecture recommandée pour le placement manuel s'appuyait-elle sur le référentiel du cours, ou seulement sur l'inférence de l'IA ?

**Proposition examinée**
Pour TICKET « placement manuel de la flotte » (`docs/adr/0013-placement-manuel.md`), l'IA a d'abord posé le choix d'architecture (placement groupé dans `POST /api/games` vs. nouvelle phase serveur `AwaitingPlacement` avec endpoint dédié) via un outil de choix multiple, avec une recommandation motivée uniquement par `CLAUDE.md` (le fichier de règles du dépôt, pas le support de cours lui-même).

**Hypothèse à vérifier**
Que classer le placement manuel comme « extension légitime, pas réécriture du socle » — et donc justifier une architecture plus légère — est réellement fondé sur l'énoncé du cours, et pas seulement une inférence plausible à partir d'un document dérivé (`CLAUDE.md`) que l'IA a elle-même rédigé lors d'une session précédente.

**Expérience**
L'utilisateur a refusé à deux reprises de trancher sur la seule base des options présentées (« The user wants to clarify these questions », sans réponse) et a demandé explicitement : « A ton avis, en prenant compte les consignes, quel est le mieux ? », puis, après une nouvelle tentative de confirmation par choix multiple restée sans suite : « prends en compte @csharp-school\ ». Plutôt que de répéter la même recommandation, l'IA a lu le support de cours source (`csharp-school/Ressources Bataille Navale/Cours C  ASP.NET - Bataille Navale - autonomie.md`, diapos 5, 6 et 36) au lieu de s'appuyer uniquement sur sa propre synthèse antérieure dans `CLAUDE.md`. Résultat attendu avant lecture : si l'hypothèse est fausse, le support de cours devrait soit imposer une architecture de placement précise, soit ne rien dire de la distinction socle/extension invoquée.

**Résultat réellement observé**
Diapo 36 (« Spécification 1 — le moteur de jeu ») liste « créer deux grilles et placer les flottes aléatoirement » parmi les *comportements attendus du socle*, sans l'exprimer comme une interdiction d'ajouter un autre mode. Diapo 6 (« Ce qui est imposé, ce qui est libre ») classe explicitement « composition de la flotte » et « interface et expérience de jeu » dans la colonne « vos choix, votre responsabilité ». Rien dans le support ne mandate un mécanisme serveur particulier (phase d'attente, endpoint dédié) pour le placement.

**Décision et justification**
Recommandation confirmée inchangée (placement groupé à la création, sans nouvel état serveur ; interaction clic + orientation), mais désormais appuyée sur une citation directe du référentiel plutôt que sur la seule reformulation de `CLAUDE.md` — la vérification a changé la nature de la preuve, pas la conclusion. Le contexte tiré du support a été intégré au « Contexte » du plan et à l'ADR 0013, avec la diapo citée.

**Preuves et limites**
`docs/adr/0013-placement-manuel.md` (section Contexte, référence diapo 36), plan de la session (`~/.claude/plans/contexte-aujourd-hui-le-agile-fern.md`). Limite : l'absence de mandat explicite dans le support ne prouve pas que l'architecture choisie est la meilleure possible, seulement qu'aucune des deux options proposées ne viole une contrainte du socle — le choix final entre les deux reste un arbitrage d'ingénierie (moins d'état à tester/expliquer), pas une obligation du cours.

---

## Revue : le système de succès livré respecte-t-il vraiment la règle anti-fuite, ou seulement par argument de type ?

**Proposition examinée**
Code livré par le commit `4864370` (« feat: ajout des succès ») : `AchievementContext` (`BattleShip.Models/Achievements/AchievementContext.cs`) affirme en commentaire un anti-triche « structurel », calqué explicitement sur l'argument déjà validé pour `IComputerTargeting` (ADR 0008, revue ci-dessus) — une règle de succès ne recevrait jamais `ComputerBoard.Ships`, seulement une projection de ce que le joueur voit déjà.

**Hypothèse à vérifier**
1) L'argument de type ne suffit pas seul (même limite que la revue ADR 0008) : `AchievementContext.From(Game)` pourrait très bien être modifié pour inclure une information cachée sans que la signature de `IAchievementRule.IsUnlocked(AchievementContext)` ne bouge — il faut un test comportemental qui échouerait dans ce cas, pas seulement une lecture de type. 2) Le test qui prétend jouer ce rôle (`AchievementLeakTests.TwoGamesWithIdenticalVisibleProjection_UnlockTheSameAchievements`) ne passe pas trivialement (par exemple parce qu'aucun succès n'est jamais débloqué dans le scénario testé).

**Scénario**
Lecture de `AchievementContext.From` : construit exclusivement `game.Options`, `game.Winner`, `game.Status`, `game.History`, `game.ComputerBoard.ShotsReceived` (les propres tirs du joueur) et une projection de `game.HumanBoard.Ships` (sa propre flotte) — jamais `game.ComputerBoard.Ships`. Lecture ligne à ligne du test `AchievementLeakTests` : deux parties construites avec un navire adverse caché à des positions différentes (`(0,0)` vs `(9,9)`), jamais visées par la séquence de tirs jouée (le motif du cœur, fenêtre `(2,2)`-`(6,6)`) ; la même séquence de tirs est rejouée sur les deux parties, puis les deux listes de succès débloqués sont comparées avec `Assert.Equal`. Un garde-fou explicite (`Assert.NotEmpty(...Achievements)`) empêche le test de passer trivialement en comparant deux listes vides.

**Résultat réellement observé**
Par lecture du code (aucune exécution de `dotnet test` possible dans cette session : aucun SDK .NET 10 n'était installé dans cet environnement, contrainte différente des revues précédentes qui avaient pu relancer la suite) :
- `AchievementContext.From` ne référence textuellement aucun champ de `ComputerBoard` autre que `ShotsReceived` (grep sur le fichier) : l'argument de type tient à la lecture.
- Le test `AchievementLeakTests` construit bien un scénario où la seule différence entre les deux parties est une case jamais visée de la flotte cachée, avec un garde-fou anti-trivialité — sa conception correspond à ce qu'exige l'hypothèse 2, contrairement à un test qui se contenterait de comparer deux parties identiques en tout point (ce qui prouverait uniquement le déterminisme, pas l'absence de fuite).
- Le rapport d'audit indépendant `docs/audits/bugs-lint.md` (même commit `4864370`, exécuté séparément avec `dotnet test`) rapporte 265/265 tests verts, `AchievementLeakTests` inclus — c'est la seule confirmation d'exécution disponible pour cette session, pas une exécution que cette revue a effectuée elle-même.

**Décision et justification**
Test et argument acceptés tels quels, avec une réserve consignée plutôt qu'omise : cette revue n'a pas pu reproduire l'exercice de neutralisation manuelle des revues précédentes (retirer volontairement la garde anti-fuite, observer l'échec, restaurer) faute d'environnement d'exécution .NET 10 disponible dans cette session. La conclusion s'appuie sur la lecture du code et sur l'exécution déjà réalisée par un audit indépendant du même commit, pas sur une vérification dynamique menée ici.

**Preuves et limites**
`BattleShip.Models/Achievements/AchievementContext.cs`, `BattleShip.Tests/Contracts/AchievementLeakTests.cs`, `BattleShip.Tests/Engine/Achievements/AchievementEvaluationTests.cs` (`AchievementContextTests.Context_ExposesNoOpponentFleet`, test structurel par réflexion complémentaire), `docs/audits/bugs-lint.md` (2026-09-17, commit `4864370`). Limite explicite : contrairement aux revues précédentes de ce fichier, celle-ci n'a vérifié la propriété que par lecture statique et par un rapport d'exécution tiers, pas par une neutralisation exécutée dans cette session — à refaire avec `dotnet test` dès qu'un SDK .NET 10 est disponible, pour retrouver le niveau de preuve des revues précédentes.

---

## Revue : la suite de 308 tests verts suffit-elle à garantir que le mini-jeu de précision fonctionne réellement dans le navigateur ?

**Proposition examinée**
Implémentation du mini-jeu de précision (`docs/adr/0019-mini-jeu-de-precision.md`) : au moment de cette revue,
308 tests xUnit passaient (moteur avec horloge injectée, contrats API/gRPC), et le raisonnement de conception
tenait la route sur le papier — en particulier `GameStateUpdate.Apply(TurnResultDto)`, qui fusionne la réponse
d'un coup dans l'état déjà affiché via une `with`-expression, avait été jugé ne nécessiter aucune modification :
« un `TurnResultDto` signifie que le tour est fini, donc `PendingChallenge` y est toujours implicitement absent ».

**Hypothèse à vérifier**
Une suite verte au niveau moteur/API (qui ne touche jamais `BattleShip.App`, absent des références de
`BattleShip.Tests` par construction du dépôt) suffit à garantir que l'enchaînement réel dans le navigateur —
tirer, voir le mini-jeu s'ouvrir, l'arrêter, voir l'état se mettre à jour — fonctionne sans accroc.

**Scénario**
`dotnet dev-certs https --trust`, lancement de `BattleShip.API` et `BattleShip.App` en profil `https`, partie
créée avec l'option activée depuis le navigateur (outils `claude-in-chrome`), tirs répétés sur le plateau adverse
jusqu'à toucher un navire, résolution du défi par clic. Résultat attendu avant exécution : le défi se résout et
l'interface redevient identique à son état normal (grille interactive, aucun résidu visuel).

**Résultat réellement observé**
Après un premier tir résolu avec succès (le navire touché s'affichait correctement), la barre « Tir ! » restait
affichée à l'écran alors qu'une lecture directe de l'état via l'API (`GET /api/games/{id}`) confirmait
`pendingChallenge: null` côté serveur — écart entre l'état serveur (correct) et l'affichage client (figé).
Cause identifiée par lecture de `GameStateUpdate.Apply` : la `with`-expression ne liste que
`Status/Winner/MyBoard/OpponentBoard/Actions/History/Achievements` ; `PendingChallenge`, absent de cette liste,
conserve donc sa valeur précédente au lieu d'être remis à `null` — exactement le piège que le commentaire déjà
présent sur ce fichier avertissait depuis TICKET-10 (« un champ oublié ici reste figé jusqu'au prochain
scan/salve »), et que l'hypothèse de conception avait pourtant explicitement écarté comme non pertinent ici.

**Décision et justification**
Hypothèse rejetée : la suite verte ne suffisait pas, parce qu'aucun test du dépôt n'exerce
`BattleShip.App` (choix d'architecture assumé, voir `CLAUDE.md` — Tests → API seulement). Correctif appliqué :
`GameStateUpdate.Apply` met désormais explicitement `PendingChallenge = null`. Revérifié dans le navigateur
après correctif : attaque réussie/ratée, défense réussie/ratée (titre « Défense ! » confirmé à l'écran), touche
Espace, et reprise correcte après rechargement de page en plein défi (`GameStateDto.PendingChallenge` réhydraté
par `GetGameState`) — plus aucun résidu observé sur une dizaine de résolutions consécutives.

**Preuves et limites**
`BattleShip.App/Game/GameStateUpdate.cs` (avant/après), captures d'écran prises pendant la session (« Tir ! »
figé avant correctif, « Défense ! » et grille redevenue interactive après). Limite : la vérification manuelle
n'a pas réussi à observer une défense **réussie** à l'écran de façon fiable (fenêtre de succès ~180 ms sur un
cycle de 1800 ms, plus courte que le temps d'aller-retour d'un clic programmatique piloté depuis cette session) ;
le succès défensif reste prouvé uniquement par `PrecisionMinigameTests.ResolveChallenge_DefenseInsideZone_...`
(horloge contrôlée), pas observé visuellement. Aucun test automatisé n'a été ajouté pour ce défaut précis
(l'App n'étant pas dans le graphe de test), donc rien n'empêche structurellement une régression identique sur un
autre champ ajouté plus tard à `GameStateDto` sans passer par `Apply` — seule la vérification manuelle au
navigateur (`CLAUDE.md`, point 6) l'a détecté ici.

---

## Revue : le raté forcé du mini-jeu de précision consomme-t-il la case sans conséquence, comme un vrai raté ?

**Proposition examinée**
Décision actée dans `docs/adr/0019-mini-jeu-de-precision.md` (« Conséquences sur la résolution d'un tour ») :
`Board.ReceiveShotForcedMiss` marque la case comme jouée en cas d'échec du défi de timing à l'attaque, au motif
que ce raté doit se comporter « comme un vrai raté ». Signalé par l'utilisateur en jouant : « il y a un bug
lorsqu'on rate un tire sur un bateau ennemi, le tire est comptabilisé mais le bateau ne coule jamais ».

**Hypothèse à vérifier**
Consommer la case d'un raté forcé n'a pas d'effet secondaire, exactement comme consommer une case vide sur un
vrai raté — implicitement, l'ADR traitait les deux comme équivalents.

**Scénario**
`ReceiveShotForcedMiss_OnShipCell_ReturnsMiss_ShipNotMutated_CellStillPlayable` (`BoardTests`),
`SubmitChallengeResult_WithForcedMissResolution_LeavesShipUnhit_CellStillPlayable` (`VolleySequencerTests`) et
`ResolveChallenge_AttackOutsideZone_ThenHittingTheSameCellAgain_CanStillSinkTheShip` (`PrecisionMinigameTests`) :
un raté forcé sur la case d'un navire, puis un second tir réussi sur la même case. Résultat attendu si
l'hypothèse est vraie : la case reste consommée (comme documenté), mais rien n'empêche par ailleurs le navire
d'être coulé par un autre mécanisme. Erreur que ce contrôle serait capable de détecter : un navire qui ne peut
plus jamais être totalement touché après un raté forcé sur une de ses cases.

**Résultat réellement observé**
Hypothèse fausse : `Ship.IsSunk` (`Ship.cs`) exige `_hits.Count == Cells.Count`, c'est-à-dire un coup enregistré
sur *chacune* des cases du navire ; `ReceiveShotForcedMiss` n'enregistre jamais de coup sur le navire tout en
rendant la case définitivement injouable (`IsValidTarget` exclut les cases déjà dans `ShotsReceived`). Un navire
touché par un seul raté forcé sur une de ses cases ne pouvait donc plus jamais atteindre `IsSunk == true` — la
différence avec un vrai raté (case vide, donc rien à perdre en la consommant) n'avait pas été identifiée au
moment de la décision. Les trois tests ci-dessus échouaient avant correctif, exactement comme prédit
(`Assert.True(board.IsValidTarget(target))` → `Actual: False`), confirmant que la case, contrairement à
l'ADR original, ne pouvait pas être retentée avec succès.

**Décision et justification**
Décision de l'ADR 0019 renversée sur ce point précis : `ReceiveShotForcedMiss` ne consomme plus la case (délègue
à `ReceiveShotDodged`, dont c'était déjà le comportement) — un raté forcé doit rester rejouable pour que le
navire reste sinkable, contrairement à un vrai raté sur une case vide où consommer la case ne bloque rien. Les
deux méthodes restent nommées séparément pour la lisibilité côté `Game.ResolveChallenge` (attaque vs défense),
mais partagent désormais la même implémentation.

**Preuves et limites**
`BattleShip.Models/Domain/Board.cs`, tests cités ci-dessus (309/309 verts après correctif, les trois ciblés
rouges avant). Limite : la suite de tests moteur avait cette règle depuis l'ADR 0019 sans qu'aucun test ne
rejoue une case après un raté forcé pour vérifier qu'elle peut réellement conduire à couler le navire — seul le
signalement de l'utilisateur en jouant a révélé le défaut, pas la suite automatisée existante.
