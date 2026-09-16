using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

/// <summary>TICKET-10 : journal de partie stocké côté serveur (voir docs/adr/0016-journal-de-partie.md).</summary>
public class GameHistoryTests
{
    /// <summary>Ordinateur : croiseur en (5,3)-(5,6) et torpilleur en (5,8). Joueur : un navire d'une case en (0,0).</summary>
    private static Game CreateGame(GameOptions options)
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Croiseur, new Coordinate(5, 3), Orientation.Horizontal, 4);
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(5, 8), Orientation.Horizontal, 1);
        return new Game(Guid.NewGuid(), human, computer, options, new BottomUpTargeting(), new Random(0));
    }

    [Fact]
    public void History_RecordsShotScanAndWeaponTurns_InOrder_AndNeverARejectedMove()
    {
        var game = CreateGame(new GameOptions { Radar = true, SpecialWeapons = true });

        game.PlayHumanShot(new Coordinate(0, 1)); // rate, ne coule rien
        game.PlayHumanScan(new Coordinate(2, 2)); // zone vide
        var rejected = game.PlayHumanScan(new Coordinate(-1, 0)); // hors grille : refusé
        game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 5)); // touche le croiseur en (5,3)

        Assert.IsType<MoveResult.Rejected>(rejected);
        Assert.Equal(3, game.History.Count);

        Assert.Single(game.History[0].PlayerShots);
        Assert.Null(game.History[0].PlayerScan);
        Assert.Null(game.History[0].PlayerWeapon);

        Assert.Empty(game.History[1].PlayerShots);
        Assert.NotNull(game.History[1].PlayerScan);
        Assert.False(game.History[1].PlayerScan!.ShipDetected);

        Assert.Null(game.History[2].PlayerScan);
        Assert.Equal(WeaponKind.Torpedo, game.History[2].PlayerWeapon);
        Assert.NotEmpty(game.History[2].PlayerShots);
    }

    [Fact]
    public void History_InSalvoGame_RecordsWholeSalvoAsOneEntry()
    {
        var game = Game.CreateRandom(Guid.NewGuid(), new Random(3), new GameOptions { ShotMode = ShotMode.Salvo });
        var salvoSize = game.HumanSalvoSize;
        var targets = Enumerable.Range(0, salvoSize).Select(i => new Coordinate(0, i)).ToList();

        game.PlayHumanSalvo(targets);

        var entry = Assert.Single(game.History);
        Assert.Equal(salvoSize, entry.PlayerShots.Count);
    }
}
