using BattleShip.Models.Ai;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Tests.TestData;

/// <summary>Référence de comparaison : tir uniforme parmi les cases non jouées (ancienne stratégie du socle, ADR 0004).</summary>
public sealed class RandomTargeting : IComputerTargeting
{
    public IReadOnlyList<Coordinate> PickTargets(OpponentBoardDto view, int count, Random rng)
    {
        var played = view.Hits.Concat(view.Misses).Concat(view.SunkShips.SelectMany(s => s.Cells))
            .Select(c => new Coordinate(c.Row, c.Column)).ToHashSet();
        var untried = Enumerable.Range(0, view.Size * view.Size)
            .Select(i => new Coordinate(i / view.Size, i % view.Size))
            .Where(c => !played.Contains(c))
            .ToArray();
        rng.Shuffle(untried);
        return untried.Take(count).ToList();
    }
}

/// <summary>Stratégie qui renvoie toujours les mêmes cases, pour vérifier comment Game traite les cibles proposées.</summary>
public sealed class FixedTargeting(params Coordinate[] targets) : IComputerTargeting
{
    public IReadOnlyList<Coordinate> PickTargets(OpponentBoardDto view, int count, Random rng) => targets.Take(count).ToList();
}

/// <summary>Tire sur les cases non jouées en partant du coin bas-droit : riposte prévisible qui n'atteint (0,0) qu'en dernier.</summary>
public sealed class BottomUpTargeting : IComputerTargeting
{
    public IReadOnlyList<Coordinate> PickTargets(OpponentBoardDto view, int count, Random rng)
    {
        var played = view.Hits.Concat(view.Misses).Concat(view.SunkShips.SelectMany(s => s.Cells))
            .Select(c => new Coordinate(c.Row, c.Column)).ToHashSet();
        return Enumerable.Range(0, view.Size * view.Size).Reverse()
            .Select(i => new Coordinate(i / view.Size, i % view.Size))
            .Where(c => !played.Contains(c))
            .Take(count)
            .ToList();
    }
}

/// <summary>Veut toujours tirer une torpille, qu'il lui en reste ou non : sert à vérifier que Game applique à l'IA les mêmes quotas qu'au joueur.</summary>
public sealed class AlwaysTorpedoTargeting : IComputerTargeting
{
    private int _nextLane = BoardGrid.Size - 1;

    public IReadOnlyList<Coordinate> PickTargets(OpponentBoardDto view, int count, Random rng) =>
        new BottomUpTargeting().PickTargets(view, count, rng);

    public WeaponAction? PickWeapon(OpponentBoardDto view, bool torpedoAvailable, bool airStrikeAvailable, Random rng) =>
        new WeaponAction.Torpedo(Edge.Left, _nextLane--);
}
