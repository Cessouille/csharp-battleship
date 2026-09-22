# ADR 0014 : salve du joueur, aussi exposée en gRPC-Web

## Statut et date

Accepté — 16/09/2026. Complète `docs/adr/0005-transport-grpc-web.md` et `docs/adr/0010-radar-grpc-web.md`.

## Contexte

TICKET-07 (`docs/ticket.md`) : un seul flux gRPC-Web mutant existait (`ScanZone`, ADR 0010), en plus de la
lecture `GetGameState`. L'objectif est d'élargir la démonstration gRPC-Web attendue par le cours au-delà
d'un unique échange, et de vérifier que le pattern retenu par l'ADR 0010 (verrou + erreurs typées) se
généralise à un second flux sans réécriture. Deux questions de conception : quel flux migrer, et si le
chemin REST existant doit être conservé ou retiré.

## Options envisagées

- Flux à migrer : (a) la Salve (`POST /api/games/{id}/salvos`, TICKET-01/ADR 0011) ; (b) une Arme
  (torpille/frappe, TICKET-04/ADR 0012).
- Devenir du chemin REST existant : (a) conserver les deux chemins ; (b) retirer le chemin REST au profit
  de gRPC-Web seul, comme `ScanZone` qui n'a jamais eu d'équivalent REST.

## Décision

- **Salve**, pas une arme. Une salve est un lot de coordonnées résolu en un seul appel — exactement la
  forme de `ScanZoneRequest`/`ScanZoneReply` (`repeated CoordinateMessage`), ce qui réutilise directement
  le pattern de validation et de mapping déjà en place plutôt que de modéliser deux nouveaux types de
  message (torpille et frappe ont des formes différentes entre elles).
- **Les deux chemins coexistent** : `POST /api/games/{id}/salvos` reste tel quel (ADR 0011 inchangée,
  `SalvoEndpointsTests.cs` non touché) ; `rpc PlaySalvo` s'ajoute en plus. Contrairement à `ScanZone`
  (jamais exposé qu'en gRPC), c'est le premier cas de ce projet où une même action de jeu est accessible
  par deux transports. Raison : la Salve est une fonctionnalité déjà livrée et testée (ADR 0011) ;
  retirer son chemin REST aurait été une régression sur un contrat existant sans bénéfice pour la
  démonstration gRPC-Web, seul objectif de ce ticket.
- **L'App n'utilise désormais que gRPC-Web pour tirer une salve** (`Game.razor.FireSalvoGrpcAsync`),
  pour que le second flux soit réellement démontrable depuis le navigateur et pas seulement testé en
  intégration — sinon l'ajout serait invisible pour qui joue une partie. Le chemin REST reste disponible
  pour d'autres clients (ex. `BattleShip.API.http`) et pour les tests d'intégration existants.
- Erreurs : mêmes règles que `ScanZone` (`ToRpcException` déjà générique) — `OutOfGrid` et les rejets de
  validation FluentValidation (doublon, lot vide, hors grille) en `InvalidArgument` ; les autres motifs
  (`WrongShotMode`, `WrongSalvoSize`) en `FailedPrecondition`, avec le même détail texte que le `code` 409
  REST.

## Conséquences

- Le contrat proto gagne `rpc PlaySalvo` + `PlaySalvoRequest`/`PlaySalvoReply` (ajout pur, aucun champ
  renuméroté).
- Deux transports pour une même action (Salve) : à surveiller si une troisième action suivait le même
  chemin, pour ne pas dupliquer indéfiniment la même logique sur deux surfaces.
- `PlaySalvoReply` reprend la forme de `ScanZoneReply` (état imbriqué dans `state`, listes de résultats à
  plat) plutôt que la forme aplatie de `TurnResultDto` REST — cohérence entre les deux flux gRPC-Web, pas
  avec REST.

## Vérification et réexamen

`Api/GrpcSalvoTests.cs` (succès + `FailedPrecondition` taille/mode + `InvalidArgument` doublon/vide/GUID +
`NotFound`) ; `Api/SalvoEndpointsTests.cs` inchangé et toujours vert (non-régression REST) ; chemin de
succès vérifié manuellement dans l'App (partie Salvo, sélection de 5 cases, « Tirer la salve ») — capture
réseau du navigateur : `POST https://localhost:8080/battleship.Battleship/PlaySalvo` → 200, état mis à
jour, riposte de l'ordinateur affichée.

Contrairement à `ScanZone` (ADR 0010), le refus métier (`WrongSalvoSize`) n'est **pas** démontrable en
cliquant dans l'App : `ActionBar.razor` désactive « Tirer la salve » tant que la sélection n'a pas
exactement `State.Actions.SalvoSize` cases (`disabled="@(SelectionCount != State.Actions.SalvoSize || Busy)"`),
donc le client ne peut structurellement pas construire une salve de mauvaise taille — vérifié en essayant
depuis le navigateur (bouton non cliquable à 2/5). C'est une différence assumée avec le radar, pas un
oubli : le refus reste couvert par `GrpcSalvoTests.cs` et sert de défense en profondeur pour tout autre
client que l'App (ex. `BattleShip.API.http`, un futur client mobile).

## Références

`Protos/battleship.proto`, `BattleShip.API/Grpc/BattleshipGrpcService.cs`,
`BattleShip.API/Grpc/GameStateMapper.cs`, `BattleShip.App/Pages/Game.razor`.
