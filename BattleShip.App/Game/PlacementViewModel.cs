using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.App.Game;

/// <summary>
/// Aide de saisie côté client pour le placement manuel (voir docs/adr/0013-placement-manuel.md) : le plateau
/// interne réutilise <see cref="Board.TryPlaceShip"/> pour donner un retour immédiat, mais ne fait jamais foi —
/// le serveur revalide tout via <c>Game.TryCreateManual</c> à la création de partie.
/// </summary>
public sealed class PlacementViewModel
{
    private Board _board = new();

    public int Size => BoardGrid.Size;

    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    public bool IsComplete => _board.Ships.Count == Fleet.Standard.Count;

    public (ShipKind Kind, int Size)? NextShip =>
        _board.Ships.Count < Fleet.Standard.Count ? Fleet.Standard[_board.Ships.Count] : null;

    public IReadOnlyList<ShipViewDto> PlacedShips => _board.Ships.Select(BoardViewMapper.ToShipViewDto).ToList();

    public bool TryPlaceNext(Coordinate origin) =>
        NextShip is { } next && _board.TryPlaceShip(next.Kind, origin, Orientation, next.Size);

    public void Reset() => _board = new Board();

    /// <summary>Complète le reste de la flotte au hasard, en conservant les navires déjà posés à la main.</summary>
    public void FillRandomly(Random rng) =>
        _board.PlaceFleetRandomly(Fleet.Standard.Skip(_board.Ships.Count).ToList(), rng);

    public CellDisplayState[,] ToGrid() => _board.ToMyBoardDto().ToGrid();

    public IReadOnlyList<ShipPlacementDto> ToPlacementDtos() =>
        _board.Ships.Select(s => new ShipPlacementDto(
            s.Kind.ToString(),
            s.Cells[0].Row,
            s.Cells[0].Column,
            s.Cells.Count > 1 && s.Cells[1].Row == s.Cells[0].Row
                ? nameof(Orientation.Horizontal)
                : nameof(Orientation.Vertical)))
            .ToList();
}
