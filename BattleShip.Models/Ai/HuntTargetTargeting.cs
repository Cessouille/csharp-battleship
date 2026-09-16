using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Ai;

/// <summary>
/// Palier « Moyen » (voir docs/adr/0015-ia-difficulte-reglable.md) : tir au hasard tant qu'aucune touche n'est en
/// attente (mode chasse), puis cible les cases adjacentes à une touche non coulée (mode cible) — sans la grille de
/// densité de <see cref="ProbabilityTargeting"/>. <see cref="OpponentBoardDto.Hits"/> ne contient déjà que les
/// touches sur un navire non coulé (BoardViewMapper), donc toute case de <c>Hits</c> est une touche « active ».
/// N'utilise jamais les armes spéciales (comportement par défaut de l'interface).
/// </summary>
public sealed class HuntTargetTargeting : IComputerTargeting
{
    public IReadOnlyList<Coordinate> PickTargets(OpponentBoardDto view, int count, Random rng)
    {
        var played = ProbabilityTargeting.PlayedCells(view);

        var targetCandidates = view.Hits
            .SelectMany(h => Neighbours(new Coordinate(h.Row, h.Column), view.Size))
            .Where(c => !played.Contains(c))
            .Distinct()
            .ToArray();
        rng.Shuffle(targetCandidates);

        var huntCandidates = Enumerable.Range(0, view.Size * view.Size)
            .Select(i => new Coordinate(i / view.Size, i % view.Size))
            .Where(c => !played.Contains(c) && !targetCandidates.Contains(c))
            .ToArray();
        rng.Shuffle(huntCandidates);

        var picked = targetCandidates.Concat(huntCandidates).Take(count).ToList();
        if (picked.Count < count)
            throw new InvalidOperationException($"{count} cibles demandées pour {picked.Count} cases non jouées.");

        return picked;
    }

    private static IEnumerable<Coordinate> Neighbours(Coordinate c, int size)
    {
        if (c.Row > 0) yield return c with { Row = c.Row - 1 };
        if (c.Row < size - 1) yield return c with { Row = c.Row + 1 };
        if (c.Column > 0) yield return c with { Column = c.Column - 1 };
        if (c.Column < size - 1) yield return c with { Column = c.Column + 1 };
    }
}
