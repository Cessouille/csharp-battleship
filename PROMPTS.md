# Échanges décisifs avec l'IA

## 2026-09-15 — Périmètre du premier socle

**Outil / modèle** : Claude Code (Sonnet 5).
**Contexte** : dépôt encore au scaffold `dotnet new` par défaut. Avant de coder quoi que ce soit, il fallait fixer le périmètre de la première itération : socle complet vs. moteur seul, règles de grille/flotte, difficulté de l'adversaire — des décisions du binôme d'après `CLAUDE.md`, pas de l'IA seule.
**Prompt** : « Planifie l'implémentation de Battleship » (après une demande initiale de démarrer l'implémentation).

**Réponse résumée** : proposition de trois options de périmètre (socle complet 1 joueur / moteur seul / socle + extension ambitieuse), trois options de règles (standard 10×10 et flotte 5 navires / variante simplifiée / choix libre), deux options d'adversaire (tir aléatoire / chasse-cible dès le départ), présentées via un choix à trancher plutôt que décidées seules.

**Décision** : socle complet 1 joueur, règles standard classique, adversaire à tir aléatoire — les trois recommandations proposées, actées par l'utilisateur. Cette décision cadre l'ensemble du plan (voir `docs/adr/0004-strategie-adversaire.md` pour la justification détaillée du choix d'adversaire).

**Vérification** : le plan écrit à partir de ce périmètre a été relu section par section avant implémentation (architecture du moteur, endpoints, flux gRPC-Web, découpage Blazor, liste de tests) puis exécuté intégralement ; résultat observé : 42 tests passants, partie jouable de bout en bout vérifiée au navigateur, échange gRPC-Web succès + erreur démontré.

**Preuve** : commits `eae51cc` à `21af4e2` (implémentation du socle), ce fichier même en étant la trace.

---

## 2026-09-15 — Conception du moteur et de la frontière anti-fuite

**Outil / modèle** : Claude Code (Sonnet 5), agent de planification dédié (sous-agent « Plan »).
**Contexte** : la règle non négociable « les positions adverses non découvertes restent cachées dans toute réponse API » (`CLAUDE.md`) est facile à énoncer et facile à violer par inadvertance (un DTO partagé avec un champ optionnel oublié, par exemple).
**Prompt** : conception détaillée demandée pour le moteur (`BattleShip.Models/Domain`) et la frontière DTO (`BattleShip.Models/Contracts`), avec obligation explicite de proposer une solution qui rende la fuite structurellement impossible, pas seulement testée a posteriori.

**Réponse résumée** : proposition de deux types DTO disjoints (`MyBoardDto` / `OpponentBoardDto`) plutôt qu'un type partagé avec un champ secret nullable, et d'un unique point de code (`BoardViewMapper`) autorisé à lire `Board.Ships` pour construire la vue adverse.

**Décision** : acceptée telle quelle — l'argument (« aucun champ ne peut structurellement porter une case non découverte ») est vérifiable par lecture du type, pas seulement par test. Documentée dans le code (commentaire sur `OpponentBoardDto`) plutôt que dans un ADR séparé, la représentation d'état globale étant déjà couverte par `docs/adr/0001-modele.md`.

**Vérification** : test `BoardViewMapperTests.ToOpponentBoardDto_WithNoShotsTaken_ExposesNothing` (plateau avec flotte connue, zéro tir reçu → `Hits`/`Misses`/`SunkShips` tous vides) et son pendant réseau `GetGameState_OpponentBoardView_RevealsExactlyOneCellPerShotTaken` côté API. Résultat attendu : aucune case non ciblée ne doit apparaître dans la réponse ; résultat observé : conforme sur les deux tests.

**Preuve** : `BattleShip.Models/Contracts/BoardViewMapper.cs`, `BattleShip.Tests/Contracts/BoardViewMapperTests.cs`, `BattleShip.Tests/Api/GameEndpointsTests.cs`, commit `223603d`.

---

## 2026-09-15 — Tri des constats de trois audits automatisés

**Outil / modèle** : Claude Code (Sonnet 5), en tant que consommateur des rapports produits par les skills `audit-bugs-lint`/`audit-code-health`/`audit-documentation` (mis en place par le binôme, voir `.github/workflows/nightly-audit.yml`).
**Contexte** : trois rapports d'audit collés dans la conversation (documentation, bugs/lint, santé du code), tous sur le commit `cc9908a`. Il fallait distinguer ce qui méritait une correction immédiate de ce qui était déjà une décision assumée ailleurs dans le dépôt.
**Prompt** : les trois rapports transmis tels quels par l'utilisateur, sans instruction plus précise que d'y donner suite.

**Réponse résumée** : proposition de traiter comme réels : le `CLAUDE.md` périmé (scaffold vide, nettoyage déjà fait), un nom de test erroné dans l'ADR 0004, `PROMPTS.md`/`REVUE-IA.md` encore vides, les 2 violations `dotnet format`, l'absence de verrou sur `Game` partagé entre requêtes concurrentes, un `!` non défensif dans `Game.razor`, et la duplication mineure de données de test — et de **ne pas** toucher au constat SOLID sur `Game` (mélange orchestration des règles / stratégie IA), que l'audit lui-même qualifie de déjà discuté et volontairement différé dans l'ADR 0004.

**Décision** : tous les constats « réels » acceptés et corrigés ; le constat SOLID rejeté comme action immédiate, conformément au raisonnement de l'audit lui-même (« à réexaminer alors, pas avant »).

**Vérification** : `dotnet build`/`dotnet test`/`dotnet format --verify-no-changes` relancés après chaque correctif (43/43 tests au final, 0 avertissement). Pour le correctif de concurrence spécifiquement, voir la revue dédiée ci-dessous.

**Preuve** : commits `250aa6c` à `2d205b3`.
