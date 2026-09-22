# ADR 0006 : stockage de l'état des parties

## Statut et date

Accepté — 15/09/2026.

## Contexte

Le serveur doit conserver l'état d'une partie entre la création, les tirs successifs et sa consultation (REST et gRPC-Web), pour un socle mono-partie-à-la-fois par visite, sans notion de compte joueur.

## Options envisagées

1. **`ConcurrentDictionary<Guid, Game>` en mémoire**, singleton du conteneur DI.
2. **Persistance externe** (fichier, base de données) pour survivre à un redémarrage de l'API et/ou permettre plusieurs instances de serveur.

## Décision

Option 1. Le cours n'exige pas de survie au redémarrage ni de compte joueur pour ce socle ; une structure en mémoire est la plus simple à expliquer et à tester, et suffit à démontrer le parcours complet (création → tirs → fin → nouvelle partie) dans une session de développement/démo.

## Conséquences

- **Limite assumée** : l'état de toutes les parties est perdu si l'API redémarre. À documenter dans le README comme limite connue, pas comme un défaut caché.
- **Limite assumée** : aucune authentification par joueur — quiconque connaît un `gameId` (GUID, donc non devinable en pratique) peut consulter/jouer cette partie. Acceptable pour une démo locale à deux navigateurs au plus.
- `ConcurrentDictionary` évite d'avoir à gérer explicitement un verrou pour les accès concurrents basiques (créations/lectures simultanées) ; les mutations d'une même partie restent séquentielles côté client dans ce socle (un seul joueur humain par partie, un seul tir à la fois).

## Vérification et réexamen

Couvert indirectement par les tests d'intégration API (`GameEndpointsTests`) qui créent, lisent et jouent plusieurs parties dans le même processus de test. À réexaminer si le backlog retient la persistance ou le multijoueur (une partie survivant à un redémarrage, ou partagée entre deux navigateurs distincts) — cette ADR serait alors remplacée.

## Références

`BattleShip.API/Storage/InMemoryGameStore.cs`.
