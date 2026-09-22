# ADR 0019 : mini-jeu de précision (timing) pour l'attaque et la défense

## Statut et date

Accepté — 17/09/2026.

## Contexte

Aujourd'hui, cliquer une case du plateau adverse tire immédiatement, sans compétence requise autre que choisir
la bonne case. L'utilisateur a demandé une option rendant le jeu plus difficile, façon combat Undertale : une
barre horizontale avec une zone rose au centre et rouge/orange aux extrémités, un curseur qui balaie la barre en
continu, un clic ou la touche Espace pour l'arrêter — dans la zone rose, le tir/la défense réussit. La mécanique
devait exister pour l'attaque du joueur **et** pour la défense contre la riposte de l'ordinateur, activable via
les Options de partie.

Trois questions de conception ont été posées au binôme plutôt que tranchées seules (voir `PROMPTS.md`) : le
modèle de confiance sur le résultat du mini-jeu, les conditions de déclenchement côté attaque et côté défense, et
le périmètre par type d'action offensive.

## Options envisagées (modèle de confiance sur le résultat)

1. **Résultat déclaré par le client** : le client calcule lui-même réussi/raté et l'envoie au serveur.
2. **Graine déterministe sans aller-retour** : le serveur dérive les paramètres de la barre d'un élément déjà
   présent dans l'état de partie (ex. numéro de tour), sans appel réseau dédié ; le client les recalcule à
   l'identique pour l'affichage, mais l'instant d'arrêt reste déclaré par le client.
3. **Vérification serveur par horodatage** : le serveur mémorise l'instant de démarrage du défi (son horloge) ;
   quand le joueur signale l'arrêt, le serveur calcule lui-même la position du curseur à partir du temps
   réellement écoulé côté serveur, sans jamais demander au client s'il a gagné.

## Décision

Option 3. Les options 1 et 2 confient en dernier ressort le résultat au client, ce qui s'écarte de la règle non
négociable « le client n'est pas une source de vérité » (`CLAUDE.md`) — un client modifié pourrait toujours
déclarer « réussi ». L'option 3 est plus coûteuse à construire (elle exige de suspendre la résolution d'un tour
en plein milieu) mais reste la seule où le résultat est calculé exclusivement à partir de données que le serveur
contrôle intégralement (son horloge, les paramètres de la barre qu'il a lui-même fixés).

Règles de déclenchement, actées avec l'utilisateur :

- **Attaque** : un défi s'ouvre uniquement quand la case visée contient un navire adverse (une case vide reste un
  raté immédiat, comme aujourd'hui). Couvre toutes les actions offensives du joueur — tir simple, salve, torpille,
  frappe aérienne — chaque case visée qui contient un navire déclenche son propre défi, en séquence.
- **Défense** : un défi s'ouvre uniquement quand un tir adverse couperait un navire humain (le coup fatal), y
  compris si l'IA utilise une arme spéciale. Réussir épargne le navire (la case reste rejouable plus tard, voir
  `Board.ReceiveShotDodged`) ; rater résout normalement (le navire peut couler).
- Un seul interrupteur « Mini-jeu de précision » (`GameOptions.PrecisionMinigame`) active les deux mécaniques
  ensemble.
- La zone rose est fixe au centre de la barre (pas randomisée), le curseur fait un va-et-vient continu sans
  timeout (`TimingRules` : `ZoneWidth`, `ZoneStart` centré, `PeriodMs`).

### Conséquences sur la résolution d'un tour

`Game.CompleteTurn` (100 % synchrone jusqu'ici) devient interruptible. Une case dont la résolution nécessite un
défi ne peut pas être décidée à l'avance : une frappe aérienne ou une salve peuvent toucher plusieurs cases d'un
même navire, et le besoin d'un défi sur une case dépend du résultat (encore inconnu) d'un défi précédent sur ce
même navire dans le même lot. `VolleySequencer` remplace la boucle `foreach` qui résolvait autrefois un lot d'un
bloc : il avance case par case, s'arrête sans muter dès qu'une case a besoin d'un défi, et reprend une fois le
résultat connu. `MoveResult` gagne un troisième cas, `AwaitingChallenge`, à côté de `Accepted`/`Rejected`.

Deux primitifs symétriques sur `Board` couvrent les deux échecs possibles du mini-jeu, avec le même comportement
côté case (voir correction ci-dessous) : `ReceiveShotForcedMiss` (attaque ratée au timing) et `ReceiveShotDodged`
(défense réussie) — dans les deux cas le tir a eu lieu (compte dans le journal de partie) mais n'affecte jamais
le navire et ne marque jamais la case comme jouée ; elle reste une cible valide plus tard, pour un tir qui pourra
cette fois réussir.

**Correction (17/09/2026)** : la version livrée initialement faisait consommer la case par
`ReceiveShotForcedMiss` (« comme un vrai raté »), en cohérence apparente avec un vrai raté sur une case vide.
Mais `Ship.IsSunk` exige un coup enregistré sur *toutes* les cases du navire ; une case consommée sans jamais
enregistrer de coup rend donc ce navire définitivement impossible à couler dès qu'un seul raté forcé le touche —
un défaut réel, pas une nuance cosmétique (voir la revue dédiée dans `REVUE-IA.md`). Corrigé en alignant
`ReceiveShotForcedMiss` sur `ReceiveShotDodged` (la case reste jouable) ; les deux noms sont conservés séparés
uniquement pour la lisibilité au point d'appel (`Game.ResolveChallenge` distingue toujours attaque/défense).

## Contrat réseau

Pas de nouveau type d'enveloppe : quand une action de tour ne peut pas encore se conclure, l'endpoint REST
répond **202 Accepted** avec l'état de partie complet (`GameStateDto`, y compris les cases déjà résolues plus tôt
dans le même lot et le nouveau champ `PendingChallenge`) au lieu du **200 OK** habituel avec `TurnResultDto`.
Un nouvel endpoint, `POST /api/games/{gameId}/challenges/{challengeId}/resolve`, résout le défi actuellement
ouvert — sans corps de requête, le résultat se calcule uniquement à partir de l'horloge serveur. Il reste le
**point d'entrée unique** de résolution, y compris quand le défi a été ouvert par une action gRPC (`PlaySalvo`,
`ScanZone`) : les réponses gRPC portent le défi en attente dans leur `GameStateReply.pending_challenge` imbriqué,
mais sa résolution passe toujours par REST, pour ne pas dupliquer la logique sur les deux transports.

## Vérification et réexamen

Unitaires purs (`TimingEvaluatorTests`), `Board`/`Ship`/`VolleySequencer` (`BoardTests`, `ShipTests`,
`VolleySequencerTests`), moteur avec horloge injectée pour contrôler le temps écoulé
(`PrecisionMinigameTests` — attaque réussie/ratée, case vide sans défi, défense réussie/ratée, séquence de
défis sur une même salve, option désactivée sans régression, action refusée pendant qu'un défi est ouvert),
contrats API/gRPC (`PrecisionMinigameEndpointsTests`, `GrpcSalvoTests.PlaySalvo_TriggeringAChallenge_...`).
Scénario navigateur exécuté (voir `REVUE-IA.md`) : attaque et défense déclenchées et résolues (succès et échec),
touche Espace, rechargement de page en plein défi — un défaut réel a été trouvé et corrigé pendant cette
vérification (voir revue dédiée). Un second défaut réel, signalé cette fois par l'utilisateur en jouant (un
navire touché par un raté forcé ne coulait plus jamais), a été reproduit par un test qui échouait avant correctif
puis corrigé — voir la revue « le raté forcé... » dans `REVUE-IA.md` et la correction ci-dessus.

## Références

`BattleShip.Models/Domain/{TimingChallenge,VolleySequencer,Game,Board,Ship,GameOptions}.cs`,
`BattleShip.Models/Contracts/{Dtos,BoardViewMapper}.cs`, `BattleShip.API/Endpoints/GameEndpoints.cs`,
`BattleShip.API/Grpc/{GameStateMapper,BattleshipGrpcService}.cs`, `Protos/battleship.proto`,
`BattleShip.App/Components/TimingChallenge.razor`, `BattleShip.App/Game/GameStateUpdate.cs`,
`BattleShip.App/Pages/{Home,Game}.razor`.
