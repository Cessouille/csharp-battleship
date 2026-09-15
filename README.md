# Bataille Navale

Projet d'école (binôme) — ASP.NET Core Minimal API + Blazor WebAssembly + gRPC-Web.

## Membres du binôme

- Célian CHAUSSON
- Diego CARRIERE

## Prérequis

- SDK .NET 10 stable (piloté par `global.json` à la racine, vérifié avec le SDK `10.0.401`).
- Certificat HTTPS local approuvé, une seule fois :

```
dotnet dev-certs https --trust
```

## Lancer le projet

Depuis la racine du dépôt, dans deux terminaux distincts (API puis App) :

```
dotnet build BattleShip.slnx
dotnet run --project BattleShip.API --launch-profile https   # https://localhost:7186
dotnet run --project BattleShip.App --launch-profile https   # https://localhost:7206
```

Ouvrir `https://localhost:7206` dans le navigateur. L'API doit être lancée en premier (l'App en dépend pour REST et gRPC-Web).

## Lancer les tests

```
dotnet test BattleShip.Tests
```

## Fonctionnalités livrées (socle)

- Moteur de jeu (`BattleShip.Models/Domain`) indépendant de HTTP/JSON/gRPC : grille 10×10, flotte classique à 5
  navires (porte-avions 5, croiseur 4, contre-torpilleur 3, sous-marin 3, torpilleur 2) placée aléatoirement sans
  chevauchement ni débordement, résolution des tirs, détection de fin de partie.
- API Minimal API (`POST /api/games`, `GET /api/games/{id}`, `POST /api/games/{id}/shots`) : règles vérifiées côté
  serveur uniquement, coup invalide refusé sans mutation d'état, aucun coup accepté après la fin de partie, DTO
  explicites qui ne révèlent jamais les positions adverses non découvertes (`OpponentBoardDto`).
- FluentValidation sur les entrées HTTP et gRPC.
- Interface Blazor WebAssembly : accueil, page de jeu avec les deux grilles, tirs, fin de partie et nouvelle partie.
- Un échange gRPC-Web fonctionnel (`GetGameState`), utilisé par l'hydratation de la page de jeu et démontrable
  isolément (succès + erreur `NotFound`/`InvalidArgument`) sur la page **Vérifier une partie** (`/verifier-partie`).
- Adversaire ordinateur à tir aléatoire, soumis aux mêmes règles de validité que le joueur.
- Tests xUnit : moteur de jeu, mapping anti-fuite, intégration API (REST) et intégration gRPC.

## Arbitrages du backlog

Périmètre retenu pour ce socle : une partie complète jouable de bout en bout contre l'ordinateur, avec un
adversaire à tir aléatoire (pas de stratégie chasse/cible) et un stockage de partie en mémoire (pas de
persistance, pas de compte joueur). Ces choix sont documentés et justifiés dans `docs/adr/0004-strategie-adversaire.md`
et `docs/adr/0006-stockage-etat-partie.md`.

Pistes du backlog explicitement écartées pour cette itération, avec la raison : multijoueur (nécessiterait une
machine à état "à qui le tour", voir `docs/adr/0001-modele.md`), adversaire plus élaboré (chasse/cible après un
tir touché), sauvegarde/persistance, historique et statistiques, personnalisation du placement de la flotte par
le joueur. Aucune de ces pistes n'était nécessaire pour démontrer le parcours complet exigé par le socle.

## Limites connues

- L'état des parties est perdu au redémarrage de l'API (stockage en mémoire, voir ADR 0006).
- Aucune authentification : quiconque connaît l'identifiant (GUID) d'une partie peut la consulter/y jouer.
- L'adversaire ordinateur tire au hasard, sans mémoriser ses tirs touchés pour cibler ensuite.
- Le placement de la flotte est toujours aléatoire ; le joueur ne choisit pas la disposition de ses navires.

## Documentation complémentaire

- `docs/adr/` : décisions d'architecture (modèle de données, placement, résolution des tirs, stratégie de
  l'adversaire, transport gRPC-Web, stockage).
- `docs/ticket.md` : pistes de backlog proposées au-delà du socle (non tranchées), en complément de la section
  « Arbitrages du backlog » ci-dessus.
- `PROMPTS.md` : échanges décisifs avec l'IA.
- `REVUE-IA.md` : revues critiques de propositions IA.
