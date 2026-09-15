namespace BattleShip.Models.Contracts;

public enum ShotOutcomeDto
{
    Miss,
    Hit,
    Sunk
}

public sealed record CoordinateDto(int Row, int Column);

public sealed record ShipViewDto(string Kind, IReadOnlyList<CoordinateDto> Cells, bool IsSunk);

/// <summary>Vue de son propre plateau : la flotte complète est visible, c'est la sienne.</summary>
public sealed record MyBoardDto(
    int Size,
    IReadOnlyList<ShipViewDto> Ships,
    IReadOnlyList<CoordinateDto> CellsHitByOpponent);

/// <summary>
/// Vue du plateau adverse : structurellement incapable de porter une case de navire non découverte.
/// Aucun champ ici ne peut contenir une position de navire adverse tant qu'il n'a pas été coulé.
/// </summary>
public sealed record OpponentBoardDto(
    int Size,
    IReadOnlyList<CoordinateDto> Hits,
    IReadOnlyList<CoordinateDto> Misses,
    IReadOnlyList<ShipViewDto> SunkShips);

public sealed record GameStateDto(Guid GameId, string Status, string? Winner, MyBoardDto MyBoard, OpponentBoardDto OpponentBoard);

public sealed record CreateGameResponseDto(Guid GameId, string Status, MyBoardDto MyBoard, OpponentBoardDto OpponentBoard);

public sealed record ShotRequestDto(int Row, int Column);

public sealed record ShotResultDto(CoordinateDto Target, ShotOutcomeDto Outcome, string? SunkShipKind);

public sealed record TurnResultDto(
    ShotResultDto PlayerShot,
    ShotResultDto? ComputerShot,
    string Status,
    string? Winner,
    MyBoardDto MyBoard,
    OpponentBoardDto OpponentBoard);
