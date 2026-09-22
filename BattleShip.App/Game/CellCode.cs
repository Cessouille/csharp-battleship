namespace BattleShip.App.Game;

/// <summary>Conversion case &lt;-&gt; code "colonne-lettre + ligne-chiffre" (ex. "B5"), uniquement pour l'affichage
/// et la saisie côté client ; la validation de fond des coups reste serveur.</summary>
public static class CellCode
{
    public static string ColumnLabel(int column) => ((char)('A' + column)).ToString();

    public static string Format(int row, int column) => $"{ColumnLabel(column)}{row + 1}";

    public static bool TryParse(string? input, int size, out (int Row, int Column) cell)
    {
        cell = default;
        var trimmed = input?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 2)
            return false;

        var columnChar = char.ToUpperInvariant(trimmed[0]);
        if (columnChar < 'A' || columnChar >= 'A' + size)
            return false;

        if (!int.TryParse(trimmed.AsSpan(1), out var rowNumber) || rowNumber < 1 || rowNumber > size)
            return false;

        cell = (rowNumber - 1, columnChar - 'A');
        return true;
    }
}
