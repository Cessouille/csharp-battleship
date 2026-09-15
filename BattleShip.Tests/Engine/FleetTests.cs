using BattleShip.Models.Domain;

namespace BattleShip.Tests.Engine;

public class FleetTests
{
    [Fact]
    public void Standard_HasExpectedShipCountAndSizes()
    {
        var sizes = Fleet.Standard.Select(s => s.Size).OrderDescending().ToArray();

        Assert.Equal(5, Fleet.Standard.Count);
        Assert.Equal([5, 4, 3, 3, 2], sizes);
    }
}

public class ShipTests
{
    [Fact]
    public void RegisterHit_OnCellNotBelongingToShip_DoesNotAffectIsSunk()
    {
        var ship = new Ship(ShipKind.Torpilleur, [new Coordinate(0, 0), new Coordinate(0, 1)]);

        var registered = ship.RegisterHit(new Coordinate(5, 5));

        Assert.False(registered);
        Assert.False(ship.IsSunk);
    }
}
