using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

public class RadarTests
{
    private static readonly GameOptions WithRadar = new() { Radar = true };

    [Fact]
    public void PlayHumanScan_OnZoneWithShip_DetectsIt_WithoutMarkingAnyCellAsPlayed()
    {
        var game = TestGames.SingleCellShips(WithRadar);

        var result = game.PlayHumanScan(new Coordinate(8, 8)); // zone (8,8)-(9,9), contient le navire en (9,9)

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.True(accepted.Turn.PlayerScan!.ShipDetected);
        Assert.Empty(accepted.Turn.PlayerShots);
        Assert.Empty(game.ComputerBoard.ShotsReceived);
        Assert.True(game.ComputerBoard.IsValidTarget(TestGames.ComputerShip));
        Assert.False(game.ComputerBoard.Ships.Single().IsSunk);
    }

    [Fact]
    public void PlayHumanScan_OnEmptyZone_ReportsNoShip()
    {
        var game = TestGames.SingleCellShips(WithRadar);

        var accepted = Assert.IsType<MoveResult.Accepted>(game.PlayHumanScan(new Coordinate(0, 0)));

        Assert.False(accepted.Turn.PlayerScan!.ShipDetected);
    }

    [Fact]
    public void PlayHumanScan_ConsumesTurn_ComputerRipostes()
    {
        var game = TestGames.SingleCellShips(WithRadar);

        var accepted = Assert.IsType<MoveResult.Accepted>(game.PlayHumanScan(new Coordinate(0, 0)));

        Assert.Single(accepted.Turn.ComputerShots);
        Assert.Single(game.HumanBoard.ShotsReceived);
        Assert.Equal(RadarRules.ScansPerGame - 1, game.ScansRemaining);
    }

    [Fact]
    public void PlayHumanScan_BeyondQuota_IsRejected_WithoutMutationNorRiposte()
    {
        var game = TestGames.SingleCellShips(WithRadar);
        for (var i = 0; i < RadarRules.ScansPerGame; i++)
            Assert.IsType<MoveResult.Accepted>(game.PlayHumanScan(new Coordinate(0, 2 * i)));
        var humanShotsBefore = game.HumanBoard.ShotsReceived.Count;

        var result = game.PlayHumanScan(new Coordinate(4, 4));

        var rejected = Assert.IsType<MoveResult.Rejected>(result);
        Assert.Equal(MoveRejectionReason.NoScansLeft, rejected.Reason);
        Assert.Equal(RadarRules.ScansPerGame, game.ComputerBoard.ScansReceived.Count);
        Assert.Equal(humanShotsBefore, game.HumanBoard.ShotsReceived.Count);
        Assert.Equal(0, game.ScansRemaining);
    }

    [Fact]
    public async Task PlayHumanScan_ConcurrentCallsBeyondQuota_OnlyQuotaIsAccepted()
    {
        // Le quota de scans est un compteur partagé (ScansRemaining) : sans le verrou de Game.Guarded, des scans
        // concurrents pourraient tous lire un quota disponible avant qu'aucun ne l'ait consommé, et le dépasser.
        // Flotte complète (pas TestGames.SingleCellShips) pour qu'un ou deux tirs de riposte ne puisse pas terminer
        // la partie et fausser l'assertion sur NoScansLeft.
        var game = Game.CreateRandom(Guid.NewGuid(), new Random(7), WithRadar);
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return game.PlayHumanScan(new Coordinate(0, 0));
            }))
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(RadarRules.ScansPerGame, results.Count(r => r is MoveResult.Accepted));
        Assert.Equal(32 - RadarRules.ScansPerGame,
            results.Count(r => r is MoveResult.Rejected { Reason: MoveRejectionReason.NoScansLeft }));
        Assert.Equal(RadarRules.ScansPerGame, game.ComputerBoard.ScansReceived.Count);
    }

    [Fact]
    public void PlayHumanScan_WithRadarDisabled_IsRejected()
    {
        var game = TestGames.SingleCellShips();

        var result = game.PlayHumanScan(new Coordinate(0, 0));

        Assert.Equal(MoveRejectionReason.RadarDisabled, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ScansReceived);
        Assert.Equal(0, game.ScansRemaining);
    }

    [Theory]
    [InlineData(9, 0)]  // la zone déborde en bas
    [InlineData(0, 9)]  // la zone déborde à droite
    [InlineData(-1, 0)]
    [InlineData(0, 10)]
    public void PlayHumanScan_WithZoneOutsideGrid_IsRejected(int row, int column)
    {
        var game = TestGames.SingleCellShips(WithRadar);

        var result = game.PlayHumanScan(new Coordinate(row, column));

        Assert.Equal(MoveRejectionReason.OutOfGrid, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ScansReceived);
    }

    [Fact]
    public void PlayHumanScan_AfterGameFinished_IsRejected()
    {
        var game = TestGames.SingleCellShips(WithRadar);
        game.PlayHumanShot(TestGames.ComputerShip);

        var result = game.PlayHumanScan(new Coordinate(0, 0));

        Assert.Equal(MoveRejectionReason.GameAlreadyFinished, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ScansReceived);
    }
}
