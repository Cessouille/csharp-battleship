# ADR 0015 : IA à difficulté réglable

## Statut et date

Accepté — 2026-09-16. Succède à `docs/adr/0008-ia-grille-probabilite.md` (ne le remplace pas : la grille de
probabilité reste le palier « Difficile »).

## Contexte

TICKET-06 (`docs/ticket.md`) : le README listait comme limite connue « l'adversaire n'a pas de niveau de
difficulté réglable : la grille de probabilité est toujours utilisée ». `IComputerTargeting` (ADR 0008) est
une interface déjà pensée pour être substituable — c'est le seam à exploiter plutôt qu'une nouvelle
architecture. Deux questions de conception : combien de paliers, et où loger le choix.

## Options envisagées

- Paliers : (a) deux (Facile/Difficile) ; (b) trois (Facile/Moyen/Difficile), avec un palier intermédiaire
  chasse/cible mentionné dans `PROMPTS.md` lors du réalignement du backlog puis écarté au profit direct de la
  grille de probabilité.
- Emplacement du choix : (a) nouveau champ sur `GameOptions`, au même niveau que `ShotMode`/`SpecialWeapons` ;
  (b) paramètre séparé hors `GameOptions`. (a) retenu sans discussion : la difficulté est une option de partie
  comme les autres, figée à la création (ADR 0009).

## Décision

- **Trois paliers.** `AiDifficulty { Easy, Medium, Hard }`, nouveau champ sur `GameOptions` avec `Hard` par
  défaut (comportement historique inchangé pour tout appelant qui ne précise pas la difficulté — tests
  existants compris).
- **Facile = `RandomTargeting`** (`BattleShip.Models/Ai/RandomTargeting.cs`) : tir uniforme parmi les cases non
  jouées. C'est la stratégie qui existait avant l'ADR 0008 ; elle vivait uniquement comme doublure de test
  (`BattleShip.Tests.TestData.RandomTargeting`, référence de comparaison pour le test de performance de
  `ProbabilityTargeting`). Cette doublure est supprimée : la vraie implémentation de
  `BattleShip.Models/Ai` la remplace partout, y compris dans ce test de comparaison.
- **Moyen = `HuntTargetTargeting`** (`BattleShip.Models/Ai/HuntTargetTargeting.cs`) : hasard tant qu'aucune
  touche n'est en attente, puis cases adjacentes (haut/bas/gauche/droite) à une touche non coulée. Ne calcule
  aucune grille de densité, volontairement plus simple que `ProbabilityTargeting`.
- **Difficile = `ProbabilityTargeting`, inchangé.**
- `Game` choisit la stratégie par défaut via `Game.CreateTargeting(Options.Difficulty)` (`internal`, visible
  aux tests via `InternalsVisibleTo`) ; une stratégie passée explicitement au constructeur (tests TICKET-03/04)
  continue de primer sur `Options.Difficulty`, sans changement de signature publique.
- Contrat : `CreateGameRequestDto.Difficulty` (string, défaut `"Hard"`, validé comme `ShotMode`),
  `GameOptionsDto.Difficulty`, `GameOptionsMessage.difficulty = 4` (proto, champ ajouté, jamais renuméroté).
  Aucune des deux stratégies ajoutées n'utilise d'arme spéciale (comportement par défaut de l'interface) :
  seule `ProbabilityTargeting` en profite, une différence de richesse assumée entre paliers, pas un oubli.

## Conséquences

- `GameOptions` gagne un quatrième champ ; `GameOptionsMessage` un quatrième champ proto.
- La doublure de test `RandomTargeting` (namespace `BattleShip.Tests.TestData`) disparaît, remplacée par la
  stratégie livrée du même nom dans `BattleShip.Models.Ai` — un seul `RandomTargeting` dans tout le dépôt.
- Aucun changement pour une partie créée sans préciser `Difficulty` (défaut `Hard`, identique à avant ce ticket).

## Vérification et réexamen

`Engine/RandomTargetingTests.cs`, `Engine/HuntTargetTargetingTests.cs` (jamais de case déjà jouée sur une
partie complète, ciblage adjacent à une touche isolée/en coin, priorité du mode cible sur le mode chasse en
salve) ; `Engine/AiDifficultyTests.cs` (`Game.CreateTargeting` mappe chaque palier à la bonne classe, une
stratégie explicite prime toujours sur `Options.Difficulty`, moyenne de tirs pour couler la flotte sur 30
graines fixes : Facile > Moyen > Difficile) ; round-trip REST (`Api/GameEndpointsTests.cs`) et gRPC
(`Api/GrpcGameStateTests.cs`) de l'option. Vérifié dans le navigateur : partie créée en Facile, `GET
/api/games/{id}` renvoie `"difficulty":"Easy"`, tir et riposte fonctionnels.

## Références

`BattleShip.Models/Domain/GameOptions.cs`, `BattleShip.Models/Domain/Game.cs`,
`BattleShip.Models/Ai/RandomTargeting.cs`, `BattleShip.Models/Ai/HuntTargetTargeting.cs`,
`Protos/battleship.proto`, `BattleShip.App/Pages/Home.razor`.
