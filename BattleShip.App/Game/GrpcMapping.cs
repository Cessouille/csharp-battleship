using BattleShip.Grpc;
using BattleShip.Models.Contracts;

namespace BattleShip.App.Game;

/// <summary>Traduit les messages proto reçus en gRPC-Web vers les mêmes DTO que le REST, pour réutiliser BoardViewModel.ToGrid des deux côtés.</summary>
public static class GrpcMapping
{
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
            message.SunkShips.Select(ToShipViewDto).ToList());

    private static ShipViewDto ToShipViewDto(ShipMessage ship) =>
        new(ship.Kind, ship.Cells.Select(ToCoordinateDto).ToList(), ship.IsSunk);

    private static CoordinateDto ToCoordinateDto(CoordinateMessage c) => new(c.Row, c.Column);
}
