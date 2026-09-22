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

/// <summary>Placement choisi par le joueur pour un navire ; <c>Orientation</c> : Horizontal ou Vertical à partir de (Row, Column).</summary>
public sealed record ShipPlacementDto(string Kind, int Row, int Column, string Orientation);

/// <summary>
/// Options demandées à la création ; tout champ absent garde la valeur de la partie classique (<c>{}</c> = partie classique).
/// <c>Placements</c> absent ou vide : flotte humaine placée aléatoirement (comportement historique) ; sinon, placement
/// manuel revalidé côté serveur (voir docs/adr/0013-placement-manuel.md). <c>PlayerId</c> : GUID facultatif obtenu via
/// <c>POST /api/players</c> (TICKET-14) ; absent, la partie n'est associée à aucun profil.
/// </summary>
public sealed record CreateGameRequestDto(
    bool Radar = false,
    string ShotMode = "Classic",
    bool SpecialWeapons = false,
    IReadOnlyList<ShipPlacementDto>? Placements = null,
    string Difficulty = "Hard",
    string? PlayerId = null,
    bool PrecisionMinigame = false);

public sealed record GameOptionsDto(bool Radar, string ShotMode, bool SpecialWeapons, string Difficulty, bool PrecisionMinigame);

/// <summary>
/// Défi de timing ouvert (voir docs/adr/0019-mini-jeu-de-precision.md) : le client anime la barre à partir de ces
/// paramètres mais ne décide jamais du résultat — <c>StartedAtUtc</c> est l'horloge serveur, seule source de
/// vérité une fois le défi résolu (POST /api/games/{gameId}/challenges/{challengeId}/resolve).
/// </summary>
public sealed record TimingChallengeDto(Guid ChallengeId, string Kind, double ZoneStart, double ZoneWidth, int PeriodMs, DateTimeOffset StartedAtUtc);

public sealed record ArsenalDto(int Torpedoes, int AirStrikes);

/// <summary>
/// Ce que le joueur peut encore faire au prochain tour, calculé par le serveur (le client n'en déduit rien lui-même).
/// Les munitions adverses sont publiques : elles se déduisent des armes que l'ordinateur a déjà utilisées.
/// </summary>
public sealed record PlayerActionsDto(int ScansRemaining, int SalvoSize, ArsenalDto Arsenal, ArsenalDto OpponentArsenal);

/// <summary>Une entrée du journal de partie (TICKET-10) : les mêmes champs qu'un tour, sans l'état de plateau qui l'accompagne dans TurnResultDto.</summary>
public sealed record JournalEntryDto(
    IReadOnlyList<ShotResultDto> PlayerShots,
    ScanResultDto? PlayerScan,
    string? PlayerWeapon,
    IReadOnlyList<ShotResultDto> ComputerShots,
    string? ComputerWeapon);

/// <summary>
/// <c>Achievements</c> : identifiants de succès débloqués sur cette partie (TICKET-12, docs/adr/0017-systeme-de-succes.md) ;
/// libellés, emoji et images vivent côté App, jamais dans ce DTO.
/// </summary>
public sealed record GameStateDto(
    Guid GameId,
    string Status,
    string? Winner,
    MyBoardDto MyBoard,
    OpponentBoardDto OpponentBoard,
    GameOptionsDto Options,
    PlayerActionsDto Actions,
    IReadOnlyList<JournalEntryDto> History,
    IReadOnlyList<string> Achievements,
    TimingChallengeDto? PendingChallenge);

public sealed record CreateGameResponseDto(
    Guid GameId,
    string Status,
    MyBoardDto MyBoard,
    OpponentBoardDto OpponentBoard,
    GameOptionsDto Options,
    PlayerActionsDto Actions,
    IReadOnlyList<string> Achievements);

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
    PlayerActionsDto Actions,
    IReadOnlyList<JournalEntryDto> History,
    IReadOnlyList<string> Achievements);

/// <summary>
/// Profil joueur (TICKET-14) : projection dérivée des parties créées avec ce PlayerId, jamais un état stocké
/// séparément (voir docs/adr/0018-profil-joueur-anonyme.md). Bien défini, potentiellement vide, pour tout
/// GUID syntaxiquement valide — il n'existe pas de « profil inconnu ».
/// </summary>
public sealed record PlayerProfileDto(Guid PlayerId, int HardVictories, IReadOnlyList<string> Achievements);
