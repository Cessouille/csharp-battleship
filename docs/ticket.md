# Backlog d'extensions — tickets

Ce fichier liste les extensions au-delà du socle (partie jouable de bout en bout), issues d'une recherche web
sur les variantes de bataille navale (2026-09-15) puis **réalignées sur le code réel** le même jour (voir
`PROMPTS.md`, entrée « Réalignement du backlog d'extensions »). Chaque ticket retenu sera documenté dans
`docs/adr/` une fois implémenté (gabarit `docs/adr/0001-modele.md`).

Le socle est fonctionnel (moteur de jeu, API REST, App Blazor, un échange gRPC-Web `GetGameState`) — voir
`README.md` § *Arbitrages du backlog* et § *Limites connues*, et les ADR 0001 à 0007 déjà actés. L'ADR 0007
(verrou par partie, `Game._gate` / `Game.Locked`) s'applique à **toute** nouvelle opération listée ici.

Statut de chaque ticket :
- `proposé` — piste identifiée, pas encore discutée/actée par le binôme
- `retenu` — décidé par le binôme, prêt à passer en ADR + implémentation
- `livré` — implémenté, testé, ADR et README à jour
- `écarté` — envisagé puis abandonné (raison à consigner)

## Arbitrages transverses (actés le 2026-09-15)

- **Ordre d'implémentation** : TICKET-03 → TICKET-00 → TICKET-02 → TICKET-01 → TICKET-04, du moins invasif
  (aucun changement de contrat) au plus invasif (refonte de la résolution d'un tour).
- **Activation** : Salvo, radar et armes spéciales sont des **options choisies à la création** de la partie ;
  la partie classique reste le défaut (parcours et tests actuels inchangés).
- **Salvo symétrique** : l'ordinateur tire lui aussi une salve dépendant de ses navires restants.
- **Radar** : un scan **consomme le tour** du joueur et déclenche la riposte de l'ordinateur.
- **Options livrées au fil de l'eau** : TICKET-00 n'introduit que l'option radar ; Salvo (TICKET-01) et armes
  (TICKET-04) ajoutent chacun leur option avec leur règle, pour ne jamais exposer une option sans effet.
- **Refonte du résultat de tour** : `TurnResult` / `TurnResultDto` passent à des listes de résolutions dès
  TICKET-02 (un scan n'a pas de tir joueur), refonte réutilisée telle quelle par TICKET-01 et TICKET-04.

Le découpage détaillé (fichiers, tests, ADR 0008 à 0012, commits) est dans le plan d'implémentation validé
le 2026-09-15 ; chaque ticket ci-dessous en reprend l'essentiel.

## Contraintes communes à tous les tickets

- Toute lecture/écriture d'une partie passe par le verrou de `Game` (ADR 0007).
- Toute vue du plateau adverse passe par `BoardViewMapper` (`BattleShip.Models/Contracts`), unique chokepoint
  anti-fuite pour REST et gRPC.
- Un coup refusé ne modifie pas l'état et ne déclenche pas de riposte ; les nouveaux motifs de refus s'ajoutent
  à `MoveRejectionReason` (`BattleShip.Models/Domain/Game.cs`).
- Numéros de champs de `Protos/battleship.proto` : uniquement ajoutés, jamais renumérotés ni réutilisés.
- Chaque règle ajoutée a au moins un test xUnit qui échoue quand la règle est neutralisée.
- `Pages/Game.razor` affiche aujourd'hui un message fixe pour tout 409 (« case déjà jouée ou partie terminée ») :
  dès qu'un nouveau motif de refus apparaît, l'App doit lire le champ `code` renvoyé par `GameEndpoints.ToConflict`.

---

## TICKET-03 — IA adverse par grille de probabilité

**Statut** : livré (ADR 0008)

**Description** : avant chaque tir, l'IA calcule pour chaque case non jouée le nombre de placements valides
des navires non encore coulés qui la recouvrent (mode *hunt*) ; les placements recouvrant une case touchée
d'un navire non coulé sont fortement pondérés (mode *target*). Elle tire sur la case de plus forte densité
(égalités départagées par le `Random` fourni).

**État actuel du code** :
- `Game.PickComputerTarget(Random)` est `private`, tire uniformément parmi les cases valides de `HumanBoard`.
- `Game.PlayHumanShot` code en dur `Random.Shared` : aucun test ne peut fixer le hasard de la riposte.
- `PickComputerTarget` reçoit implicitement `HumanBoard` entier, **positions des navires comprises** : rien
  n'empêche structurellement une stratégie plus élaborée de tricher.
- Test existant à conserver : `ComputerAutoShot_OnlyTargetsUntriedCellsOnHumanBoard_AcrossFullGame`
  (`BattleShip.Tests/Engine/GameTests.cs`).

**Impact technique** :
- Nouveau dossier `BattleShip.Models/Ai/` : `IComputerTargeting.PickTarget(OpponentBoardDto view, Random rng)`
  et son implémentation `ProbabilityTargeting`.
- **Anti-triche structurel** : l'IA ne reçoit que `HumanBoard.ToOpponentBoardDto()` (touches, ratés, navires
  coulés), jamais `Board.Ships`. Tailles des navires restants = `Fleet.Standard` moins les `SunkShips` de la vue.
- `Game` accepte une stratégie et un `Random` optionnels (défauts : probabiliste, `Random.Shared`) ; une cible
  invalide renvoyée par la stratégie lève une exception (bug, jamais corrigé silencieusement).
- Contrat public REST/gRPC/App inchangé ; seul le texte d'accueil de `Home.razor` (« tire au hasard ») change.
- **ADR obligatoire** (CLAUDE.md §3 cite la stratégie de l'adversaire) : `0008-ia-grille-probabilite.md`,
  qui remplace l'ADR 0004.

**Tests attendus** (`BattleShip.Tests/Engine/ProbabilityTargetingTests.cs`) :
- ne cible jamais une case déjà jouée, sur plusieurs graines et une partie complète ;
- touche isolée au centre → cible adjacente ; touche en coin → cible dans la grille ;
- deux plateaux au même historique visible mais aux navires cachés différents → même cible pour la même graine ;
- case entourée de ratés où aucun navire restant ne tient → jamais choisie ;
- sur des graines fixes, nombre moyen de tirs pour couler la flotte inférieur à une stratégie aléatoire de
  référence définie dans le projet de tests (pas dans le code livré).

**Sources** :
- [Coding an Intelligent Battleship Agent – Towards Data Science](https://towardsdatascience.com/coding-an-intelligent-battleship-agent-bf0064a4b319/)
- [Beating Battleships with Algorithms and AI](https://paulvanderlaken.com/2019/01/21/beating-battleships-with-algorithms-and-ai/)
- [GeeksforGeeks — Play Battleships Game with AI](https://www.geeksforgeeks.org/artificial-intelligence/play-battleships-game-with-ai/)

---

## TICKET-00 — Options de partie (prérequis de 02, 01 et 04)

**Statut** : livré (ADR 0009) — corps JSON obligatoire à la création, `{}` = partie classique (le corps facultatif prévu n'est pas acheminé par ASP.NET Core, voir ADR)

**Description** : le joueur choisit, avant de créer la partie, le mode de tir (classique / Salvo), l'activation
du radar et celle des armes spéciales.

**État actuel du code** : `POST /api/games` n'a **aucun corps** (`Home.razor` appelle
`PostAsync("api/games", null)`, les tests d'intégration aussi) ; `Game` ne porte aucune configuration.

**Impact technique** :
- `BattleShip.Models/Domain/GameOptions.cs` : record immuable stocké dans `Game`, avec une valeur `Classic`.
- `CreateGameRequestDto` + `CreateGameRequestDtoValidator` (FluentValidation, via `ValidationFilter<T>`).
  Corps **optionnel** : absent → partie classique, les appels existants restent valides.
- Options exposées dans `CreateGameResponseDto`, `GameStateDto` et `GameStateReply` (nouveau champ proto
  `options = 6`), mappées par `GameStateMapper` (API) et `GrpcMapping` (App).
- `Home.razor` : formulaire d'options avant « Nouvelle partie ».
- ADR `0009-options-de-partie.md` (changement de représentation de l'état).

**Tests attendus** : création sans corps → classique ; options invalides → 400 ; options relues identiques en
REST et en gRPC.

---

## TICKET-02 — Radar / reconnaissance à usage limité (gRPC-Web)

**Statut** : livré (ADR 0010)

**Description** : un nombre limité de fois par partie, le joueur scanne une zone du plateau adverse qui indique
seulement la présence ou l'absence d'un navire. Le scan **remplace le tir du tour** : l'ordinateur riposte.

**Règles actées (2026-09-15)** :
- zone 2×2, origine = coin haut-gauche, entièrement dans la grille sinon refus ;
- quota **fixe** de 2 scans par partie (l'option de création est un simple on/off) ;
- radar réservé au **joueur** : la grille de probabilité exploite déjà toute l'information visible, asymétrie
  à justifier dans l'ADR 0010 ;
- un scan ne marque aucune case comme jouée ; son résultat (booléen) reste affiché pour toute la partie.

**Correction par rapport à la version initiale** : le ticket présentait `ScanZone` comme « ne touchant pas la
boucle de jeu ». Avec l'arbitrage retenu, le scan décrémente un quota et consomme le tour : c'est un **flux
gRPC-Web mutant**, exactement le cas que l'ADR 0005 marque « à réexaminer ». Il reste un bon candidat
(opération nouvelle, sans risque de régression sur le tir REST), mais pas pour la raison avancée.

**Impact technique** :
- Domaine : `Board.ScanZone(origin)` ne renvoie qu'un booléen ; `Game.PlayHumanScan(origin)` sous verrou, refus
  `GameAlreadyFinished` / `OutOfGrid` / `RadarUnavailable` / `NoScansLeft`, puis riposte ordinateur ; historique
  des scans et quota restant stockés dans `Game`.
- Persistance de l'affichage : sans stockage des scans, l'hydratation `GetGameState` de `Game.razor` les perdrait
  au rechargement → `OpponentBoardDto.Scans` produit par `BoardViewMapper`, proto `OpponentBoardMessage.scans = 5`
  et quota restant dans `GameStateReply`.
- gRPC : `rpc ScanZone (ScanZoneRequest) returns (ScanZoneReply)` ; `ScanZoneRequestValidator` (GUID valide,
  origine telle que la zone tienne dans la grille). Erreurs : `InvalidArgument` (validation), `NotFound`,
  `FailedPrecondition` (quota épuisé, partie terminée, radar désactivé).
- App : bascule « Tir / Scan » dans `Game.razor`, état d'affichage des zones scannées dans `BoardViewModel`,
  compteur de scans. Erreur attendue démontrable : scan avec quota épuisé.
- ADR `0010-radar-grpc-web.md` + mise à jour du statut de l'ADR 0005.

**Tests attendus** : quota épuisé refusé sans mutation ni riposte ; le scan ne révèle aucune cellule de navire
dans la vue adverse ; scan refusé après fin de partie ; intégration gRPC (succès, `FailedPrecondition`,
`InvalidArgument`, `NotFound`) dans le style de `Api/GrpcGameStateTests.cs`.

**Sources** :
- [Battleship board game rules — variantes](https://gamerules.com/rules/battleship-board-game/)
- [UltraBoardGames — variante "Intelligence"](https://www.ultraboardgames.com/battleship/variations.php)

---

## TICKET-01 — Mode Salvo (tirs multiples liés aux navires restants)

**Statut** : livré (ADR 0011)

**Description** : chaque tour, le joueur envoie un lot de coordonnées dont la taille maximale est son nombre de
navires encore à flot ; l'ordinateur fait de même avec les siens (symétrie actée). La variante à nombre de tirs
fixe (« Speedy Rules ») n'est pas retenue.

**Règles actées (2026-09-15)** :
- une salve contient **exactement N** tirs, N = min(navires à flot du tireur, cases non jouées de la cible),
  calculé au début du tour du tireur (donc après la salve du joueur pour la riposte de l'ordinateur) ;
- si le dernier navire adverse coule en cours de salve : **arrêt immédiat**, tirs restants non résolus, pas de
  riposte ;
- l'ordinateur choisit ses N cibles sur une **même vue** (il ne profite pas des résultats de sa propre salve,
  comme le joueur qui tire à l'aveugle) ;
- en partie Salvo avec radar, un scan remplace toute la salve du tour.

**État actuel du code** :
- Il n'existe pas de DTO `PlayShot` : le tir passe par `ShotRequestDto(Row, Column)` sur
  `POST /api/games/{gameId}/shots`, résolu par `Game.PlayHumanShot(Coordinate)`.
- `TurnResult` (domaine) et `TurnResultDto` portent **un** `PlayerShot` et **un** `ComputerShot`.
- Le nombre de tirs autorisés n'a pas besoin d'un nouvel état : `Ships.Count(s => !s.IsSunk)` sur le plateau
  **du tireur** (`HumanBoard` pour le joueur, `ComputerBoard` pour l'ordinateur).

**Impact technique** :
- Refonte partagée avec TICKET-04 : `TurnResult` / `TurnResultDto` passent à des **listes** de résolutions
  (un élément en mode classique) ; adaptation de `GameEndpoints`, `Game.razor` et des tests existants.
- `Game.PlayHumanSalvo(coordonnées)` sous verrou : **tout le lot est validé avant le premier `ReceiveShot`**
  (hors grille, déjà joué, doublon dans le lot, lot trop grand, partie non Salvo) ; un seul élément invalide →
  refus complet sans mutation. Riposte : N appels à la stratégie de TICKET-03, la vue étant mise à jour entre
  chaque tir.
- API : `POST /api/games/{gameId}/salvos` avec `SalvoRequestDto` + validateur (non vide, au plus 5 éléments,
  coordonnées dans la grille, sans doublon). `/shots` reste le chemin du mode classique ; chaque chemin refuse
  l'autre mode (409).
- App : sélection de plusieurs cases puis « Tirer la salve ».
- ADR `0011-mode-salvo.md` (le tour reste résolu de façon synchrone, ADR 0001 inchangée sur ce point).

**Tests attendus** : lot plus grand que les navires restants refusé ; doublon intra-lot refusé ; un élément
invalide → aucun tir appliqué ; navire perdu → salve suivante réduite, pour le joueur **et** l'ordinateur ;
arrêt en fin de partie ; intégration REST 200/400/409.

**Sources** :
- [Battleship Salvo Game Rules – UltraBoardGames](https://www.ultraboardgames.com/battleship/salvo-rules.php)
- [The Boardwalk Games — Salvo Rules](https://theboardwalkgames.com/2016/01/12/board-game-of-the-week-battleship-salvo-rules/)

---

## TICKET-04 — Armes spéciales à munitions limitées (torpille / frappe aérienne)

**Statut** : livré (ADR 0012) — règle de la frappe sur cases déjà jouées à confirmer

**Description** : la torpille parcourt une rangée ou une colonne jusqu'au premier navire rencontré ; la frappe
aérienne touche plusieurs cases alignées d'un coup. Utilisables un nombre limité de fois par partie.

**Règles actées (2026-09-15)** :
- munitions : **1 torpille et 1 frappe** par joueur ; l'**ordinateur dispose des mêmes armes** ;
- torpille : part d'un bord choisi (gauche/droite/haut/bas) sur une ligne ou colonne, traverse sans effet les
  cases déjà jouées, marque chaque case vide franchie comme ratée et s'arrête sur le premier navire touché ;
  refusée si toute la trajectoire est déjà jouée ;
- frappe : 3 cases alignées (horizontal/vertical) entièrement dans la grille sinon refus ; cases déjà jouées
  ignorées, refus si les 3 le sont ; arrêt immédiat si la flotte adverse est coulée *(règle déduite de celle de
  la torpille, à confirmer à la relecture)* ;
- utiliser une arme **remplace tout le tour**, y compris toute la salve en mode Salvo.

**Impact technique** :
- Comme l'indiquait le ticket initial, `Board.ReceiveShot` (ADR 0003) ne cible qu'une coordonnée : il est
  **conservé tel quel**, l'arme se contente d'étendre une action en liste de coordonnées résolues une à une.
- Réutilise les listes de résolutions de `TurnResult` introduites par TICKET-01 (pas de seconde refonte).
- Munitions par joueur stockées dans `Game`, refus `NoAmmoLeft` / `WeaponsDisabled`.
- API : `POST /api/games/{gameId}/weapons` + `WeaponRequestDto` + validateur ; App : sélecteur d'arme et direction.
- **ADR obligatoire** : `0012-armes-speciales.md`.

**Tests attendus** : la torpille s'arrête au premier navire et rien au-delà n'apparaît dans la vue adverse ;
frappe débordant de la grille refusée sans mutation ; munitions épuisées refusées ; l'ordinateur soumis aux
mêmes quotas.

**Sources** :
- [BATTLESHIP App — Commanders Mode (App Store)](https://apps.apple.com/us/app/battleship/id1336026283)
- [Geeky Hobbies — règles alternatives](https://www.geekyhobbies.com/how-to-play-battleship-board-game-rules-and-instructions/)

---

## Deuxième vague de propositions (2026-09-16)

Le premier lot (TICKET-00 à TICKET-04) est entièrement livré. Cette deuxième vague part des limites déjà
documentées dans `README.md` § *Limites connues* et des trous relevés par `docs/audits/documentation.md`,
et non d'une nouvelle recherche web sur les variantes du jeu. **Aucun ordre d'implémentation n'était acté au
départ** : contrairement à la première vague, ces tickets sont restés au statut `proposé` avec leurs propres
questions ouvertes jusqu'à ce que le binôme les tranche un par un — TICKET-06, TICKET-07, TICKET-08 et
TICKET-10 sont depuis passés `livré` (voir leurs sections ci-dessous) ; TICKET-09 et TICKET-11 restent
`proposé`, non tranchés faute de temps. Le gradient plausible envisagé au départ (TICKET-08 → TICKET-07 →
TICKET-06 → TICKET-10 → TICKET-09) n'a pas été suivi à la lettre : TICKET-09 a été dépriorisé plutôt
qu'implémenté après TICKET-10.

Le placement manuel de flotte (ADR 0013), livré après le dernier arbitrage du 2026-09-15, n'avait jamais
été documenté dans ce fichier : TICKET-05 comble cet oubli a posteriori.

---

## TICKET-05 — Placement manuel de la flotte (entrée rétroactive)

**Statut** : livré (ADR 0013) — entrée ajoutée a posteriori pour réconcilier ce fichier avec le code ;
aucune implémentation nouvelle n'est impliquée par cet ajout.

**Description** : à la création, le joueur peut placer sa flotte manuellement au lieu du tirage aléatoire
par défaut ; l'ordinateur reste toujours placé au hasard.

**État actuel du code** :
- `Game.TryCreateManual` renvoie `CreateGameResult.Created` / `Rejected` selon la validité du placement fourni.
- `Board.TryPlaceFleet` rejoue les placements choisis par le client contre les tailles de navires possédées
  par le serveur : il ne fait jamais confiance à une taille envoyée par le client.
- `FleetPlacementRejectionReason` (`InvalidFleetComposition`, `OutOfGridOrOverlap`) porte les motifs de refus.
- `Home.razor` propose un `PlacementPanel` pour ce mode, en plus du tirage aléatoire.

**Impact technique** : aucune nouvelle phase serveur introduite — le placement est intégré à
`POST /api/games`, décision documentée dans l'ADR 0013 lui-même ; contrat REST/gRPC inchangé par ailleurs.

**Tests** : couverts par la suite existante (`BattleShip.Tests`), déjà verts.

---

## TICKET-06 — IA à difficulté réglable

**Statut** : livré (ADR 0015)

**Description** : le joueur choisit à la création un niveau de difficulté de l'IA adverse — Facile, Moyen
ou Difficile — au lieu de la grille de probabilité systématique. Adresse la limite qui était documentée
dans le README : « l'adversaire n'a pas de niveau de difficulté réglable ».

**Décisions tranchées** : trois paliers plutôt que deux (le « Moyen » chasse/cible envisagé puis écarté
lors du réalignement du backlog est finalement implémenté) ; verrouillé à la création comme les autres
options, défaut `Hard` pour ne rien changer aux parties existantes.

**Réalisé** :
- `AiDifficulty` (`GameOptions.Difficulty`, défaut `Hard`).
- `BattleShip.Models/Ai/RandomTargeting.cs` (Facile) et `HuntTargetTargeting.cs` (Moyen), aux côtés de
  `ProbabilityTargeting` (Difficile, inchangé). La doublure de test du même nom
  (`BattleShip.Tests.TestData.RandomTargeting`) a été supprimée au profit de la vraie implémentation.
- `Game.CreateTargeting(Options.Difficulty)` (`internal`) sélectionne la stratégie par défaut ; une
  stratégie passée explicitement au constructeur (tests TICKET-03/04) continue de primer.
- Contrat : `CreateGameRequestDto.Difficulty`, `GameOptionsDto.Difficulty`,
  `GameOptionsMessage.difficulty = 4` ; sélecteur dans `Home.razor`.
- `Engine/RandomTargetingTests.cs`, `Engine/HuntTargetTargetingTests.cs`, `Engine/AiDifficultyTests.cs`
  (mapping difficulté→stratégie, moyenne de tirs Facile > Moyen > Difficile sur 30 graines) ; round-trip
  REST et gRPC de l'option.

---

## TICKET-07 — Extension de gRPC-Web à un second flux mutant

**Statut** : livré (ADR 0014)

**Description** : seuls `GetGameState` (lecture) et `ScanZone` (mutant, TICKET-02) passaient par gRPC-Web ;
la Salve (TICKET-01) est ajoutée comme second flux mutant, en plus du `POST /salvos` REST existant.

**Décisions tranchées** : Salve plutôt qu'une arme (même forme « lot de coordonnées » que `ScanZone`) ;
les deux chemins (REST et gRPC-Web) coexistent plutôt que de retirer le REST déjà livré et testé
(ADR 0011) ; l'App n'utilise désormais que gRPC-Web pour tirer une salve, pour que le second flux soit
réellement démontrable depuis le navigateur, pas seulement en intégration.

**Réalisé** :
- `Protos/battleship.proto` : `rpc PlaySalvo` + `PlaySalvoRequest`/`PlaySalvoReply` (ajout pur).
- `BattleshipGrpcService.PlaySalvo` + `PlaySalvoRequestValidator` (contrôles de forme, comme
  `SalvoRequestDtoValidator` côté REST) ; `GameStateMapper.ToPlaySalvoReply`.
- `Game.razor.FireSalvoGrpcAsync` remplace l'appel REST pour l'action « Tirer la salve ».
- `Api/GrpcSalvoTests.cs` : succès, `FailedPrecondition` (taille, mode classique), `InvalidArgument`
  (doublon, lot vide, GUID malformé), `NotFound`. `Api/SalvoEndpointsTests.cs` inchangé, toujours vert.

---

## TICKET-08 — Renforcement des tests de concurrence réelle

**Statut** : livré

**Description** : `BattleShip.Tests/Engine/GameTests.cs` avait déjà un test de concurrence réelle (pas
simulée) mais limité au tir classique sur une seule cellule
(`PlayHumanShot_ConcurrentCallsOnSameCell_OnlyOneIsAccepted`, barrière `ManualResetEventSlim` + 32 tâches
+ `Task.WhenAll`). Ce ticket étend ce pattern à deux angles morts : les compteurs partagés (quotas) et le
pipeline HTTP complet, plutôt qu'un unique appel direct sur `Game`.

**État actuel du code (avant ce ticket)** : le test existant ne couvrait ni la décrémentation d'un quota
(scans, munitions) sous contention, ni la concurrence à travers `WebApplicationFactory`/Minimal API — les
deux étant des candidats plausibles à un bug de double-décompte que l'ADR 0007 corrige structurellement
mais qu'aucun test ne vérifiait dans ces deux configurations.

**Réalisé** :
- `Engine/RadarTests.cs` — `PlayHumanScan_ConcurrentCallsBeyondQuota_OnlyQuotaIsAccepted` : 32 scans
  concurrents sur une partie à flotte complète (radar, quota de 2), vérifie exactement
  `RadarRules.ScansPerGame` acceptés et le reste rejeté `NoScansLeft`.
- `Engine/WeaponTests.cs` — `PlayHumanWeapon_ConcurrentTorpedoCalls_OnlyOneIsAccepted` : 32 torpilles
  concurrentes avec 1 seule munition, vérifie exactement une acceptée et le reste rejeté `NoAmmoLeft`.
- `Api/GameEndpointsTests.cs` — `PostShots_ConcurrentRequestsOnSameCell_OnlyOneSucceeds` : 16 requêtes
  HTTP `POST /shots` concurrentes sur la même cellule à travers le vrai pipeline (validation,
  `InMemoryGameStore`, `Game.Locked`), vérifie un seul 200 et le reste en 409 `AlreadyPlayed`.

Pas d'ADR (renforcement de tests, aucun changement de contrat ni de comportement).

**Écarté de ce ticket** : les tests de composants Blazor (bUnit) — voir TICKET-11 ci-dessous, reporté à
part vu le surcoût (nouveau framework de test jamais utilisé dans le projet) pour une valeur plus faible
qu'un test de règle métier.

---

## TICKET-11 — Tests de composants Blazor (bUnit)

**Statut** : proposé

**Description** : ajouter des tests de rendu/interaction sur les composants Razor de `BattleShip.App`
(actuellement non testés autrement que manuellement au navigateur), alors que le front est un livrable
imposé par le cours. Séparé de TICKET-08 parce que c'est un nouveau framework de test à introduire dans
le projet (`bunit`), pas une extension d'un pattern déjà en place.

**État actuel du code** : `BattleShip.Tests.csproj` ne référence aucun package bUnit ; aucun test de
composant Razor n'existe.

**Impact technique** : ajout du package `bunit` ; quelques tests ciblés sur `Home.razor` (formulaire
d'options + placement manuel) et `Game.razor` (bascule de mode d'action, affichage des erreurs de
conflit). Pas d'ADR requis.

**Questions à trancher** : périmètre (couverture large ou quelques scénarios clés, vu le temps restant du
projet).

**Tests attendus** : ils sont l'objet même du ticket.

---

## TICKET-09 — Grille et flotte configurables

**Statut** : proposé

**Description** : le joueur choisit à la création la taille de grille et/ou la composition de flotte, au
lieu des valeurs fixes actuelles (10×10, `Fleet.Standard` à 5 navires). CLAUDE.md liste ce choix comme une
décision du binôme, jamais exploitée dans le code à ce jour.

**État actuel du code** : `Coordinate`/`BoardGrid.Size` et `Fleet.Standard` sont des constantes utilisées
à de nombreux endroits (placement aléatoire et manuel, mapping DTO, `BoardGrid.razor`, énumération des
tailles de navires restants par `ProbabilityTargeting`) — un changement de taille toucherait potentiellement
chacun de ces points.

**Impact technique** (le plus invasif de cette vague, comparable par son ampleur à la piste « tétrominos »
déjà écartée pour cette raison) :
- `GameOptions` gagnerait `GridSize`/`FleetComposition`, remplaçant les constantes actuelles par des
  valeurs portées par la partie.
- `Board`, `Fleet`, `Coordinate` deviendraient paramétrables plutôt que figés par constante statique —
  changement de représentation d'état plus large que les tickets précédents.
- `ProbabilityTargeting` devrait lire la composition réelle de la partie plutôt qu'une constante.
- `BoardGrid.razor`/`PlacementPanel` à adapter à une grille de taille variable.
- **ADR obligatoire** ; le binôme doit explicitement juger si l'effort est justifié pour le temps restant.

**Questions à trancher** : bornes acceptables (grille et flotte min/max) ; symétrie avec la flotte de
l'ordinateur ; compatibilité avec le placement manuel (ADR-0013) sur une grille de taille variable.

**Tests attendus** : placement rejeté si la flotte ne tient pas dans la grille choisie ; IA calcule ses
densités sur la flotte réellement configurée (pas `Fleet.Standard` codé en dur) ; options invalides
(grille trop petite pour la flotte demandée) rejetées à la création.

---

## TICKET-10 — Journal de partie affiché en direct

**Statut** : livré (ADR 0016)

**Description** : un panneau liste, tour par tour, chaque coup joué (tir, scan, salve, arme) et la riposte
de l'ordinateur. **Distinct** de « l'historique et les statistiques » inter-parties explicitement écarté
dans le README (qui visait une persistance de partie en partie) : ce journal ne couvre qu'une seule partie.

**Décision tranchée** : stockage **côté serveur** plutôt qu'accumulation côté client — le journal doit
survivre à un rechargement de page, comme le reste de l'état d'une partie. La formulation initiale
envisageait un test mêlant « tir » et « salve » dans la même séquence : impossible, `ShotMode` est figé par
partie (ADR 0009) ; testés séparément.

**Réalisé** :
- `Game.History` (`List<TurnResult>` interne), alimenté au chokepoint déjà commun à toute action
  (`CompleteTurn`) : un coup refusé n'y apparaît jamais.
- `JournalEntryDto` ; `GameStateDto.History` et `TurnResultDto.History` portent la liste complète à chaque
  réponse (pas seulement la nouvelle entrée). `GameStateReply.history = 8` (proto) + `JournalEntryMessage` ;
  `ScanZoneReply`/`PlaySalvoReply` en héritent gratuitement via leur `state` imbriqué.
- `Game.razor` : l'ancien résumé à une ligne (dernière riposte seulement) est remplacé par un panneau
  listant tout `_state.History`.
- `Engine/GameHistoryTests.cs` (ordre et contenu sur tir/scan/arme mêlés, une entrée par salve entière,
  aucune entrée pour un coup refusé) ; round-trip REST et gRPC.

---

## Troisième vague — Système de succès (2026-09-17)

Cette vague part d'une liste d'idées du binôme, complétée par cinq propositions de l'IA pensées pour le thème
visuel actuel de l'App (palette `--rose-vif` / `--lilas` / `--dore` / `--creme` de `wwwroot/css/app.css`,
polices Fredoka/Quicksand). L'origine de chaque idée est tracée (binôme ou IA). **Livrée entièrement** (commit
`4864370`, « feat: ajout des succès ») : les trois tickets et les 11 succès sont au statut `livré`, décisions
détaillées dans `docs/adr/0017-systeme-de-succes.md` et `docs/adr/0018-profil-joueur-anonyme.md`. Cette entrée
n'a été mise à jour qu'après coup, à l'occasion de l'audit `docs/audits/documentation.md` du 2026-09-17 qui a
relevé l'écart entre ce fichier (encore `proposé`) et le code déjà livré.

Découpage :
- **TICKET-12** — socle technique commun (évaluation serveur, stockage, contrat, affichage) ;
- **TICKET-13** — catalogue des succès évaluables **dans une seule partie** (S-01, S-02, S-04 à S-11) ;
- **TICKET-14** — succès **inter-parties**, qui imposent un profil joueur (S-03).

Ordre effectivement suivi : TICKET-12 (socle) livré avec l'intégralité de TICKET-13 et TICKET-14 en une seule
passe plutôt qu'en plusieurs — voir ADR 0017 pour la justification (périmètre regroupé plutôt que livré succès
par succès).

### Récapitulatif du catalogue

| Id | Nom proposé | Condition résumée | Origine | Portée | Ticket |
|---|---|---|---|---|---|
| S-01 | 💖 Cœur de tirs | Dessiner un cœur 5×5 avec ses tirs | binôme | partie | 13 |
| S-02 | 🛡️ Sans une égratignure | Gagner sans perdre de navire | binôme | partie | 13 |
| S-03 | 👑 Reine du difficile | Gagner 3 parties en Difficile | binôme | inter-parties | 14 |
| S-04 | 💫 Série étincelante | Toucher 5 fois d'affilée | binôme | partie | 13 |
| S-05 | 💎 Quatre coins | Gagner en ayant tiré dans chaque coin | binôme | partie | 13 |
| S-06 | 📏 Rangée parfaite | Flotte placée sur uniquement deux lignes | binôme | partie | 13 |
| S-07 | 💘 Coup de foudre | Toucher dès le premier tir de la partie | IA | partie | 13 |
| S-08 | 🎀 Nœud papillon | Dessiner un nœud 5×5 avec ses tirs | IA | partie | 13 |
| S-09 | 👛 Panoplie complète | Gagner avec Radar + Salvo + Armes spéciales | IA | partie | 13 |
| S-10 | ✨ Baguette magique | Couler un navire avec une arme spéciale | IA | partie | 13 |
| S-11 | 🦋 Glow up | Gagner avec un seul navire encore à flot | IA | partie | 13 |

---

## TICKET-12 — Socle du système de succès

**Statut** : livré (ADR 0017)

**Description** : des succès se débloquent pendant une partie selon ce que fait le joueur ; ils sont détectés
par le serveur, conservés avec la partie et affichés dans l'App (notification au déblocage + panneau de badges).

**État actuel du code** :
- `Game.History` (`IReadOnlyList<TurnResult>`, TICKET-10) est alimenté uniquement par `CompleteTurn`, donc
  jamais par un coup refusé. Chaque `TurnResult` porte `PlayerShots` (`ShotResolution` : `Target`,
  `Outcome` ∈ `Miss`/`Hit`/`Sunk`, `SunkShipKind`), `PlayerScan`, `PlayerWeapon`, `ComputerShots`,
  `ComputerWeapon`, `Winner`, `Status`.
- `ComputerBoard.ShotsReceived` = ensemble des cases jouées par le joueur ; `HumanBoard.Ships[].Cells` et
  `IsSunk` = flotte du joueur ; `Game.Options` = options de la partie (dont `Difficulty`).
- Les cases vides traversées par une torpille sont résolues en `Miss` par `Board.ReceiveWeapon` : elles
  apparaissent dans `PlayerShots` et `ShotsReceived` comme n'importe quel tir raté (impact sur S-01, S-04, S-05,
  S-08, voir leurs questions).
- Aucune notion de succès n'existe, ni côté serveur ni côté App.

**Contraintes (issues de CLAUDE.md et des ADR existants)** :
- **Évaluation côté serveur uniquement** : le client n'envoie jamais « succès débloqué », il ne fait qu'afficher.
- Évaluation **sous le verrou** de `Game` (ADR 0007), au même chokepoint que le journal (`CompleteTurn`) : un
  coup refusé ne débloque rien, rejouer une case déjà jouée ne fait pas progresser une série.
- **Anti-fuite** : le déblocage d'un succès en cours de partie ne doit dépendre que d'informations déjà
  visibles par le joueur (ses propres tirs et leurs résultats, sa propre flotte, les options). Toute règle qui
  lirait une case adverse non découverte est refusée, même si elle « ne donne qu'un booléen ».
- Un succès débloqué n'est **jamais retiré** (même logique « ajout seul » que `History`).
- Numéros de champs proto uniquement ajoutés : prochain numéro libre de `GameStateReply` = `9`.

**Impact technique (piste, à confirmer dans l'ADR)** :
- Domaine, `BattleShip.Models/Achievements/` : `AchievementId` (enum), une règle par succès derrière une
  interface commune (ex. `IAchievementRule { AchievementId Id; bool IsUnlocked(Game game); }`) et un
  `AchievementEvaluator` qui les exécute toutes. Une classe par règle : chaque succès se teste et s'explique
  isolément, et en ajouter un ne modifie pas les autres.
- `Game.Achievements` (`IReadOnlyCollection<AchievementId>`), complété dans `CompleteTurn` après l'ajout au
  journal ; éventuellement aussi à la création pour S-06 (voir TICKET-13).
- Contrat : `GameStateDto.Achievements` et `TurnResultDto.Achievements` (liste complète d'identifiants
  `string`, comme `History`) ; proto `repeated string achievements = 9` dans `GameStateReply` ;
  mapping dans `BoardViewMapper` / `GameStateMapper` (API) et `GrpcMapping` (App). `ScanZoneReply` et
  `PlaySalvoReply` en héritent via leur `state` imbriqué, comme pour le journal.
- App : libellés, emoji et descriptions vivent côté App (présentation), le serveur n'expose que des
  identifiants. Notification (« toast » arrondi rose vif / doré) calculée par différence entre la liste
  précédente et la nouvelle ; panneau « Mes succès » sous les grilles, badges débloqués en `--dore`, badges
  verrouillés en silhouette `--lilas`.
- **ADR obligatoire** (changement de représentation de l'état) : `0017-systeme-de-succes.md`.
- README : fonctionnalités livrées + limites connues à mettre à jour à la livraison.

**Décisions tranchées** (voir ADR 0017 pour le détail et les alternatives écartées) :
- Succès **visibles verrouillés** avec leur condition — pas de secret, cohérent avec le panneau « Mes succès ».
- Évaluation **à chaque tour** (`Game.CompleteTurn`), plus une fois à la construction pour Rangée parfaite.
- Périmètre livré en une seule passe : TICKET-12 + TICKET-13 + TICKET-14 (profil joueur, ADR 0018 inclus).

**Réalisé** :
- `BattleShip.Models/Achievements/` : `AchievementId`, `IAchievementRule` (une classe par succès),
  `AchievementRules.All` (catalogue déclaratif), `AchievementContext` (anti-fuite structurelle, ne reçoit que
  ce que le joueur voit déjà de sa propre partie).
- `Game.Achievements` (`IReadOnlyList<AchievementId>`), alimenté par `EvaluateAchievements()` — chokepoint
  unique, ajout seul jamais retiré.
- Contrat : `GameStateDto.Achievements`, `TurnResultDto.Achievements` ; proto `repeated string achievements = 9`
  dans `GameStateReply`. `ScanZoneReply`/`PlaySalvoReply` en héritent via leur `state` imbriqué.
- App : `AchievementCatalog` (présentation), `AchievementBadge.razor`/`AchievementToast.razor` (affichage),
  page `Pages/Achievements.razor` (`/mes-succes`), groupée par portée (`AchievementScope.Partie` /
  `InterParties`).
- `Engine/Achievements/AchievementEvaluationTests.cs`, `Engine/Achievements/AchievementRulesCoverageTests.cs`
  (chaque règle en positif et en négatif), `Contracts/AchievementLeakTests.cs`
  (`TwoGamesWithIdenticalVisibleProjection_UnlockTheSameAchievements`), `Api/AchievementsContractTests.cs`
  (round-trip REST et gRPC).

---

## TICKET-13 — Catalogue des succès intra-partie

**Statut** : livré (ADR 0017)

Chaque succès ci-dessous est une règle indépendante. « Tir du joueur » désigne un `ShotResolution` de
`PlayerShots`, quelle que soit l'action qui l'a produit (tir, salve, arme), sauf arbitrage contraire.

### S-01 — 💖 Cœur de tirs (idée du binôme)

**Condition** : les cases jouées par le joueur (`ComputerBoard.ShotsReceived`) contiennent un cœur dans une
fenêtre 5×5 quelconque de la grille. Motif candidat, **à valider** (16 cases) :

```
. X . X .
X X X X X
X X X X X
. X X X .
. . X . .
```

**Décision (ADR 0017)** : motif ci-dessus retenu tel quel ; des tirs supplémentaires dans la fenêtre
n'invalident pas le dessin ; aucune rotation acceptée (translation seule).

**Tests attendus** : motif complet n'importe où dans la grille → débloqué ; motif à une case près → non ;
motif collé au bord droit/bas (fenêtre qui tient tout juste) → débloqué.

### S-02 — 🛡️ Sans une égratignure (idée du binôme)

**Condition** : `Winner == Human` et aucun navire de `HumanBoard.Ships` n'est `IsSunk`.

**Décision (ADR 0017)** : « perdre un bateau » = navire **coulé** (`Ship` n'expose pas les touches partielles,
« aucune case touchée » n'est pas observable).

**Tests attendus** : victoire avec un navire coulé → non ; victoire flotte intacte → débloqué ; partie en
cours flotte intacte → non (la condition exige la victoire).

### S-04 — 💫 Série étincelante (idée du binôme)

**Condition** : 5 tirs du joueur consécutifs, dans l'ordre du journal, dont le `Outcome` est `Hit` ou `Sunk` ;
un `Miss` remet la série à zéro.

**Décision (ADR 0017)** : un scan n'interrompt pas la série (aucune résolution contribuée) ; l'ordre d'une salve
est celui envoyé par le joueur ; les `Miss` d'une torpille traversant des cases vides cassent la série.

**Tests attendus** : touche ×4 puis raté puis touche → non ; 5 touches réparties sur 5 tours classiques →
débloqué ; case déjà jouée refusée au milieu de la série → série intacte (le refus n'est pas dans le journal).

### S-05 — 💎 Quatre coins (idée du binôme)

**Condition** : `Winner == Human` et `ShotsReceived` contient les quatre coins `(0,0)`, `(0,9)`, `(9,0)`,
`(9,9)` (bornes dérivées de `BoardGrid.Size`, pas codées en dur).

**Décision (ADR 0017)** : le tir gagnant n'a pas à tomber lui-même dans un coin ; toute résolution qui marque
la case d'un coin (tir classique, salve, torpille, frappe) compte, `FourCornersRule` ne distingue pas la source.

**Tests attendus** : 3 coins + victoire → non ; 4 coins + défaite → non ; 4 coins + victoire → débloqué.

### S-06 — 📏 Rangée parfaite (idée du binôme)

**Condition** : les cases de tous les navires de `HumanBoard.Ships` n'occupent que deux lignes distinctes.
Faisable avec la flotte standard (5 + 4 + 3 + 3 + 2 = 17 cases ≤ 20) grâce au placement manuel (TICKET-05,
ADR 0013).

**Décision (ADR 0017)** : évaluée **dès la création** de la partie — obtenable sans gagner ni même tirer un
coup, arbitrage assumé plutôt que le moment « fin de partie gagnée » envisagé pour éviter ce contournement ;
un navire vertical à cheval sur les deux lignes est accepté ; les deux lignes n'ont pas besoin d'être
adjacentes ; un placement aléatoire qui tomberait sur deux lignes compte aussi (`PerfectRowRule` ne distingue
pas l'origine du placement).

**Tests attendus** : flotte sur 3 lignes → non ; flotte sur 2 lignes (placement manuel) + condition de
moment retenue → débloqué ; placement manuel refusé (chevauchement) → aucune partie, aucun succès.

### S-07 — 💘 Coup de foudre (proposition IA)

**Condition** : le premier tir du joueur de toute la partie (premier `ShotResolution` du premier
`TurnResult` dont `PlayerShots` n'est pas vide) a un `Outcome` `Hit` ou `Sunk`.

**Décision (ADR 0017)** : un scan préalable ne disqualifie pas (il ne contribue aucune résolution) — le premier
tir cherché reste le premier `ShotResolution` du premier tour qui en contient un ; en Salvo, c'est la première
case de la salve (premier élément de `PlayerShots`) qui doit toucher, pas « au moins une touche dans la salve ».

**Tests attendus** : premier tir raté puis touche au second → non ; premier tir touché → débloqué ; scan puis
tir touché → selon l'arbitrage.

### S-08 — 🎀 Nœud papillon (proposition IA)

**Condition** : même moteur que S-01, autre motif 5×5. Motif candidat, **à valider** (17 cases) :

```
X . . . X
X X . X X
X X X X X
X X . X X
X . . . X
```

**Décision (ADR 0017)** : identique à S-01 (translation seule, tirs en plus dans la fenêtre tolérés) ; le nœud
tourné d'un quart de tour n'est **pas** accepté (même moteur `ShotPatternRule`, aucune rotation).

**Tests attendus** : identiques à S-01 ; le cœur complet ne débloque pas le nœud et inversement.

### S-09 — 👛 Panoplie complète (proposition IA)

**Condition** : `Winner == Human` avec `Options.Radar`, `Options.ShotMode == Salvo` et
`Options.SpecialWeapons` tous activés.

**Décision (ADR 0017)** : oui, radar et arme doivent avoir été **réellement utilisés** au moins une fois (pas
seulement les options cochées) ; Salvo n'a pas d'équivalent « utiliser » (mode figé pour toute la partie), donc
seule son activation est requise ; aucune difficulté minimale exigée.

**Tests attendus** : une option manquante + victoire → non ; toutes les options + défaite → non ; toutes les
options + victoire → débloqué.

### S-10 — ✨ Baguette magique (proposition IA)

**Condition** : un `TurnResult` du joueur avec `PlayerWeapon` non nul contient un tir `Sunk`.

**Décision (ADR 0017)** : le coup de grâce suffit — le navire peut avoir déjà été endommagé par un tir
classique lors d'un tour précédent, pas de version stricte « coulé entièrement par l'arme ».

**Tests attendus** : arme qui touche sans couler → non ; tir classique qui coule → non ; arme qui coule → débloqué.

### S-11 — 🦋 Glow up (proposition IA)

**Condition** : `Winner == Human` et exactement un navire de `HumanBoard.Ships` n'est pas `IsSunk`. Un navire
coulé ne se relève jamais : « un seul navire restant à la fin » équivaut à « être descendue à un seul navire ».

**Questions à trancher** : aucune identifiée.

**Tests attendus** : victoire avec 2 navires à flot → non ; victoire avec 1 navire à flot → débloqué ; S-02 et
S-11 ne peuvent jamais être débloqués ensemble dans la même partie.

---

## TICKET-14 — Succès inter-parties et profil joueur

**Statut** : livré (ADR 0018)

**Description** : succès cumulés sur plusieurs parties. Premier cas : **S-03 — 👑 Reine du difficile** (idée
du binôme), gagner 3 parties en difficulté `Hard`.

**État actuel du code** : `InMemoryGameStore` ne connaît que des parties (`ConcurrentDictionary<Guid, Game>`),
aucune notion de joueur ; stockage en mémoire perdu au redémarrage (ADR 0006) ; le README écarte explicitement
« historique et statistiques » et tout compte joueur. Ce ticket **revient sur cet arbitrage** : à assumer et à
documenter comme tel.

**Options envisagées** (voir ADR 0018 pour le détail) :
- **A — identifiant joueur anonyme, profil dérivé sans état stocké** (retenue) : le serveur génère un
  identifiant, l'App le conserve dans le `localStorage` et l'envoie à la création de partie ; le profil
  (compteurs, succès) est **recalculé à chaque lecture** à partir des parties connues pour ce joueur — pas un
  compteur accumulé séparément. Cohérent avec l'ADR 0006 ; aucun risque de double-comptage puisqu'il n'y a rien
  à synchroniser.
- **B — compteur côté client uniquement** (`localStorage`) : écartée, le client deviendrait source de vérité et
  le succès serait falsifiable depuis la console du navigateur.
- **C — persistance réelle** (base de données) : écartée, hors budget du projet, contredit l'ADR 0006.

**Réalisé** :
- `PlayerProfile.Project` (projection pure, pas de store de profil séparé) + `GameOutcome` (instantané
  anonymisé construit sous verrou) dans `BattleShip.Models/Achievements/PlayerProfile.cs`.
- `InMemoryGameStore` gagne un index `playerId → gameIds` (`FindByPlayer`), pas un second store.
- `CreateGameRequestDto.PlayerId` (GUID optionnel, forme validée par FluentValidation, aucune existence
  préalable requise) ; **contrat REST seul** (`POST /api/players`, `GET /api/players/{playerId:guid}`) — pas de
  RPC, le flux gRPC-Web obligatoire du cours est déjà couvert par `GetGameState`/`ScanZone`/`PlaySalvo`.
- `PlayerSession` côté App (`localStorage`, création à la volée).
- « 3 parties » = **cumulées**, pas nécessairement consécutives ; une partie non terminée ou perdue n'incrémente
  rien.
- README (arbitrages + limites connues) mis à jour : sans authentification, quiconque connaît l'identifiant lit
  le profil, même limite que les GUID de partie. Un seul succès inter-parties livré (S-03) — le coût d'un
  second aurait été à repeser, non tranché faute d'un second candidat retenu par le binôme.

**Tests** : `Engine/Achievements/PlayerProfileTests.cs` (3 victoires `Hard` → débloqué ; 2 `Hard` + 1 `Medium` →
non ; défaite ou partie non terminée → aucun décompte ; victoires non consécutives comptées quand même) ;
`Api/PlayerEndpointsTests.cs` (identifiant malformé dans le corps → 400 ; GUID malformé dans la route → 404 ;
identifiant inconnu → profil vide, pas une erreur ; deux joueurs ne voient jamais les parties l'un de l'autre).

---

## Piste écartée pour l'instant

### Navires en formes libres façon Tetris (tétrominos au lieu de segments droits)

**Statut** : écarté (non retenu lors du premier passage en revue avec l'utilisateur)

Change toute la validation de placement (chevauchement, débordement, formes non rectilignes), donc en
particulier `Board.TryPlaceShip` documenté dans `docs/adr/0002-placement-flotte.md`. Changement de
représentation d'état → aurait nécessité un ADR dédié. Laissé de côté pour concentrer l'effort sur les tickets
ci-dessus ; à reconsidérer si le socle est solide et qu'il reste du temps.

**Source** : [UltraBoardGames — variante "Tetris Battleship"](https://www.ultraboardgames.com/battleship/variations.php)
