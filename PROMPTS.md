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

---

## 2026-09-15 — Réalignement du backlog d'extensions

**Outil / modèle** : Claude Code (Opus 5), en mode planification.
**Contexte** : `docs/ticket.md` avait été rédigé à partir d'une recherche web sur les variantes de bataille navale, sans confrontation systématique au code. Avant d'en implémenter un seul, il fallait vérifier que ses hypothèses techniques tenaient et faire trancher les règles de jeu restées implicites.
**Prompt** : « dans ticket.md, il y a une liste d'idées de features […] ces tickets ne sont pas totalement alignés avec le code actuel. Adapter les tickets au code actuel et planifier leur implémentation. »

**Réponse résumée** : relecture du domaine, des endpoints, du proto, de l'App et des ADR, puis relevé d'écarts concrets : DTO `PlayShot` inexistant (le tir passe par `ShotRequestDto` sur `/shots`), ADR 0007 oubliée, nom de test erroné, `POST /api/games` sans corps (aucune option de partie possible), `ScanZone` présenté comme non mutant alors qu'il consomme un quota, affirmation « pas d'ADR obligatoire » pour l'IA contraire à `CLAUDE.md`, et surtout `PickComputerTarget` qui reçoit le plateau humain avec les positions des navires — une IA plus élaborée pourrait tricher sans que rien ne l'en empêche structurellement. Quatre questions de règle posées au lieu d'être tranchées seules.

**Décision** : ordre 03 → 02 → 01 → 04 (plus un ticket prérequis « options de partie ») ; modes activés par options à la création, classique par défaut ; Salvo symétrique pour l'ordinateur ; le scan radar consomme le tour. Les recommandations proposées ont toutes été retenues par l'utilisateur. Proposition acceptée de n'alimenter l'IA qu'avec `OpponentBoardDto` (réutilisation du chokepoint anti-fuite existant). Les points de règle plus fins (taille de zone radar, munitions, fin de partie au milieu d'une salve…) restent consignés comme ouverts, à trancher au début de chaque ticket. Détail dans `docs/ticket.md`, non recopié ici.

**Vérification** : chaque écart a été confronté au fichier concerné (`Game.cs`, `Dtos.cs`, `GameEndpoints.cs`, `battleship.proto`, `Home.razor`, `GameTests.cs`, ADR 0004/0005/0007) avant d'être inscrit dans le ticket. Aucun code modifié à ce stade : la vérification des choix techniques eux-mêmes se fera par les tests listés dans chaque ticket.

**Suite — planification de l'implémentation** : lors d'une seconde passe de planification, huit points de règle restés ouverts ont été soumis au binôme plutôt que décidés par l'IA (zone et quota du radar, radar de l'ordinateur, taille et fin de salve, munitions, torpille sur cases jouées, armes de l'ordinateur, combinaison armes/Salvo). Les recommandations proposées ont toutes été retenues ; la règle de la frappe aérienne sur cases déjà jouées n'a pas été posée explicitement et a été déduite de celle de la torpille — signalée comme telle, à confirmer. Deux choix de conception en ont découlé : n'introduire chaque option de partie qu'avec la règle qui lui donne un effet, et passer `TurnResult` en listes dès le radar pour ne faire qu'une seule refonte. Règles actées dans `docs/ticket.md`.

**Preuve** : `docs/ticket.md` (version réalignée, puis règles actées).

---

## 2026-09-15 — Implémentation des tickets 03, 00, 02, 01 et 04

**Outil / modèle** : Claude Code (Opus 5).
**Contexte** : exécution du plan validé (règles actées dans `docs/ticket.md`).
**Prompt** : « Implémenter les 4 étapes. »

**Réponse résumée** : implémentation étape par étape avec tests. Plusieurs choix de conception proposés par l'IA en cours de route : n'alimenter l'IA qu'avec `OpponentBoardDto` (ADR 0008) ; valider une salve entière avant tout tir, via une fonction commune au joueur et à l'ordinateur (ADR 0011) ; faire choisir les N cibles de l'ordinateur sur une même vue (ADR 0011) ; exprimer une arme comme une liste de cases résolues par le `ReceiveShot` existant (ADR 0012). Une hypothèse du plan s'est révélée fausse : le corps facultatif sur `POST /api/games`.

**Décision** : choix de conception consignés dans les ADR 0008 à 0012, sans les recopier ici. Corps de création rendu obligatoire (`{}` = partie classique).

**Vérification** : 133 tests verts, `dotnet format --verify-no-changes` propre. Règles clés neutralisées une à une (anti-triche IA, tailles des navires coulés, quota radar, validation atomique de salve, munitions) : chaque neutralisation fait échouer au moins un test (voir `REVUE-IA.md`, ADR 0010 à 0012). Vérification dans le navigateur **non effectuée** : le certificat HTTPS de développement n'était pas approuvé par le navigateur utilisé.

**Preuve** : `BattleShip.Tests/Engine/{ProbabilityTargeting,Radar,Salvo,Weapon}Tests.cs`, `BattleShip.Tests/Api/{GrpcScanZone,SalvoEndpoints,WeaponEndpoints}Tests.cs`.

---

## 2026-09-16 — Placement manuel de la flotte : architecture et interaction

**Outil / modèle** : Claude Code (Sonnet 5), en mode planification puis implémentation.
**Contexte** : le placement de la flotte humaine était toujours aléatoire ; `README.md` listait explicitement
« personnalisation du placement de la flotte par le joueur » comme piste écartée. L'utilisateur a demandé de
revenir dessus : offrir le choix entre placement aléatoire et placement à la main.
**Prompt** : « aujourd'hui, le joueur ne peut pas placer ses bateaux à la main sur la grille. goal : avoir
l'option de soit les placer aléatoirement soit les placer à la main » puis, après une première question sans
réponse, « A ton avis, en prenant compte les consignes, quel est le mieux ? ».

**Réponse résumée** : deux choix structurants posés au binôme plutôt que tranchés seuls — (1) regrouper le
placement dans la requête de création de partie (pas de nouvel état serveur) vs. une phase serveur dédiée
`AwaitingPlacement` avec un nouvel endpoint ; (2) interaction clic + bouton d'orientation (réutilise le pattern
déjà présent dans `ActionBar.razor`) vs. glisser-déposer. Recommandation motivée par `CLAUDE.md` (règles et
interface = décisions du binôme ; moins de nouveaux états à tester et expliquer ; pas d'interop JS absente du
projet) : option groupée + clic/orientation.

**Décision** : les deux recommandations retenues par l'utilisateur, après relecture du référentiel de cours
(`csharp-school/Ressources Bataille Navale/Cours...md`, diapo 36) confirmant que le placement aléatoire n'est
cité que pour le socle minimal — le placement manuel est une extension légitime, pas une réécriture du socle.

**Vérification** : `Board.TryPlaceFleet`/`Game.TryCreateManual` développés en TDD (test écrit et vu échouer
avant chaque implémentation) ; 143 tests xUnit verts au final. Scénario navigateur exécuté : mode "Aléatoire"
inchangé, mode "À la main" avec un navire posé manuellement puis le reste complété via "Placer aléatoirement",
partie créée avec exactement les positions choisies affichées sur "Votre plateau".

**Preuve** : `BattleShip.Models/Domain/{Board,Game}.cs`, `BattleShip.API/{Endpoints/GameEndpoints,Storage/InMemoryGameStore,Validation/CreateGameRequestDtoValidator}.cs`, `BattleShip.App/{Game/PlacementViewModel.cs,Components/PlacementPanel.razor,Pages/Home.razor}`, `BattleShip.Tests/{Engine/BoardTests,Engine/GameTests,Api/GameEndpointsTests}.cs`, `docs/adr/0013-placement-manuel.md`.

---

## 2026-09-17 — Tri des constats de trois nouveaux audits automatisés (commit `4864370`)

**Outil / modèle** : Claude Code (Sonnet 5), en tant que consommateur des rapports produits par les skills `audit-bugs-lint`/`audit-code-health`/`audit-documentation`.
**Contexte** : trois nouveaux rapports d'audit collés dans la conversation, cette fois sur le commit `4864370` (« feat: ajout des succès »), qui livrait le système de succès et le profil joueur. Même exercice de tri qu'à l'entrée du 2026-09-15 ci-dessus, sur un commit plus récent et plus volumineux.
**Prompt** : les trois rapports transmis tels quels par l'utilisateur (« Voici les 3 audits réalisés »), sans instruction plus précise que d'y donner suite.

**Réponse résumée** : avant de corriger quoi que ce soit, deux questions posées à l'utilisateur plutôt que tranchées seules (`AskUserQuestion`) — périmètre de correction (documentation/traçabilité, qualité de code, les deux, ou rien pour l'instant) et sort du champ `AchievementScope` non consommé (l'exploiter vs. le retirer, constat YAGNI de l'audit `code-health`). L'utilisateur a choisi « tout » et « l'exploiter ». Constat le plus significatif des trois rapports, plus grave qu'un simple retard de documentation : `docs/ticket.md` marquait encore TICKET-12/13/14 (succès + profil joueur) `proposé` — « rien n'est acté » — alors que le code correspondant était entièrement livré, testé et référençait déjà deux ADR (`0017-systeme-de-succes.md`, `0018-profil-joueur-anonyme.md`) qui n'existaient pas sur le disque. Plutôt que de deviner le contenu de ces ADR, lecture complète du code du domaine des succès (`BattleShip.Models/Achievements/*`, chaque règle et ses arbitrages documentés en commentaire) et du profil joueur (`PlayerProfile`, `PlayerEndpoints`, `PlayerSession`) avant d'écrire quoi que ce soit, pour que les ADR et la mise à jour de `docs/ticket.md` décrivent des décisions réellement prises dans le code livré, pas des décisions inventées après coup.

**Décision** : périmètre traité intégralement — ports du README corrigés (`7186`/`7206` → `8080`/`3000`, l'audit `documentation` avait raison), fonctionnalités manquantes ajoutées au README (placement manuel, IA réglable, Salve gRPC-Web, journal, succès, profil), `CLAUDE.md` « État actuel » mis à jour, `docs/adr/0017` et `0018` rédigés à partir du code réel, `docs/ticket.md` TICKET-12/13/14 repassés `proposé` → `livré` avec chaque question ouverte remplacée par la décision effectivement implémentée, duplication `Game.razor` (`catch (RpcException)`) extraite en `SetMoveErrorFromRpc`, motif de validation d'énumération FluentValidation répété six fois factorisé en `MustBeEnumName<T, TEnum>`, `AchievementScope` exploité pour grouper `/mes-succes` en deux sections (inter-parties / partie).

**Vérification** : `dotnet build`/`dotnet test` **non exécutables dans cette session** (aucun SDK .NET 10 installé dans cet environnement, seulement 7.0.400 et 8.0.101 — contrainte d'environnement, pas un choix). Vérification faite par lecture croisée : chaque décision consignée dans les ADR et dans `docs/ticket.md` a été confrontée au fichier de règle correspondant (`ShotPatternRule`, `NoScratchRule`, `SparklingStreakRule`, etc.) avant d'être écrite, et le rapport `docs/audits/bugs-lint.md` du même commit (265/265 tests verts, `dotnet format --verify-no-changes` propre) sert de seule confirmation d'exécution disponible. Limite explicitement signalée à l'utilisateur et dans `REVUE-IA.md` : à revérifier par `dotnet build`/`dotnet test` dès qu'un SDK .NET 10 est disponible, en particulier pour les trois nouveaux fichiers de validation FluentValidation et le composant `Game.razor` modifiés dans cette session.

**Preuve** : `README.md`, `CLAUDE.md`, `docs/adr/0017-systeme-de-succes.md`, `docs/adr/0018-profil-joueur-anonyme.md`, `docs/ticket.md`, `REVUE-IA.md` (nouvelle revue ci-dessus), `BattleShip.App/Pages/{Game,Achievements}.razor`, `BattleShip.API/Validation/{EnumValidationExtensions,CreateGameRequestDtoValidator,WeaponRequestValidators}.cs`.
