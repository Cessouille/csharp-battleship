# Échanges décisifs avec l'IA

## 15/09/2026 — Conception du moteur et de la frontière anti-fuite

**Modèle** : Claude Code (Sonnet 5), mode planification.
**Contexte** : la règle non négociable « les positions adverses non découvertes restent cachées dans toute
réponse API » (`CLAUDE.md`) est facile à énoncer et facile à violer par inadvertance (un DTO partagé avec un
champ optionnel oublié, par exemple).
**Prompt** : conception détaillée demandée pour le moteur (`BattleShip.Models/Domain`) et la frontière DTO
(`BattleShip.Models/Contracts`), avec obligation explicite de proposer une solution qui rende la fuite
structurellement impossible, pas seulement testée a posteriori.

**Réponse résumée** : proposition de deux types DTO disjoints (`MyBoardDto` / `OpponentBoardDto`) plutôt qu'un
type partagé avec un champ secret nullable, et d'un unique point de code (`BoardViewMapper`) autorisé à lire
`Board.Ships` pour construire la vue adverse — un choix architectural, pas une simple règle de validation
ajoutée après coup.

**Décision** : acceptée telle quelle — l'argument (« aucun champ ne peut structurellement porter une case non
découverte ») est vérifiable par lecture du type, pas seulement par test. Documentée dans le code (commentaire
sur `OpponentBoardDto`) plutôt que dans un ADR séparé, la représentation d'état globale étant déjà couverte par
`docs/adr/0001-modele.md`.

**Vérification** : test `BoardViewMapperTests.ToOpponentBoardDto_WithNoShotsTaken_ExposesNothing`
(`BattleShip.Tests/Contracts/BoardViewMapperTests.cs` — plateau avec flotte connue, zéro tir reçu →
`Hits`/`Misses`/`SunkShips` tous vides) et son pendant réseau
`GetGameState_OpponentBoardView_RevealsExactlyOneCellPerShotTaken`
(`BattleShip.Tests/Api/GameEndpointsTests.cs`). Résultat attendu : aucune case non ciblée ne doit apparaître dans
la réponse ; résultat observé : conforme sur les deux tests. Cette même architecture est ce qui rend possible,
un peu plus tard dans le projet, la vérification comportementale plus poussée sur l'IA probabiliste (voir
`REVUE-IA.md`) — le chokepoint posé ici est réutilisé tel quel plutôt que redécouvert à chaque nouvelle
fonctionnalité qui touche à l'information cachée.

**Preuve** : `BattleShip.Models/Contracts/BoardViewMapper.cs`, `BattleShip.Tests/Contracts/BoardViewMapperTests.cs`,
`BattleShip.Tests/Api/GameEndpointsTests.cs`, commit `223603d`.

---

## 15/09/2026 — Réalignement du backlog d'extensions

**Modèle** : Claude Code (Opus 5), mode planification.
**Contexte** : `docs/ticket.md` avait été rédigé à partir d'une recherche web sur les variantes de bataille
navale, sans confrontation systématique au code. Avant d'en implémenter un seul, il fallait vérifier que ses
hypothèses techniques tenaient et faire trancher les règles de jeu restées implicites.
**Prompt** : « dans ticket.md, il y a une liste d'idées de features […] ces tickets ne sont pas totalement
alignés avec le code actuel. Adapter les tickets au code actuel et planifier leur implémentation. »

**Réponse résumée** : relecture du domaine, des endpoints, du proto, de l'App et des ADR, puis relevé d'écarts
concrets : DTO `PlayShot` inexistant (le tir passe par `ShotRequestDto` sur `/shots`), ADR 0007 oubliée, nom de
test erroné, `POST /api/games` sans corps (aucune option de partie possible), `ScanZone` présenté comme non
mutant alors qu'il consomme un quota, affirmation « pas d'ADR obligatoire » pour l'IA contraire à `CLAUDE.md`,
et surtout `PickComputerTarget` qui reçoit le plateau humain avec les positions des navires — une IA plus
élaborée pourrait tricher sans que rien ne l'en empêche structurellement. C'est ce dernier constat, trouvé par
relecture avant toute implémentation plutôt que découvert après coup, qui a motivé l'architecture anti-fuite de
`ProbabilityTargeting` revue en détail dans `REVUE-IA.md`. Quatre questions de règle posées au lieu d'être
tranchées seules.

**Décision** : ordre 03 → 02 → 01 → 04 (plus un ticket prérequis « options de partie ») ; modes activés par
options à la création, classique par défaut ; Salvo symétrique pour l'ordinateur ; le scan radar consomme le
tour. Les recommandations proposées ont toutes été retenues par l'utilisateur. Proposition acceptée de
n'alimenter l'IA qu'avec `OpponentBoardDto` (réutilisation du chokepoint anti-fuite posé dans l'échange
précédent). Les points de règle plus fins (taille de zone radar, munitions, fin de partie au milieu d'une
salve…) restent consignés comme ouverts, à trancher au début de chaque ticket. Détail dans `docs/ticket.md`, non
recopié ici.

**Vérification** : chaque écart a été confronté au fichier concerné (`Game.cs`, `Dtos.cs`, `GameEndpoints.cs`,
`battleship.proto`, `Home.razor`, `GameTests.cs`, ADR 0004/0005/0007) avant d'être inscrit dans le ticket. Aucun
code modifié à ce stade : la vérification des choix techniques eux-mêmes se fera par les tests listés dans
chaque ticket, puis par les revues dédiées de `REVUE-IA.md` une fois le code livré.

**Preuve** : `docs/ticket.md` (version réalignée, puis règles actées).

---

## 16/09/2026 — Placement manuel de la flotte : architecture et interaction

**Modèle** : Claude Code (Sonnet 5), mode planification.
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
cité que pour le socle minimal — le placement manuel est une extension légitime, pas une réécriture du socle. Ce
recours au référentiel plutôt qu'à la seule synthèse interne (`CLAUDE.md`) a été demandé explicitement par
l'utilisateur (« prends en compte @csharp-school ») après une première tentative de confirmation restée sans
réponse, pour ne pas fonder une décision d'architecture sur l'inférence de l'IA seule.

**Vérification** : `Board.TryPlaceFleet`/`Game.TryCreateManual` développés en TDD (test écrit et vu échouer
avant chaque implémentation) ; 143 tests xUnit verts au final. Scénario navigateur exécuté : mode « Aléatoire »
inchangé, mode « À la main » avec un navire posé manuellement puis le reste complété via « Placer aléatoirement »,
partie créée avec exactement les positions choisies affichées sur « Votre plateau ».

**Preuve** : `BattleShip.Models/Domain/{Board,Game}.cs`,
`BattleShip.API/{Endpoints/GameEndpoints,Storage/InMemoryGameStore,Validation/CreateGameRequestDtoValidator}.cs`,
`BattleShip.App/{Game/PlacementViewModel.cs,Components/PlacementPanel.razor,Pages/Home.razor}`,
`BattleShip.Tests/{Engine/BoardTests,Engine/GameTests,Api/GameEndpointsTests}.cs`,
`docs/adr/0013-placement-manuel.md`.

---

## 17/09/2026 — Mini-jeu de précision (timing) : modèle de confiance et déclenchement

**Modèle** : Claude Code (Sonnet 5), en mode planification
**Contexte** : demande initiale de l'utilisateur — « il suffit de cliquer sur une case pour lancer un tir […] je
souhaite pouvoir rendre la mécanique plus difficile via les Options de la partie », avec une mécanique décrite en
détail façon combat Undertale (barre horizontale, zone rose au centre, curseur, arrêt au clic/espace), à proposer
« pour l'attaque et la défense ». Deux points structurants restaient implicites dans la demande : comment le
serveur peut rester seul juge d'un mini-jeu par nature calculé en continu côté client, et ce que « défense »
signifie dans un jeu où, normalement, subir un tir ne dépend que de la position des navires.

**Prompt** : demande initiale, puis questions posées explicitement à l'utilisateur (`AskUserQuestion`, cinq
échanges) plutôt que tranchées seules — effet de la défense sur l'issue d'un tir, modèle de confiance sur le
résultat (client déclaré / graine déterministe / horodatage serveur vérifié), fréquence de déclenchement de la
défense, portée par type d'arme, granularité de l'option, ampleur du chantier (tout d'un bloc ou étapes).

**Réponse résumée** : trois approches présentées pour le modèle de confiance, avec recommandation motivée par la
règle non négociable « le client n'est pas source de vérité » (`CLAUDE.md`) — vérification serveur par
horodatage, plus coûteuse à construire (elle exige de rendre la résolution d'un tour interruptible) mais seule
option qui ne fait confiance à aucune donnée fournie par le client pour décider du résultat. Pour la défense,
proposition que la mécanique ne s'applique qu'au coup qui couperait effectivement un navire (pas à chaque tir
adverse), pour rester rare et dramatique plutôt que d'alourdir chaque tour.

**Décision** : les cinq recommandations proposées ont toutes été retenues par l'utilisateur — vérification serveur
par horodatage ; défense déclenchée uniquement sur le coup fatal ; attaque déclenchée sur toute case contenant un
navire, sur toutes les actions offensives (tir, salve, torpille, frappe) ; un seul interrupteur pour les deux
mécaniques ; périmètre complet en un seul chantier plutôt que découpé en jalons. Détail des options envisagées et
de l'arbitrage dans `docs/adr/0019-mini-jeu-de-precision.md`.

**Vérification** : développement en TDD strict (test écrit et vu échouer avant chaque implémentation) sur
`TimingEvaluator`, `Ship.WouldSink`, `Board.{ReceiveShotForcedMiss,ReceiveShotDodged,WouldSink}`,
`VolleySequencer` et `Game` (horloge injectée pour contrôler le temps écoulé sans dépendre d'un vrai minuteur) ;
308 tests xUnit verts au final, aucune régression sur la suite existante (option désactivée par défaut, chemin
inchangé pour tout appelant qui ne l'active pas). Scénario navigateur exécuté de bout en bout — voir les deux
revues dédiées dans `REVUE-IA.md`, qui ont mis au jour et corrigé deux défauts réels non détectés par la suite
de tests au moment de cet échange (un état de défi resté figé côté client, et un raté forcé rendant un navire
insinkable).

**Preuve** : `docs/adr/0019-mini-jeu-de-precision.md`,
`BattleShip.Tests/Engine/{TimingEvaluator,VolleySequencer,PrecisionMinigame}Tests.cs`,
`BattleShip.Tests/Api/{PrecisionMinigameEndpoints,GrpcSalvo}Tests.cs`, `REVUE-IA.md`.

---

## 18/09/2026 — Gestion centralisée des versions de packages (`Directory.Packages.props`)

**Modèle** : Claude Code (Sonnet 5), mode planification.
**Contexte** : demande de classification de toutes les dépendances du projet (interne/externe, directe/transitive,
explicite/implicite, niveau de couplage). L'exercice de classification a nécessité de vérifier le graphe réel
plutôt que de le déduire des `.csproj` (`dotnet list package --include-transitive`), ce qui a révélé un écart non
documenté : `Google.Protobuf` résolu en `3.31.1` côté `BattleShip.API` (transitif via `Grpc.AspNetCore`, absent de
tout `.csproj`) contre `3.36.1` côté `BattleShip.App` (direct) — la bibliothèque qui sérialise chaque message de
l'unique canal gRPC-Web du projet, sur deux versions différentes sans qu'aucun ADR ni commit ne l'ait décidé.

**Prompt** : « Classez les dépendances: interne ou externe, directe ou transitive, explicite ou implicite. Notez
leur couplage avec mon code : faible, moyen, fort. Entoure celle qui inquiète le plus et dit pourquoi. » puis
« procéder à l'ajout du Directory.Packages.props ».

**Réponse résumée** : l'écart `Google.Protobuf` désigné comme risque principal plutôt que l'écart `Grpc.Tools`
(2.83.0 transitif vs 2.84.0 direct, également réel mais build-time only, sans impact runtime) — argument :
invisible en lisant les `.csproj`, situé exactement sur le chemin le plus surveillé du cahier des charges
(l'échange gRPC-Web démontrable au navigateur), et non issu d'une décision documentée. Correctif proposé et
exécuté : `Directory.Packages.props` à la racine (`ManagePackageVersionsCentrally`), une version unique par
package pour toute la solution, et les deux dépendances jusque-là transitives dans l'API (`Google.Protobuf`,
`Grpc.Tools`) rendues explicites (`<PackageReference>` sans version, résolue par le fichier central) pour
qu'elles apparaissent enfin dans le `.csproj` au lieu de dépendre silencieusement de ce que `Grpc.AspNetCore`
tire en transitif.

**Décision** : proposition acceptée telle quelle — `Google.Protobuf` et `Grpc.Tools` consolidés sur la version la
plus récente déjà utilisée côté `App` (`3.36.1` / `2.84.0`), tous les autres packages sur leur version déjà en
place (aucun autre changement fonctionnel).

**Vérification** : `dotnet restore` propre ; `dotnet list package --include-transitive` relancé après coup sur
`BattleShip.API` et `BattleShip.App` — les deux résolvent désormais `Google.Protobuf 3.36.1` et `Grpc.Tools
2.84.0` à l'identique (avant/après comparé, pas seulement supposé corrigé) ; `dotnet build BattleShip.slnx` (0
avertissement, 0 erreur) ; `dotnet test BattleShip.Tests` (309/309 verts, aucune régression).

**Preuve** : `Directory.Packages.props`, `BattleShip.{API,App,Tests}/*.csproj`.
