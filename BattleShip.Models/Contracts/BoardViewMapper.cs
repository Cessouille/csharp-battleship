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

        return new OpponentBoardDto(BoardGrid.Size, hits, misses, sunkShips);
    }

    public static ShipViewDto ToShipViewDto(this Ship ship) =>
        new(ship.Kind.ToString(), ship.Cells.Select(ToDto).ToList(), ship.IsSunk);

    public static ShotResultDto ToShotResultDto(this ShotResolution resolution) =>
        new(resolution.Target.ToDto(), (ShotOutcomeDto)resolution.Outcome, resolution.SunkShipKind?.ToString());

    public static CreateGameResponseDto ToCreateGameResponseDto(this Game game) =>
        new(game.Id, game.Status.ToString(), game.HumanBoard.ToMyBoardDto(), game.ComputerBoard.ToOpponentBoardDto());

    public static GameStateDto ToGameStateDto(this Game game) =>
        new(game.Id, game.Status.ToString(), game.Winner?.ToString(), game.HumanBoard.ToMyBoardDto(), game.ComputerBoard.ToOpponentBoardDto());

    public static TurnResultDto ToTurnResultDto(this Game game, TurnResult turn) =>
        new(
            turn.PlayerShot.ToShotResultDto(),
            turn.ComputerShot?.ToShotResultDto(),
            turn.Status.ToString(),
            turn.Winner?.ToString(),
            game.HumanBoard.ToMyBoardDto(),
            game.ComputerBoard.ToOpponentBoardDto());
}
