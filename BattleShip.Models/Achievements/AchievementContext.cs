using BattleShip.Models.Domain;

namespace BattleShip.Models.Achievements;

/// <summary>Projection immuable d'un navire du joueur, pour ne jamais exposer aux règles le <see cref="Ship"/> mutable du domaine.</summary>
public sealed record OwnShipView(ShipKind Kind, IReadOnlyList<Coordinate> Cells, bool IsSunk);

/// <summary>
/// Tout ce qu'une <see cref="IAchievementRule"/> peut lire, et rien d'autre. Calqué sur l'argument anti-fuite
/// de <see cref="Ai.IComputerTargeting"/> : une règle reçoit ce que le joueur voit déjà (ses tirs, sa propre
/// flotte, les options, l'issue de la partie), jamais <c>ComputerBoard.Ships</c> ni un <see cref="Game"/>
/// vivant — aucune règle ne peut donc, même par erreur, lire ou muter une case adverse non découverte.
/// Vérifié par <c>AchievementContextTests.Context_ExposesNoOpponentFleet</c> (BattleShip.Tests).
/// </summary>
public sealed record AchievementContext(
    GameOptions Options,
    PlayerId? Winner,
    GameStatus Status,
    IReadOnlyList<TurnResult> History,
    IReadOnlySet<Coordinate> CellsFiredAt,
    IReadOnlyList<OwnShipView> OwnFleet)
{
    internal static AchievementContext From(Game game) => new(
        game.Options,
        game.Winner,
        game.Status,
        game.History,
        game.ComputerBoard.ShotsReceived,
        game.HumanBoard.Ships.Select(s => new OwnShipView(s.Kind, s.Cells, s.IsSunk)).ToList());
}
