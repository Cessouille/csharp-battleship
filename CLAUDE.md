# AGENTS.md - Bataille Navale

## Ce que c'est

Projet d'école (binôme) : une bataille navale jouable dans le navigateur.
- API ASP.NET Core (Minimal API) qui porte les règles du jeu et l'état de partie.
- Client Blazor WebAssembly qui affiche les deux grilles et les actions du joueur.
- Bibliothèque de modèles partagés entre API et App (DTOs, types de domaine).
- Tests xUnit : tests métier (moteur de jeu) **et** tests d'intégration (contrats API/gRPC bout en bout) — les deux sont exigés, pas l'un ou l'autre.
- Au moins un échange gRPC-Web fonctionnel depuis le navigateur.

Le socle attendu (contrainte du cours, diapos 5-6) est une **partie complète et jouable de bout en bout contre l'ordinateur** : création de partie → placement → tirs alternés → détection de fin → nouvelle partie. Ce n'est pas une somme de morceaux isolés : tant que ce parcours complet n'est pas démontrable dans le navigateur, le socle n'est pas atteint, même si chaque brique compile et a des tests verts séparément.

État actuel : scaffold par défaut (`dotnet new` : pages Weather/Counter, `Class1.cs`, `UnitTest1.cs`). Rien du jeu n'est implémenté.

### État des lieux vérifié à la racine

| Élément | État |
|---|---|
| Les 4 projets + `BattleShip.slnx` | présents |
| `.gitignore` | **absent** |
| `global.json` | **absent** — prérequis avant toute commande de build/solution (copier `csharp-school/Ressources Bataille Navale/global.json`) |
| `README.md` | présent mais vide en pratique (UTF-16, une ligne de titre) |
| `PROMPTS.md`, `REVUE-IA.md`, `docs/adr/` | **absents** — ce sont trois livrables notés (diapo 11) |
| `Protos/` | **absent** — à créer pour le contrat gRPC-Web |

SDK : `10.0.401` installé sur ce poste, compatible avec le `global.json` du cours (`10.0.100` + `rollForward: latestFeature`). Revérifier `dotnet --version` depuis la racine une fois le fichier copié.

**Problème connu à corriger en priorité** : sans `.gitignore`, le commit `init` (`5c9435e`, **déjà poussé sur `origin/main`**) a embarqué 1327 fichiers de build sur 1401 fichiers suivis. Avant toute nouvelle fonctionnalité, nettoyer par un commit en avant — pas de réécriture d'historique, il est publié :

```
dotnet new gitignore                                    # à la racine
git rm -r --cached .                                    # désindexe tout, sans toucher aux fichiers du disque
git add .                                               # ré-indexe en appliquant le .gitignore
git commit -m "chore: ignore les artefacts de build"
```

Le gabarit `dotnet new gitignore` couvre au passage `.idea/`, actuellement non suivi mais non ignoré non plus. Commit à isoler du reste du travail, et à proposer à l'utilisateur avant de le passer : ce n'est pas anodin sur un historique déjà poussé.

## Structure du dépôt

| Projet | Rôle |
|---|---|
| `BattleShip.Models` | Types partagés (DTOs, état de jeu, enums) — pas de dépendance vers API ou App. |
| `BattleShip.API` | Minimal API : endpoints HTTP, service gRPC-Web, validation FluentValidation, logique de partie. |
| `BattleShip.App` | Blazor WebAssembly : composants de jeu, appels HTTP/gRPC-Web vers l'API. |
| `BattleShip.Tests` | xUnit : moteur de jeu, contrats, règles de validation. |

Références réelles entre projets : `API → Models`, `App → Models`, `Tests → API` (donc `Models` en transitif). Un test métier écrit contre `Models` et un test d'intégration écrit contre l'API compilent tous les deux depuis `BattleShip.Tests` sans ajouter de référence.

Les gabarits vivent en dehors de ce dépôt, dans `../csharp-school/Ressources Bataille Navale/` : `Referentiel.md` (cours complet), `Exemples/` (extraits génériques catalogue — jamais à copier tels quels, seulement le mécanisme), `docs/adr/0001-modele.md`, `PROMPTS.md`, `REVUE-IA.md`, `CONTEXTE-IA.md`, `api.http`.

## Ce qui est imposé, ce qui appartient au binôme (diapo 6)

Distinction structurante pour un agent : la colonne de gauche ne se discute pas, la colonne de droite **ne se décide pas seul**. Proposer, argumenter, faire trancher — ne pas trancher à la place de l'utilisateur au motif que « c'est la règle classique de la bataille navale ». La diapo 5 est explicite : une reproduction fidèle du jeu de plateau n'est pas demandée.

| Imposé par le cours | Décisions du binôme |
|---|---|
| API ASP.NET Core en Minimal API (.NET 10) | Règles du jeu, taille de la grille, composition de la flotte |
| Front Blazor WebAssembly | Représentations de l'état et organisation du code |
| Bibliothèque de modèles partagée | Stockage de l'état des parties |
| Partie complète contre l'ordinateur | Algorithme et difficulté de l'adversaire |
| FluentValidation sur les entrées serveur | Interface et expérience de jeu |
| gRPC fonctionnel sur au moins un échange | Extensions et priorités du backlog |
| Tests métier **et** tests d'intégration | |
| Livrables IA et historique Git exploitable | |

## Règles du moteur de jeu (non négociables)

Ce sont des contraintes du cours, pas des suggestions :
- Les règles de la partie sont vérifiées **côté serveur** uniquement. Le client n'est pas une source de vérité.
- Les positions adverses non découvertes restent cachées dans toute réponse API — jamais de fuite de la grille adverse dans un DTO.
- Un coup invalide (case déjà jouée, coordonnées hors grille, partie terminée, etc.) est **refusé** et ne modifie pas l'état de partie ; rejouer une case déjà jouée ne compte pas comme un nouveau coup.
- Aucun coup n'est joué après la fin de partie.
- Le placement des flottes respecte tailles/formes choisies, sans chevauchement ni débordement.
- L'IA adverse doit respecter les mêmes règles de validité des coups que le joueur.

## Contraintes techniques du cours

- .NET 10 stable ; SDK piloté par `global.json` (copié à la racine, non versionné avec une version fictive).
- FluentValidation sur toutes les entrées serveur (HTTP et gRPC).
- Au moins un échange gRPC-Web fonctionnel, démontrable depuis le navigateur (voir Referentiel diapos 47-52 pour le mécanisme, pas le contenu métier).
- CORS configuré explicitement entre `BattleShip.App` et `BattleShip.API` (origines/ports réels du projet, pas ceux des exemples catalogue).
- DTOs explicites à la frontière API ↔ App ; ne jamais exposer les types de domaine internes bruts s'ils portent une information censée rester cachée.
- Certificat HTTPS local de dev accepté (`dotnet dev-certs https --trust`) avant de tester les appels du navigateur vers l'API.

### Ports et adresses réels du projet

Relevés dans les `Properties/launchSettings.json` — ce sont ceux à utiliser, jamais les `7001`/`7043` des exemples catalogue :

| Projet | Profil `https` | Profil `http` |
|---|---|---|
| `BattleShip.API` | `https://localhost:7186` | `http://localhost:5119` |
| `BattleShip.App` | `https://localhost:7206` | `http://localhost:5209` |

Trois endroits dépendent de ces valeurs et doivent rester cohérents entre eux :
1. `BattleShip.App/Program.cs` — `HttpClient.BaseAddress` pointe aujourd'hui sur `builder.HostEnvironment.BaseAddress`, c'est-à-dire l'App elle-même (scaffold par défaut). À repointer sur l'API pour tout appel réel.
2. La politique CORS côté API — doit autoriser l'origine de l'App, sinon le navigateur bloque la réponse alors que le serveur a répondu correctement.
3. L'adresse du `GrpcChannel` côté App (`GrpcWebHandler`) — même origine que l'API.

Lancer les deux projets avec `--launch-profile https` pour que ces ports soient ceux effectivement écoutés.

### Mise en place gRPC-Web (checklist technique)

- Contrat partagé dans un dossier `Protos/` à la racine (ex. `Protos/battleship.proto`), référencé par les deux projets.
- `BattleShip.API.csproj` : `<Protobuf Include="../Protos/battleship.proto" GrpcServices="Server" />`, packages `Grpc.AspNetCore` + `Grpc.AspNetCore.Web` ; enregistrer `AddGrpc()` avant `Build()`, puis `UseGrpcWeb()` et `MapGrpcService<...>().EnableGrpcWeb()` après.
- `BattleShip.App.csproj` : `<Protobuf Include="../Protos/battleship.proto" GrpcServices="Client" />`, packages `Grpc.Net.Client`, `Grpc.Net.Client.Web`, `Google.Protobuf`, `Grpc.Tools` (`PrivateAssets="all"`, c'est un outil de build).
- Numéros de champs proto stables une fois posés (compatibilité des évolutions).
- Valider les messages gRPC entrants avec FluentValidation comme les entrées HTTP — même exigence, même sévérité.

## Comment travailler ici en tant qu'agent

1. **Avant de coder une fonctionnalité de jeu** : vérifier qu'elle est couverte par une spécification (moteur, contrat API, boucle de jeu, interface — diapos 36 à 46 du référentiel) ou par une entrée de backlog déjà actée avec l'utilisateur. Ne pas inventer de règles de jeu non discutées.
2. **Tests d'abord ou en parallèle** : toute règle de jeu ajoutée ou modifiée doit avoir un test xUnit qui échouerait sans elle (pas seulement un cas nominal). S'inspirer du style `Exemples/ContratsExemples.cs.txt` (Fact/Theory), pas du contenu.
3. **Décisions structurantes → ADR** : changement de représentation d'état, choix d'algorithme de placement/résolution, choix de transport (REST vs gRPC-Web) pour un flux donné, stratégie de l'adversaire, etc. Créer un fichier dans `docs/adr/` de ce dépôt (créer le dossier s'il n'existe pas) en suivant le gabarit `csharp-school/Ressources Bataille Navale/docs/adr/0001-modele.md`.
4. **Échanges décisifs avec l'IA → PROMPTS.md** : quand une réponse d'agent influence une décision de conception (pas les échanges de routine), consigner l'entrée dans `PROMPTS.md` à la racine de ce dépôt selon le gabarit du référentiel (diapo 56). Ne pas dupliquer l'analyse déjà faite dans un ADR — lier plutôt que recopier.
5. **Revue critique → REVUE-IA.md** : au moins trois revues argumentées sur des propositions IA (acceptées, adaptées ou rejetées), avec hypothèse vérifiable, scénario, résultat attendu vs observé, décision. Une proposition qui "a l'air de marcher" n'est pas une vérification.
6. **Ne jamais confondre plausible et vérifié** : une API citée doit être confrontée à sa documentation puis reproduite ; le ton assuré d'une réponse IA ne prouve rien. Avant d'affirmer qu'un comportement est correct, l'exécuter (build, tests, ou scénario manuel documenté).
7. **README.md** : doit rester suffisant à lui seul pour qu'un autre binôme lance le projet en s'y limitant. Contenu attendu : noms des membres, commandes de lancement (API + App, ports réels), fonctionnalités livrées, arbitrages du backlog (ce qui a été fait, écarté, et pourquoi) et limites connues. Le mettre à jour dès qu'une commande, un prérequis ou le périmètre change — jamais en fin de projet seulement.
8. **Ne pas laisser traîner de scaffold** : les fichiers du scaffold `dotnet new` sans rapport avec le jeu ont déjà été supprimés (voir « État actuel » en tête de ce document). Le principe reste valable pour tout nouveau générateur utilisé plus tard (ex. un futur `dotnet new` pour une extension) : supprimer ou remplacer les gabarits de démarrage au fur et à mesure qu'une vraie fonctionnalité les couvre, ne rien laisser traîner à côté du code de jeu.
9. **Confidentialité dans les prompts** : ne jamais transmettre de secrets, clés, ou données personnelles réelles à un outil IA (contrainte explicite du cours, diapo 7). Utiliser des exemples de données pour illustrer un besoin.
10. **Code défendable par le binôme** : chaque membre doit pouvoir expliquer le fonctionnement, le périmètre et les limites du code livré, et le QCM individuel porte sur ces mêmes notions. À qualité égale, préférer la solution que l'utilisateur peut expliquer à celle qui est seulement plus courte ou plus astucieuse.

## Git

- **Ne jamais se mettre en co-auteur d'un commit ou d'une pull request** : pas de ligne `Co-Authored-By: ...` ni d'équivalent pointant vers un outil IA, y compris quand le changement a été largement généré par un agent. L'historique Git remis pour la notation (diapos 11-12, 61-63) doit rester attribué aux deux membres du binôme uniquement. La contribution de l'IA se documente dans `PROMPTS.md` et `REVUE-IA.md`, pas dans les métadonnées du commit.
- Commits qui identifient le travail effectué (message clair, un sujet par commit), changements relus avant validation — voir aussi le point 6 de la checklist de rendu.
- Ne pas committer de secrets ni d'artefacts de build (`bin/`, `obj/`) — voir le nettoyage à faire en priorité, section « Ce que c'est » ci-dessus.
- Pas de réécriture d'un historique déjà poussé sur `origin/main` : corriger par un commit en avant, jamais par un `rebase`/`amend`/`push --force` sur ce qui est publié.

## Commandes utiles

```
# Une seule fois, avant les appels navigateur → API :
dotnet dev-certs https --trust

# Depuis la racine du dépôt, une fois global.json copié :
dotnet --version                       # doit annoncer un SDK 10.x
dotnet build BattleShip.slnx
dotnet test BattleShip.Tests
dotnet run --project BattleShip.API --launch-profile https   # https://localhost:7186
dotnet run --project BattleShip.App --launch-profile https   # https://localhost:7206

# Diagnostic quand un build ou un test échoue sans message exploitable (diapo 22) :
dotnet build -v normal
dotnet test --logger "console;verbosity=detailed"
```

Pour vérifier un comportement isolé (syntaxe, API douteuse proposée par une IA) sans polluer les projets : `dotnet run --file essai.cs`, avec le fichier placé **hors** des dossiers de projet — sinon la présence d'un `.csproj` détourne la commande.

## Évaluation et rendu

La note se partage 50 % projet (binôme) / 50 % QCM individuel, dernière heure du jour 5 (diapo 12).

Critères du projet (diapo 61) : fonctionnement d'une partie complète, qualité et responsabilités du code, maîtrise critique de l'IA (analyse, décisions justifiées, vérifications probantes), tests qui détectent de vraies violations de règles, historique Git exploitable, et pertinence/finition des extensions choisies au-delà du socle. Le socle minimal est fonctionnel mais volontairement peu ambitieux ; les arbitrages de périmètre (fait / écarté / pourquoi) doivent être explicites et défendables, pas implicites dans le code.

Livrables attendus dans le dépôt (diapo 62) : les quatre projets et les tests, `README.md`, `PROMPTS.md`, `docs/adr/`, `REVUE-IA.md`, et un historique dont les changements ont été relus.

Remise : lien du dépôt + hash du commit envoyés à `contact@hts-learning.com` avant le début du QCM du jour 5, `README.md` également déposé sur l'espace étudiant. **Seul ce commit, poussé avant le QCM, est évalué** — donc un travail non poussé n'existe pas pour la correction.

### Checklist de rendu (diapo 63) — vaut définition de « terminé »

- [ ] Le `README.md` suffit à lancer le projet avec les prérequis indiqués (à faire tester par un autre binôme, sans rien expliquer de vive voix).
- [ ] Une partie complète se joue, y compris la fin de partie et la création d'une nouvelle partie.
- [ ] Un échange gRPC-Web **et une erreur attendue** sont démontrables depuis le navigateur.
- [ ] Les entrées HTTP et gRPC sont validées ; les règles sont vérifiées côté serveur.
- [ ] Les tests passent **et** détectent des règles violées, pas seulement un succès nominal.
- [ ] `PROMPTS.md`, les ADR et les trois revues IA sont à jour et reliés à leurs preuves (commits, tests).
- [ ] Les changements ont été relus ; les commits identifient le travail effectué.
- [ ] Le code généré automatiquement est identifié et son rôle est compris.
- [ ] Chaque membre explique le fonctionnement, le périmètre choisi et les limites du projet.
