using System.Net.Http.Json;
using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GrpcGameStateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Battleship.BattleshipClient _client;
    private readonly HttpClient _restClient;

    public GrpcGameStateTests(WebApplicationFactory<Program> factory)
    {
        _restClient = factory.CreateClient();
        var httpClient = factory.CreateDefaultClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
        var channel = GrpcChannel.ForAddress(httpClient.BaseAddress!, new GrpcChannelOptions { HttpClient = httpClient });
        _client = new Battleship.BattleshipClient(channel);
    }

    private async Task<Guid> CreateGameAsync()
    {
        var response = await _restClient.PostAsync("/api/games", null);
        var created = await response.Content.ReadFromJsonAsync<CreateGameResponseDto>();
        return created!.GameId;
    }

    [Fact]
    public async Task GetGameState_ExistingGame_ReturnsExpectedShape()
    {
        var gameId = await CreateGameAsync();

        var reply = await _client.GetGameStateAsync(new GetGameStateRequest { GameId = gameId.ToString() });

        Assert.Equal(gameId.ToString(), reply.GameId);
        Assert.Equal("InProgress", reply.Status);
        Assert.Equal(10, reply.MyBoard.Size);
        Assert.Equal(Models.Domain.Fleet.Standard.Count, reply.MyBoard.Ships.Count);
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
}
