using BattleShip.API.Storage;
using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using FluentValidation;
using Grpc.Core;

namespace BattleShip.API.Grpc;

public sealed class BattleshipGrpcService(InMemoryGameStore store, IValidator<GetGameStateRequest> validator)
    : Battleship.BattleshipBase
{
    public override async Task<GameStateReply> GetGameState(GetGameStateRequest request, ServerCallContext context)
    {
        var check = await validator.ValidateAsync(request, context.CancellationToken);
        if (!check.IsValid)
            throw new RpcException(new Status(StatusCode.InvalidArgument, check.ToString()));

        var game = store.Find(Guid.Parse(request.GameId));
        if (game is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Partie introuvable."));

        return game.Locked(() => GameStateMapper.ToGameStateReply(game));
    }
}

public sealed class GetGameStateRequestValidator : AbstractValidator<GetGameStateRequest>
{
    public GetGameStateRequestValidator() =>
        RuleFor(r => r.GameId).NotEmpty().Must(id => Guid.TryParse(id, out _))
            .WithMessage("game_id doit être un GUID valide.");
}
