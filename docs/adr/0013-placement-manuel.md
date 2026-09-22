# ADR 0013 : placement manuel de la flotte, en option du placement aléatoire

## Statut et date

Accepté — 16/09/2026. Complète l'ADR 0002 (algorithme de placement aléatoire), ne le remplace pas : les deux
mécanismes coexistent, choisis par le joueur à la création de la partie.

## Contexte

Jusqu'ici, `Game.CreateRandom` plaçait systématiquement la flotte humaine au hasard, comme celle de
l'ordinateur ; c'était explicitement listé comme piste écartée dans `README.md` § *Arbitrages du backlog*
(« personnalisation du placement de la flotte par le joueur »). Le binôme revient sur cet arbitrage :
le joueur doit pouvoir choisir entre placement aléatoire (comportement actuel, inchangé) et placement
manuel de ses 5 navires avant que la partie ne commence.

La contrainte non négociable du cours s'applique sans exception : le serveur reste la seule source de
vérité pour les règles de placement (bornes, chevauchement, composition de la flotte) ; le client ne peut
être qu'une aide de saisie.

## Options envisagées

1. **Nouvelle phase serveur `AwaitingPlacement`** : `POST /api/games` crée immédiatement la partie avec un
   plateau humain vide, un statut `AwaitingPlacement` ; un nouvel endpoint `POST /api/games/{id}/placement`
   pose la flotte ; tout coup est refusé tant que le placement n'est pas soumis. Plus fidèle à un parcours
   "création → placement → tirs" en étapes serveur séparées, mais introduit un nouvel état persistant dans
   `Game`, un nouveau motif de refus pour les tirs prématurés, un nouvel endpoint et une page dédiée côté
   App — pour un projet à un seul joueur humain (pas de multijoueur), le bénéfice ne compensait pas la
   surface supplémentaire à tester et à expliquer.
2. **Placement côté client, regroupé dans la requête de création** : le joueur pose ses navires sur une
   grille avant de cliquer "Nouvelle partie" ; la liste des positions choisies est envoyée dans la même
   requête `POST /api/games` que les options de partie existantes (Radar, Salvo, armes). Le serveur revalide
   tout et crée la partie déjà entièrement placée, ou refuse la création en bloc — aucun état intermédiaire
   à persister.

## Décision

Option 2. `CreateGameRequestDto.Placements` (optionnel, absent ou vide = comportement historique) porte la
liste des positions choisies. `Board.TryPlaceFleet` rejoue chaque position via `Board.TryPlaceShip` — la
taille de chaque navire est relue dans `Fleet.Standard`, jamais transmise par le client, pour ne pas lui
faire confiance sur ce point non plus. `Game.TryCreateManual` vérifie d'abord que l'ensemble des types de
navire demandés correspond exactement à `Fleet.Standard` (motif de refus `InvalidFleetComposition`), puis
délègue à `TryPlaceFleet` (motif `OutOfGridOrOverlap`) ; en cas de succès, le plateau ordinateur est placé
au hasard comme avant. Le résultat suit le même style que `MoveResult.Accepted/Rejected` déjà utilisé pour
les tirs (`CreateGameResult.Created`/`Rejected`), et l'endpoint traduit un refus en `409 Conflict` avec un
`code`, exactement comme pour un coup invalide.

Côté App, `PlacementViewModel` réutilise directement `Board`/`TryPlaceShip`/`PlaceFleetRandomly` (déjà
référencés depuis `BattleShip.Models.Domain`) comme aide de saisie : retour immédiat sur un placement
invalide, mais aucune autorité — le serveur revalide tout à la création. L'interaction est clic + bouton
d'orientation (`PlacementPanel.razor`), sur le modèle du toggle d'orientation déjà utilisé pour la frappe
aérienne (`ActionBar.razor`) : pas de glisser-déposer, qui aurait demandé de l'interop JS absente du projet
pour un gain d'ergonomie marginal par rapport à la demande.

## Conséquences

- Aucun nouvel état de `Game` ni nouvel endpoint : la surface testée reste celle d'une création de partie
  (acceptée ou refusée), pas une machine à états supplémentaire.
- `Game.CreateRandom` reste inchangé (toujours utilisé directement par plusieurs tests existants et par
  `InMemoryGameStore` quand aucun placement n'est fourni) : aucune régression sur le chemin aléatoire.
- Le client duplique une partie du calcul de validité (via le même `Board` que le serveur, pas une
  réimplémentation) pour l'ergonomie ; ceci n'affaiblit pas la garantie serveur, qui revalide indépendamment
  chaque requête de création.
- La flotte de l'ordinateur reste toujours placée au hasard : le placement manuel ne concerne que le joueur
  humain, conformément à la demande.

## Vérification et réexamen

`BoardTests.TryPlaceFleet_*`, `GameTests.TryCreateManual_*` et `GameEndpointsTests.PostGames_With*Placements*`
couvrent respectivement : flotte valide acceptée, chevauchement/débordement refusés, composition incorrecte
refusée, et la même chose bout en bout via l'API (201/409/400). Scénario manuel dans le navigateur : créer
une partie en mode "Aléatoire" (comportement inchangé), puis en mode "À la main" en posant les 5 navires,
et vérifier que "Votre plateau" affiche exactement les positions choisies. À réexaminer si un mode
multijoueur est un jour retenu : une phase de placement serveur explicite (option 1 ci-dessus) redeviendrait
alors nécessaire pour synchroniser deux joueurs humains.

## Références

`BattleShip.Models/Domain/Board.cs` (`TryPlaceFleet`), `BattleShip.Models/Domain/Game.cs`
(`TryCreateManual`, `CreateGameResult`), `BattleShip.API/Endpoints/GameEndpoints.cs`,
`BattleShip.App/Game/PlacementViewModel.cs`, `BattleShip.App/Components/PlacementPanel.razor`,
`docs/adr/0002-placement-flotte.md`.
