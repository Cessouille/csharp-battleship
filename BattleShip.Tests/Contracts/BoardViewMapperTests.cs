using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.Contracts;

public class BoardViewMapperTests
{
    [Fact]
    public void ToOpponentBoardDto_WithNoShotsTaken_ExposesNothing()
    {
        var board = new Board();
        board.PlaceFleetRandomly(Fleet.Standard, new Random(1));

        var dto = board.ToOpponentBoardDto();

        Assert.Empty(dto.Hits);
        Assert.Empty(dto.Misses);
        Assert.Empty(dto.SunkShips);
    }

    [Fact]
    public void ToOpponentBoardDto_RevealsOnlyShotCells_AndSunkShipsInFull()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2);
        board.TryPlaceShip(ShipKind.SousMarin, new Coordinate(5, 5), Orientation.Horizontal, 3);

        board.ReceiveShot(new Coordinate(0, 0)); // touche mais ne coule pas
        board.ReceiveShot(new Coordinate(0, 1)); // coule le torpilleur
        board.ReceiveShot(new Coordinate(9, 9)); // manque

        var dto = board.ToOpponentBoardDto();

        Assert.Empty(dto.Hits); // le seul hit non-coulé (0,0) a été absorbé par le navire désormais coulé
        Assert.Single(dto.Misses);
        var sunk = Assert.Single(dto.SunkShips);
        Assert.Equal("Torpilleur", sunk.Kind);
        Assert.Equal(2, sunk.Cells.Count);
        Assert.DoesNotContain(dto.SunkShips, s => s.Kind == "SousMarin"); // navire non touché : jamais révélé
    }

    [Fact]
    public void ToMyBoardDto_ExposesFullOwnFleetRegardlessOfShots()
    {
        var board = new Board();
        board.PlaceFleetRandomly(Fleet.Standard, new Random(2));

        var dto = board.ToMyBoardDto();

        Assert.Equal(Fleet.Standard.Count, dto.Ships.Count);
        Assert.All(dto.Ships, s => Assert.NotEmpty(s.Cells));
    }
}
