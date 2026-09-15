# Backlog d'extensions — idées d'implémentation

Ce fichier liste des pistes d'extension au-delà du socle (partie jouable de bout en bout),
issues d'une recherche web sur les variantes de bataille navale (2026-09-15). Rien ici n'est
décidé : ce sont des **propositions à trancher en binôme**, à documenter dans `docs/adr/` une
fois retenues et implémentées (gabarit `docs/adr/0001-modele.md`).

Le socle est désormais fonctionnel (moteur de jeu, API REST, App Blazor, un échange
gRPC-Web) — voir `README.md` § *Arbitrages du backlog* et § *Limites connues*, et les ADR
0001 à 0006 déjà actés. Ce fichier ne duplique pas ces arbitrages ; il les complète avec des
pistes supplémentaires pas encore évoquées ailleurs, plus le lien vers ce qui l'a déjà été.

Statut de chaque ticket :
- `proposé` — piste identifiée, pas encore discutée/actée par le binôme
- `retenu` — décidé par le binôme, prêt à passer en ADR + implémentation
- `écarté` — envisagé puis abandonné (raison à consigner)

---

## TICKET-01 — Mode Salvo (tirs multiples liés aux navires restants)

**Statut** : proposé

**Description** : au lieu d'un tir par tour, chaque joueur envoie un lot de coordonnées dont
le nombre dépend de ses navires encore en vie (perdre un navire réduit la salve suivante).
Variante alternative plus simple : un nombre de tirs fixe par tour ("Speedy Rules").

**Pourquoi ça a du sens ici** :
- Touche directement la boucle de jeu et la validation serveur : il faut valider un lot de
  coordonnées en une seule requête, rejeter les doublons/hors-grille/coups déjà joués *à
  l'intérieur même du lot*, sans modifier l'état si une partie du lot est invalide.
- Bon terrain pour un test xUnit "la partie ne doit pas accepter plus de tirs que de navires
  restants" — un test qui échouerait clairement sans la règle.

**Impact technique** : `Game`/`Board` doivent exposer le nombre de tirs autorisés au tour
courant (dépend du nombre de navires encore en vie du côté humain, cf. fleet 5/4/3/3/2 de
`docs/adr/0002-placement-flotte.md`) ; le DTO de requête `PlayShot` doit accepter une liste de
coordonnées plutôt qu'une seule ; FluentValidation sur la taille du lot.

**Sources** :
- [Battleship Salvo Game Rules – UltraBoardGames](https://www.ultraboardgames.com/battleship/salvo-rules.php)
- [The Boardwalk Games — Salvo Rules](https://theboardwalkgames.com/2016/01/12/board-game-of-the-week-battleship-salvo-rules/)

---

## TICKET-02 — Radar / reconnaissance à usage limité

**Statut** : proposé

**Description** : un joueur peut, un nombre limité de fois par partie, scanner une zone
(ex. 2x2) qui révèle la présence ou non d'un navire sans que cela compte comme un tir qui touche.

**Pourquoi ça a du sens ici** :
- Ajoute un type d'action distinct du tir, avec son propre compteur d'utilisation à vérifier
  côté serveur (encore un cas de "coup refusé si quota épuisé").
- `docs/adr/0005-transport-grpc-web.md` a délibérément laissé `PlayShot` en REST uniquement
  pour le socle et note qu'exposer une opération mutante en gRPC-Web est une extension backlog
  possible. `ScanZone` est un candidat plus sûr que `PlayShot` pour cette extension : il ne
  touche pas la boucle de jeu déjà validée, et donne un cas d'erreur simple à démontrer
  (quota épuisé → `RpcException`) en plus du `GetGameState` déjà exposé.

**Impact technique** : nouvel état "quota de scans restants" par partie/joueur ; nouveau
message proto + méthode de service gRPC ; validation FluentValidation sur les messages gRPC
entrants (même exigence que HTTP, cf. CLAUDE.md).

**Sources** :
- [Battleship board game rules — variantes](https://gamerules.com/rules/battleship-board-game/)
- [UltraBoardGames — variante "Intelligence"](https://www.ultraboardgames.com/battleship/variations.php)

---

## TICKET-03 — IA adverse par grille de probabilité

**Statut** : proposé

**Description** : après chaque tir, l'IA recalcule une grille de probabilité (compte, pour
chaque case, le nombre de placements valides restants pour chaque navire non coulé) et tire
sur la case la plus probable. Bascule en mode "target" (cases adjacentes à un hit) dès qu'un
navire est touché, puis retour en mode "hunt" (probabilité) une fois la piste épuisée.

**Pourquoi ça a du sens ici** :
- `docs/adr/0004-strategie-adversaire.md` a retenu le tir aléatoire uniforme pour le socle et
  documente explicitement la stratégie chasse/cible comme piste de backlog, en notant qu'elle
  s'intégrerait dans `Game.PickComputerTarget` sans changer le contrat public. La grille de
  probabilité est une évolution plus poussée de cette même piste déjà actée : chasse/cible
  est un cas particulier (probabilité binaire adjacent/non-adjacent) de l'approche probabiliste.
- Isolable et testable unitairement sans dépendre de l'API (fonction pure sur `Models`), donc
  facile à coupler à des tests xUnit dans l'esprit de `ComputerAutoShot_OnlyTargetsUntriedCellsOnHumanBoard`
  déjà en place.

**Impact technique** : remplace (ou complète) `Game.PickComputerTarget` ; changement
d'algorithme sans changement de contrat public d'après l'ADR 0004, donc pas d'ADR obligatoire
au sens strict — mais un réexamen de l'ADR 0004 est recommandé si cette piste est retenue,
puisqu'elle referme la question qu'il laisse ouverte.

**Sources** :
- [Coding an Intelligent Battleship Agent – Towards Data Science](https://towardsdatascience.com/coding-an-intelligent-battleship-agent-bf0064a4b319/)
- [Beating Battleships with Algorithms and AI](https://paulvanderlaken.com/2019/01/21/beating-battleships-with-algorithms-and-ai/)
- [GeeksforGeeks — Play Battleships Game with AI](https://www.geeksforgeeks.org/artificial-intelligence/play-battleships-game-with-ai/)

---

## TICKET-04 — Armes spéciales à munitions limitées (torpille / frappe aérienne)

**Statut** : proposé

**Description** : la torpille touche toute une rangée/colonne jusqu'au premier navire
rencontré ; la frappe aérienne touche plusieurs cases alignées d'un coup. Utilisables un
nombre limité de fois par partie.

**Pourquoi ça a du sens ici** :
- Complexifie la résolution d'un tir : un seul "coup" peut affecter 0 à N cases, alors que
  `Board.ReceiveShot` (voir `docs/adr/0003-resolution-tirs.md`) suppose aujourd'hui qu'un tir
  cible une seule coordonnée. Bon exercice de conception (distinguer `Shot` simple et tir à
  zone d'effet) et bon sujet d'ADR dédié si retenu.

**Impact technique** : modifie le modèle de résolution de tir actuel ; nécessite un compteur
de munitions par arme et par joueur, validé côté serveur ; changement de représentation
d'état → **ADR obligatoire** si retenu.

**Sources** :
- [BATTLESHIP App — Commanders Mode (App Store)](https://apps.apple.com/us/app/battleship/id1336026283)
- [Geeky Hobbies — règles alternatives](https://www.geekyhobbies.com/how-to-play-battleship-board-game-rules-and-instructions/)

---

## Piste écartée pour l'instant

### Navires en formes libres façon Tetris (tétrominos au lieu de segments droits)

**Statut** : écarté (non retenu lors du premier passage en revue avec l'utilisateur)

Change toute la validation de placement (chevauchement, débordement, formes non rectilignes),
donc en particulier `Board.TryPlaceShip` documenté dans `docs/adr/0002-placement-flotte.md`.
Changement de représentation d'état → aurait nécessité un ADR dédié. Laissé de côté pour
concentrer l'effort sur les 4 tickets ci-dessus ; à reconsidérer si le socle est solide et qu'il
reste du temps.

**Source** : [UltraBoardGames — variante "Tetris Battleship"](https://www.ultraboardgames.com/battleship/variations.php)
