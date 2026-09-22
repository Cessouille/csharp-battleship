# ADR 0018 : profil joueur anonyme

## Statut et date

Accepté — 17/09/2026. Rédigé a posteriori pour réconcilier le dépôt avec le code déjà livré par le commit
`4864370` (« feat: ajout des succès ») : le code référence cet ADR depuis sa livraison, mais le fichier
n'existait pas encore — écart relevé par l'audit `docs/audits/documentation.md` du 17/09/2026. Complète l'ADR
0017 (système de succès) pour le seul succès inter-parties du catalogue : S-03 Reine du difficile.

## Contexte

TICKET-14 (`docs/ticket.md`) : succès cumulés sur plusieurs parties. `InMemoryGameStore` ne connaissait que des
parties (`ConcurrentDictionary<Guid, Game>`), aucune notion de joueur ; `README.md` écartait explicitement
« historique et statistiques » et tout compte joueur pour le socle (`docs/adr/0006-stockage-etat-partie.md`).
Ce ticket **revient sur cet arbitrage**, assumé et documenté comme tel plutôt qu'appliqué silencieusement.

## Options envisagées

- **A — identifiant joueur anonyme** : le serveur génère un GUID, l'App le conserve dans le `localStorage` du
  navigateur et l'envoie à la création de partie ; le profil (compteurs, succès) est **dérivé** des parties
  connues pour ce joueur, jamais stocké séparément — cohérent avec l'ADR 0006 (état de partie en mémoire,
  perdu au redémarrage, aucune nouvelle forme de persistance).
- **B — compteur côté client uniquement** (`localStorage`) : écartée — le client deviendrait source de vérité
  et un succès inter-parties serait falsifiable depuis la console du navigateur, contraire à la règle non
  négociable « les règles sont vérifiées côté serveur uniquement » (`CLAUDE.md`).
- **C — persistance réelle** (base de données) : écartée — hors budget du projet, contredit frontalement l'ADR
  0006.

## Décision

**Option A retenue.**

- **Aucun état de profil stocké.** `PlayerProfile.Project(IEnumerable<GameOutcome>)` est une projection pure,
  recalculée à chaque lecture à partir des parties connues pour un joueur — pas de compteur à synchroniser à
  chaque coup, donc aucun risque de double-comptage à gérer (contrairement aux quotas de munitions/scans,
  ADR 0007/0010/0012).
- `GameOutcome` : instantané anonymisé d'une partie (`GameId`, `Status`, `Winner`, `Difficulty`, `Achievements`),
  construit **sous le verrou** `Game.Locked` — jamais un `Game` vivant passé hors de son verrou, même règle que
  tout autre mapping DTO/proto du dépôt.
- `PlayerProfile` : `HardVictories` = nombre de parties `Finished` gagnées par le joueur en difficulté `Hard` ;
  `Achievements` = union sans doublon des succès de toutes ses parties, plus `ReineDuDifficile` ajouté dès que
  `HardVictories >= 3`. Une partie non terminée ou perdue ne compte jamais comme victoire ; le seuil est sur le
  total cumulé, pas sur des victoires consécutives.
- **Identifiant joueur optionnel, sans notion d'« inconnu ».** `CreateGameRequestDto.PlayerId` (string GUID,
  nullable) est validé en forme seulement par FluentValidation (`Guid.TryParse`) — pas d'existence préalable
  requise : dans un profil dérivé, un identifiant qui n'a jamais servi n'est pas une erreur, seulement un profil
  vide tant qu'aucune partie n'a été jouée avec lui.
- **Index, pas de store de profil** : `InMemoryGameStore` garde un `ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, byte>>`
  (playerId → ensemble de gameIds), thread-safe pour des créations concurrentes du même joueur ;
  `FindByPlayer` retombe sur une liste vide si le joueur est inconnu, jamais une exception.
- **Contrat REST seul, pas de RPC.** `POST /api/players` génère un GUID sans dépendre du store (aucun
  identifiant n'existe avant d'avoir servi à créer une partie) ; `GET /api/players/{playerId:guid}` recalcule
  le profil à la volée. La contrainte de route `:guid` fait office de validation de forme pour la lecture (un
  GUID malformé dans l'URL renvoie 404 par le routage ASP.NET Core avant d'atteindre le handler) ; il n'y a pas
  de second flux gRPC-Web pour cette fonctionnalité — celui déjà livré par l'ADR 0014 (Salve) suffit à
  l'exigence du cours d'au moins un échange gRPC-Web mutant, et un profil en lecture seule n'apportait pas de
  cas d'usage supplémentaire pour gRPC-Web.
- **App** : `PlayerSession` (`BattleShip.App/Game/PlayerSession.cs`) conserve l'identifiant dans le
  `localStorage`, créé à la volée au premier besoin. Un GUID plus ancien qu'un redémarrage de l'API reste
  valide : il ne pointe jamais vers un « joueur inconnu », seulement vers un profil vide tant qu'aucune partie
  n'a encore été rejouée avec lui.

## Conséquences

- Revient explicitement sur l'arbitrage du socle « pas de compte joueur » (`README.md`, `docs/adr/0006-stockage-etat-partie.md`) :
  assumé, documenté ici et dans les limites connues du README plutôt qu'implicite dans le code.
- Nouvelle limite : aucune authentification — quiconque connaît un identifiant joueur peut lire son profil,
  même limite déjà assumée pour les identifiants de partie.
- Le profil est perdu au redémarrage de l'API comme le reste de l'état (ADR 0006) ; un joueur qui vide son
  `localStorage` perd l'accès à son historique de succès, sans mécanisme de récupération.
- Aucun nouveau champ proto : le profil ne voyage jamais dans `GameStateReply`, uniquement en REST.

## Vérification et réexamen

`Engine/Achievements/PlayerProfileTests.cs` (`ThreeHardVictories_UnlockReineDuDifficile`,
`TwoHardVictories_DoNotUnlock`, `EasyAndMediumVictories_DoNotCountTowardsHardVictories`,
`LostHardGame_DoesNotCount`, `UnfinishedHardGame_DoesNotCount`, `NonConsecutiveVictories_StillCount`,
`GameAchievements_AreUnioned_WithoutDuplicates`, `SameGameListedTwice_CountsItsVictoryOnce`,
`NoGames_ReturnsEmptyProfile`) ; `Api/PlayerEndpointsTests.cs` (`PostPlayers_Returns201WithEmptyProfile`,
`PostPlayers_ReturnsADifferentIdEachTime`, `GetPlayer_WithNoGames_ReturnsEmptyProfile`,
`GetPlayer_MalformedGuidInRoute_Returns404`, `CreateGame_WithMalformedPlayerId_Returns400`,
`CreateGame_WithUnknownPlayerId_StillSucceeds`, `CreateGame_WithTwoRowFleetAndPlayerId_ShowsRangeeParfaiteInTheProfile`,
`TwoPlayers_EachSeeOnlyTheirOwnGames`). Suite complète : 265/265 tests verts (audit `docs/audits/bugs-lint.md`,
17/09/2026).

## Références

`BattleShip.Models/Achievements/PlayerProfile.cs`, `BattleShip.API/Endpoints/PlayerEndpoints.cs`,
`BattleShip.API/Storage/InMemoryGameStore.cs`, `BattleShip.API/Validation/CreateGameRequestDtoValidator.cs`,
`BattleShip.App/Game/PlayerSession.cs`, `BattleShip.App/Pages/Achievements.razor`,
`docs/adr/0017-systeme-de-succes.md`, `docs/adr/0006-stockage-etat-partie.md`.
