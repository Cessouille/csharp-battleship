using BattleShip.Models.Achievements;
using BattleShip.Models.Ai;
using BattleShip.Models.Contracts;

namespace BattleShip.Models.Domain;

public enum PlayerId
{
    Human,
    Computer
}

public enum GameStatus
{
    InProgress,
    Finished
}

public enum FleetPlacementRejectionReason
{
    InvalidFleetComposition,
    OutOfGridOrOverlap
}

public abstract record CreateGameResult
{
    private CreateGameResult()
    {
    }

    public sealed record Created(Game Game) : CreateGameResult;

    public sealed record Rejected(FleetPlacementRejectionReason Reason) : CreateGameResult;
}

public enum MoveRejectionReason
{
    OutOfGrid,
    AlreadyPlayed,
    GameAlreadyFinished,
    RadarDisabled,
    NoScansLeft,
    WrongShotMode,
    WrongSalvoSize,
    DuplicateTarget,
    WeaponsDisabled,
    NoAmmoLeft,

    /// <summary>Voir docs/adr/0019-mini-jeu-de-precision.md : le défi résolu n'existe pas, est déjà résolu, ou son id ne correspond pas à celui en attente.</summary>
    NoChallengePending,

    /// <summary>Un mini-jeu de précision est déjà ouvert : aucune nouvelle action tant qu'il n'est pas résolu.</summary>
    ChallengeInProgress
}

public abstract record MoveResult
{
    private MoveResult()
    {
    }

    public sealed record Accepted(TurnResult Turn) : MoveResult;

    /// <summary>Voir docs/adr/0019-mini-jeu-de-precision.md : le tour est suspendu en attendant que le joueur résolve ce défi de timing.</summary>
    public sealed record AwaitingChallenge(PendingChallenge Challenge) : MoveResult;

    public sealed record Rejected(MoveRejectionReason Reason) : MoveResult;
}

public sealed record TurnResult(
    IReadOnlyList<ShotResolution> PlayerShots,
    ScanResult? PlayerScan,
    WeaponKind? PlayerWeapon,
    IReadOnlyList<ShotResolution> ComputerShots,
    WeaponKind? ComputerWeapon,
    PlayerId? Winner,
    GameStatus Status);

public sealed class Game
{
    /// <summary>
    /// Un même Game (donc un même gameId) est un singleton partagé côté serveur, potentiellement atteint par
    /// deux requêtes HTTP/gRPC concurrentes (deux onglets, deux clients qui connaissent le même gameId). Toute
    /// lecture ou écriture passe par ce verrou d'instance pour éviter qu'un tir concurrent ne double-compte une
    /// case ou ne lise un état à moitié muté (voir docs/adr/0007-concurrence-partie.md).
    /// </summary>
    private readonly Lock _gate = new();

    private readonly IComputerTargeting _targeting;
    private readonly Random _rng;
    private readonly Func<DateTimeOffset> _clock;
    private readonly List<TurnResult> _history = [];
    private readonly List<AchievementId> _achievements = [];

    /// <summary>Défi de timing actuellement affiché au joueur, ou <c>null</c> si aucun n'est en attente (voir docs/adr/0019-mini-jeu-de-precision.md).</summary>
    private PendingChallenge? _pendingChallenge;

    /// <summary>État de tour suspendu tant qu'un défi de timing (attaque ou défense) n'est pas résolu ; <c>null</c> hors mini-jeu.</summary>
    private PendingTurn? _pendingTurn;

    public Game(
        Guid id,
        Board humanBoard,
        Board computerBoard,
        GameOptions? options = null,
        IComputerTargeting? targeting = null,
        Random? rng = null,
        Func<DateTimeOffset>? clock = null)
    {
        Id = id;
        HumanBoard = humanBoard;
        ComputerBoard = computerBoard;
        Options = options ?? GameOptions.Classic;
        _targeting = targeting ?? CreateTargeting(Options.Difficulty);
        _rng = rng ?? Random.Shared;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);

        // Après l'affectation de tous les champs ci-dessus : un succès comme RangeeParfaite (placement) doit
        // pouvoir se débloquer avant le moindre tir (docs/adr/0017-systeme-de-succes.md).
        EvaluateAchievements();
    }

    /// <summary>
    /// État de tour suspendu le temps qu'un ou plusieurs défis de timing se résolvent (attaque, puis défense).
    /// Toujours <c>null</c> hors mini-jeu de précision, ou entre deux tours. <see cref="AttackSequencer"/> et
    /// <see cref="DefenseSequencer"/> ne sont jamais tous deux non-null : l'attaque se termine intégralement
    /// avant que la défense ne commence.
    /// </summary>
    private sealed class PendingTurn
    {
        public VolleySequencer? AttackSequencer { get; init; }
        public IReadOnlyList<ShotResolution>? PlayerShots { get; set; }
        public ScanResult? PlayerScan { get; init; }
        public WeaponKind? PlayerWeapon { get; init; }
        public VolleySequencer? DefenseSequencer { get; set; }
        public WeaponKind? ComputerWeapon { get; set; }
    }

    /// <summary>
    /// Voir docs/adr/0015-ia-difficulte-reglable.md. Ignoré si un appelant passe explicitement une stratégie (tests).
    /// Interne mais visible aux tests (InternalsVisibleTo) pour vérifier directement le choix par difficulté.
    /// </summary>
    internal static IComputerTargeting CreateTargeting(AiDifficulty difficulty) => difficulty switch
    {
        AiDifficulty.Easy => new RandomTargeting(),
        AiDifficulty.Medium => new HuntTargetTargeting(),
        _ => new ProbabilityTargeting()
    };

    public Guid Id { get; }
    public GameOptions Options { get; }
    public Arsenal HumanArsenal { get; } = new();
    public Arsenal ComputerArsenal { get; } = new();
    public Board HumanBoard { get; }
    public Board ComputerBoard { get; }
    public PlayerId? Winner { get; private set; }
    public GameStatus Status => Winner is null ? GameStatus.InProgress : GameStatus.Finished;

    /// <summary>Défi de timing actuellement affiché au joueur, ou <c>null</c> hors mini-jeu (voir docs/adr/0019-mini-jeu-de-precision.md). Exposé pour qu'un rechargement de page puisse le réafficher.</summary>
    public PendingChallenge? PendingChallenge => _pendingChallenge;

    /// <summary>
    /// Journal chronologique des tours joués (TICKET-10, voir docs/adr/0016-journal-de-partie.md), pour survivre
    /// au rechargement de la page (renvoyé par GetGameState). Alimenté au même chokepoint que la résolution d'un
    /// tour (<see cref="FinalizeTurn"/>), jamais retiré : un coup refusé n'y apparaît jamais (aucun MoveResult.Rejected
    /// n'atteint FinalizeTurn).
    /// </summary>
    public IReadOnlyList<TurnResult> History => _history;

    /// <summary>
    /// Succès débloqués sur cette partie (TICKET-12, docs/adr/0017-systeme-de-succes.md). Alimentée au même
    /// chokepoint que <see cref="History"/> (<see cref="FinalizeTurn"/>) et au constructeur (pour un succès
    /// évaluable dès le placement) ; jamais retirée. Un coup refusé n'y change jamais rien, structurellement :
    /// aucun MoveResult.Rejected n'atteint CompleteTurn ni ne rappelle EvaluateAchievements.
    /// </summary>
    public IReadOnlyList<AchievementId> Achievements => _achievements;

    /// <summary>Le quota se déduit des scans déjà enregistrés sur le plateau adverse : pas de compteur séparé qui pourrait diverger.</summary>
    public int ScansRemaining => Options.Radar ? RadarRules.ScansPerGame - ComputerBoard.ScansReceived.Count : 0;

    /// <summary>Nombre de tirs exigés au prochain tour du joueur : 1 en classique, sinon la taille de sa salve.</summary>
    public int HumanSalvoSize => Options.ShotMode == ShotMode.Salvo ? SalvoSize(HumanBoard, ComputerBoard) : 1;

    /// <summary>
    /// Règle Salvo : autant de tirs que de navires encore à flot chez le tireur, bornés par les cases que la cible
    /// n'a pas encore reçues. Calculée au début du tour du tireur, donc après la salve adverse qui le précède.
    /// </summary>
    public static int SalvoSize(Board shooter, Board target) =>
        Math.Min(shooter.Ships.Count(s => !s.IsSunk), BoardGrid.Size * BoardGrid.Size - target.ShotsReceived.Count);

    public static Game CreateRandom(Guid id, Random rng, GameOptions? options = null)
    {
        var human = new Board();
        var computer = new Board();
        human.PlaceFleetRandomly(Fleet.Standard, rng);
        computer.PlaceFleetRandomly(Fleet.Standard, rng);
        return new Game(id, human, computer, options, rng: rng);
    }

    /// <summary>
    /// Placement choisi par le joueur (voir docs/adr/0013-placement-manuel.md) : la composition de
    /// <paramref name="placements"/> doit correspondre exactement à <see cref="Fleet.Standard"/> (mêmes types
    /// de navire, un de chaque), puis chaque position est revalidée par <see cref="Board.TryPlaceFleet"/> — le
    /// client n'est jamais une source de vérité. Le plateau ordinateur reste placé au hasard comme aujourd'hui.
    /// </summary>
    public static CreateGameResult TryCreateManual(
        Guid id,
        IReadOnlyList<(ShipKind Kind, Coordinate Origin, Orientation Orientation)> placements,
        Random rng,
        GameOptions? options = null)
    {
        var expectedKinds = Fleet.Standard.Select(f => f.Kind).OrderBy(k => k);
        var actualKinds = placements.Select(p => p.Kind).OrderBy(k => k);
        if (!expectedKinds.SequenceEqual(actualKinds))
            return new CreateGameResult.Rejected(FleetPlacementRejectionReason.InvalidFleetComposition);

        var human = new Board();
        if (!human.TryPlaceFleet(placements, Fleet.Standard))
            return new CreateGameResult.Rejected(FleetPlacementRejectionReason.OutOfGridOrOverlap);

        var computer = new Board();
        computer.PlaceFleetRandomly(Fleet.Standard, rng);
        return new CreateGameResult.Created(new Game(id, human, computer, options, rng: rng));
    }

    /// <summary>Exécute <paramref name="func"/> sous le verrou de cette partie — à utiliser pour toute lecture (mapping DTO/proto) comme pour l'écriture, afin qu'aucune requête concurrente sur le même gameId ne voie un état à moitié muté.</summary>
    public TResult Locked<TResult>(Func<TResult> func)
    {
        lock (_gate)
        {
            return func();
        }
    }

    /// <summary>
    /// Résout le tir du joueur puis, si la partie continue, la riposte de l'ordinateur — dans le même appel.
    /// Pas de machine à état "à qui le tour" : sans latence réseau entre les deux joueurs, la résolution
    /// synchrone est suffisante (voir docs/adr/0001-modele.md). L'ensemble de la méthode tient sous le même
    /// verrou d'instance (<see cref="Locked{TResult}"/> est réentrant pour le même thread) : deux tirs
    /// concurrents sur la même partie s'exécutent en série, jamais entrelacés.
    /// </summary>
    public MoveResult PlayHumanShot(Coordinate target) => Guarded(() =>
    {
        if (Options.ShotMode != ShotMode.Classic)
            return new MoveResult.Rejected(MoveRejectionReason.WrongShotMode);

        return PlayHumanVolley([target], expectedCount: 1);
    });

    /// <summary>
    /// Salve du joueur en mode Salvo. Le lot entier est validé avant le moindre tir : un seul élément invalide et
    /// rien n'est appliqué. Les tirs sont ensuite résolus dans l'ordre du lot, en s'arrêtant dès que le dernier
    /// navire adverse coule (aucun coup après la fin de partie).
    /// </summary>
    public MoveResult PlayHumanSalvo(IReadOnlyList<Coordinate> targets) => Guarded(() =>
    {
        if (Options.ShotMode != ShotMode.Salvo)
            return new MoveResult.Rejected(MoveRejectionReason.WrongShotMode);

        return PlayHumanVolley(targets, HumanSalvoSize);
    });

    /// <summary>Le scan remplace le tir du tour : l'ordinateur riposte ensuite comme après un tir. Jamais gaté par un défi (pas de navire ciblé).</summary>
    public MoveResult PlayHumanScan(Coordinate origin) => Guarded(() =>
    {
        if (!Options.Radar)
            return new MoveResult.Rejected(MoveRejectionReason.RadarDisabled);

        if (!RadarRules.ZoneFits(origin))
            return new MoveResult.Rejected(MoveRejectionReason.OutOfGrid);

        if (ScansRemaining <= 0)
            return new MoveResult.Rejected(MoveRejectionReason.NoScansLeft);

        var scan = ComputerBoard.ReceiveScan(origin);
        return FinishAttackPhase([], scan, null);
    });

    /// <summary>Une arme remplace tout le tour du joueur, salve comprise ; l'ordinateur riposte ensuite.</summary>
    public MoveResult PlayHumanWeapon(WeaponAction action) => Guarded(() =>
    {
        if (ValidateWeapon(ComputerBoard, HumanArsenal, action) is { } reason)
            return new MoveResult.Rejected(reason);

        HumanArsenal.Consume(action.Kind);
        var cells = WeaponRules.Cells(action)!; // non-null garanti : ValidateWeapon l'a déjà vérifié
        var sequencer = new VolleySequencer(ComputerBoard, cells, stopOnFirstHit: action is WeaponAction.Torpedo, NeedsAttackChallenge);
        return BeginAttack(sequencer, playerScan: null, action.Kind);
    });

    /// <summary>
    /// Squelette commun à tout point d'entrée de coup du joueur : verrouille la partie, refuse tout coup une fois
    /// la partie terminée ou tant qu'un défi de timing est ouvert, puis délègue à <paramref name="body"/> pour la
    /// validation/résolution spécifique à l'action. Centralise le verrou + les contrôles communs pour qu'une
    /// future action de tour n'ait pas à les recopier.
    /// </summary>
    private MoveResult Guarded(Func<MoveResult> body)
    {
        lock (_gate)
        {
            if (Status == GameStatus.Finished)
                return new MoveResult.Rejected(MoveRejectionReason.GameAlreadyFinished);

            if (_pendingChallenge is not null)
                return new MoveResult.Rejected(MoveRejectionReason.ChallengeInProgress);

            return body();
        }
    }

    private MoveResult PlayHumanVolley(IReadOnlyList<Coordinate> targets, int expectedCount)
    {
        if (ValidateVolley(ComputerBoard, targets, expectedCount) is { } reason)
            return new MoveResult.Rejected(reason);

        var sequencer = new VolleySequencer(ComputerBoard, targets, stopOnFirstHit: false, NeedsAttackChallenge);
        return BeginAttack(sequencer, playerScan: null, playerWeapon: null);
    }

    /// <summary>Voir docs/adr/0019-mini-jeu-de-precision.md : une case déclenche un défi d'attaque seulement si elle porte un navire adverse.</summary>
    private bool NeedsAttackChallenge(Coordinate cell) => Options.PrecisionMinigame && ComputerBoard.IsOccupied(cell);

    /// <summary>Voir docs/adr/0019-mini-jeu-de-precision.md : côté défense, seul le coup qui couperait un navire déclenche un défi.</summary>
    private bool NeedsDefenseChallenge(Coordinate cell) => Options.PrecisionMinigame && HumanBoard.WouldSink(cell);

    /// <summary>
    /// Règles de validité d'un lot de tirs, communes au joueur et à l'ordinateur (classique = lot d'un seul tir).
    /// Ne modifie rien : c'est ce qui permet de refuser un lot entier sans appliquer ses premiers tirs.
    /// </summary>
    private static MoveRejectionReason? ValidateVolley(Board target, IReadOnlyList<Coordinate> targets, int expectedCount)
    {
        if (targets.Count != expectedCount)
            return MoveRejectionReason.WrongSalvoSize;

        if (targets.Any(c => !BoardGrid.Contains(c)))
            return MoveRejectionReason.OutOfGrid;

        if (targets.Distinct().Count() != targets.Count)
            return MoveRejectionReason.DuplicateTarget;

        if (targets.Any(c => !target.IsValidTarget(c)))
            return MoveRejectionReason.AlreadyPlayed;

        return null;
    }

    /// <summary>Règles de validité d'une arme, communes au joueur et à l'ordinateur. Ne modifie rien.</summary>
    private MoveRejectionReason? ValidateWeapon(Board target, Arsenal arsenal, WeaponAction action)
    {
        if (!Options.SpecialWeapons)
            return MoveRejectionReason.WeaponsDisabled;

        if (!arsenal.Has(action.Kind))
            return MoveRejectionReason.NoAmmoLeft;

        if (WeaponRules.Cells(action) is not { } cells)
            return MoveRejectionReason.OutOfGrid;

        if (!cells.Any(target.IsValidTarget))
            return MoveRejectionReason.AlreadyPlayed;

        return null;
    }

    /// <summary>
    /// Fait avancer le lot d'attaque du joueur : soit il se termine d'un bloc (comportement historique, cas
    /// majoritaire quand l'option est désactivée), soit il s'arrête sur un défi et le tour reste en suspens.
    /// </summary>
    private MoveResult BeginAttack(VolleySequencer sequencer, ScanResult? playerScan, WeaponKind? playerWeapon)
    {
        var next = sequencer.Advance();
        if (next is { } cell)
        {
            _pendingTurn = new PendingTurn { AttackSequencer = sequencer, PlayerScan = playerScan, PlayerWeapon = playerWeapon };
            return OpenChallenge(ChallengeKind.Attack, cell);
        }

        return FinishAttackPhase(sequencer.Resolutions, playerScan, playerWeapon);
    }

    private MoveResult OpenChallenge(ChallengeKind kind, Coordinate cell)
    {
        _pendingChallenge = new PendingChallenge(Guid.NewGuid(), kind, cell, _clock());
        return new MoveResult.AwaitingChallenge(_pendingChallenge);
    }

    /// <summary>
    /// Le lot d'attaque du joueur est entièrement résolu (avec ou sans défi entre-temps) : victoire immédiate si
    /// la flotte adverse est coulée, sinon on prépare la riposte de l'ordinateur (elle-même potentiellement
    /// suspendue par un défi de défense).
    /// </summary>
    private MoveResult FinishAttackPhase(IReadOnlyList<ShotResolution> playerShots, ScanResult? playerScan, WeaponKind? playerWeapon)
    {
        if (ComputerBoard.AllSunk)
        {
            Winner = PlayerId.Human;
            return FinalizeTurn(playerShots, playerScan, playerWeapon, [], null);
        }

        var (targets, computerWeapon, stopOnFirstHit) = ChooseComputerMove();
        var sequencer = new VolleySequencer(HumanBoard, targets, stopOnFirstHit, NeedsDefenseChallenge);
        return AdvanceDefense(sequencer, playerShots, playerScan, playerWeapon, computerWeapon);
    }

    private MoveResult AdvanceDefense(
        VolleySequencer sequencer, IReadOnlyList<ShotResolution> playerShots, ScanResult? playerScan, WeaponKind? playerWeapon, WeaponKind? computerWeapon)
    {
        var next = sequencer.Advance();
        if (next is { } cell)
        {
            _pendingTurn = new PendingTurn
            {
                PlayerShots = playerShots,
                PlayerScan = playerScan,
                PlayerWeapon = playerWeapon,
                DefenseSequencer = sequencer,
                ComputerWeapon = computerWeapon
            };
            return OpenChallenge(ChallengeKind.Defense, cell);
        }

        return FinalizeTurn(playerShots, playerScan, playerWeapon, sequencer.Resolutions, computerWeapon);
    }

    /// <summary>
    /// Résout le défi de timing actuellement affiché au joueur à partir du seul temps écoulé côté serveur (voir
    /// docs/adr/0019-mini-jeu-de-precision.md) : le client n'est jamais consulté sur le résultat, seulement sur
    /// l'instant où il a signalé l'arrêt. Reprend ensuite le lot (attaque ou défense) suspendu.
    /// </summary>
    public MoveResult ResolveChallenge(Guid challengeId)
    {
        lock (_gate)
        {
            if (_pendingChallenge is not { } challenge || challenge.Id != challengeId)
                return new MoveResult.Rejected(MoveRejectionReason.NoChallengePending);

            var elapsed = _clock() - challenge.StartedAtUtc;
            var success = TimingEvaluator.Succeeds(TimingRules.ZoneStart, TimingRules.ZoneWidth, TimingRules.PeriodMs, elapsed);
            _pendingChallenge = null;
            var pending = _pendingTurn!;

            if (challenge.Kind == ChallengeKind.Attack)
            {
                var attackSequencer = pending.AttackSequencer!;
                // Réussite = tir appliqué normalement ; échec = raté forcé, la case reste consommée.
                var next = attackSequencer.SubmitChallengeResult(success ? ComputerBoard.ReceiveShot : ComputerBoard.ReceiveShotForcedMiss);
                if (next is { } cell)
                    return OpenChallenge(ChallengeKind.Attack, cell);

                _pendingTurn = null;
                return FinishAttackPhase(attackSequencer.Resolutions, pending.PlayerScan, pending.PlayerWeapon);
            }

            var defenseSequencer = pending.DefenseSequencer!;
            // Réussite = le navire esquive ce tir (case toujours rejouable) ; échec = résolution normale (peut couler).
            var defenseNext = defenseSequencer.SubmitChallengeResult(success ? HumanBoard.ReceiveShotDodged : HumanBoard.ReceiveShot);
            if (defenseNext is { } defenseCell)
                return OpenChallenge(ChallengeKind.Defense, defenseCell);

            _pendingTurn = null;
            return FinalizeTurn(pending.PlayerShots!, pending.PlayerScan, pending.PlayerWeapon, defenseSequencer.Resolutions, pending.ComputerWeapon);
        }
    }

    /// <summary>Construit et enregistre le tour terminé (attaque et défense toutes deux résolues) ; seul point qui déclare une victoire par épuisement de la flotte adverse.</summary>
    private MoveResult FinalizeTurn(
        IReadOnlyList<ShotResolution> playerShots, ScanResult? playerScan, WeaponKind? playerWeapon,
        IReadOnlyList<ShotResolution> computerShots, WeaponKind? computerWeapon)
    {
        if (Winner is null && HumanBoard.AllSunk)
            Winner = PlayerId.Computer;

        var turn = new TurnResult(playerShots, playerScan, playerWeapon, computerShots, computerWeapon, Winner, Status);
        _history.Add(turn);
        _pendingTurn = null;
        EvaluateAchievements();
        return new MoveResult.Accepted(turn);
    }

    /// <summary>
    /// Évalue chaque règle de BattleShip.Models.Achievements.AchievementRules.All contre l'état courant de la
    /// partie. Une règle déjà débloquée n'est jamais réévaluée : "ajout seul, jamais retiré" est ainsi garanti
    /// par construction plutôt que par convention.
    /// </summary>
    private void EvaluateAchievements()
    {
        var context = AchievementContext.From(this);
        foreach (var rule in AchievementRules.All)
        {
            if (_achievements.Contains(rule.Id))
                continue;

            if (rule.IsUnlocked(context))
                _achievements.Add(rule.Id);
        }
    }

    /// <summary>
    /// La stratégie ne reçoit que la vue adverse de HumanBoard (jamais Board.Ships). Ses choix repassent par
    /// <see cref="ValidateWeapon"/> ou <see cref="ValidateVolley"/> avant d'être résolus (par l'appelant, via un
    /// <see cref="VolleySequencer"/>, potentiellement gaté par le mini-jeu de précision) sur le même Board que
    /// pour le joueur : une stratégie boguée lève une exception au lieu de jouer un coup interdit au joueur.
    /// </summary>
    private (IReadOnlyList<Coordinate> Targets, WeaponKind? Weapon, bool StopOnFirstHit) ChooseComputerMove()
    {
        var view = HumanBoard.ToOpponentBoardDto();

        if (Options.SpecialWeapons
            && _targeting.PickWeapon(view, ComputerArsenal.Has(WeaponKind.Torpedo), ComputerArsenal.Has(WeaponKind.AirStrike), _rng) is { } weapon)
        {
            if (ValidateWeapon(HumanBoard, ComputerArsenal, weapon) is { } weaponRejection)
                throw new InvalidOperationException($"La stratégie de l'ordinateur a proposé une arme invalide ({weaponRejection}) : {weapon}.");

            ComputerArsenal.Consume(weapon.Kind);
            return (WeaponRules.Cells(weapon)!, weapon.Kind, weapon is WeaponAction.Torpedo);
        }

        var count = Options.ShotMode == ShotMode.Salvo ? SalvoSize(ComputerBoard, HumanBoard) : 1;
        var targets = _targeting.PickTargets(view, count, _rng);

        if (ValidateVolley(HumanBoard, targets, count) is { } reason)
            throw new InvalidOperationException($"La stratégie de l'ordinateur a proposé un tir invalide ({reason}) : {string.Join(", ", targets)}.");

        return (targets, null, false);
    }
}
