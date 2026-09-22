# ADR 0017 : système de succès

## Statut et date

Accepté — 17/09/2026. Rédigé a posteriori pour réconcilier le dépôt avec le code déjà livré par le commit
`4864370` (« feat: ajout des succès ») : le code référence cet ADR depuis sa livraison, mais le fichier
n'existait pas encore — écart relevé par l'audit `docs/audits/documentation.md` du 17/09/2026.

## Contexte

Troisième vague de propositions (`docs/ticket.md`, 17/09/2026) : TICKET-12 (socle technique commun) et
TICKET-13 (catalogue des 11 succès intra-partie). Catalogue composé de 6 idées du binôme et de 5 propositions
IA pensées pour le thème visuel de l'App (palette `--rose-vif`/`--lilas`/`--dore`/`--creme`, polices
Fredoka/Quicksand) — origine de chaque idée tracée dans `docs/ticket.md`.

Questions posées par le ticket avant implémentation :
- Succès affichés **verrouillés avec leur condition**, ou **secrets** jusqu'au déblocage ?
- Évaluation **à chaque tour** (retour immédiat) ou **en fin de partie seulement** ?
- Périmètre de la première livraison : TICKET-12 + TICKET-13 seuls, ou TICKET-14 (succès inter-parties, ADR
  0018) inclus ?

## Décision

- **Succès visibles verrouillés.** La page **Mes succès** (`/mes-succes`) affiche les 11 succès du catalogue,
  débloqués en doré et verrouillés en silhouette avec leur description — aucun succès caché tant qu'il n'est
  pas débloqué.
- **Évaluation à chaque tour**, au même chokepoint que le journal de partie (ADR 0016) : `Game.CompleteTurn`
  appelle `EvaluateAchievements()` après chaque coup accepté, plus une fois au constructeur de `Game` pour que
  Rangée parfaite (placement de la flotte) puisse se débloquer avant le moindre tir. Un coup refusé
  (`MoveResult.Rejected`) n'atteint jamais `CompleteTurn` et ne débloque donc structurellement rien.
- **Périmètre livré : TICKET-12, TICKET-13 et TICKET-14** (succès inter-parties + profil joueur) en une seule
  fois plutôt qu'en deux passes — voir ADR 0018 pour le profil joueur lui-même.
- **Anti-fuite structurelle** (même argument que `IComputerTargeting`, ADR 0008) : `AchievementContext` ne
  porte que ce que le joueur voit déjà de sa propre partie — `Options`, `Winner`, `Status`, `History`,
  `ComputerBoard.ShotsReceived` (ses propres tirs) et une projection immuable de `HumanBoard.Ships` (sa propre
  flotte, jamais `ComputerBoard.Ships`). Aucune règle ne peut donc lire ou muter une case adverse non
  découverte, même par erreur.
- **Ajout seul, jamais retiré** : `EvaluateAchievements` ignore toute règle déjà présente dans `_achievements`
  avant de la retester — garanti par construction, pas par convention.
- **Strategy + catalogue déclaratif** : `BattleShip.Models/Achievements/` définit `IAchievementRule` (une
  classe par succès, testable isolément) et `AchievementRules.All` comme liste ordonnée. Les 10 règles
  intra-partie y figurent ; Reine du difficile (S-03) n'y figure pas — elle se calcule sur plusieurs parties
  via `PlayerProfile` (ADR 0018), pas sur l'état d'une seule `Game`.
- **Identifiants figés côté serveur, présentation côté App** : `AchievementId` (enum) n'est jamais renommé une
  fois livré — un catalogue App plus ancien doit pouvoir ignorer un identifiant inconnu (`AchievementCatalog.Find`
  renvoie `null`), jamais planter sur un identifiant renommé. Emoji, libellés, descriptions et images vivent
  uniquement côté App (`AchievementCatalog`), le serveur n'expose que des chaînes.

### Arbitrages par règle (docs/ticket.md, questions « à trancher » résolues à l'implémentation)

- **S-01 Cœur de tirs / S-08 Nœud papillon** (`ShotPatternRule`, motifs dans `ShotPatterns`) : le motif 5×5 doit
  apparaître par simple translation dans une fenêtre quelconque de la grille ; des tirs supplémentaires dans la
  fenêtre (sur les cases hors motif) n'invalident pas le dessin ; aucune rotation n'est acceptée.
- **S-02 Sans une égratignure** (`NoScratchRule`) : « perdre un navire » = navire **coulé** (`Ship` n'expose pas
  les touches partielles, donc « aucune case touchée » n'est pas observable).
- **S-04 Série étincelante** (`SparklingStreakRule`) : 5 résolutions `Hit`/`Sunk` consécutives du joueur, toutes
  actions confondues (tir, salve, arme), dans l'ordre du journal. Un tour sans tir (scan) ne casse rien : il ne
  contribue aucune résolution. Les cases vides traversées par une torpille sont résolues en `Miss` et cassent
  donc la série.
- **S-05 Quatre coins** (`FourCornersRule`) : coins dérivés de `BoardGrid.Size`, jamais codés en dur ; le tir
  gagnant n'a pas à tomber lui-même dans un coin.
- **S-06 Rangée parfaite** (`PerfectRowRule`) : évaluée **dès la création** de la partie, obtenable sans tirer
  un seul coup (arbitrage assumé) ; les deux lignes n'ont pas besoin d'être adjacentes ; un navire vertical à
  cheval sur les deux est accepté — seul le nombre de lignes distinctes compte.
- **S-07 Coup de foudre** (`LoveAtFirstSightRule`) : le tout premier `ShotResolution` rencontré dans
  l'historique. Un scan préalable ne disqualifie pas (il ne contribue aucune résolution) ; une torpille en
  première action traverse d'abord ses cases vides (`Miss`), rendant le succès quasi inaccessible par ce
  chemin — arbitrage assumé, pas corrigé.
- **S-09 Panoplie complète** (`FullKitRule`) : Radar, Salvo et armes spéciales tous activés **et** radar/armes
  réellement utilisés au moins une fois. Salvo n'a rien d'équivalent à « utiliser » (mode figé pour toute la
  partie, ADR 0009) : asymétrie assumée.
- **S-10 Baguette magique** (`MagicWandRule`) : le coup de grâce porté par une arme suffit, même si le navire
  avait déjà été endommagé par un tir classique lors d'un tour précédent.
- **S-11 Glow up** (`GlowUpRule`) : victoire avec exactement un navire encore à flot.

### Contrat

`GameStateDto.Achievements` et `TurnResultDto.Achievements` (liste complète d'identifiants `string`, même
principe que `History`, ADR 0016) ; proto `repeated string achievements = 9` dans `GameStateReply` (champ
ajouté, jamais renuméroté) — `ScanZoneReply`/`PlaySalvoReply` en héritent gratuitement via leur `state` imbriqué.

## Conséquences

- `GameStateDto`/`TurnResultDto` gagnent un champ `Achievements` chacun ; `GameStateReply` gagne un champ proto.
- Nouveau dossier `BattleShip.Models/Achievements/` : ajouter un succès = ajouter une classe à
  `AchievementRules.All`, jamais modifier les règles existantes.
- README (fonctionnalités livrées + limites connues) et `docs/ticket.md` (statut des tickets) mis à jour à la
  livraison de cet ADR, avec un léger retard documenté par l'audit du 17/09/2026.

## Vérification et réexamen

`Engine/Achievements/AchievementEvaluationTests.cs` (une entrée par tour, aucun succès sur un coup refusé, un
succès débloqué reste présent) ; `Engine/Achievements/AchievementRulesCoverageTests.cs` (chaque règle testée en
positif **et** en négatif) ; `Contracts/AchievementLeakTests.cs` —
`TwoGamesWithIdenticalVisibleProjection_UnlockTheSameAchievements` : deux parties à projection visible
identique mais flottes adverses cachées différentes débloquent exactement les mêmes succès, test comportemental
en plus du test structurel par réflexion sur `AchievementContext` ; `Api/AchievementsContractTests.cs`
(round-trip REST et gRPC de la liste de succès). Suite complète : 265/265 tests verts, `dotnet format
--verify-no-changes` propre (audit `docs/audits/bugs-lint.md`, 17/09/2026).

## Références

`BattleShip.Models/Achievements/*`, `BattleShip.Models/Domain/Game.cs` (`EvaluateAchievements`,
`AchievementContext.From`), `BattleShip.Models/Contracts/Dtos.cs`, `Protos/battleship.proto`,
`BattleShip.API/Grpc/GameStateMapper.cs`, `BattleShip.App/Game/AchievementCatalog.cs`,
`BattleShip.App/Components/{AchievementBadge,AchievementToast}.razor`, `BattleShip.App/Pages/Achievements.razor`,
`docs/adr/0018-profil-joueur-anonyme.md` (S-03, seul succès hors de cet ADR).
