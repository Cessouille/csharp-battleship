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

## Arbitrages du backlog

Périmètre retenu pour ce socle : une partie complète jouable de bout en bout contre l'ordinateur, avec un
stockage de partie en mémoire (pas de persistance, pas de compte joueur, voir `docs/adr/0006-stockage-etat-partie.md`).
Le socle a d'abord été livré avec un adversaire à tir aléatoire (`docs/adr/0004-strategie-adversaire.md`), remplacé
ensuite par la grille de probabilité (`docs/adr/0008-ia-grille-probabilite.md`).

Pistes du backlog explicitement écartées pour le socle (première itération), avec la raison : multijoueur
(nécessiterait une machine à état "à qui le tour", voir `docs/adr/0001-modele.md`), sauvegarde/persistance,
personnalisation du placement de la flotte par le joueur. Aucune de ces pistes n'était nécessaire pour démontrer
le parcours complet exigé par le socle. Deux d'entre elles ont depuis été revisitées comme extensions : le
placement manuel (ADR 0013) et, sous une forme limitée assumée par l'ADR 0006 (état de partie toujours en
mémoire, pas de vraie persistance), un historique inter-parties via le profil joueur (ADR 0018).

Extensions retenues puis livrées au-delà du socle, dans cet ordre : adversaire par grille de probabilité,
options de partie à la création, radar en gRPC-Web, mode Salvo symétrique, armes spéciales, puis (deuxième et
troisième vagues) placement manuel de la flotte, IA à difficulté réglable, Salve en second flux gRPC-Web,
journal de partie affiché en direct, système de succès et profil joueur anonyme. L'ordre va du moins invasif
(aucun changement de contrat) au plus invasif (refonte de la résolution d'un tour, ou nouvelle représentation
d'état pour les succès/le profil). Règles tranchées par le binôme et détail par ticket dans `docs/ticket.md`.
Écarté : navires en formes libres (tétrominos), qui aurait imposé de refaire toute la validation du placement.
Grille et flotte configurables (TICKET-09) et tests de composants Blazor bUnit (TICKET-11) restent au statut
`proposé` dans `docs/ticket.md`, non tranchés par le binôme faute de temps.

## Limites connues

- L'état des parties (et le profil joueur, qui en est dérivé) est perdu au redémarrage de l'API (stockage en
  mémoire, voir ADR 0006).
- Aucune authentification : quiconque connaît l'identifiant (GUID) d'une partie peut la consulter/y jouer, et
  quiconque connaît un identifiant joueur peut lire son profil (`GET /api/players/{id}`, même limite assumée
  dans l'ADR 0018).
- Avec les armes spéciales, l'ordinateur lance sa torpille dès son premier tour (heuristique simple, ADR 0012).
- La règle de la frappe aérienne sur des cases déjà jouées (ignorées) a été déduite de celle de la torpille et
  reste à confirmer par le binôme.
- Le radar est réservé au joueur (asymétrie assumée, ADR 0010).
- Le catalogue de succès est verrouillé (11 identifiants figés, jamais renommés) ; plusieurs points de règle
  fins de chaque succès (motif exact du cœur/nœud papillon, interruption d'une série par un scan, etc.) sont
  des arbitrages assumés documentés directement dans le code des règles et dans l'ADR 0017, pas des règles
  universellement établies de bataille navale.
- Le profil joueur (ADR 0018) est une projection recalculée à chaque lecture, sans aucune protection contre un
  navigateur qui viderait son `localStorage` : un joueur qui perd son identifiant perd l'accès à son historique
  de succès, sans possibilité de le retrouver.

## Documentation complémentaire

- `docs/adr/` : décisions d'architecture (modèle de données, placement, résolution des tirs, stratégie de
  l'adversaire, transport gRPC-Web, stockage, concurrence, options de partie, radar, Salvo, armes).
- `docs/ticket.md` : tickets d'extension au-delà du socle (règles actées, statut, ADR associée), en complément de
  la section « Arbitrages du backlog » ci-dessus.
- `PROMPTS.md` : échanges décisifs avec l'IA.
- `REVUE-IA.md` : revues critiques de propositions IA.
