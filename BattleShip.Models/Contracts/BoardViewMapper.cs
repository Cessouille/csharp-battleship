using BattleShip.Models.Domain;

namespace BattleShip.Models.Contracts;

/// <summary>
/// Seul point de code autorisé à lire <see cref="Board.Ships"/> pour produire une vue adverse.
/// Chokepoint unique de la règle "aucune fuite des positions adverses non découvertes" : par construction,
/// <see cref="OpponentBoardDto"/> ne peut porter que des cases déjà ciblées ou des navires entièrement coulés.
/// </summary>
public static class BoardViewMapper
{
    public static CoordinateDto ToDto(this Coordinate c) => new(c.Row, c.Column);

    public static MyBoardDto ToMyBoardDto(this Board board) =>
        new(
            BoardGrid.Size,
            board.Ships.Select(ToShipViewDto).ToList(),
            board.ShotsReceived.Select(ToDto).ToList());

    public static OpponentBoardDto ToOpponentBoardDto(this Board board)
    {
        var hits = new List<CoordinateDto>();
        var misses = new List<CoordinateDto>();

        foreach (var shot in board.ShotsReceived)
        {
            var ship = board.Ships.FirstOrDefault(s => s.Occupies(shot));
            if (ship is null)
            {
                misses.Add(shot.ToDto());
            }
            else if (!ship.IsSunk)
            {
                hits.Add(shot.ToDto());
            }
            // Case appartenant à un navire coulé : révélée uniquement via SunkShips ci-dessous, pas ici.
        }

        var sunkShips = board.Ships.Where(s => s.IsSunk).Select(ToShipViewDto).ToList();
        var scans = board.ScansReceived.Select(ToScanResultDto).ToList();

        return new OpponentBoardDto(BoardGrid.Size, hits, misses, sunkShips, scans);
    }

    public static ScanResultDto ToScanResultDto(this ScanResult scan) => new(scan.Origin.ToDto(), scan.ShipDetected);

    public static GameOptionsDto ToDto(this GameOptions options) =>
        new(options.Radar, options.ShotMode.ToString(), options.SpecialWeapons, options.Difficulty.ToString());

    /// <summary>Suppose la requête déjà validée (CreateGameRequestDtoValidator) : un mode ou une difficulté inconnus lèvent ici.</summary>
    public static GameOptions ToGameOptions(this CreateGameRequestDto request) =>
        new()
        {
            Radar = request.Radar,
            ShotMode = Enum.Parse<ShotMode>(request.ShotMode),
            SpecialWeapons = request.SpecialWeapons,
            Difficulty = Enum.Parse<AiDifficulty>(request.Difficulty)
        };

    /// <summary>Suppose la requête déjà validée (CreateGameRequestDtoValidator) : un Kind/Orientation inconnu lève ici.</summary>
    public static (ShipKind Kind, Coordinate Origin, Orientation Orientation) ToPlacementSpec(this ShipPlacementDto dto) =>
        (Enum.Parse<ShipKind>(dto.Kind), new Coordinate(dto.Row, dto.Column), Enum.Parse<Orientation>(dto.Orientation));

    public static WeaponAction ToWeaponAction(this TorpedoRequestDto request) =>
        new WeaponAction.Torpedo(Enum.Parse<Edge>(request.From), request.Lane);

    public static WeaponAction ToWeaponAction(this AirStrikeRequestDto request) =>
        new WeaponAction.AirStrike(new Coordinate(request.Row, request.Column), Enum.Parse<Orientation>(request.Orientation));

    public static PlayerActionsDto ToPlayerActionsDto(this Game game) =>
        new(
            game.ScansRemaining,
            game.HumanSalvoSize,
            game.Options.SpecialWeapons ? game.HumanArsenal.ToDto() : new ArsenalDto(0, 0),
            game.Options.SpecialWeapons ? game.ComputerArsenal.ToDto() : new ArsenalDto(0, 0));

    public static ArsenalDto ToDto(this Arsenal arsenal) => new(arsenal.Torpedoes, arsenal.AirStrikes);

    public static ShipViewDto ToShipViewDto(this Ship ship) =>
        new(ship.Kind.ToString(), ship.Cells.Select(ToDto).ToList(), ship.IsSunk);

    public static ShotResultDto ToShotResultDto(this ShotResolution resolution) =>
        new(resolution.Target.ToDto(), (ShotOutcomeDto)resolution.Outcome, resolution.SunkShipKind?.ToString());

    public static CreateGameResponseDto ToCreateGameResponseDto(this Game game) =>
        new(
            game.Id,
            game.Status.ToString(),
            game.HumanBoard.ToMyBoardDto(),
            game.ComputerBoard.ToOpponentBoardDto(),
            game.Options.ToDto(),
            game.ToPlayerActionsDto(),
            game.ToAchievementsDto());

    public static GameStateDto ToGameStateDto(this Game game) =>
        new(
            game.Id,
            game.Status.ToString(),
            game.Winner?.ToString(),
            game.HumanBoard.ToMyBoardDto(),
            game.ComputerBoard.ToOpponentBoardDto(),
            game.Options.ToDto(),
            game.ToPlayerActionsDto(),
            game.History.Select(ToJournalEntryDto).ToList(),
            game.ToAchievementsDto());

    public static TurnResultDto ToTurnResultDto(this Game game, TurnResult turn) =>
        new(
            turn.PlayerShots.Select(ToShotResultDto).ToList(),
            turn.PlayerScan?.ToScanResultDto(),
            turn.PlayerWeapon?.ToString(),
            turn.ComputerShots.Select(ToShotResultDto).ToList(),
            turn.ComputerWeapon?.ToString(),
            turn.Status.ToString(),
            turn.Winner?.ToString(),
            game.HumanBoard.ToMyBoardDto(),
            game.ComputerBoard.ToOpponentBoardDto(),
            game.ToPlayerActionsDto(),
            game.History.Select(ToJournalEntryDto).ToList(),
            game.ToAchievementsDto());

    /// <summary>Identifiants seuls (voir GameStateDto) : le nom de l'enum, comme pour Options/Winner ailleurs dans ce mapper.</summary>
    private static IReadOnlyList<string> ToAchievementsDto(this Game game) =>
        game.Achievements.Select(a => a.ToString()).ToList();

    public static JournalEntryDto ToJournalEntryDto(this TurnResult turn) =>
        new(
            turn.PlayerShots.Select(ToShotResultDto).ToList(),
            turn.PlayerScan?.ToScanResultDto(),
            turn.PlayerWeapon?.ToString(),
            turn.ComputerShots.Select(ToShotResultDto).ToList(),
            turn.ComputerWeapon?.ToString());
}
