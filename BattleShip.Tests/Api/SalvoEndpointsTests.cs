using System.Net;
using System.Net.Http.Json;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class SalvoEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<CreateGameResponseDto> CreateSalvoGameAsync() =>
        _client.CreateGameAsync(new CreateGameRequestDto(ShotMode: nameof(ShotMode.Salvo)));

    private static SalvoRequestDto Salvo(params (int Row, int Column)[] cells) =>
        new(cells.Select(c => new ShotRequestDto(c.Row, c.Column)).ToList());

    [Fact]
    public async Task PostGames_SalvoMode_ExposesSalvoSizeOfWholeFleet()
    {
        var game = await CreateSalvoGameAsync();

        Assert.Equal(nameof(ShotMode.Salvo), game.Options.ShotMode);
        Assert.Equal(Fleet.Standard.Count, game.Actions.SalvoSize);
    }

    [Theory]
    [InlineData("Turbo")]
    [InlineData("1")]
    [InlineData("salvo")]
    public async Task PostGames_UnknownShotMode_Returns400(string shotMode)
    {
        var response = await _client.PostAsJsonAsync("/api/games", new CreateGameRequestDto(ShotMode: shotMode));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSalvos_WithExpectedSize_Returns200_WithAllShots()
    {
        var game = await CreateSalvoGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/salvos", Salvo((5, 0), (5, 1), (5, 2), (5, 3), (5, 4)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var turn = await response.Content.ReadFromJsonAsync<TurnResultDto>();
        Assert.Equal(5, turn!.PlayerShots.Count);
        Assert.NotEmpty(turn.ComputerShots);
    }

    [Fact]
    public async Task PostSalvos_WithWrongSize_Returns409_AndNoShotApplied()
    {
        var game = await CreateSalvoGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/salvos", Salvo((5, 0), (5, 1)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.WrongSalvoSize), await response.ConflictCodeAsync());
        var state = await _client.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
        Assert.Empty(state!.OpponentBoard.Misses);
        Assert.Empty(state.OpponentBoard.Hits);
    }

    [Fact]
    public async Task PostSalvos_WithDuplicateCell_Returns400()
    {
        var game = await CreateSalvoGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/salvos", Salvo((5, 0), (5, 0), (5, 1), (5, 2), (5, 3)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSalvos_WithOutOfGridCell_Returns400()
    {
        var game = await CreateSalvoGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/salvos", Salvo((5, 0), (5, 1), (5, 2), (5, 3), (10, 0)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSalvos_Empty_Returns400()
    {
        var game = await CreateSalvoGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/salvos", Salvo());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSalvos_OnClassicGame_Returns409WrongShotMode()
    {
        var game = await _client.CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/salvos", Salvo((5, 0)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.WrongShotMode), await response.ConflictCodeAsync());
    }

    [Fact]
    public async Task PostShots_OnSalvoGame_Returns409WrongShotMode()
    {
        var game = await CreateSalvoGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(0, 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(nameof(MoveRejectionReason.WrongShotMode), await response.ConflictCodeAsync());
    }

    [Fact]
    public async Task PostSalvos_OnUnknownGame_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/games/{Guid.NewGuid()}/salvos", Salvo((5, 0)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
