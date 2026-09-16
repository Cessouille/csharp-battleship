using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Ai;

/// <summary>
/// Tir uniforme parmi les cases non jouées : comportement du socle avant l'ADR 0008, réintroduit comme palier
/// « Facile » (voir docs/adr/0015-ia-difficulte-reglable.md) plutôt que recodé — IComputerTargeting est conçu
/// pour être substituable. N'utilise jamais les armes spéciales (comportement par défaut de l'interface).
/// </summary>
public sealed class RandomTargeting : IComputerTargeting
{
    public IReadOnlyList<Coordinate> PickTargets(OpponentBoardDto view, int count, Random rng)
    {
        var played = ProbabilityTargeting.PlayedCells(view);
        var unplayed = Enumerable.Range(0, view.Size * view.Size)
            .Select(i => new Coordinate(i / view.Size, i % view.Size))
            .Where(c => !played.Contains(c))
            .ToArray();

        if (unplayed.Length < count)
            throw new InvalidOperationException($"{count} cibles demandées pour {unplayed.Length} cases non jouées.");

        rng.Shuffle(unplayed);
        return unplayed[..count];
    }
}
