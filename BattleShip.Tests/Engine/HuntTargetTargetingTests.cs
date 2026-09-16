using BattleShip.Models.Ai;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Engine;

public class HuntTargetTargetingTests
{
    private readonly HuntTargetTargeting _targeting = new();

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
    public void PickTargets_WithIsolatedHitInCenter_TargetsAdjacentCell()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Croiseur, new Coordinate(5, 4), Orientation.Horizontal, 4);
        board.ReceiveShot(new Coordinate(5, 5));

        var target = Assert.Single(_targeting.PickTargets(board.ToOpponentBoardDto(), 1, new Random(0)));

        Assert.Equal(1, Math.Abs(target.Row - 5) + Math.Abs(target.Column - 5));
    }

    [Fact]
    public void PickTargets_WithHitInCorner_TargetsAdjacentCellInsideGrid()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2);
        board.ReceiveShot(new Coordinate(0, 0));

        var target = Assert.Single(_targeting.PickTargets(board.ToOpponentBoardDto(), 1, new Random(0)));

        Assert.Contains(target, new[] { new Coordinate(0, 1), new Coordinate(1, 0) });
    }

    [Fact]
    public void PickTargets_WithoutAnyHit_PicksAnyUnplayedCell_NotOnlyNeighboursOfSomething()
    {
        var board = new Board();

        var targets = Enumerable.Range(0, 20)
            .Select(seed => Assert.Single(_targeting.PickTargets(board.ToOpponentBoardDto(), 1, new Random(seed))))
            .ToHashSet();

        Assert.True(targets.Count > 1, "Sans touche en attente, le mode chasse doit varier ses cibles.");
    }

    [Fact]
    public void PickTargets_Salvo_PrioritizesAllAdjacentCellsOfAHit_BeforeHunting()
    {
        // Touche isolée au centre : au plus 4 cases adjacentes valides. Une salve de 4 doit toutes les épuiser
        // avant de retomber sur une case de chasse ; ce test échouerait si le mode chasse était pioché en premier.
        var board = new Board();
        board.TryPlaceShip(ShipKind.Croiseur, new Coordinate(5, 4), Orientation.Horizontal, 4);
        board.ReceiveShot(new Coordinate(5, 5));
        var expectedNeighbours = new[] { new Coordinate(4, 5), new Coordinate(6, 5), new Coordinate(5, 4), new Coordinate(5, 6) };

        var targets = _targeting.PickTargets(board.ToOpponentBoardDto(), 4, new Random(0));

        Assert.Equal(expectedNeighbours.OrderBy(c => c.Row).ThenBy(c => c.Column),
            targets.OrderBy(c => c.Row).ThenBy(c => c.Column));
    }
}
