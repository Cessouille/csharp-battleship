using BattleShip.Models.Ai;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.TestData;

/// <summary>Parties à flottes minimales et positions connues, pour tester une règle sans dépendre du placement aléatoire.</summary>
public static class TestGames
{
    public static readonly Coordinate HumanShip = new(0, 0);
    public static readonly Coordinate ComputerShip = new(9, 9);

    public static Game SingleCellShips(GameOptions? options = null, IComputerTargeting? targeting = null)
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, HumanShip, Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, ComputerShip, Orientation.Horizontal, 1);
        return new Game(Guid.NewGuid(), human, computer, options, targeting, new Random(0));
    }
}
