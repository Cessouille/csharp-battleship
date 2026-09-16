using BattleShip.Models.Ai;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Engine;

public class RandomTargetingTests
{
    private readonly RandomTargeting _targeting = new();

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    public void PickTargets_NeverTargetsPlayedCell_AcrossFullGame(int seed)
    {
        var board = new Board();
        board.PlaceFleetRandomly(Fleet.Standard, new Random(seed));
        var rng = new Random(seed);

        while (!board.AllSunk)
        {
            var target = Assert.Single(_targeting.PickTargets(board.ToOpponentBoardDto(), 1, rng));
            Assert.True(board.IsValidTarget(target), $"Cible déjà jouée ou hors grille : {target}");
            board.ReceiveShot(target);
        }
    }

    [Fact]
    public void PickTargets_IgnoresHits_PicksAnyUnplayedCell()
    {
        // Contrairement à ProbabilityTargeting/HuntTargetTargeting, une touche ne change rien à la sélection :
        // ce test échouerait si RandomTargeting se mettait à cibler préférentiellement les cases adjacentes.
        var board = new Board();
        board.TryPlaceShip(ShipKind.Croiseur, new Coordinate(5, 4), Orientation.Horizontal, 4);
        board.ReceiveShot(new Coordinate(5, 5));

        var targets = Enumerable.Range(0, 20)
            .Select(seed => Assert.Single(_targeting.PickTargets(board.ToOpponentBoardDto(), 1, new Random(seed))))
            .ToHashSet();

        Assert.Contains(targets, t => Math.Abs(t.Row - 5) + Math.Abs(t.Column - 5) > 1);
    }
}
