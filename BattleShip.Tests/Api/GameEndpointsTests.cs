using System.Net;
using System.Net.Http.Json;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BattleShip.Tests.Api;

public class GameEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<CreateGameResponseDto> CreateGameAsync() => _client.CreateGameAsync();

    [Fact]
    public async Task PostGames_Returns201_WithBothBoardViews()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new CreateGameRequestDto());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateGameResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(Fleet.Standard.Count, body!.MyBoard.Ships.Count);
        Assert.Empty(body.OpponentBoard.Hits);
        Assert.Empty(body.OpponentBoard.Misses);
        Assert.Empty(body.OpponentBoard.SunkShips);
    }

    [Fact]
    public async Task PostGames_WithEmptyJsonObject_CreatesClassicGame()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto());

        Assert.False(game.Options.Radar);
        Assert.Equal(0, game.Actions.ScansRemaining);
    }

    [Fact]
    public async Task PostGames_WithRadar_ReturnsOptionsAndScanQuota()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(Radar: true));

        Assert.True(game.Options.Radar);
        Assert.Equal(RadarRules.ScansPerGame, game.Actions.ScansRemaining);
        var state = await _client.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
        Assert.Equal(game.Options, state!.Options);
    }

    [Fact]
    public async Task PostShots_ConcurrentRequestsOnSameCell_OnlyOneSucceeds()
    {
        // Même risque que Engine/GameTests.PlayHumanShot_ConcurrentCallsOnSameCell_OnlyOneIsAccepted, mais à
        // travers le pipeline HTTP complet (FluentValidation, InMemoryGameStore, Game.Locked) plutôt qu'un appel
        // direct sur Game : vérifie que le verrou tient aussi quand les requêtes traversent Minimal API.
        var game = await CreateGameAsync();
        var target = new ShotRequestDto(0, 0);
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(async () =>
            {
                start.Wait();
                return await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", target);
            }))
            .ToArray();
        start.Set();
        var responses = await Task.WhenAll(tasks);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToArray();
        Assert.Equal(15, conflicts.Length);
        foreach (var conflict in conflicts)
            Assert.Equal(nameof(MoveRejectionReason.AlreadyPlayed), await conflict.ConflictCodeAsync());
    }

    private static readonly ShipPlacementDto[] ValidStandardPlacements =
    [
        new(nameof(ShipKind.PorteAvions), 0, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.Croiseur), 2, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.ContreTorpilleur), 4, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.SousMarin), 6, 0, nameof(Orientation.Horizontal)),
        new(nameof(ShipKind.Torpilleur), 8, 0, nameof(Orientation.Horizontal)),
    ];

    [Fact]
    public async Task PostGames_WithValidManualPlacements_Returns201_WithThatExactFleet()
    {
        var game = await _client.CreateGameAsync(new CreateGameRequestDto(Placements: ValidStandardPlacements));

        Assert.Equal(Fleet.Standard.Count, game.MyBoard.Ships.Count);
        Assert.Contains(game.MyBoard.Ships, s => s.Kind == nameof(ShipKind.PorteAvions) && s.Cells[0] == new CoordinateDto(0, 0));
    }

    [Fact]
    public async Task PostGames_WithOverlappingManualPlacements_Returns409()
    {
        var overlapping = new ShipPlacementDto[]
        {
            new(nameof(ShipKind.PorteAvions), 0, 0, nameof(Orientation.Horizontal)),
            new(nameof(ShipKind.Croiseur), 0, 2, nameof(Orientation.Vertical)), // chevauche le PorteAvions
            new(nameof(ShipKind.ContreTorpilleur), 4, 0, nameof(Orientation.Horizontal)),
            new(nameof(ShipKind.SousMarin), 6, 0, nameof(Orientation.Horizontal)),
            new(nameof(ShipKind.Torpilleur), 8, 0, nameof(Orientation.Horizontal)),
        };

        var response = await _client.PostAsJsonAsync("/api/games", new CreateGameRequestDto(Placements: overlapping));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(nameof(FleetPlacementRejectionReason.OutOfGridOrOverlap), await response.ConflictCodeAsync());
    }

    [Fact]
    public async Task PostGames_WithWrongPlacementCount_Returns400()
    {
        var tooFew = ValidStandardPlacements.Take(4).ToList();

        var response = await _client.PostAsJsonAsync("/api/games", new CreateGameRequestDto(Placements: tooFew));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostGames_WithMalformedOptions_Returns400()
    {
        using var body = new StringContent("""{ "radar": "oui" }""", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/games", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostShots_ValidCoordinate_Returns200_WithTurnResult()
    {
        var game = await CreateGameAsync();

        var response = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/shots", new ShotRequestDto(0, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TurnResultDto>();
        Assert.NotNull(body);
        var playerShot = Assert.Single(body!.PlayerShots);
        Assert.Equal(0, playerShot.Target.Row);
        Assert.Equal(0, playerShot.Target.Column);
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
    [MemberData(nameof(OutOfGridCoordinates.Values), MemberType = typeof(OutOfGridCoordinates))]
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
