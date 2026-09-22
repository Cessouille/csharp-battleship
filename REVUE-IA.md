# Revues de propositions IA

Trois revues argumentées minimum. Aucune erreur n'est exigée ; chaque conclusion doit être étayée.

Les cinq revues retenues ci-dessous ont été sélectionnées parmi un ensemble plus large pour couvrir des
méthodes de vérification distinctes — test de mutation manuelle, benchmark quantitatif, scénario HTTP direct
(`curl`) et vérification manuelle bout en bout au navigateur — et des issues différentes : proposition acceptée
telle quelle, acceptée mais complétée, rejetée et corrigée, et décision antérieure renversée après un défaut
signalé en jouant. Chacune répond à la même question : la vérification effectuée démontre-t-elle réellement ce
qu'elle prétend démontrer, ou se contente-t-elle d'un test qui « passerait de toute façon » ?

---

## Revue : le correctif de concurrence sur `Game` empêche-t-il vraiment le scénario décrit par l'audit ?

**Proposition examinée**
Constat de l'audit `audit-bugs-lint` (rapport du 2026-09-15, commit `cc9908a`) : `InMemoryGameStore` expose un
`Game` mutable sans verrou (`BattleShip.Models/Domain/Game.cs`), si bien que deux requêtes concurrentes sur le
même `gameId` pourraient toutes deux passer `ComputerBoard.IsValidTarget(target)` avant que l'une des deux
n'appelle `ReceiveShot`, produisant un coup compté deux fois. Correctif proposé (par l'audit) : verrouiller
autour de toute la méthode `PlayHumanShot`.

**Hypothèse à vérifier**
1) Le scénario décrit est réellement reproductible sans correctif (pas seulement plausible en théorie).
2) Un verrou par instance `Game` (`Lock _gate`, méthode `Locked<T>`) suffit à l'empêcher, sans introduire
d'interblocage malgré son usage imbriqué (`PlayHumanShot` verrouille en interne, les endpoints ré-englobent
l'appel dans `Locked(...)`).

**Scénario**
Test `GameTests.PlayHumanShot_ConcurrentCallsOnSameCell_OnlyOneIsAccepted`
(`BattleShip.Tests/Engine/GameTests.cs`) : 32 tâches lancées en parallèle (démarrage synchronisé par un
`ManualResetEventSlim`) appellent toutes `PlayHumanShot` sur la même coordonnée d'une partie fraîche. Résultat
attendu avant exécution : exactement un `MoveResult.Accepted`, les 31 autres `Rejected(AlreadyPlayed)`. Erreur
que ce contrôle serait capable de détecter : plus d'un `Accepted` (double-comptage), ou une exception issue
d'une corruption du `HashSet<Coordinate>` sous-jacent.

**Résultat réellement observé**
Avec le verrou en place : test vert de façon reproductible (plusieurs exécutions). Pour vérifier que le test
détecte vraiment la régression et ne passe pas « par hasard » : verrou temporairement retiré et délai artificiel
de 5 ms inséré entre la validation et l'écriture (pour forcer l'entrelacement, la fenêtre de course naturelle
étant trop étroite pour se déclencher de façon fiable sur 32 tâches sans cette aide). Résultat : échec
reproductible du test, confirmant à la fois que le scénario de l'audit est réel et que le test le détecte.

**Décision et justification**
Correctif de l'audit accepté, adapté dans sa portée : verrou par instance de `Game` (pas un verrou global sur le
store, qui aurait pénalisé des parties sans rapport) et méthode `Locked<T>` réentrante exposée pour englober
aussi les lectures côté `GameEndpoints` (`BattleShip.API/Endpoints/GameEndpoints.cs`) et
`BattleshipGrpcService` (`BattleShip.API/Grpc/BattleshipGrpcService.cs`) — l'audit ne mentionnait que l'écriture,
mais une lecture concurrente à une écriture en cours pose le même risque de lecture d'un état à moitié muté.

**Preuves et limites**
Test restauré et vert dans la suite complète (43/43 à ce stade) après restauration du verrou ; commit `9d3c501`,
détail du raisonnement dans `docs/adr/0007-concurrence-partie.md`. Limite explicitement documentée dans l'ADR :
ce verrou ne protège qu'un seul processus API ; il ne couvrirait pas un futur déploiement multi-instance avec le
stockage en mémoire actuel.

---

## Revue : l'IA probabiliste proposée respecte-t-elle vraiment la règle « pas d'information cachée » ?

**Proposition examinée**
Implémentation de TICKET-03 générée par l'IA : `ProbabilityTargeting` (`BattleShip.Models/Ai`) alimentée
uniquement par `HumanBoard.ToOpponentBoardDto()`, avec l'argument que l'anti-triche est « structurel » puisque
l'interface `IComputerTargeting` ne reçoit jamais de `Board`.

**Hypothèse à vérifier**
1) L'argument de type ne suffit pas à lui seul : `Game.PlayComputerShot` pourrait très bien construire une vue
enrichie des vraies positions avant de la passer à la stratégie. Il faut un test qui échoue dans ce cas.
2) La stratégie est réellement meilleure que l'ancien tir aléatoire (sinon l'ADR 0004 n'a aucune raison d'être
remplacée).

**Scénario**
Test `GameTests.ComputerShot_SameVisibleHistory_DifferentHiddenFleets_SameTarget` : deux parties à graine
identique, même historique visible sur le plateau humain (une touche, un raté), croiseur caché horizontal dans
l'une et vertical dans l'autre. Résultat attendu : même cible de riposte. Erreur détectable : une riposte qui
dépend des positions cachées. Pour la qualité :
`ProbabilityTargeting_SinksFleetInFewerShotsThanRandom_OnFixedSeeds`
(`BattleShip.Tests/Engine/ProbabilityTargetingTests.cs`) sur 30 graines fixes.

**Résultat réellement observé**
Test vert avec le code livré. Neutralisation : `PlayComputerShot` modifié temporairement pour ajouter toutes les
cases de navires non coulés aux `Hits` de la vue transmise → le test échoue (`Assert.Equal() Failure: Values
differ`), de même que `ComputerAutoShot_OnlyTargetsUntriedCellsOnHumanBoard_AcrossFullGame`. Code restauré,
suite verte. Mesure relevée pendant la vérification : 45,2 tirs en moyenne pour couler la flotte contre 94,9 pour
la référence aléatoire. Le test `ComputeDensity_IgnoresSunkShipSizes` a aussi été neutralisé (tailles des
navires coulés non retirées) : il échoue, puis repasse une fois la règle restaurée.

**Décision et justification**
Proposition acceptée, complétée : l'argument de type a été conservé mais doublé d'un test comportemental au
niveau de `Game`, là où la triche serait réellement introduite. Le seuil du benchmark reste volontairement
« strictement meilleur que l'aléatoire » plutôt qu'une valeur chiffrée, pour ne pas figer un détail d'algorithme
dans un test.

**Preuves et limites**
`BattleShip.Tests/Engine/ProbabilityTargetingTests.cs`, `BattleShip.Tests/Engine/GameTests.cs`,
`docs/adr/0008-ia-grille-probabilite.md`. Limite : le test anti-triche compare deux configurations cachées
précises ; il ne prouve pas l'absence de toute dépendance cachée possible, seulement qu'une fuite des positions
dans la vue est détectée.

---

## Revue : un paramètre de corps nullable rend-il vraiment le corps facultatif sur `POST /api/games` ?

**Proposition examinée**
Plan d'implémentation généré par l'IA pour TICKET-00 : ajouter un paramètre `CreateGameRequestDto?` à l'endpoint
de création pour que « sans corps → partie classique », en affirmant que les tests existants, qui postent sans
corps, le prouveraient. Le plan marquait lui-même ce point « à vérifier ».

**Hypothèse à vérifier**
Une requête `POST /api/games` sans corps ni `Content-Type` atteint l'endpoint et reçoit `201 Created`.

**Scénario**
Suite de tests existante (`GameEndpointsTests`, `GrpcGameStateTests` postent `null`), puis API lancée localement
et interrogée avec `curl` : sans corps, avec `Content-Type: application/json` sans corps, avec `{}`, avec
`{"radar":"oui"}`. Journalisation `Microsoft.AspNetCore` passée en `Debug` pour lire la décision du routage.

**Résultat réellement observé**
10 tests en échec. `curl` sans corps : `404`. Journal : « Request did not match any endpoints » — seul le point
de terminaison gRPC « Unimplemented service » est évalué, l'endpoint Minimal API n'est même pas candidat. Avec
`Content-Type: application/json` sans corps : `201` ; avec `{}` : `201` ; avec un booléen mal typé : `400`.

**Décision et justification**
Proposition rejetée dans sa forme : le corps devient **obligatoire** (`{}` = partie classique), plutôt que de
lire le corps à la main pour contourner le routage (plus de code, et perte du `ValidationFilter<T>` générique).
Tests, App et `.http` envoient désormais un corps JSON. Décision documentée dans
`docs/adr/0009-options-de-partie.md`.

**Preuves et limites**
Suite complète verte après correction. Limite : la raison précise du rejet par le routage (politique de
correspondance sur le type de contenu) est déduite du journal, pas confirmée dans le code source d'ASP.NET Core.

---

## Revue : la suite de 308 tests verts suffit-elle à garantir que le mini-jeu de précision fonctionne réellement dans le navigateur ?

**Proposition examinée**
Implémentation du mini-jeu de précision (`docs/adr/0019-mini-jeu-de-precision.md`) : au moment de cette revue,
308 tests xUnit passaient (moteur avec horloge injectée, contrats API/gRPC), et le raisonnement de conception
tenait la route sur le papier — en particulier `GameStateUpdate.Apply(TurnResultDto)`
(`BattleShip.App/Game/GameStateUpdate.cs`), qui fusionne la réponse d'un coup dans l'état déjà affiché via une
`with`-expression, avait été jugé ne nécessiter aucune modification : « un `TurnResultDto` signifie que le tour
est fini, donc `PendingChallenge` y est toujours implicitement absent ».

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
`pendingChallenge: null` côté serveur — écart entre l'état serveur (correct) et l'affichage client (figé). Cause
identifiée par lecture de `GameStateUpdate.Apply` : la `with`-expression ne liste que
`Status/Winner/MyBoard/OpponentBoard/Actions/History/Achievements` ; `PendingChallenge`, absent de cette liste,
conserve donc sa valeur précédente au lieu d'être remis à `null` — exactement le piège que le commentaire déjà
présent sur ce fichier avertissait depuis TICKET-10 (« un champ oublié ici reste figé jusqu'au prochain
scan/salve »), et que l'hypothèse de conception avait pourtant explicitement écarté comme non pertinent ici.

**Décision et justification**
Hypothèse rejetée : la suite verte ne suffisait pas, parce qu'aucun test du dépôt n'exerce `BattleShip.App`
(choix d'architecture assumé, voir `CLAUDE.md` — Tests → API seulement). Correctif appliqué :
`GameStateUpdate.Apply` met désormais explicitement `PendingChallenge = null`. Revérifié dans le navigateur après
correctif : attaque réussie/ratée, défense réussie/ratée (titre « Défense ! » confirmé à l'écran), touche Espace,
et reprise correcte après rechargement de page en plein défi (`GameStateDto.PendingChallenge` réhydraté par
`GetGameState`) — plus aucun résidu observé sur une dizaine de résolutions consécutives.

**Preuves et limites**
`BattleShip.App/Game/GameStateUpdate.cs` (avant/après), captures d'écran prises pendant la session (« Tir ! »
figé avant correctif, « Défense ! » et grille redevenue interactive après). Limite : la vérification manuelle n'a
pas réussi à observer une défense **réussie** à l'écran de façon fiable (fenêtre de succès ~180 ms sur un cycle
de 1800 ms, plus courte que le temps d'aller-retour d'un clic programmatique piloté depuis cette session) ; le
succès défensif reste prouvé uniquement par `PrecisionMinigameTests.ResolveChallenge_DefenseInsideZone_...`
(horloge contrôlée), pas observé visuellement. Aucun test automatisé n'a été ajouté pour ce défaut précis (l'App
n'étant pas dans le graphe de test), donc rien n'empêche structurellement une régression identique sur un autre
champ ajouté plus tard à `GameStateDto` sans passer par `Apply` — seule la vérification manuelle au navigateur
(`CLAUDE.md`, point 6) l'a détecté ici.

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
Hypothèse fausse : `Ship.IsSunk` (`BattleShip.Models/Domain/Ship.cs`) exige `_hits.Count == Cells.Count`, c'est-à-
dire un coup enregistré sur *chacune* des cases du navire ; `ReceiveShotForcedMiss` n'enregistre jamais de coup
sur le navire tout en rendant la case définitivement injouable (`IsValidTarget` exclut les cases déjà dans
`ShotsReceived`). Un navire touché par un seul raté forcé sur une de ses cases ne pouvait donc plus jamais
atteindre `IsSunk == true` — la différence avec un vrai raté (case vide, donc rien à perdre en la consommant)
n'avait pas été identifiée au moment de la décision. Les trois tests ci-dessus échouaient avant correctif,
exactement comme prédit (`Assert.True(board.IsValidTarget(target))` → `Actual: False`), confirmant que la case,
contrairement à l'ADR original, ne pouvait pas être retentée avec succès.

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
