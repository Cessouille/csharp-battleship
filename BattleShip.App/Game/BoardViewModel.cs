using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.App.Game;

/// <summary>État d'affichage d'une case, purement présentation (choix de classe CSS) — jamais transmis sur le réseau, donc hors de BattleShip.Models.</summary>
public enum CellDisplayState
{
    Empty,
    Ship,
    Hit,
    Miss,
    Sunk,
    ScannedDetected,
    ScannedClear
}

public static class BoardViewModel
{
    public static CellDisplayState[,] ToGrid(this MyBoardDto board)
    {
        var grid = new CellDisplayState[board.Size, board.Size];
        var hitCells = board.CellsHitByOpponent.Select(c => (c.Row, c.Column)).ToHashSet();

        foreach (var ship in board.Ships)
        {
            foreach (var cell in ship.Cells)
            {
                grid[cell.Row, cell.Column] = ship.IsSunk
                    ? CellDisplayState.Sunk
                    : hitCells.Contains((cell.Row, cell.Column)) ? CellDisplayState.Hit : CellDisplayState.Ship;
            }
        }

        foreach (var (row, column) in hitCells)
        {
            if (grid[row, column] == CellDisplayState.Empty)
                grid[row, column] = CellDisplayState.Miss;
        }

        return grid;
    }

    public static CellDisplayState[,] ToGrid(this OpponentBoardDto board)
    {
        var grid = new CellDisplayState[board.Size, board.Size];

        // Les zones scannées sont posées en premier : un tir ultérieur sur l'une de leurs cases prend le dessus.
        // Une zone « vide » garantit l'absence de navire sur chacune de ses cases, alors qu'une zone « détectée »
        // ne dit pas laquelle est occupée : en cas de chevauchement, l'information « vide » l'emporte.
        foreach (var scan in board.Scans)
            foreach (var cell in RadarRules.ZoneCells(new Coordinate(scan.Origin.Row, scan.Origin.Column)))
                if (grid[cell.Row, cell.Column] != CellDisplayState.ScannedClear)
                    grid[cell.Row, cell.Column] = scan.ShipDetected ? CellDisplayState.ScannedDetected : CellDisplayState.ScannedClear;

        foreach (var c in board.Misses)
            grid[c.Row, c.Column] = CellDisplayState.Miss;

        foreach (var c in board.Hits)
            grid[c.Row, c.Column] = CellDisplayState.Hit;

        foreach (var ship in board.SunkShips)
            foreach (var c in ship.Cells)
                grid[c.Row, c.Column] = CellDisplayState.Sunk;

        return grid;
    }

    /// <summary>Case encore jamais tirée (éventuellement scannée) : seule cible possible pour un tir.</summary>
    public static bool IsUnplayed(this CellDisplayState state) =>
        state is CellDisplayState.Empty or CellDisplayState.Ship or CellDisplayState.ScannedDetected or CellDisplayState.ScannedClear;
}
