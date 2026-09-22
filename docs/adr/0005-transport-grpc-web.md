# ADR 0005 : flux exposé en gRPC-Web

## Statut et date

Accepté — 15/09/2026. Réexaminé le même jour : un flux mutant (`ScanZone`) est ajouté par `docs/adr/0010-radar-grpc-web.md`.

## Contexte

Le cours exige au moins un échange gRPC-Web fonctionnel, démontrable depuis le navigateur, avec un cas de succès et un cas d'erreur. Le socle expose déjà trois opérations en REST (créer une partie, lire son état, jouer un tir). Il faut choisir laquelle (ou lesquelles) exposer aussi en gRPC-Web.

## Options envisagées

1. **`GetGameState(gameId) → GameStateReply`** : lecture seule, unaire, mirroir du `GET /api/games/{id}` REST.
2. **`PlayShot`** : exposer le tir (l'opération qui mute l'état) en gRPC-Web, en plus ou à la place du REST.

## Décision

Option 1. Raisons :
- Elle calque presque exactement l'exemple travaillé du cours (`ProductQuery{id} → ProductReply`), ce qui réduit le risque d'implémentation pour un premier échange gRPC-Web.
- Elle donne un cas d'erreur trivial et reproductible sans avoir à mettre en scène un coup invalide : un `gameId` syntaxiquement valide mais inconnu → `NOT_FOUND` ; un `gameId` malformé → `INVALID_ARGUMENT` via FluentValidation.
- Elle ne touche pas la boucle de jeu mutante (`PlayHumanShot`) : une régression éventuelle dans le câblage gRPC ne peut pas casser le parcours de jeu REST déjà validé au point de contrôle A.
- Elle est réellement utilisée par `Pages/Game.razor` pour l'hydratation de l'état au chargement — pas une démonstration isolée sans usage réel.

Le `GET` REST équivalent est conservé en parallèle plutôt que remplacé, pour garder la parité avec `BattleShip.API.http` et les essais manuels. Léger doublon assumé : le coût de maintenir deux chemins de lecture identiques est faible face au gain de sûreté pour ce premier passage gRPC-Web.

## Conséquences

- `PlayShot` reste REST uniquement pour ce socle ; l'exposer aussi en gRPC-Web est une extension backlog possible, pas un manque du socle.
- `BoardViewMapper` (BattleShip.Models/Contracts) reste l'unique chokepoint anti-fuite ; `GameStateMapper` (BattleShip.API/Grpc) traduit les DTO REST déjà produits par `BoardViewMapper` vers les messages proto, sans relire `Domain.Board.Ships` — un seul endroit du code décide ce qui est sûr à révéler, pour les deux transports.

## Vérification et réexamen

Tests d'intégration gRPC in-process (`Api/GrpcGameStateTests.cs`) : partie existante → forme attendue, id inconnu → `NotFound`, id malformé → `InvalidArgument`. Démonstration navigateur : succès via l'hydratation normale de `/game/{id}`, erreur via `/verifier-partie`. À réexaminer si le backlog priorise un flux gRPC-Web mutant.

## Références

`Protos/battleship.proto`, `BattleShip.API/Grpc/BattleshipGrpcService.cs`, `BattleShip.API/Grpc/GameStateMapper.cs`, Referentiel.md diapos 47-52.
