using System.Net.Http.Json;
using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

/// <summary>
/// Transport alternatif au POST /salvos REST (docs/adr/0014-salvo-grpc-web.md) : les deux chemins coexistent, ce
/// fichier ne revérifie donc pas les règles métier déjà couvertes par SalvoEndpointsTests, juste que le même
/// comportement traverse gRPC-Web (validation, refus métier, succès).
/// </summary>
public class GrpcSalvoTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Battleship.BattleshipClient _client = ApiClients.CreateGrpcClient(factory);
    private readonly HttpClient _restClient = factory.CreateClient();

    private Task<CreateGameResponseDto> CreateSalvoGameAsync() =>
        _restClient.CreateGameAsync(new CreateGameRequestDto(ShotMode: nameof(ShotMode.Salvo)));

    private static PlaySalvoRequest Salvo(Guid gameId, params (int Row, int Column)[] cells)
    {
        var request = new PlaySalvoRequest { GameId = gameId.ToString() };
        request.Shots.AddRange(cells.Select(c => new CoordinateMessage { Row = c.Row, Column = c.Column }));
        return request;
    }

    [Fact]
    public async Task PlaySalvo_WithExpectedSize_ReturnsAllShotsAndComputerRiposte()
    {
        var game = await CreateSalvoGameAsync();

        var reply = await _client.PlaySalvoAsync(Salvo(game.GameId, (5, 0), (5, 1), (5, 2), (5, 3), (5, 4)));

        Assert.Equal(5, reply.PlayerShots.Count);
        Assert.NotEmpty(reply.ComputerShots);

        // La salve fait partie de l'état : relue à l'identique par le GET REST (hydratation après rechargement).
        var state = await _restClient.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
        Assert.Equal(reply.State.OpponentBoard.Hits.Count + reply.State.OpponentBoard.Misses.Count,
            state!.OpponentBoard.Hits.Count + state.OpponentBoard.Misses.Count);
    }

    [Fact]
    public async Task PlaySalvo_WithWrongSize_ThrowsFailedPrecondition_WithoutMutation()
    {
        var game = await CreateSalvoGameAsync();

        var ex = await Assert.ThrowsAsync<RpcException>(() => _client.PlaySalvoAsync(Salvo(game.GameId, (5, 0), (5, 1))).ResponseAsync);

        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.WrongSalvoSize), ex.Status.Detail);
        var state = await _restClient.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
        Assert.Empty(state!.OpponentBoard.Hits);
        Assert.Empty(state.OpponentBoard.Misses);
    }

    [Fact]
    public async Task PlaySalvo_OnClassicGame_ThrowsFailedPrecondition_WrongShotMode()
    {
        var game = await _restClient.CreateGameAsync();

        var ex = await Assert.ThrowsAsync<RpcException>(() => _client.PlaySalvoAsync(Salvo(game.GameId, (0, 0))).ResponseAsync);

        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.WrongShotMode), ex.Status.Detail);
    }

    [Fact]
    public async Task PlaySalvo_WithDuplicateCell_ThrowsInvalidArgument()
    {
        var game = await CreateSalvoGameAsync();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _client.PlaySalvoAsync(Salvo(game.GameId, (5, 0), (5, 0), (5, 1), (5, 2), (5, 3))).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task PlaySalvo_Empty_ThrowsInvalidArgument()
    {
        var game = await CreateSalvoGameAsync();

        var ex = await Assert.ThrowsAsync<RpcException>(() => _client.PlaySalvoAsync(Salvo(game.GameId)).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task PlaySalvo_UnknownGame_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() => _client.PlaySalvoAsync(Salvo(Guid.NewGuid(), (0, 0))).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task PlaySalvo_MalformedGameId_ThrowsInvalidArgument()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _client.PlaySalvoAsync(new PlaySalvoRequest { GameId = "pas-un-guid" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }
}
