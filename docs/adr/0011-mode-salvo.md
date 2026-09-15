# ADR 0011 : mode Salvo symétrique

## Statut et date

Accepté — 2026-09-15.

## Contexte

TICKET-01 : en mode Salvo, chaque camp tire autant de coups que de navires encore à flot. Règles actées par le
binôme dans `docs/ticket.md` : salve d'exactement N tirs, N borné par les cases non jouées, arrêt immédiat quand
le dernier navire adverse coule, et même règle pour l'ordinateur. Il fallait décider comment valider un lot,
comment l'IA compose sa salve et comment exposer le mode sans casser le tir classique.

## Options envisagées

1. Validation : (a) tir par tir, en s'arrêtant au premier invalide ; (b) lot entier validé avant le moindre tir.
2. Salve de l'IA : (a) N appels successifs en voyant le résultat de chaque tir ; (b) N cibles distinctes choisies
   sur la même vue.
3. API : (a) `/shots` accepte une liste ; (b) endpoint dédié `POST /api/games/{id}/salvos`.

## Décision

- **1b** : `Game.ValidateVolley` vérifie taille, grille, doublons et cases jouées sans rien modifier, puis
  `ResolveVolley` applique les tirs. Un coup refusé ne doit jamais modifier l'état (règle non négociable) ;
  l'option 1a appliquerait les premiers tirs d'un lot refusé. Le tir classique passe par la même fonction avec
  un lot d'un tir.
- **2b** : `IComputerTargeting.PickTargets(view, count, rng)`. Le joueur compose sa salve sans connaître le
  résultat de ses tirs ; donner à l'IA le résultat de chaque tir avant le suivant lui offrirait un avantage.
  La salve de l'IA repasse par `ValidateVolley` : une stratégie boguée lève une exception.
- **3b** : `/shots` reste le chemin classique ; chaque endpoint refuse l'autre mode (409 `WrongShotMode`). La
  forme du lot (non vide, au plus 5 tirs, dans la grille, sans doublon) est validée par FluentValidation (400) ;
  la taille exacte dépend de l'état de la partie et est refusée par le domaine (409 `WrongSalvoSize`).
- La taille de salve est calculée au début du tour du tireur : la riposte tient compte des navires que la salve
  du joueur vient de couler. Elle est exposée dans `PlayerActionsDto.SalvoSize`.
- `CreateGameRequestDtoValidator` n'accepte que les noms exacts de `ShotMode` (`Enum.TryParse` accepterait `"1"`).

## Conséquences

- La résolution d'un tour reste synchrone : l'ADR 0001 n'est pas remise en cause.
- Un scan ou une arme remplace toute la salve du joueur ; la riposte est alors une salve complète.

## Vérification et réexamen

`Engine/SalvoTests.cs` et `Api/SalvoEndpointsTests.cs`. Validation atomique neutralisée (premiers tirs appliqués
avant le refus) : 6 tests échouent, puis repassent une fois le code restauré.

## Références

`BattleShip.Models/Domain/Game.cs` (`PlayHumanSalvo`, `ValidateVolley`, `ResolveVolley`, `SalvoSize`),
`BattleShip.API/Validation/SalvoRequestDtoValidator.cs`.
