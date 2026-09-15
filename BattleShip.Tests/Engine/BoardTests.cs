using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

public class BoardTests
{
    [Theory]
    [InlineData(0, 8, 0, 5)] // horizontal, dépasse la colonne 9
    [InlineData(8, 0, 1, 5)] // vertical, dépasse la ligne 9
    [InlineData(-1, 0, 0, 3)]
    [InlineData(0, -1, 1, 3)]
    public void TryPlaceShip_RejectsOutOfGridPlacement(int row, int column, int orientationValue, int size)
    {
        var board = new Board();

        var placed = board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(row, column), (Orientation)orientationValue, size);

        Assert.False(placed);
        Assert.Empty(board.Ships);
    }

    [Fact]
    public void TryPlaceShip_RejectsOverlapWithExistingShip()
    {
        var board = new Board();
        Assert.True(board.TryPlaceShip(ShipKind.Croiseur, new Coordinate(2, 2), Orientation.Horizontal, 4));

        var overlapped = board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(2, 3), Orientation.Vertical, 2);

        Assert.False(overlapped);
        Assert.Single(board.Ships);
    }

    [Fact]
    public void PlaceFleetRandomly_ProducesExactlyStandardFleet_NoOverlap_NoOutOfBounds()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var board = new Board();

            board.PlaceFleetRandomly(Fleet.Standard, new Random(seed));

            Assert.Equal(Fleet.Standard.Count, board.Ships.Count);

            var allCells = board.Ships.SelectMany(s => s.Cells).ToList();
            Assert.Equal(Fleet.Standard.Sum(f => f.Size), allCells.Count);
            Assert.Equal(allCells.Count, allCells.Distinct().Count());
            Assert.All(allCells, c => Assert.True(BoardGrid.Contains(c)));
        }
    }

    [Fact]
    public void ReceiveShot_OnEmptyCell_ReturnsMiss_NoShipMutated()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2);

        var resolution = board.ReceiveShot(new Coordinate(5, 5));

        Assert.Equal(ShotOutcome.Miss, resolution.Outcome);
        Assert.Null(resolution.SunkShipKind);
        Assert.False(board.Ships[0].IsSunk);
    }

    [Fact]
    public void ReceiveShot_OnShipCell_ReturnsHit_ShipNotYetSunk()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2);

        var resolution = board.ReceiveShot(new Coordinate(0, 0));

        Assert.Equal(ShotOutcome.Hit, resolution.Outcome);
        Assert.False(board.Ships[0].IsSunk);
    }

    [Fact]
    public void ReceiveShot_OnLastRemainingCellOfShip_ReturnsSunk()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2);
        board.ReceiveShot(new Coordinate(0, 0));

        var resolution = board.ReceiveShot(new Coordinate(0, 1));

        Assert.Equal(ShotOutcome.Sunk, resolution.Outcome);
        Assert.Equal(ShipKind.Torpilleur, resolution.SunkShipKind);
        Assert.True(board.Ships[0].IsSunk);
    }

    [Fact]
    public void IsValidTarget_ReturnsFalseForAlreadyShotCell()
    {
        var board = new Board();
        var target = new Coordinate(3, 3);
        board.ReceiveShot(target);

        Assert.False(board.IsValidTarget(target));
    }

    [Theory]
    [MemberData(nameof(OutOfGridCoordinates.Values), MemberType = typeof(OutOfGridCoordinates))]
    public void IsValidTarget_ReturnsFalseForOutOfGridCoordinate(int row, int column)
    {
        var board = new Board();

        Assert.False(board.IsValidTarget(new Coordinate(row, column)));
    }
}
