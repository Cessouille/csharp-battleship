using BattleShip.Grpc;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Game;

/// <summary>Traduit les messages proto reçus en gRPC-Web vers les mêmes DTO que le REST, pour réutiliser BoardViewModel.ToGrid des deux côtés.</summary>
public static class GrpcMapping
{
    public static GameStateDto ToGameStateDto(this GameStateReply reply) =>
        new(
            Guid.Parse(reply.GameId),
            reply.Status,
            string.IsNullOrEmpty(reply.Winner) ? null : reply.Winner,
            reply.MyBoard.ToMyBoardDto(),
            reply.OpponentBoard.ToOpponentBoardDto(),
            new GameOptionsDto(reply.Options.Radar, reply.Options.ShotMode, reply.Options.SpecialWeapons, reply.Options.Difficulty),
            new PlayerActionsDto(
                reply.Actions.ScansRemaining,
                reply.Actions.SalvoSize,
                new ArsenalDto(reply.Actions.Arsenal.Torpedoes, reply.Actions.Arsenal.AirStrikes),
                new ArsenalDto(reply.Actions.OpponentArsenal.Torpedoes, reply.Actions.OpponentArsenal.AirStrikes)));

    public static MyBoardDto ToMyBoardDto(this MyBoardMessage message) =>
        new(
            message.Size,
            message.Ships.Select(ToShipViewDto).ToList(),
            message.CellsHitByOpponent.Select(ToCoordinateDto).ToList());

    public static OpponentBoardDto ToOpponentBoardDto(this OpponentBoardMessage message) =>
        new(
            message.Size,
            message.Hits.Select(ToCoordinateDto).ToList(),
            message.Misses.Select(ToCoordinateDto).ToList(),
            message.SunkShips.Select(ToShipViewDto).ToList(),
            message.Scans.Select(s => new ScanResultDto(ToCoordinateDto(s.Origin), s.ShipDetected)).ToList());

    private static ShipViewDto ToShipViewDto(ShipMessage ship) =>
        new(ship.Kind, ship.Cells.Select(ToCoordinateDto).ToList(), ship.IsSunk);

    private static CoordinateDto ToCoordinateDto(CoordinateMessage c) => new(c.Row, c.Column);
}
