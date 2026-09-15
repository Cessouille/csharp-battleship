namespace BattleShip.Models.Contracts;

public enum ShotOutcomeDto
{
    Miss,
    Hit,
    Sunk
}

public sealed record CoordinateDto(int Row, int Column);

public sealed record ScanResultDto(CoordinateDto Origin, bool ShipDetected);

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
    IReadOnlyList<ShipViewDto> SunkShips,
    IReadOnlyList<ScanResultDto> Scans);

/// <summary>Options demandées à la création ; tout champ absent garde la valeur de la partie classique (<c>{}</c> = partie classique).</summary>
public sealed record CreateGameRequestDto(bool Radar = false, string ShotMode = "Classic", bool SpecialWeapons = false);

public sealed record GameOptionsDto(bool Radar, string ShotMode, bool SpecialWeapons);

public sealed record ArsenalDto(int Torpedoes, int AirStrikes);

/// <summary>
/// Ce que le joueur peut encore faire au prochain tour, calculé par le serveur (le client n'en déduit rien lui-même).
/// Les munitions adverses sont publiques : elles se déduisent des armes que l'ordinateur a déjà utilisées.
/// </summary>
public sealed record PlayerActionsDto(int ScansRemaining, int SalvoSize, ArsenalDto Arsenal, ArsenalDto OpponentArsenal);

public sealed record GameStateDto(
    Guid GameId,
    string Status,
    string? Winner,
    MyBoardDto MyBoard,
    OpponentBoardDto OpponentBoard,
    GameOptionsDto Options,
    PlayerActionsDto Actions);

public sealed record CreateGameResponseDto(
    Guid GameId,
    string Status,
    MyBoardDto MyBoard,
    OpponentBoardDto OpponentBoard,
    GameOptionsDto Options,
    PlayerActionsDto Actions);

public sealed record ShotRequestDto(int Row, int Column);

public sealed record SalvoRequestDto(IReadOnlyList<ShotRequestDto> Shots);

/// <summary><c>From</c> : Left, Right, Top ou Bottom ; <c>Lane</c> : ligne (Left/Right) ou colonne (Top/Bottom).</summary>
public sealed record TorpedoRequestDto(string From, int Lane);

/// <summary><c>Orientation</c> : Horizontal (vers la droite) ou Vertical (vers le bas) à partir de (Row, Column).</summary>
public sealed record AirStrikeRequestDto(int Row, int Column, string Orientation);

public sealed record ShotResultDto(CoordinateDto Target, ShotOutcomeDto Outcome, string? SunkShipKind);

public sealed record TurnResultDto(
    IReadOnlyList<ShotResultDto> PlayerShots,
    ScanResultDto? PlayerScan,
    string? PlayerWeapon,
    IReadOnlyList<ShotResultDto> ComputerShots,
    string? ComputerWeapon,
    string Status,
    string? Winner,
    MyBoardDto MyBoard,
    OpponentBoardDto OpponentBoard,
    PlayerActionsDto Actions);
