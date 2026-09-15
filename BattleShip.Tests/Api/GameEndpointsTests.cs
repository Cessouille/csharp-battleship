using System.Net;
using System.Net.Http.Json;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GameEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<CreateGameResponseDto> CreateGameAsync()
    {
        var response = await _client.PostAsync("/api/games", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateGameResponseDto>())!;
    }

    [Fact]
    public async Task PostGames_Returns201_WithBothBoardViews()
    {
        var response = await _client.PostAsync("/api/games", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateGameResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(Fleet.Standard.Count, body!.MyBoard.Ships.Count);
        Assert.Empty(body.OpponentBoard.Hits);
        Assert.Empty(body.OpponentBoard.Misses);
        Assert.Empty(body.OpponentBoard.SunkShips);
    }

    [Fact]
    public async Task PostShots_ValidCoordinate_Returns200_WithTurnResult()
    {
        var game = await CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(0, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TurnResultDto>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.PlayerShot.Target.Row);
        Assert.Equal(0, body.PlayerShot.Target.Column);
    }

    [Fact]
    public async Task PostShots_ReplayingAlreadyPlayedCell_Returns409_AndStateUnchanged()
    {
        var game = await CreateGameAsync();
        await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(0, 0));
        var stateAfterFirstShot = await _client.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");

        var replay = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(0, 0));

        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        var stateAfterReplay = await _client.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
        var revealedBefore = stateAfterFirstShot!.OpponentBoard.Hits.Count + stateAfterFirstShot.OpponentBoard.Misses.Count;
        var revealedAfter = stateAfterReplay!.OpponentBoard.Hits.Count + stateAfterReplay.OpponentBoard.Misses.Count;
        Assert.Equal(revealedBefore, revealedAfter);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(10, 0)]
    [InlineData(0, 10)]
    public async Task PostShots_OutOfGridCoordinate_Returns400ValidationProblem(int row, int column)
    {
        var game = await CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(row, column));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostShots_OnUnknownGameId_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/games/{Guid.NewGuid()}/shots", new ShotRequestDto(0, 0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostShots_AfterGameFinished_Returns409ForFurtherMoves()
    {
        var game = await CreateGameAsync();

        GameStateDto? state = null;
        for (var row = 0; row < BoardGrid.Size; row++)
        {
            for (var column = 0; column < BoardGrid.Size; column++)
            {
                await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(row, column));
                state = await _client.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
                if (state!.Status == GameStatus.Finished.ToString())
                    break;
            }

            if (state!.Status == GameStatus.Finished.ToString())
                break;
        }

        Assert.Equal(GameStatus.Finished.ToString(), state!.Status);

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(0, 0));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetGameState_OnUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetGameState_OpponentBoardView_RevealsExactlyOneCellPerShotTaken()
    {
        var game = await CreateGameAsync();
        var shotsTaken = 0;
        for (var row = 0; row < 3 && shotsTaken < 9; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(row, column));
                if (response.StatusCode == HttpStatusCode.OK)
                    shotsTaken++;
                else
                    break; // partie terminée avant d'épuiser la boucle : arrêter, ne pas fausser le compte
            }
        }

        var state = await _client.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");

        // Chaque tir reçu par le plateau adverse tombe dans exactement un des trois compartiments
        // (miss / hit non coulé / case d'un navire coulé) : jamais plus de cases révélées que de tirs joués.
        var revealedOpponentCells = state!.OpponentBoard.Hits.Count
            + state.OpponentBoard.Misses.Count
            + state.OpponentBoard.SunkShips.Sum(s => s.Cells.Count);
        Assert.Equal(shotsTaken, revealedOpponentCells);
    }
}
