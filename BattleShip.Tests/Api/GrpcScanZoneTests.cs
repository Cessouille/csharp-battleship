using System.Net.Http.Json;
using BattleShip.Grpc;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GrpcScanZoneTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Battleship.BattleshipClient _client = ApiClients.CreateGrpcClient(factory);
    private readonly HttpClient _restClient = factory.CreateClient();

    private static ScanZoneRequest Scan(Guid gameId, int row, int column) =>
        new() { GameId = gameId.ToString(), Row = row, Column = column };

    [Fact]
    public async Task ScanZone_WithRadar_ReturnsScanComputerShotAndUpdatedState()
    {
        var game = await _restClient.CreateGameAsync(new CreateGameRequestDto(Radar: true));

        var reply = await _client.ScanZoneAsync(Scan(game.GameId, 4, 4));

        Assert.Equal(4, reply.Scan.Origin.Row);
        Assert.Single(reply.ComputerShots);
        Assert.Equal(RadarRules.ScansPerGame - 1, reply.State.Actions.ScansRemaining);
        Assert.Single(reply.State.OpponentBoard.Scans);
        Assert.Empty(reply.State.OpponentBoard.Hits);
        Assert.Empty(reply.State.OpponentBoard.Misses);

        // Le scan fait partie de l'état : il est relu à l'identique par le GET REST (hydratation après rechargement).
        var state = await _restClient.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
        Assert.Equal(reply.Scan.ShipDetected, Assert.Single(state!.OpponentBoard.Scans).ShipDetected);
    }

    [Fact]
    public async Task ScanZone_BeyondQuota_ThrowsFailedPrecondition()
    {
        var game = await _restClient.CreateGameAsync(new CreateGameRequestDto(Radar: true));
        for (var i = 0; i < RadarRules.ScansPerGame; i++)
            await _client.ScanZoneAsync(Scan(game.GameId, 0, 2 * i));

        var ex = await Assert.ThrowsAsync<RpcException>(() => _client.ScanZoneAsync(Scan(game.GameId, 4, 4)).ResponseAsync);

        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.NoScansLeft), ex.Status.Detail);
    }

    [Fact]
    public async Task ScanZone_OnClassicGame_ThrowsFailedPrecondition()
    {
        var game = await _restClient.CreateGameAsync();

        var ex = await Assert.ThrowsAsync<RpcException>(() => _client.ScanZoneAsync(Scan(game.GameId, 0, 0)).ResponseAsync);

        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.RadarDisabled), ex.Status.Detail);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(0, 9)]
    [InlineData(-1, 0)]
    public async Task ScanZone_WithZoneOutsideGrid_ThrowsInvalidArgument(int row, int column)
    {
        var game = await _restClient.CreateGameAsync(new CreateGameRequestDto(Radar: true));

        var ex = await Assert.ThrowsAsync<RpcException>(() => _client.ScanZoneAsync(Scan(game.GameId, row, column)).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        var state = await _restClient.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
        Assert.Empty(state!.OpponentBoard.Scans);
    }

    [Fact]
    public async Task ScanZone_UnknownGame_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() => _client.ScanZoneAsync(Scan(Guid.NewGuid(), 0, 0)).ResponseAsync);

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task ScanZone_MalformedGameId_ThrowsInvalidArgument()
    {
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _client.ScanZoneAsync(new ScanZoneRequest { GameId = "pas-un-guid" }).ResponseAsync);

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }
}
