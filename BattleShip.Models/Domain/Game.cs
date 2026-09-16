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
    NoAmmoLeft
}

public abstract record MoveResult
{
    private MoveResult()
    {
    }

    public sealed record Accepted(TurnResult Turn) : MoveResult;

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

    public Game(
        Guid id,
        Board humanBoard,
        Board computerBoard,
        GameOptions? options = null,
        IComputerTargeting? targeting = null,
        Random? rng = null)
    {
        Id = id;
        HumanBoard = humanBoard;
        ComputerBoard = computerBoard;
        Options = options ?? GameOptions.Classic;
        _targeting = targeting ?? new ProbabilityTargeting();
        _rng = rng ?? Random.Shared;
    }

    public Guid Id { get; }
    public GameOptions Options { get; }
    public Arsenal HumanArsenal { get; } = new();
    public Arsenal ComputerArsenal { get; } = new();
    public Board HumanBoard { get; }
    public Board ComputerBoard { get; }
    public PlayerId? Winner { get; private set; }
    public GameStatus Status => Winner is null ? GameStatus.InProgress : GameStatus.Finished;

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

    /// <summary>Le scan remplace le tir du tour : l'ordinateur riposte ensuite comme après un tir.</summary>
    public MoveResult PlayHumanScan(Coordinate origin) => Guarded(() =>
    {
        if (!Options.Radar)
            return new MoveResult.Rejected(MoveRejectionReason.RadarDisabled);

        if (!RadarRules.ZoneFits(origin))
            return new MoveResult.Rejected(MoveRejectionReason.OutOfGrid);

        if (ScansRemaining <= 0)
            return new MoveResult.Rejected(MoveRejectionReason.NoScansLeft);

        var scan = ComputerBoard.ReceiveScan(origin);
        return CompleteTurn([], scan, null);
    });

    /// <summary>Une arme remplace tout le tour du joueur, salve comprise ; l'ordinateur riposte ensuite.</summary>
    public MoveResult PlayHumanWeapon(WeaponAction action) => Guarded(() =>
    {
        if (ValidateWeapon(ComputerBoard, HumanArsenal, action) is { } reason)
            return new MoveResult.Rejected(reason);

        HumanArsenal.Consume(action.Kind);
        return CompleteTurn(ComputerBoard.ReceiveWeapon(action), null, action.Kind);
    });

    /// <summary>
    /// Squelette commun à tout point d'entrée de coup du joueur : verrouille la partie, refuse tout coup une fois
    /// la partie terminée, puis délègue à <paramref name="body"/> pour la validation/résolution spécifique à
    /// l'action. Centralise le verrou + le contrôle de fin de partie pour qu'une future action de tour n'ait pas
    /// à les recopier.
    /// </summary>
    private MoveResult Guarded(Func<MoveResult> body)
    {
        lock (_gate)
        {
            if (Status == GameStatus.Finished)
                return new MoveResult.Rejected(MoveRejectionReason.GameAlreadyFinished);

            return body();
        }
    }

    private MoveResult PlayHumanVolley(IReadOnlyList<Coordinate> targets, int expectedCount)
    {
        if (ValidateVolley(ComputerBoard, targets, expectedCount) is { } reason)
            return new MoveResult.Rejected(reason);

        return CompleteTurn(ResolveVolley(ComputerBoard, targets), null, null);
    }

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

    private static List<ShotResolution> ResolveVolley(Board target, IReadOnlyList<Coordinate> targets)
    {
        var resolutions = new List<ShotResolution>(targets.Count);
        foreach (var coordinate in targets)
        {
            resolutions.Add(target.ReceiveShot(coordinate));
            if (target.AllSunk)
                break;
        }

        return resolutions;
    }

    /// <summary>Fin commune à toute action du joueur déjà appliquée : victoire immédiate, sinon riposte de l'ordinateur.</summary>
    private MoveResult CompleteTurn(IReadOnlyList<ShotResolution> playerShots, ScanResult? playerScan, WeaponKind? playerWeapon)
    {
        if (ComputerBoard.AllSunk)
        {
            Winner = PlayerId.Human;
            return new MoveResult.Accepted(new TurnResult(playerShots, playerScan, playerWeapon, [], null, Winner, Status));
        }

        var (computerShots, computerWeapon) = PlayComputerTurn();
        if (HumanBoard.AllSunk)
            Winner = PlayerId.Computer;

        return new MoveResult.Accepted(
            new TurnResult(playerShots, playerScan, playerWeapon, computerShots, computerWeapon, Winner, Status));
    }

    /// <summary>
    /// La stratégie ne reçoit que la vue adverse de HumanBoard (jamais Board.Ships). Ses choix repassent par
    /// <see cref="ValidateWeapon"/> ou <see cref="ValidateVolley"/>, puis sont résolus par le même Board que pour
    /// le joueur : une stratégie boguée lève une exception au lieu de jouer un coup interdit au joueur.
    /// </summary>
    private (IReadOnlyList<ShotResolution> Shots, WeaponKind? Weapon) PlayComputerTurn()
    {
        var view = HumanBoard.ToOpponentBoardDto();

        if (Options.SpecialWeapons
            && _targeting.PickWeapon(view, ComputerArsenal.Has(WeaponKind.Torpedo), ComputerArsenal.Has(WeaponKind.AirStrike), _rng) is { } weapon)
        {
            if (ValidateWeapon(HumanBoard, ComputerArsenal, weapon) is { } weaponRejection)
                throw new InvalidOperationException($"La stratégie de l'ordinateur a proposé une arme invalide ({weaponRejection}) : {weapon}.");

            ComputerArsenal.Consume(weapon.Kind);
            return (HumanBoard.ReceiveWeapon(weapon), weapon.Kind);
        }

        var count = Options.ShotMode == ShotMode.Salvo ? SalvoSize(ComputerBoard, HumanBoard) : 1;
        var targets = _targeting.PickTargets(view, count, _rng);

        if (ValidateVolley(HumanBoard, targets, count) is { } reason)
            throw new InvalidOperationException($"La stratégie de l'ordinateur a proposé un tir invalide ({reason}) : {string.Join(", ", targets)}.");

        return (ResolveVolley(HumanBoard, targets), null);
    }
}
