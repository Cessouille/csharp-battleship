using BattleShip.API.Storage;
using BattleShip.Grpc;
using BattleShip.Models.Domain;
using FluentValidation;
using Grpc.Core;

namespace BattleShip.API.Grpc;

public sealed class BattleshipGrpcService(
    InMemoryGameStore store,
    IValidator<GetGameStateRequest> getGameStateValidator,
    IValidator<ScanZoneRequest> scanZoneValidator)
    : Battleship.BattleshipBase
{
    public override async Task<GameStateReply> GetGameState(GetGameStateRequest request, ServerCallContext context)
    {
        await ValidateOrThrowAsync(getGameStateValidator, request, context);
        var game = FindOrThrow(request.GameId);

        return game.Locked(() => GameStateMapper.ToGameStateReply(game));
    }

    public override async Task<ScanZoneReply> ScanZone(ScanZoneRequest request, ServerCallContext context)
    {
        await ValidateOrThrowAsync(scanZoneValidator, request, context);
        var game = FindOrThrow(request.GameId);

        return game.Locked(() => game.PlayHumanScan(new Coordinate(request.Row, request.Column)) switch
        {
            MoveResult.Accepted accepted => GameStateMapper.ToScanZoneReply(game, accepted.Turn),
            MoveResult.Rejected rejected => throw ToRpcException(rejected.Reason),
            _ => throw new RpcException(new Status(StatusCode.Internal, "Résultat de coup inattendu."))
        });
    }

    private static async Task ValidateOrThrowAsync<T>(IValidator<T> validator, T request, ServerCallContext context)
    {
        var check = await validator.ValidateAsync(request, context.CancellationToken);
        if (!check.IsValid)
            throw new RpcException(new Status(StatusCode.InvalidArgument, check.ToString()));
    }

    private Game FindOrThrow(string gameId) =>
        store.Find(Guid.Parse(gameId))
        ?? throw new RpcException(new Status(StatusCode.NotFound, "Partie introuvable."));

    /// <summary>
    /// Le détail porte le nom du motif (même valeur que le champ <c>code</c> des 409 REST) pour que le client
    /// traduise les refus de la même façon quel que soit le transport. OutOfGrid est normalement intercepté par
    /// la validation en amont : le contrôle du domaine n'est qu'une défense en profondeur.
    /// </summary>
    private static RpcException ToRpcException(MoveRejectionReason reason) =>
        new(new Status(
            reason == MoveRejectionReason.OutOfGrid ? StatusCode.InvalidArgument : StatusCode.FailedPrecondition,
            reason.ToString()));
}

public sealed class GetGameStateRequestValidator : AbstractValidator<GetGameStateRequest>
{
    public GetGameStateRequestValidator() =>
        RuleFor(r => r.GameId).NotEmpty().Must(id => Guid.TryParse(id, out _))
            .WithMessage("game_id doit être un GUID valide.");
}

public sealed class ScanZoneRequestValidator : AbstractValidator<ScanZoneRequest>
{
    public ScanZoneRequestValidator()
    {
        RuleFor(r => r.GameId).NotEmpty().Must(id => Guid.TryParse(id, out _))
            .WithMessage("game_id doit être un GUID valide.");
        RuleFor(r => r.Row).InclusiveBetween(0, BoardGrid.Size - RadarRules.ZoneSize)
            .WithMessage($"row doit être compris entre 0 et {BoardGrid.Size - RadarRules.ZoneSize} (la zone {RadarRules.ZoneSize}x{RadarRules.ZoneSize} doit tenir dans la grille).");
        RuleFor(r => r.Column).InclusiveBetween(0, BoardGrid.Size - RadarRules.ZoneSize)
            .WithMessage($"column doit être compris entre 0 et {BoardGrid.Size - RadarRules.ZoneSize} (la zone {RadarRules.ZoneSize}x{RadarRules.ZoneSize} doit tenir dans la grille).");
    }
}
