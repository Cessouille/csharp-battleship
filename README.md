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
dotnet run --project BattleShip.API --launch-profile https   # https://localhost:8080
dotnet run --project BattleShip.App --launch-profile https   # https://localhost:3000
```

Ouvrir `https://localhost:3000` dans le navigateur. L'API doit être lancée en premier (l'App en dépend pour REST et gRPC-Web).

### Hot-reload en développement

Pour recompiler et relancer automatiquement à chaque modification, remplacer `dotnet run` par `dotnet watch` (mêmes projets, mêmes ports) :

```
dotnet watch --project BattleShip.API --launch-profile https   # https://localhost:8080
dotnet watch --project BattleShip.App --launch-profile https   # https://localhost:3000
```

- API : le serveur redémarre automatiquement à chaque modification d'un fichier `.cs`.
- App (Blazor WebAssembly) : les modifications sont appliquées dans le navigateur par Hot Reload quand c'est possible, sinon la page est rechargée automatiquement (comportement normal de `dotnet watch`, pas une erreur).
- Sur Linux, si `dotnet watch` échoue avec une erreur `inotify` (« configured user limit (128) on the number of inotify instances has been reached »), fermer les autres `dotnet watch`/IDE qui surveillent déjà des fichiers, ou augmenter la limite : `sudo sysctl fs.inotify.max_user_instances=512` (à ajouter dans `/etc/sysctl.conf` pour la rendre permanente).

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
  explicites qui ne révèlent jamais les positions adverses non découvertes (`OpponentBoardDto`). La création
  attend un corps JSON : `{}` pour une partie classique (voir `BattleShip.API/BattleShip.API.http`).
- FluentValidation sur les entrées HTTP et gRPC.
- Interface Blazor WebAssembly : accueil, page de jeu avec les deux grilles, tirs, fin de partie et nouvelle partie.
- Un échange gRPC-Web fonctionnel (`GetGameState`), utilisé par l'hydratation de la page de jeu et démontrable
  isolément (succès + erreur `NotFound`/`InvalidArgument`) sur la page **Vérifier une partie** (`/verifier-partie`).
- Adversaire ordinateur par grille de probabilité (`BattleShip.Models/Ai`), qui ne voit de votre plateau que ce
  que vous verriez du sien et reste soumis aux mêmes règles de validité que le joueur (ADR 0008).
- Tests xUnit : moteur de jeu, mapping anti-fuite, intégration API (REST) et intégration gRPC.

## Extensions livrées (options choisies à la création de la partie)

Toutes les options se cochent sur l'accueil et se combinent ; la partie classique reste le défaut.

- **Radar** (ADR 0010) : 2 scans par partie d'une zone 2×2, qui indiquent seulement la présence d'un navire et
  remplacent le tir du tour. Le scan passe par **gRPC-Web** (`rpc ScanZone`). Erreur attendue démontrable depuis
  le navigateur : cocher « Radar », faire deux scans, puis tenter un troisième → message
  `Plus aucun scan radar disponible (gRPC FailedPrecondition)`.
- **Salvo** (ADR 0011) : chaque tour, exactement un tir par navire encore à flot, pour le joueur comme pour
  l'ordinateur. Sélectionner les cases puis « Tirer la salve », transmise en **gRPC-Web** (`rpc PlaySalvo`, ADR
  0014) ; l'équivalent REST (`POST /api/games/{id}/salvos`) reste disponible pour d'autres clients. Une salve de
  mauvaise taille est refusée en entier, sans qu'aucun de ses tirs ne soit appliqué.
- **Armes spéciales** (ADR 0012) : une torpille (part d'un bord et s'arrête sur le premier navire touché) et une
  frappe aérienne de 3 cases alignées par camp, l'ordinateur compris (`POST /api/games/{id}/torpedoes`,
  `POST /api/games/{id}/airstrikes`). Chaque arme remplace tout le tour.
- **Placement manuel de la flotte** (ADR 0013) : à la création, le joueur choisit entre tirage aléatoire (défaut)
  et pose manuelle case par case (clic + orientation) ; l'ordinateur reste toujours placé au hasard.
- **IA à difficulté réglable** (ADR 0015) : trois paliers choisis à la création — Facile (tir aléatoire), Moyen
  (chasse/cible sans grille de densité), Difficile (grille de probabilité, défaut historique inchangé).
- **Mini-jeu de précision** (ADR 0019) : un tir sur une case occupée (à l'attaque) ou une riposte adverse qui
  couperait un navire (à la défense) doit être validé par un mini-jeu de timing en SVG — une barre horizontale
  avec une zone rose centrale et un curseur en va-et-vient, à arrêter au clic ou à la touche Espace. Le serveur
  reste seul juge du résultat (horodatage serveur, jamais un résultat déclaré par le client).
- Les options sont décrites par `docs/adr/0009-options-de-partie.md`.

## Autres fonctionnalités livrées au-delà du socle

- **Journal de partie** (ADR 0016) : panneau listant, tour par tour, chaque coup joué et la riposte de
  l'ordinateur ; stocké côté serveur, survit à un rechargement de page.
- **Système de succès** (ADR 0017) : 11 succès détectés côté serveur à partir de ce que le joueur voit déjà de
  sa propre partie (ses tirs, sa flotte, les options) — jamais des positions adverses non découvertes. Affichés
  en notification au déblocage et sur la page **Mes succès** (`/mes-succes`).
- **Profil joueur anonyme** (ADR 0018) : identifiant généré côté serveur et conservé dans le `localStorage` du
  navigateur (`POST /api/players`), utilisé pour dériver un profil (victoires en Difficile, succès cumulés) sans
  aucun état de profil stocké séparément (`GET /api/players/{id}`, recalculé à chaque appel à partir des parties
  connues pour ce joueur).

## Documentation complémentaire

- `docs/adr/` : décisions d'architecture (modèle de données, placement, résolution des tirs, stratégie de
  l'adversaire, transport gRPC-Web, stockage, concurrence, options de partie, radar, Salvo, armes).
- `PROMPTS.md` : échanges décisifs avec l'IA.
- `REVUE-IA.md` : revues critiques de propositions IA.
