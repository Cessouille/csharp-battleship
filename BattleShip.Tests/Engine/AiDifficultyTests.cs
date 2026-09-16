using BattleShip.Models.Ai;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

public class AiDifficultyTests
{
    [Theory]
    [InlineData(AiDifficulty.Easy, typeof(RandomTargeting))]
    [InlineData(AiDifficulty.Medium, typeof(HuntTargetTargeting))]
    [InlineData(AiDifficulty.Hard, typeof(ProbabilityTargeting))]
    public void CreateTargeting_MapsDifficultyToExpectedStrategy(AiDifficulty difficulty, Type expectedType)
    {
        var targeting = Game.CreateTargeting(difficulty);

        Assert.IsType(expectedType, targeting);
    }

    [Fact]
    public void Game_WithExplicitTargeting_IgnoresDifficulty()
    {
        // Le seam existant (tests TICKET-03/04) doit continuer à fonctionner : une stratégie explicite prime
        // toujours sur GameOptions.Difficulty, quelle que soit sa valeur.
        var game = TestGames.SingleCellShips(new GameOptions { Difficulty = AiDifficulty.Easy }, new FixedTargeting(TestGames.ComputerShip));

        var result = game.PlayHumanShot(new Coordinate(0, 1)); // manque le navire adverse en (9,9)

        var accepted = Assert.IsType<MoveResult.Accepted>(result);
        Assert.Equal(TestGames.ComputerShip, Assert.Single(accepted.Turn.ComputerShots).Target);
    }

    private static int ShotsToSinkFleet(IComputerTargeting targeting, int seed)
    {
        var board = new Board();
        board.PlaceFleetRandomly(Fleet.Standard, new Random(seed));
        var rng = new Random(seed);
        var shots = 0;

        while (!board.AllSunk)
        {
            var target = Assert.Single(targeting.PickTargets(board.ToOpponentBoardDto(), 1, rng));
            board.ReceiveShot(target);
            shots++;
        }

        return shots;
    }

    [Fact]
    public void AveragePerformance_OrdersEasySlowerThanMediumSlowerThanHard_OnFixedSeeds()
    {
        var seeds = Enumerable.Range(0, 30).ToList();

        var easyAverage = seeds.Average(seed => ShotsToSinkFleet(new RandomTargeting(), seed));
        var mediumAverage = seeds.Average(seed => ShotsToSinkFleet(new HuntTargetTargeting(), seed));
        var hardAverage = seeds.Average(seed => ShotsToSinkFleet(new ProbabilityTargeting(), seed));

        Assert.True(hardAverage < mediumAverage,
            $"Moyenne Difficile {hardAverage:F1} tirs, Moyenne {mediumAverage:F1} tirs.");
        Assert.True(mediumAverage < easyAverage,
            $"Moyenne Moyenne {mediumAverage:F1} tirs, Facile {easyAverage:F1} tirs.");
    }
}
