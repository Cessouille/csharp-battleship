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
et non d'une nouvelle recherche web sur les variantes du jeu. **Aucun ordre d'implémentation n'est acté** :
contrairement à la première vague, ces tickets restent au statut `proposé` avec leurs propres questions
ouvertes tant que le binôme ne les a pas tranchées. À titre indicatif seulement (non décidé), un gradient
plausible du moins invasif au plus invasif serait TICKET-08 → TICKET-07 → TICKET-06 → TICKET-10 → TICKET-09.

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

**Statut** : proposé

**Description** : le joueur choisit à la création un niveau de difficulté de l'IA adverse, au lieu de la
grille de probabilité systématique actuelle. Adresse la limite documentée dans le README : « l'adversaire
n'a pas de niveau de difficulté réglable ».

**État actuel du code** :
- `Game` instancie toujours `ProbabilityTargeting` (ADR 0008) ; il n'existe aucun autre choix.
- `IComputerTargeting` est une interface déjà pensée pour être substituable — c'est le seam à réutiliser.
- La stratégie aléatoire uniforme d'origine (pré-ADR 0008) n'existe plus dans le code : à réintroduire
  comme implémentation distincte si un palier « Facile » est retenu.
- `PROMPTS.md` mentionne une stratégie chasse/cible intermédiaire, envisagée puis écartée au profit de la
  grille de probabilité — candidate pour un éventuel palier « Moyen ».

**Impact technique** :
- `GameOptions` gagne un champ `Difficulty` (valeur par défaut = comportement actuel, pour ne rien casser).
- Nouvelle(s) implémentation(s) `IComputerTargeting` dans `BattleShip.Models/Ai/` (ex. `RandomTargeting`).
- `Game` sélectionne la stratégie à partir de `Options.Difficulty` au lieu de toujours instancier
  `ProbabilityTargeting`.
- Propagation dans `CreateGameRequestDto`/validateur, `GameStateDto`/`GameStateReply` (nouveau champ proto,
  jamais renuméroté), sélecteur dans `Home.razor`.
- **ADR obligatoire** (CLAUDE.md §3 : la stratégie de l'adversaire est structurante) — succession de
  l'ADR 0008, pas un remplacement.

**Questions à trancher** : deux paliers (Facile/Difficile) ou trois (+ Moyen) ; la difficulté reste-t-elle
verrouillée à la création comme les autres options.

**Tests attendus** : chaque stratégie ne cible jamais une case déjà jouée (à dupliquer sur le modèle de
`ProbabilityTargetingTests`) ; nombre moyen de tirs pour couler la flotte plus élevé en « Facile » qu'en
« Difficile » sur des graines fixes ; options relues identiques en REST et gRPC.

---

## TICKET-07 — Extension de gRPC-Web à un second flux mutant

**Statut** : proposé

**Description** : seuls `GetGameState` (lecture) et `ScanZone` (mutant, TICKET-02) passent par gRPC-Web ;
les autres actions (tir classique, salve, armes) restent exclusivement REST. Migrer un second flux mutant
élargirait la démonstration gRPC-Web au-delà d'un unique échange et vérifierait que le pattern de
l'ADR 0010 (verrou + erreurs typées) se généralise sans réécriture.

**État actuel du code** :
- `GameEndpoints.cs` expose `/shots`, `/salvos`, `/torpedoes`, `/airstrikes` en REST uniquement.
- `Protos/battleship.proto` ne contient que `GetGameState` et `ScanZone`.
- Les DTO REST (`ShotRequestDto`, `SalvoRequestDto`, `WeaponRequestDto`) n'ont pas d'équivalent proto.

**Impact technique** :
- Nouveau `rpc` (ex. `PlaySalvo`) + validateur FluentValidation dédié, sur le modèle de
  `ScanZoneRequestValidator`.
- Bascule de l'appel correspondant dans `Game.razor` vers gRPC-Web.
- ADR : mise à jour du statut de l'ADR 0005/0010, ou nouvel ADR selon l'ampleur retenue.

**Questions à trancher** : quel flux migrer (Salve, plus proche du scan par sa forme « lot de coordonnées »,
ou Arme) ; conserver le double chemin REST + gRPC ou migrer complètement ; quelle erreur typée démontrer
pour ne pas répéter exactement le cas déjà couvert par le scan.

**Tests attendus** : intégration gRPC succès + erreurs typées dans le style de `GrpcScanZoneTests.cs` ;
non-régression du chemin REST si conservé.

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

**Statut** : proposé

**Description** : afficher pendant la partie un journal chronologique des coups joués (tirs, scans,
salves, armes, des deux côtés), non persistant. **Distinct** de « l'historique et les statistiques »
inter-parties explicitement écarté dans le README (qui visait une persistance de partie en partie) : ce
journal ne vit que dans la session de jeu affichée.

**État actuel du code** : `Game`/`Board` ne conservent que l'état courant (cases jouées, navires coulés) ;
aucun historique ordonné des coups n'est stocké. Le front ne fait qu'afficher l'état courant via
`GetGameState`, sans accumuler les `TurnResultDto` reçus.

**Impact technique** :
- Option la plus simple : accumulation **côté client uniquement**, dans l'état Blazor de `Game.razor`
  (liste des `TurnResultDto` reçus au fil des appels) — aucun changement de contrat serveur, pas d'ADR.
- Limite assumée dans ce cas : le journal disparaît aussi au rechargement de page en cours de partie
  (`GetGameState` ne renvoie que l'état courant, pas l'historique).
- Alternative plus lourde : `Game` conserve une liste ordonnée de résolutions pour survivre au
  rechargement — implique un ADR de changement de représentation d'état.
- UI : nouveau panneau listant les entrées (ex. « Tour 4 : tir en (B,3) → touché »).

**Questions à trancher** : le journal doit-il survivre à un rechargement de page (stockage côté `Game`,
ADR requis) ou seulement à la session d'affichage courante (pas d'ADR) — cette décision fixe si le ticket
reste peu invasif ou rejoint la catégorie des changements de représentation d'état.

**Tests attendus** : si stockage serveur retenu, test xUnit vérifiant un historique complet et ordonné
après une séquence mêlant tir/scan/salve/arme ; si accumulation côté client, test de composant (bUnit, cf.
TICKET-08 si retenu) vérifiant l'affichage au fil des appels.

---

## Piste écartée pour l'instant

### Navires en formes libres façon Tetris (tétrominos au lieu de segments droits)

**Statut** : écarté (non retenu lors du premier passage en revue avec l'utilisateur)

Change toute la validation de placement (chevauchement, débordement, formes non rectilignes), donc en
particulier `Board.TryPlaceShip` documenté dans `docs/adr/0002-placement-flotte.md`. Changement de
représentation d'état → aurait nécessité un ADR dédié. Laissé de côté pour concentrer l'effort sur les tickets
ci-dessus ; à reconsidérer si le socle est solide et qu'il reste du temps.

**Source** : [UltraBoardGames — variante "Tetris Battleship"](https://www.ultraboardgames.com/battleship/variations.php)
