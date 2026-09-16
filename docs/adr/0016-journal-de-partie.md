# ADR 0016 : journal de partie stocké côté serveur

## Statut et date

Accepté — 2026-09-16.

## Contexte

TICKET-10 (`docs/ticket.md`) : afficher pendant la partie un journal chronologique des coups joués. Distinct
de « l'historique et les statistiques » inter-parties explicitement écarté dans le README (persistance de
partie en partie) : ce journal ne couvre qu'une seule partie, comme le reste de son état (ADR 0006).

Question de conception laissée ouverte par le ticket : le journal doit-il survivre à un rechargement de
page en cours de partie ? Deux options avaient des conséquences très différentes sur l'ampleur du
changement (voir `docs/ticket.md`, section TICKET-10 avant ce ticket) :
- côté client uniquement (accumulation dans `Game.razor` à partir des réponses déjà reçues) : aucun
  changement de contrat, pas d'ADR, mais le journal disparaît aussi au rechargement ;
- côté serveur (`Game` conserve l'historique) : survit au rechargement comme le reste de l'état, mais
  change la représentation d'état — ADR obligatoire.

## Décision

**Côté serveur**, tranché par l'utilisateur : le journal doit survivre à un rechargement, comme les scans
(ADR 0010) ou les cases jouées.

- `Game` accumule un `List<TurnResult>` privé (`_history`), exposé en lecture par `Game.History`. Un seul
  chokepoint d'écriture : `CompleteTurn`, déjà le point de sortie commun à `PlayHumanShot`, `PlayHumanSalvo`,
  `PlayHumanScan` et `PlayHumanWeapon` (et à leurs pendants côté ordinateur). Un coup refusé n'atteint jamais
  `CompleteTurn` : il n'apparaît donc jamais dans le journal, structurellement.
- Contrat REST : `JournalEntryDto` (mêmes champs qu'une entrée de `TurnResult`, sans l'état de plateau qui
  l'accompagne) ; `GameStateDto.History` et `TurnResultDto.History` portent chacun la liste **complète** à
  ce point de la partie (pas seulement la nouvelle entrée) — l'App n'a donc jamais besoin d'un second appel
  pour rester à jour, y compris après une salve ou un scan gRPC-Web dont la réponse imbrique déjà
  `GameStateReply` (ADR 0010/0014).
- Contrat gRPC : `GameStateReply.history` (champ 8, ajouté, jamais renuméroté) + nouveau message
  `JournalEntryMessage`. `ScanZoneReply`/`PlaySalvoReply` en héritent gratuitement : les deux réutilisaient
  déjà `GameStateMapper.ToGameStateReply(game)` pour leur champ `state`.
- App : `Game.razor` remplace l'ancien résumé à une ligne (`_turnSummary`/`DescribeComputerTurn`, qui ne
  montrait que la dernière riposte) par un panneau listant `_state.History` en entier — le serveur fait
  autorité sur le contenu, l'App se contente d'afficher `DescribeEntry` sur chaque entrée.
- Salve et tir classique restent mutuellement exclusifs par partie (`GameOptions.ShotMode` figé à la
  création, ADR 0009) : un même journal ne peut donc jamais mélanger « tir » et « salve », contrairement à
  la formulation initiale du ticket qui envisageait un test les mêlant tous les deux. Testé séparément.

## Conséquences

- `GameStateDto` et `TurnResultDto` gagnent un champ `History` chacun ; `GameStateReply` gagne un champ
  proto et un nouveau message.
- Le journal grossit avec la partie (jamais purgé), comme le reste de l'état d'une partie en mémoire — même
  limite déjà assumée par l'ADR 0006 (pas de plafond, une partie ne dure jamais assez de tours pour que ça
  pèse).
- Aucun nouvel endpoint : le journal voyage dans les réponses déjà existantes (`GetGameState`, chaque
  réponse de coup).

## Vérification et réexamen

`Engine/GameHistoryTests.cs` (ordre et contenu sur une séquence tir/scan/arme, une entrée par salve entière,
aucune entrée pour un coup refusé) ; round-trip REST (`Api/GameEndpointsTests.cs`) et gRPC
(`Api/GrpcGameStateTests.cs`). Vérifié dans le navigateur : partie avec Salvo + armes + radar, trois tours
d'actions différentes journalisés dans l'ordre avec le bon décompte de touches, **rechargement complet de
la page** → journal identique (hydraté par `GetGameState`).

## Références

`BattleShip.Models/Domain/Game.cs`, `BattleShip.Models/Contracts/Dtos.cs`,
`BattleShip.Models/Contracts/BoardViewMapper.cs`, `Protos/battleship.proto`,
`BattleShip.API/Grpc/GameStateMapper.cs`, `BattleShip.App/Pages/Game.razor`.
