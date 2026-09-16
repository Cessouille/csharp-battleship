using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

public class GameTests
{
    private static Game CreateGameWithSingleCellShips() => TestGames.SingleCellShips();

    [Fact]
    public void PlayHumanShot_OnAlreadyPlayedCell_IsRejected_AndDoesNotMutateState()
    {
        var game = CreateGameWithSingleCellShips();
        var target = new Coordinate(0, 0); // ne coule pas l'unique navire adverse (9,9)
        game.PlayHumanShot(target);
        var hitsBefore = game.ComputerBoard.ShotsReceived.Count;

        var result = game.PlayHumanShot(target);

        Assert.IsType<MoveResult.Rejected>(result);
        Assert.Equal(MoveRejectionReason.AlreadyPlayed, ((MoveResult.Rejected)result).Reason);
        Assert.Equal(hitsBefore, game.ComputerBoard.ShotsReceived.Count);
    }

    [Theory]
    [MemberData(nameof(OutOfGridCoordinates.Values), MemberType = typeof(OutOfGridCoordinates))]
    public void PlayHumanShot_OnOutOfGridCoordinate_IsRejected(int row, int column)
    {
        var game = CreateGameWithSingleCellShips();

        var result = game.PlayHumanShot(new Coordinate(row, column));

        Assert.IsType<MoveResult.Rejected>(result);
        Assert.Equal(MoveRejectionReason.OutOfGrid, ((MoveResult.Rejected)result).Reason);
    }

    [Fact]
    public void PlayHumanShot_AfterGameFinished_IsRejected_AndComputerBoardUnaffected()
    {
        var game = CreateGameWithSingleCellShips();
        game.PlayHumanShot(new Coordinate(9, 9)); // coule l'unique navire adverse -> partie finie
        Assert.Equal(GameStatus.Finished, game.Status);
        var shotsBefore = game.ComputerBoard.ShotsReceived.Count;

        var result = game.PlayHumanShot(new Coordinate(5, 5));

        Assert.IsType<MoveResult.Rejected>(result);
        Assert.Equal(MoveRejectionReason.GameAlreadyFinished, ((MoveResult.Rejected)result).Reason);
        Assert.Equal(shotsBefore, game.ComputerBoard.ShotsReceived.Count);
    }

    [Fact]
    public void PlayHumanShot_SinkingLastEnemyShip_EndsGame_NoComputerCounterShot()
    {
        var game = CreateGameWithSingleCellShips();

        var result = game.PlayHumanShot(new Coordinate(9, 9));

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(ShotOutcome.Sunk, Assert.Single(accepted.Turn.PlayerShots).Outcome);
        Assert.Empty(accepted.Turn.ComputerShots);
        Assert.Equal(PlayerId.Human, accepted.Turn.Winner);
        Assert.Equal(GameStatus.Finished, accepted.Turn.Status);
        Assert.Equal(GameStatus.Finished, game.Status);
    }

    [Fact]
    public void PlayHumanShot_OnMissedCell_ReturnsComputerCounterShot_GameContinues()
    {
        var game = CreateGameWithSingleCellShips();

        var result = game.PlayHumanShot(new Coordinate(0, 1)); // manque le navire adverse en (9,9)

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(ShotOutcome.Miss, Assert.Single(accepted.Turn.PlayerShots).Outcome);
        Assert.Single(accepted.Turn.ComputerShots);
        Assert.Equal(GameStatus.InProgress, accepted.Turn.Status);
    }

    [Fact]
    public async Task PlayHumanShot_ConcurrentCallsOnSameCell_OnlyOneIsAccepted()
    {
        // Un Game est un singleton partagé côté serveur (InMemoryGameStore) : deux requêtes concurrentes sur le
        // même gameId peuvent toutes deux passer IsValidTarget avant que l'une n'appelle ReceiveShot si l'accès
        // n'est pas sérialisé. Ce test échouerait (plus d'un Accepted) sans le verrou dans Game.PlayHumanShot.
        var game = Game.CreateRandom(Guid.NewGuid(), new Random(7));
        var target = new Coordinate(0, 0);
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return game.PlayHumanShot(target);
            }))
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r is MoveResult.Accepted);
        Assert.Equal(31, results.Count(r => r is MoveResult.Rejected { Reason: MoveRejectionReason.AlreadyPlayed }));
        Assert.Equal(1, game.ComputerBoard.ShotsReceived.Count(c => c == target));
    }

    private static readonly (ShipKind Kind, Coordinate Origin, Orientation Orientation)[] ValidStandardPlacements =
    [
        (ShipKind.PorteAvions, new Coordinate(0, 0), Orientation.Horizontal),
        (ShipKind.Croiseur, new Coordinate(2, 0), Orientation.Horizontal),
        (ShipKind.ContreTorpilleur, new Coordinate(4, 0), Orientation.Horizontal),
        (ShipKind.SousMarin, new Coordinate(6, 0), Orientation.Horizontal),
        (ShipKind.Torpilleur, new Coordinate(8, 0), Orientation.Horizontal),
    ];

    [Fact]
    public void TryCreateManual_WithValidStandardPlacements_CreatesGameWithThatExactHumanFleet()
    {
        var result = Game.TryCreateManual(Guid.NewGuid(), ValidStandardPlacements, new Random(1));

        var created = Assert.IsType<CreateGameResult.Created>(result);
        Assert.Equal(Fleet.Standard.Count, created.Game.HumanBoard.Ships.Count);
        Assert.Contains(created.Game.HumanBoard.Ships, s => s.Kind == ShipKind.PorteAvions && s.Cells[0] == new Coordinate(0, 0));
    }

    [Fact]
    public void TryCreateManual_ComputerBoardIsStillPlacedRandomly()
    {
        var result = Game.TryCreateManual(Guid.NewGuid(), ValidStandardPlacements, new Random(1));

        var created = Assert.IsType<CreateGameResult.Created>(result);
        Assert.Equal(Fleet.Standard.Count, created.Game.ComputerBoard.Ships.Count);
        Assert.Equal(Fleet.Standard.Sum(f => f.Size), created.Game.ComputerBoard.Ships.Sum(s => s.Cells.Count));
    }

    [Fact]
    public void TryCreateManual_WithWrongFleetComposition_ReturnsRejected_InvalidFleetComposition()
    {
        var placements = new (ShipKind Kind, Coordinate Origin, Orientation Orientation)[]
        {
            (ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal),
            (ShipKind.Torpilleur, new Coordinate(2, 0), Orientation.Horizontal), // doublon, PorteAvions manquant
            (ShipKind.ContreTorpilleur, new Coordinate(4, 0), Orientation.Horizontal),
            (ShipKind.SousMarin, new Coordinate(6, 0), Orientation.Horizontal),
            (ShipKind.Croiseur, new Coordinate(8, 0), Orientation.Horizontal),
        };

        var result = Game.TryCreateManual(Guid.NewGuid(), placements, new Random(1));

        var rejected = Assert.IsType<CreateGameResult.Rejected>(result);
        Assert.Equal(FleetPlacementRejectionReason.InvalidFleetComposition, rejected.Reason);
    }

    [Fact]
    public void TryCreateManual_WithOverlappingPlacements_ReturnsRejected_OutOfGridOrOverlap()
    {
        var placements = new (ShipKind Kind, Coordinate Origin, Orientation Orientation)[]
        {
            (ShipKind.PorteAvions, new Coordinate(0, 0), Orientation.Horizontal),
            (ShipKind.Croiseur, new Coordinate(0, 2), Orientation.Vertical), // chevauche PorteAvions
            (ShipKind.ContreTorpilleur, new Coordinate(4, 0), Orientation.Horizontal),
            (ShipKind.SousMarin, new Coordinate(6, 0), Orientation.Horizontal),
            (ShipKind.Torpilleur, new Coordinate(8, 0), Orientation.Horizontal),
        };

        var result = Game.TryCreateManual(Guid.NewGuid(), placements, new Random(1));

        var rejected = Assert.IsType<CreateGameResult.Rejected>(result);
        Assert.Equal(FleetPlacementRejectionReason.OutOfGridOrOverlap, rejected.Reason);
    }

    [Fact]
    public void ComputerShot_UsesInjectedStrategy()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 9), Orientation.Horizontal, 1);
        var game = new Game(Guid.NewGuid(), human, computer, targeting: new FixedTargeting(new Coordinate(3, 3)));

        var result = game.PlayHumanShot(new Coordinate(0, 1));

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(new Coordinate(3, 3), Assert.Single(accepted.Turn.ComputerShots).Target);
    }

    [Fact]
    public void ComputerShot_InvalidTargetFromStrategy_Throws()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 9), Orientation.Horizontal, 1);
        var game = new Game(Guid.NewGuid(), human, computer, targeting: new FixedTargeting(new Coordinate(-1, 0)));

        Assert.Throws<InvalidOperationException>(() => game.PlayHumanShot(new Coordinate(0, 1)));
        Assert.Empty(game.HumanBoard.ShotsReceived);
    }

    [Fact]
    public void ComputerShot_SameVisibleHistory_DifferentHiddenFleets_SameTarget()
    {
        // Même historique visible sur le plateau humain (une touche en (5,5), un raté en (0,0)) mais navires
        // cachés placés différemment : si l'IA lisait les vraies positions, les deux cibles pourraient diverger.
        Coordinate CounterShotTarget(Orientation hiddenOrientation, Coordinate hiddenOrigin)
        {
            var human = new Board();
            human.TryPlaceShip(ShipKind.Croiseur, hiddenOrigin, hiddenOrientation, 4);
            human.ReceiveShot(new Coordinate(5, 5));
            human.ReceiveShot(new Coordinate(0, 0));
            var computer = new Board();
            computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(9, 9), Orientation.Horizontal, 1);
            var game = new Game(Guid.NewGuid(), human, computer, rng: new Random(5));

            var accepted = Assert.IsType<MoveResult.Accepted>(game.PlayHumanShot(new Coordinate(0, 1)));
            return Assert.Single(accepted.Turn.ComputerShots).Target;
        }

        var horizontal = CounterShotTarget(Orientation.Horizontal, new Coordinate(5, 5));
        var vertical = CounterShotTarget(Orientation.Vertical, new Coordinate(5, 5));

        Assert.Equal(horizontal, vertical);
    }

    [Fact]
    public void ComputerAutoShot_OnlyTargetsUntriedCellsOnHumanBoard_AcrossFullGame()
    {
        var rng = new Random(42);
        var game = Game.CreateRandom(Guid.NewGuid(), rng);
        var computerTargets = new List<Coordinate>();

        for (var row = 0; row < BoardGrid.Size && game.Status == GameStatus.InProgress; row++)
        {
            for (var column = 0; column < BoardGrid.Size && game.Status == GameStatus.InProgress; column++)
            {
                var target = new Coordinate(row, column);
                if (!game.ComputerBoard.IsValidTarget(target))
                    continue;

                var result = game.PlayHumanShot(target);
                if (result is MoveResult.Accepted accepted)
                    computerTargets.AddRange(accepted.Turn.ComputerShots.Select(s => s.Target));
            }
        }

        Assert.Equal(computerTargets.Count, computerTargets.Distinct().Count());
        Assert.All(computerTargets, c => Assert.True(BoardGrid.Contains(c)));
    }
}
