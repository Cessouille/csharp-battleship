# ADR 0010 : radar à usage limité, exposé en gRPC-Web

## Statut et date

Accepté — 15/09/2026. Complète `docs/adr/0005-transport-grpc-web.md`.

## Contexte

TICKET-02 : le joueur peut scanner deux fois par partie une zone 2×2 du plateau adverse ; le scan indique
seulement la présence d'un navire et consomme le tour (l'ordinateur riposte). Règles actées dans `docs/ticket.md`.
Trois questions de conception : le transport, où stocker les scans, et comment représenter un tour sans tir.

## Options envisagées

- Transport : (a) endpoint REST `POST /scans` ; (b) méthode gRPC-Web `ScanZone`.
- Stockage : (a) liste de scans dans `Game` ; (b) scans reçus par `Board`, comme les tirs.
- Résultat de tour : (a) `TurnResult.PlayerShot` rendu nullable ; (b) listes de résolutions + `PlayerScan` optionnel.

## Décision

- **gRPC-Web**. L'ADR 0005 réservait gRPC à une lecture et prévoyait de réexaminer un flux mutant. `ScanZone` est
  un bon premier flux mutant : opération nouvelle, sans risque de régression sur le tir REST déjà validé, et une
  erreur attendue simple à démontrer (3e scan → `FailedPrecondition`). Les refus métier sont traduits en
  `FailedPrecondition` avec le nom du motif en détail (même valeur que le `code` des 409 REST), la validation
  FluentValidation (`ScanZoneRequestValidator`) en `InvalidArgument`.
- **Scans stockés par `Board.ReceiveScan`**. Le quota se déduit de `ComputerBoard.ScansReceived.Count`, sans
  compteur séparé qui pourrait diverger. Les scans passent par `BoardViewMapper` (`OpponentBoardDto.Scans`) et
  survivent donc au rechargement de la page (hydratation `GetGameState`).
- **`TurnResult` en listes** (`PlayerShots`, `PlayerScan`, `ComputerShots`), refonte faite une seule fois et
  réutilisée par le Salvo et les armes.
- Radar **réservé au joueur** : l'IA probabiliste (ADR 0008) exploite déjà toute l'information visible ; lui
  donner un radar ajouterait une décision « scanner ou tirer » sans bénéfice démontrable.

## Conséquences

- Le contrat REST `TurnResultDto` change (`PlayerShots`/`ComputerShots` en listes).
- L'App utilise désormais deux transports pour agir : REST pour le tir, gRPC-Web pour le scan.
- Un scan ne révèle qu'un booléen ; la zone reste affichée pour toute la partie.

## Vérification et réexamen

`Engine/RadarTests.cs`, `Contracts/BoardViewMapperTests.ToOpponentBoardDto_ScanOverShip_RevealsOnlyPresence_NoShipCell`,
`Api/GrpcScanZoneTests.cs`. Le contrôle du quota a été neutralisé : les deux tests de quota échouent, puis repassent.

## Références

`BattleShip.Models/Domain/Radar.cs`, `Board.ReceiveScan`, `Game.PlayHumanScan`, `BattleShip.API/Grpc/BattleshipGrpcService.cs`.
