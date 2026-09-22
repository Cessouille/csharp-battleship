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

    /// <summary>
    /// Voir docs/adr/0019-mini-jeu-de-precision.md. Le plateau ordinateur reste placé au hasard (ADR 0013) : on
    /// balaie la grille salve par salve jusqu'à ce qu'un défi s'ouvre (au plus 20 salves de 5, la flotte adverse
    /// occupe 17 cases sur 100). Vérifie uniquement que PlaySalvoReply.State expose le défi — le détail des règles
    /// de déclenchement est couvert par BattleShip.Tests.Engine.PrecisionMinigameTests (horloge contrôlable).
    /// </summary>
    [Fact]
    public async Task PlaySalvo_TriggeringAChallenge_ExposesItOnReplyState()
    {
        var game = await _restClient.CreateGameAsync(new CreateGameRequestDto(ShotMode: nameof(ShotMode.Salvo), PrecisionMinigame: true));

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var state = await _restClient.GetFromJsonAsync<GameStateDto>($"/api/games/{game.GameId}");
            if (state!.PendingChallenge is not null)
            {
                AssertWellFormed(state.PendingChallenge);
                return;
            }

            var played = state.OpponentBoard.Hits.Concat(state.OpponentBoard.Misses).Select(c => (c.Row, c.Column)).ToHashSet();
            var nextCells = AllCells().Where(c => !played.Contains(c)).Take(state.Actions.SalvoSize).ToArray();
            if (nextCells.Length < state.Actions.SalvoSize)
                break; // plateau épuisé : ne devrait pas arriver, la flotte occupe 17 cases sur 100

            var reply = await _client.PlaySalvoAsync(Salvo(game.GameId, nextCells));
            if (reply.State.PendingChallenge is { } fromReply)
            {
                AssertWellFormed(fromReply);
                return;
            }
        }

        Assert.Fail("Aucun défi de timing déclenché après 20 salves — la flotte adverse occupe pourtant 17 cases sur 100.");
    }

    private static void AssertWellFormed(TimingChallengeMessage challenge)
    {
        Assert.NotEmpty(challenge.ChallengeId);
        Assert.InRange(challenge.ZoneStart, 0, 1);
        Assert.InRange(challenge.ZoneStart + challenge.ZoneWidth, 0, 1);
        Assert.True(challenge.PeriodMs > 0);
    }

    private static void AssertWellFormed(TimingChallengeDto challenge)
    {
        Assert.NotEqual(Guid.Empty, challenge.ChallengeId);
        Assert.InRange(challenge.ZoneStart, 0, 1);
        Assert.InRange(challenge.ZoneStart + challenge.ZoneWidth, 0, 1);
        Assert.True(challenge.PeriodMs > 0);
    }

    private static IEnumerable<(int Row, int Column)> AllCells()
    {
        for (var row = 0; row < BoardGrid.Size; row++)
            for (var column = 0; column < BoardGrid.Size; column++)
                yield return (row, column);
    }
}
