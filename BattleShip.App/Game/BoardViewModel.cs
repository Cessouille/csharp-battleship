using BattleShip.Models.Contracts;

namespace BattleShip.App.Game;

/// <summary>État d'affichage d'une case, purement présentation (choix de classe CSS) — jamais transmis sur le réseau, donc hors de BattleShip.Models.</summary>
public enum CellDisplayState
{
    Empty,
    Ship,
    Hit,
    Miss,
    Sunk
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

        foreach (var c in board.Misses)
            grid[c.Row, c.Column] = CellDisplayState.Miss;

        foreach (var c in board.Hits)
            grid[c.Row, c.Column] = CellDisplayState.Hit;

        foreach (var ship in board.SunkShips)
        foreach (var c in ship.Cells)
            grid[c.Row, c.Column] = CellDisplayState.Sunk;

        return grid;
    }
}
