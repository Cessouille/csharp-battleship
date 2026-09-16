using BattleShip.Models.Ai;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

public class WeaponTests
{
    private static readonly GameOptions WithWeapons = new() { SpecialWeapons = true };

    /// <summary>Ordinateur : croiseur en (5,3)-(5,6) et torpilleur en (5,8). Joueur : un navire d'une case en (0,0).</summary>
    private static Game CreateGame(GameOptions? options = null, IComputerTargeting? targeting = null)
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Croiseur, new Coordinate(5, 3), Orientation.Horizontal, 4);
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(5, 8), Orientation.Horizontal, 1);
        return new Game(Guid.NewGuid(), human, computer, options ?? WithWeapons, targeting ?? new BottomUpTargeting(), new Random(0));
    }

    [Fact]
    public void Torpedo_StopsAtFirstShip_AndRevealsNothingBeyond()
    {
        var game = CreateGame();

        var result = game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 5));

        var turn = Assert.IsType<MoveResult.Accepted>(result).Turn;
        Assert.Equal(WeaponKind.Torpedo, turn.PlayerWeapon);
        Assert.Equal([new(5, 0), new(5, 1), new(5, 2), new(5, 3)], turn.PlayerShots.Select(s => s.Target));
        Assert.Equal(ShotOutcome.Hit, turn.PlayerShots[^1].Outcome);

        var view = game.ComputerBoard.ToOpponentBoardDto();
        Assert.Equal(3, view.Misses.Count);
        Assert.Equal(new CoordinateDto(5, 3), Assert.Single(view.Hits));
        Assert.True(game.ComputerBoard.IsValidTarget(new Coordinate(5, 4)));
        Assert.Equal(0, game.HumanArsenal.Torpedoes);
    }

    [Fact]
    public void Torpedo_FromRightEdge_TravelsRightToLeft()
    {
        var game = CreateGame();

        var turn = Assert.IsType<MoveResult.Accepted>(game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Right, 5))).Turn;

        Assert.Equal([new(5, 9), new(5, 8)], turn.PlayerShots.Select(s => s.Target));
        Assert.Equal(ShotOutcome.Sunk, turn.PlayerShots[^1].Outcome);
    }

    [Fact]
    public void Torpedo_TraversesAlreadyPlayedCells_WithoutEffect()
    {
        var game = CreateGame();
        game.PlayHumanShot(new Coordinate(5, 0));
        game.PlayHumanShot(new Coordinate(5, 1));

        var turn = Assert.IsType<MoveResult.Accepted>(game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 5))).Turn;

        Assert.Equal([new(5, 2), new(5, 3)], turn.PlayerShots.Select(s => s.Target));
    }

    [Fact]
    public void Torpedo_WhenWholePathAlreadyPlayed_IsRejected_AndKeepsAmmo()
    {
        var game = CreateGame();
        for (var column = 0; column < BoardGrid.Size; column++)
            game.PlayHumanShot(new Coordinate(1, column));
        var shotsBefore = game.ComputerBoard.ShotsReceived.Count;

        var result = game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 1));

        Assert.Equal(MoveRejectionReason.AlreadyPlayed, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Equal(shotsBefore, game.ComputerBoard.ShotsReceived.Count);
        Assert.Equal(WeaponRules.TorpedoesPerGame, game.HumanArsenal.Torpedoes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Torpedo_OnLaneOutsideGrid_IsRejected(int lane)
    {
        var game = CreateGame();

        var result = game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Top, lane));

        Assert.Equal(MoveRejectionReason.OutOfGrid, Assert.IsType<MoveResult.Rejected>(result).Reason);
    }

    [Fact]
    public void SecondTorpedo_IsRejected_NoAmmoLeft_WithoutAnyShot()
    {
        var game = CreateGame();
        Assert.IsType<MoveResult.Accepted>(game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 1)));
        var shotsBefore = game.ComputerBoard.ShotsReceived.Count;
        var humanShotsBefore = game.HumanBoard.ShotsReceived.Count;

        var result = game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 2));

        Assert.Equal(MoveRejectionReason.NoAmmoLeft, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Equal(shotsBefore, game.ComputerBoard.ShotsReceived.Count);
        Assert.Equal(humanShotsBefore, game.HumanBoard.ShotsReceived.Count);
    }

    [Fact]
    public async Task PlayHumanWeapon_ConcurrentTorpedoCalls_OnlyOneIsAccepted()
    {
        // Les munitions sont un compteur partagé (Arsenal.Torpedoes) : sans le verrou de Game.Guarded, deux
        // torpilles concurrentes pourraient toutes deux lire une munition disponible avant que l'une ne l'ait
        // consommée. Random(0) dans CreateGame rend la résolution déterministe malgré la concurrence : un seul
        // appel exécute réellement le corps protégé par le verrou à la fois.
        var game = CreateGame();
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 5));
            }))
            .ToArray();
        start.Set();
        var results = await Task.WhenAll(tasks);

        Assert.Single(results, r => r is MoveResult.Accepted);
        Assert.Equal(31, results.Count(r => r is MoveResult.Rejected { Reason: MoveRejectionReason.NoAmmoLeft }));
        Assert.Equal(0, game.HumanArsenal.Torpedoes);
    }

    [Theory]
    [InlineData(0, 8, Orientation.Horizontal)]
    [InlineData(8, 0, Orientation.Vertical)]
    public void AirStrike_OverflowingGrid_IsRejected_AndKeepsAmmo(int row, int column, Orientation orientation)
    {
        var game = CreateGame();

        var result = game.PlayHumanWeapon(new WeaponAction.AirStrike(new Coordinate(row, column), orientation));

        Assert.Equal(MoveRejectionReason.OutOfGrid, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ShotsReceived);
        Assert.Equal(WeaponRules.AirStrikesPerGame, game.HumanArsenal.AirStrikes);
    }

    [Fact]
    public void AirStrike_SkipsAlreadyPlayedCells()
    {
        var game = CreateGame();
        game.PlayHumanShot(new Coordinate(5, 4));

        var turn = Assert.IsType<MoveResult.Accepted>(
            game.PlayHumanWeapon(new WeaponAction.AirStrike(new Coordinate(5, 3), Orientation.Horizontal))).Turn;

        Assert.Equal(WeaponKind.AirStrike, turn.PlayerWeapon);
        Assert.Equal([new(5, 3), new(5, 5)], turn.PlayerShots.Select(s => s.Target));
        Assert.Equal(0, game.HumanArsenal.AirStrikes);
    }

    [Fact]
    public void AirStrike_SinkingLastShip_StopsImmediately_NoRiposte()
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);
        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(5, 5), Orientation.Horizontal, 1);
        var game = new Game(Guid.NewGuid(), human, computer, WithWeapons, new BottomUpTargeting());

        var turn = Assert.IsType<MoveResult.Accepted>(
            game.PlayHumanWeapon(new WeaponAction.AirStrike(new Coordinate(5, 5), Orientation.Horizontal))).Turn;

        Assert.Single(turn.PlayerShots);
        Assert.Empty(turn.ComputerShots);
        Assert.Equal(PlayerId.Human, game.Winner);
        Assert.True(game.ComputerBoard.IsValidTarget(new Coordinate(5, 6)));
    }

    [Fact]
    public void Weapon_WithWeaponsDisabled_IsRejected()
    {
        var game = CreateGame(GameOptions.Classic);

        var result = game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 5));

        Assert.Equal(MoveRejectionReason.WeaponsDisabled, Assert.IsType<MoveResult.Rejected>(result).Reason);
        Assert.Empty(game.ComputerBoard.ShotsReceived);
    }

    [Fact]
    public void Weapon_InSalvoGame_ReplacesWholeSalvo_ThenComputerSalvo()
    {
        var game = CreateGame(new GameOptions { SpecialWeapons = true, ShotMode = ShotMode.Salvo });

        var turn = Assert.IsType<MoveResult.Accepted>(game.PlayHumanWeapon(new WeaponAction.Torpedo(Edge.Left, 1))).Turn;

        Assert.Equal(BoardGrid.Size, turn.PlayerShots.Count); // ligne 1 vide : la torpille la traverse entièrement
        Assert.Equal(2, turn.ComputerShots.Count);            // l'ordinateur a encore 2 navires à flot
    }

    [Fact]
    public void ComputerWeapon_IsSubjectToSameAmmoRules_AsPlayer()
    {
        var game = CreateGame(targeting: new AlwaysTorpedoTargeting());

        var first = Assert.IsType<MoveResult.Accepted>(game.PlayHumanShot(new Coordinate(9, 0))).Turn;
        Assert.Equal(WeaponKind.Torpedo, first.ComputerWeapon);
        Assert.Equal(0, game.ComputerArsenal.Torpedoes);

        // La stratégie redemande une torpille sans munition : Game la refuse comme pour le joueur.
        Assert.Throws<InvalidOperationException>(() => game.PlayHumanShot(new Coordinate(9, 1)));
    }

    [Fact]
    public void ProbabilityTargeting_OverFullGame_UsesEachWeaponAtMostOnce()
    {
        var game = Game.CreateRandom(Guid.NewGuid(), new Random(11), WithWeapons);
        var computerWeapons = new List<WeaponKind>();

        for (var i = 0; i < BoardGrid.Size * BoardGrid.Size && game.Status == GameStatus.InProgress; i++)
        {
            var target = new Coordinate(i / BoardGrid.Size, i % BoardGrid.Size);
            if (game.ComputerBoard.IsValidTarget(target)
                && game.PlayHumanShot(target) is MoveResult.Accepted { Turn.ComputerWeapon: { } weapon })
                computerWeapons.Add(weapon);
        }

        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.Single(computerWeapons, WeaponKind.Torpedo);
        Assert.True(computerWeapons.Count(w => w == WeaponKind.AirStrike) <= 1);
    }

    [Fact]
    public void ProbabilityTargeting_WithUnsunkHit_PicksAirStrikeCoveringANeighbour()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Croiseur, new Coordinate(5, 4), Orientation.Horizontal, 4);
        board.ReceiveShot(new Coordinate(5, 5));

        var weapon = new ProbabilityTargeting().PickWeapon(board.ToOpponentBoardDto(), torpedoAvailable: true, airStrikeAvailable: true, new Random(0));

        var strike = Assert.IsType<WeaponAction.AirStrike>(weapon);
        Assert.Contains(WeaponRules.Cells(strike)!, c => Math.Abs(c.Row - 5) + Math.Abs(c.Column - 5) == 1);
    }

    [Fact]
    public void ProbabilityTargeting_WithoutHitOrAmmo_UsesNoWeapon()
    {
        var board = new Board();

        var weapon = new ProbabilityTargeting().PickWeapon(board.ToOpponentBoardDto(), torpedoAvailable: false, airStrikeAvailable: true, new Random(0));

        Assert.Null(weapon);
    }
}
