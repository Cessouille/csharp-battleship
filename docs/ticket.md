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

## Piste écartée pour l'instant

### Navires en formes libres façon Tetris (tétrominos au lieu de segments droits)

**Statut** : écarté (non retenu lors du premier passage en revue avec l'utilisateur)

Change toute la validation de placement (chevauchement, débordement, formes non rectilignes), donc en
particulier `Board.TryPlaceShip` documenté dans `docs/adr/0002-placement-flotte.md`. Changement de
représentation d'état → aurait nécessité un ADR dédié. Laissé de côté pour concentrer l'effort sur les tickets
ci-dessus ; à reconsidérer si le socle est solide et qu'il reste du temps.

**Source** : [UltraBoardGames — variante "Tetris Battleship"](https://www.ultraboardgames.com/battleship/variations.php)
