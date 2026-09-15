using BattleShip.Models.Ai;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

public class SalvoTests
{
    private static readonly Coordinate[] HumanShips = [new(0, 0), new(0, 2), new(0, 4)];
    private static readonly Coordinate[] ComputerShips = [new(9, 9), new(9, 7)];

    /// <summary>Joueur : 3 navires d'une case (salve de 3). Ordinateur : 2 navires d'une case (salve de 2).</summary>
    private static Game CreateSalvoGame(IComputerTargeting? targeting = null, GameOptions? options = null)
    {
        var human = new Board();
        var kinds = new[] { ShipKind.Torpilleur, ShipKind.SousMarin, ShipKind.Croiseur };
        for (var i = 0; i < HumanShips.Length; i++)
            human.TryPlaceShip(kinds[i], HumanShips[i], Orientation.Horizontal, 1);

        var computer = new Board();
        for (var i = 0; i < ComputerShips.Length; i++)
            computer.TryPlaceShip(kinds[i], ComputerShips[i], Orientation.Horizontal, 1);

        return new Game(Guid.NewGuid(), human, computer, options ?? new GameOptions { ShotMode = ShotMode.Salvo }, targeting, new Random(0));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void PlayHumanSalvo_WithSizeOtherThanShipsAfloat_IsRejected_WithoutAnyShot(int count)
    {
        var game = CreateSalvoGame();
        var targets = Enumerable.Range(0, count).Select(i => new Coordinate(5, i)).ToList();

        var result = game.PlayHumanSalvo(targets);

        Assert.Equal(MoveRejectionReason.WrongSalvoSize, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ShotsReceived);
        Assert.Empty(game.HumanBoard.ShotsReceived);
    }

    [Fact]
    public void PlayHumanSalvo_WithDuplicateTarget_IsRejected_WithoutAnyShot()
    {
        var game = CreateSalvoGame();

        var result = game.PlayHumanSalvo([new(5, 5), new(5, 6), new(5, 5)]);

        Assert.Equal(MoveRejectionReason.DuplicateTarget, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ShotsReceived);
    }

    [Fact]
    public void PlayHumanSalvo_WithOneAlreadyPlayedCell_AppliesNoneOfTheOtherShots()
    {
        var game = CreateSalvoGame(new FixedTargeting(new(8, 8), new(8, 9)));
        Assert.IsType<MoveResult.Accepted>(game.PlayHumanSalvo([new(5, 5), new(5, 6), new(5, 7)]));
        var shotsBefore = game.ComputerBoard.ShotsReceived.Count;

        var result = game.PlayHumanSalvo([new(6, 0), new(5, 5), new(6, 1)]);

        Assert.Equal(MoveRejectionReason.AlreadyPlayed, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Equal(shotsBefore, game.ComputerBoard.ShotsReceived.Count);
        Assert.True(game.ComputerBoard.IsValidTarget(new Coordinate(6, 0)));
    }

    [Fact]
    public void PlayHumanSalvo_OutOfGridCell_IsRejected_WithoutAnyShot()
    {
        var game = CreateSalvoGame();

        var result = game.PlayHumanSalvo([new(5, 5), new(10, 0), new(5, 6)]);

        Assert.Equal(MoveRejectionReason.OutOfGrid, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ShotsReceived);
    }

    [Fact]
    public void ComputerSalvo_HasOneShotPerComputerShipAfloat()
    {
        var game = CreateSalvoGame(new FixedTargeting(new(8, 8), new(8, 9), new(8, 7)));

        var accepted = Assert.IsType<MoveResult.Accepted>(game.PlayHumanSalvo([new(5, 5), new(5, 6), new(5, 7)]));

        Assert.Equal(3, accepted.Turn.PlayerShots.Count);
        Assert.Equal(ComputerShips.Length, accepted.Turn.ComputerShots.Count);
    }

    [Fact]
    public void LosingAShip_ReducesNextSalvo_ForBothPlayers()
    {
        // Le joueur coule un navire adverse : la riposte ne compte plus qu'un tir. Ce tir coule un navire du
        // joueur : sa salve suivante passe de 3 à 2.
        var game = CreateSalvoGame(new FixedTargeting(HumanShips[0], new(8, 8)));

        var accepted = Assert.IsType<MoveResult.Accepted>(game.PlayHumanSalvo([ComputerShips[0], new(5, 5), new(5, 6)]));

        var computerShot = Assert.Single(accepted.Turn.ComputerShots);
        Assert.Equal(ShotOutcome.Sunk, computerShot.Outcome);
        Assert.Equal(2, game.HumanSalvoSize);
        Assert.Equal(MoveRejectionReason.WrongSalvoSize,
            Assert.IsType<MoveResult.Rejected>(game.PlayHumanSalvo([new(6, 0), new(6, 1), new(6, 2)])).Reason);
    }

    [Fact]
    public void PlayHumanSalvo_SinkingLastShipMidSalvo_StopsImmediately_NoRiposte()
    {
        var game = CreateSalvoGame();

        var result = game.PlayHumanSalvo([ComputerShips[0], ComputerShips[1], new(5, 5)]);

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(2, accepted.Turn.PlayerShots.Count);
        Assert.Empty(accepted.Turn.ComputerShots);
        Assert.Equal(PlayerId.Human, game.Winner);
        Assert.True(game.ComputerBoard.IsValidTarget(new Coordinate(5, 5)));
    }

    [Fact]
    public void PlayHumanShot_InSalvoGame_IsRejected()
    {
        var game = CreateSalvoGame();

        var result = game.PlayHumanShot(new Coordinate(5, 5));

        Assert.Equal(MoveRejectionReason.WrongShotMode, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ShotsReceived);
    }

    [Fact]
    public void PlayHumanSalvo_InClassicGame_IsRejected()
    {
        var game = TestGames.SingleCellShips();

        var result = game.PlayHumanSalvo([new(5, 5)]);

        Assert.Equal(MoveRejectionReason.WrongShotMode, Assert.IsType<MoveResult.Rejected>(result).Reason);
    }

    [Fact]
    public void Scan_InSalvoGame_TriggersFullComputerSalvo()
    {
        var game = CreateSalvoGame(new FixedTargeting(new(8, 8), new(8, 9)), new GameOptions { ShotMode = ShotMode.Salvo, Radar = true });

        var accepted = Assert.IsType<MoveResult.Accepted>(game.PlayHumanScan(new Coordinate(4, 4)));

        Assert.Equal(ComputerShips.Length, accepted.Turn.ComputerShots.Count);
    }

    [Fact]
    public void ComputerSalvo_WithDuplicateTargetsFromStrategy_Throws()
    {
        var game = CreateSalvoGame(new FixedTargeting(new(8, 8), new(8, 8)));

        Assert.Throws<InvalidOperationException>(() => game.PlayHumanSalvo([new(5, 5), new(5, 6), new(5, 7)]));
        Assert.Empty(game.HumanBoard.ShotsReceived);
    }

    [Fact]
    public void ProbabilityTargeting_PicksDistinctUnplayedTargets_ForWholeSalvo()
    {
        var game = Game.CreateRandom(Guid.NewGuid(), new Random(3), new GameOptions { ShotMode = ShotMode.Salvo });

        var targets = new ProbabilityTargeting().PickTargets(game.HumanBoard.ToOpponentBoardDto(), 5, new Random(3));

        Assert.Equal(5, targets.Distinct().Count());
        Assert.All(targets, t => Assert.True(game.HumanBoard.IsValidTarget(t)));
    }
}
