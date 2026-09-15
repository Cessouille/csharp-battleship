# ADR 0009 : options de partie choisies à la création

## Statut et date

Accepté — 2026-09-15.

## Contexte

Le binôme a retenu trois variantes de règles (radar, Salvo, armes spéciales) activables partie par partie, la
partie classique restant le défaut (`docs/ticket.md`, TICKET-00). Jusqu'ici `POST /api/games` n'avait aucun corps
et `Game` ne portait aucune configuration.

## Options envisagées

1. **Options figées à la création**, portées par un record immuable `GameOptions` stocké dans `Game`.
2. **Options modifiables en cours de partie** (endpoint dédié).
3. **Un type de partie par variante** (sous-classes de `Game`).

## Décision

Option 1. Changer de règles en cours de partie n'a pas de sens de jeu et obligerait à valider des transitions
d'état ; des sous-classes multiplieraient les combinaisons (radar + Salvo + armes). `GameOptions` utilise des
propriétés `init` avec valeurs par défaut : chaque ticket ajoute son option **en même temps que la règle qui lui
donne un effet**, sans casser les appels existants.

Le corps de `POST /api/games` est un JSON **obligatoire** (`CreateGameRequestDto`), `{}` créant une partie
classique. L'intention initiale était un corps facultatif ; vérification faite, ASP.NET Core n'achemine pas vers
l'endpoint une requête sans `Content-Type` JSON, même avec un paramètre nullable (404 observé, voir `REVUE-IA.md`).

Les options sont renvoyées dans `CreateGameResponseDto`, `GameStateDto` et `GameStateReply` (`options = 6`), avec
ce que le joueur peut encore faire (`PlayerActionsDto` / `actions = 7`) : le client affiche ces valeurs, il ne
les recalcule pas.

## Conséquences

- Tout client doit envoyer un corps JSON à la création (App, tests, `BattleShip.API.http` mis à jour).
- Un booléen mal typé est refusé en 400 par la liaison JSON ; FluentValidation s'applique dès qu'un champ a des
  valeurs bornées (mode de tir, ADR 0011).

## Vérification et réexamen

`GameEndpointsTests.PostGames_WithEmptyJsonObject_CreatesClassicGame`, `..._WithRadar_ReturnsOptionsAndScanQuota`,
`..._WithMalformedOptions_Returns400`, `GrpcGameStateTests.GetGameState_ReturnsOptionsAndRemainingScans`.

## Références

`BattleShip.Models/Domain/GameOptions.cs`, `BattleShip.API/Endpoints/GameEndpoints.cs`, `Protos/battleship.proto`.
