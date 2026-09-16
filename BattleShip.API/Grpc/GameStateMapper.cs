using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.API.Grpc;

/// <summary>
/// Traduit les DTO REST (déjà anti-fuite, voir BoardViewMapper dans BattleShip.Models) vers les messages proto —
/// aucune nouvelle lecture de Domain.Board.Ships ici, un seul chokepoint anti-fuite pour les deux transports.
/// </summary>
public static class GameStateMapper
{
    public static GameStateReply ToGameStateReply(Game game) => ToGameStateReply(game.ToGameStateDto());

    public static ScanZoneReply ToScanZoneReply(Game game, TurnResult turn)
    {
        var dto = game.ToTurnResultDto(turn);
        var reply = new ScanZoneReply
        {
            Scan = dto.PlayerScan is null ? null : ToScanMessage(dto.PlayerScan),
            State = ToGameStateReply(game),
            ComputerWeapon = dto.ComputerWeapon ?? string.Empty
        };
        reply.ComputerShots.AddRange(dto.ComputerShots.Select(ToShotResultMessage));
        return reply;
    }

    public static PlaySalvoReply ToPlaySalvoReply(Game game, TurnResult turn)
    {
        var dto = game.ToTurnResultDto(turn);
        var reply = new PlaySalvoReply
        {
            State = ToGameStateReply(game),
            ComputerWeapon = dto.ComputerWeapon ?? string.Empty
        };
        reply.PlayerShots.AddRange(dto.PlayerShots.Select(ToShotResultMessage));
        reply.ComputerShots.AddRange(dto.ComputerShots.Select(ToShotResultMessage));
        return reply;
    }

    private static GameStateReply ToGameStateReply(GameStateDto state)
    {
        var reply = new GameStateReply
        {
            GameId = state.GameId.ToString(),
            Status = state.Status,
            Winner = state.Winner ?? string.Empty,
            MyBoard = ToMyBoardMessage(state.MyBoard),
            OpponentBoard = ToOpponentBoardMessage(state.OpponentBoard),
            Options = new GameOptionsMessage
            {
                Radar = state.Options.Radar,
                ShotMode = state.Options.ShotMode,
                SpecialWeapons = state.Options.SpecialWeapons,
                Difficulty = state.Options.Difficulty
            },
            Actions = new PlayerActionsMessage
            {
                ScansRemaining = state.Actions.ScansRemaining,
                SalvoSize = state.Actions.SalvoSize,
                Arsenal = ToArsenalMessage(state.Actions.Arsenal),
                OpponentArsenal = ToArsenalMessage(state.Actions.OpponentArsenal)
            }
        };
        reply.History.AddRange(state.History.Select(ToJournalEntryMessage));
        return reply;
    }

    private static JournalEntryMessage ToJournalEntryMessage(JournalEntryDto entry)
    {
        var message = new JournalEntryMessage
        {
            PlayerScan = entry.PlayerScan is null ? null : ToScanMessage(entry.PlayerScan),
            PlayerWeapon = entry.PlayerWeapon ?? string.Empty,
            ComputerWeapon = entry.ComputerWeapon ?? string.Empty
        };
        message.PlayerShots.AddRange(entry.PlayerShots.Select(ToShotResultMessage));
        message.ComputerShots.AddRange(entry.ComputerShots.Select(ToShotResultMessage));
        return message;
    }

    private static MyBoardMessage ToMyBoardMessage(MyBoardDto board)
    {
        var message = new MyBoardMessage { Size = board.Size };
        message.Ships.AddRange(board.Ships.Select(ToShipMessage));
        message.CellsHitByOpponent.AddRange(board.CellsHitByOpponent.Select(ToCoordinateMessage));
        return message;
    }

    private static OpponentBoardMessage ToOpponentBoardMessage(OpponentBoardDto board)
    {
        var message = new OpponentBoardMessage { Size = board.Size };
        message.Hits.AddRange(board.Hits.Select(ToCoordinateMessage));
        message.Misses.AddRange(board.Misses.Select(ToCoordinateMessage));
        message.SunkShips.AddRange(board.SunkShips.Select(ToShipMessage));
        message.Scans.AddRange(board.Scans.Select(ToScanMessage));
        return message;
    }

    private static ShipMessage ToShipMessage(ShipViewDto ship)
    {
        var message = new ShipMessage { Kind = ship.Kind, IsSunk = ship.IsSunk };
        message.Cells.AddRange(ship.Cells.Select(ToCoordinateMessage));
        return message;
    }

    private static ScanMessage ToScanMessage(ScanResultDto scan) =>
        new() { Origin = ToCoordinateMessage(scan.Origin), ShipDetected = scan.ShipDetected };

    private static ShotResultMessage ToShotResultMessage(ShotResultDto shot) =>
        new()
        {
            Target = ToCoordinateMessage(shot.Target),
            Outcome = shot.Outcome.ToString(),
            SunkShipKind = shot.SunkShipKind ?? string.Empty
        };

    private static ArsenalMessage ToArsenalMessage(ArsenalDto arsenal) =>
        new() { Torpedoes = arsenal.Torpedoes, AirStrikes = arsenal.AirStrikes };

    private static CoordinateMessage ToCoordinateMessage(CoordinateDto c) => new() { Row = c.Row, Column = c.Column };
}
