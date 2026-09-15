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
    public static GameStateReply ToGameStateReply(Game game)
    {
        var myBoard = game.HumanBoard.ToMyBoardDto();
        var opponentBoard = game.ComputerBoard.ToOpponentBoardDto();

        var reply = new GameStateReply
        {
            GameId = game.Id.ToString(),
            Status = game.Status.ToString(),
            Winner = game.Winner?.ToString() ?? string.Empty,
            MyBoard = ToMyBoardMessage(myBoard),
            OpponentBoard = ToOpponentBoardMessage(opponentBoard)
        };

        return reply;
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
        return message;
    }

    private static ShipMessage ToShipMessage(ShipViewDto ship)
    {
        var message = new ShipMessage { Kind = ship.Kind, IsSunk = ship.IsSunk };
        message.Cells.AddRange(ship.Cells.Select(ToCoordinateMessage));
        return message;
    }

    private static CoordinateMessage ToCoordinateMessage(CoordinateDto c) => new() { Row = c.Row, Column = c.Column };
}
