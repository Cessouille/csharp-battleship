namespace BattleShip.Models.Domain;

public readonly record struct Coordinate(int Row, int Column);

public static class BoardGrid
{
    public const int Size = 10;

    public static bool Contains(Coordinate c) =>
        c.Row is >= 0 and < Size && c.Column is >= 0 and < Size;
}

public enum Orientation
{
    Horizontal,
    Vertical
}
