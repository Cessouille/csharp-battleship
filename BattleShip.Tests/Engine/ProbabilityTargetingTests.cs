using BattleShip.Models.Ai;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine;

public class ProbabilityTargetingTests
{
    private readonly ProbabilityTargeting _targeting = new();

    private static int ShotsToSinkFleet(IComputerTargeting targeting, int seed)
    {
        var board = new Board();
        board.PlaceFleetRandomly(Fleet.Standard, new Random(seed));
        var rng = new Random(seed);
        var shots = 0;

        while (!board.AllSunk)
        {
            var target = Assert.Single(targeting.PickTargets(board.ToOpponentBoardDto(), 1, rng));
            Assert.True(board.IsValidTarget(target), $"Cible déjà jouée ou hors grille : {target}");
            board.ReceiveShot(target);
            shots++;
        }

        return shots;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(123)]
    public void PickTarget_NeverTargetsPlayedCell_AcrossFullGames(int seed)
    {
        var shots = ShotsToSinkFleet(_targeting, seed);

        Assert.InRange(shots, Fleet.Standard.Sum(f => f.Size), BoardGrid.Size * BoardGrid.Size);
    }

    [Fact]
    public void PickTarget_WithIsolatedHitInCenter_TargetsAdjacentCell()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Croiseur, new Coordinate(5, 4), Orientation.Horizontal, 4);
        board.ReceiveShot(new Coordinate(5, 5));

        var target = Assert.Single(_targeting.PickTargets(board.ToOpponentBoardDto(), 1, new Random(0)));

        Assert.Equal(1, Math.Abs(target.Row - 5) + Math.Abs(target.Column - 5));
    }

    [Fact]
    public void PickTarget_WithHitInCorner_TargetsAdjacentCellInsideGrid()
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 2);
        board.ReceiveShot(new Coordinate(0, 0));

        var target = Assert.Single(_targeting.PickTargets(board.ToOpponentBoardDto(), 1, new Random(0)));

        Assert.Contains(target, new[] { new Coordinate(0, 1), new Coordinate(1, 0) });
    }

    [Fact]
    public void ComputeDensity_CellSurroundedByMissesTooSmallForAnyRemainingShip_IsZero()
    {
        var board = new Board();
        foreach (var miss in new[] { new Coordinate(4, 5), new Coordinate(6, 5), new Coordinate(5, 4), new Coordinate(5, 6) })
            board.ReceiveShot(miss);

        var density = ProbabilityTargeting.ComputeDensity(board.ToOpponentBoardDto());

        Assert.Equal(0, density[5, 5]);
        Assert.True(density[0, 0] > 0);
    }

    [Fact]
    public void ComputeDensity_IgnoresSunkShipSizes()
    {
        // Seul le porte-avions (5) reste à flot : aucune case ne peut accueillir un navire dans un couloir de 4.
        var board = new Board();
        var others = new (ShipKind Kind, int Row)[]
        {
            (ShipKind.Croiseur, 0), (ShipKind.ContreTorpilleur, 2), (ShipKind.SousMarin, 4), (ShipKind.Torpilleur, 6)
        };
        foreach (var (kind, row) in others)
        {
            var size = Fleet.Standard.Single(f => f.Kind == kind).Size;
            board.TryPlaceShip(kind, new Coordinate(row, 0), Orientation.Horizontal, size);
            for (var column = 0; column < size; column++)
                board.ReceiveShot(new Coordinate(row, column));
        }

        // Couloir horizontal de 4 cases libres en ligne 9, fermé par des ratés et par le bord.
        foreach (var miss in new[] { new Coordinate(9, 4), new Coordinate(8, 0), new Coordinate(8, 1), new Coordinate(8, 2), new Coordinate(8, 3) })
            board.ReceiveShot(miss);

        var density = ProbabilityTargeting.ComputeDensity(board.ToOpponentBoardDto());

        Assert.Equal(0, density[9, 0]);
    }

    [Fact]
    public void ProbabilityTargeting_SinksFleetInFewerShotsThanRandom_OnFixedSeeds()
    {
        var seeds = Enumerable.Range(0, 30).ToList();

        var probabilityAverage = seeds.Average(seed => ShotsToSinkFleet(_targeting, seed));
        var randomAverage = seeds.Average(seed => ShotsToSinkFleet(new RandomTargeting(), seed));

        Assert.True(probabilityAverage < randomAverage,
            $"Moyenne probabiliste {probabilityAverage:F1} tirs, aléatoire {randomAverage:F1} tirs.");
    }
}
