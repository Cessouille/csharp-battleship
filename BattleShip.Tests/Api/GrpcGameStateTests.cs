using System.Net.Http.Json;
using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GrpcGameStateTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Battleship.BattleshipClient _client = ApiClients.CreateGrpcClient(factory);
    private readonly HttpClient _restClient = factory.CreateClient();

    private async Task<Guid> CreateGameAsync() => (await _restClient.CreateGameAsync()).GameId;

    [Fact]
    public async Task GetGameState_ExistingGame_ReturnsExpectedShape()
    {
        var gameId = await CreateGameAsync();

        var reply = await _client.GetGameStateAsync(new GetGameStateRequest { GameId = gameId.ToString() });

        Assert.Equal(gameId.ToString(), reply.GameId);
        Assert.Equal("InProgress", reply.Status);
        Assert.Equal(10, reply.MyBoard.Size);
        Assert.Equal(Fleet.Standard.Count, reply.MyBoard.Ships.Count);
        Assert.Empty(reply.OpponentBoard.Hits);
    }

    [Fact]
    public async Task GetGameState_UnknownId_ThrowsNotFoundRpcException()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _client.GetGameStateAsync(new GetGameStateRequest { GameId = Guid.NewGuid().ToString() }).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetGameState_MalformedGameId_ThrowsInvalidArgumentRpcException()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _client.GetGameStateAsync(new GetGameStateRequest { GameId = "pas-un-guid" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task GetGameState_ReturnsOptionsAndRemainingScans()
    {
        var created = await _restClient.CreateGameAsync(new CreateGameRequestDto(Radar: true));

        var reply = await _client.GetGameStateAsync(new GetGameStateRequest { GameId = created.GameId.ToString() });

        Assert.True(reply.Options.Radar);
        Assert.Equal(RadarRules.ScansPerGame, reply.Actions.ScansRemaining);
    }

    [Fact]
    public async Task GetGameState_AfterAShot_ReturnsHistoryEntry()
    {
        var gameId = await CreateGameAsync();
        await _restClient.PostAsJsonAsync($"/api/games/{gameId}/shots", new ShotRequestDto(0, 0));

        var reply = await _client.GetGameStateAsync(new GetGameStateRequest { GameId = gameId.ToString() });

        var entry = Assert.Single(reply.History);
        Assert.Equal(0, Assert.Single(entry.PlayerShots).Target.Row);
    }

    [Fact]
    public async Task GetGameState_ReturnsDifficulty()
    {
        var created = await _restClient.CreateGameAsync(new CreateGameRequestDto(Difficulty: nameof(AiDifficulty.Easy)));

        var reply = await _client.GetGameStateAsync(new GetGameStateRequest { GameId = created.GameId.ToString() });

        Assert.Equal(nameof(AiDifficulty.Easy), reply.Options.Difficulty);
    }
}
