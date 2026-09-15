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
    GameAlreadyFinished
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
    ShotResolution PlayerShot,
    ShotResolution? ComputerShot,
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

    public Game(Guid id, Board humanBoard, Board computerBoard)
    {
        Id = id;
        HumanBoard = humanBoard;
        ComputerBoard = computerBoard;
    }

    public Guid Id { get; }
    public Board HumanBoard { get; }
    public Board ComputerBoard { get; }
    public PlayerId? Winner { get; private set; }
    public GameStatus Status => Winner is null ? GameStatus.InProgress : GameStatus.Finished;

    public static Game CreateRandom(Guid id, Random rng)
    {
        var human = new Board();
        var computer = new Board();
        human.PlaceFleetRandomly(Fleet.Standard, rng);
        computer.PlaceFleetRandomly(Fleet.Standard, rng);
        return new Game(id, human, computer);
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
    public MoveResult PlayHumanShot(Coordinate target)
    {
        lock (_gate)
        {
            if (Status == GameStatus.Finished)
                return new MoveResult.Rejected(MoveRejectionReason.GameAlreadyFinished);

            if (!BoardGrid.Contains(target))
                return new MoveResult.Rejected(MoveRejectionReason.OutOfGrid);

            if (!ComputerBoard.IsValidTarget(target))
                return new MoveResult.Rejected(MoveRejectionReason.AlreadyPlayed);

            var playerShot = ComputerBoard.ReceiveShot(target);
            if (ComputerBoard.AllSunk)
            {
                Winner = PlayerId.Human;
                return new MoveResult.Accepted(new TurnResult(playerShot, null, Winner, Status));
            }

            var computerTarget = PickComputerTarget(Random.Shared);
            var computerShot = HumanBoard.ReceiveShot(computerTarget);
            if (HumanBoard.AllSunk)
                Winner = PlayerId.Computer;

            return new MoveResult.Accepted(new TurnResult(playerShot, computerShot, Winner, Status));
        }
    }

    /// <summary>
    /// L'ordinateur ne pioche que parmi les cases de HumanBoard non encore jouées, et le tir est résolu par le
    /// même Board.ReceiveShot que celui utilisé pour le joueur : aucun chemin de code séparé ne pourrait
    /// enfreindre les règles de validité pour l'IA.
    /// </summary>
    private Coordinate PickComputerTarget(Random rng)
    {
        var untried = new List<Coordinate>(BoardGrid.Size * BoardGrid.Size);
        for (var row = 0; row < BoardGrid.Size; row++)
            for (var column = 0; column < BoardGrid.Size; column++)
            {
                var c = new Coordinate(row, column);
                if (HumanBoard.IsValidTarget(c))
                    untried.Add(c);
            }

        return untried[rng.Next(untried.Count)];
    }
}
