using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>
/// S-09 — 👛 Panoplie complète (docs/ticket.md TICKET-13) : gagner avec Radar + Salvo + armes spéciales, les
/// trois options activées ET réellement utilisées (au moins un scan et une arme dans l'historique) — Salvo est
/// un mode de la partie entière, pas une action ponctuelle, donc pas d'« usage » à vérifier pour lui.
/// </summary>
public class FullKitRuleTests
{
    private static readonly FullKitRule Rule = new();

    private static readonly GameOptions AllOptions = new()
    {
        Radar = true,
        ShotMode = ShotMode.Salvo,
        SpecialWeapons = true
    };

    private static TurnResult ScanTurn() =>
        new([], new ScanResult(new Coordinate(0, 0), false), null, [], null, null, GameStatus.InProgress);

    private static TurnResult WeaponTurn() =>
        new([], null, WeaponKind.Torpedo, [], null, null, GameStatus.InProgress);

    private static TurnResult PlainShotTurn() =>
        new([new ShotResolution(new Coordinate(1, 1), ShotOutcome.Miss, null)], null, null, [], null, null, GameStatus.InProgress);

    [Fact]
    public void Victory_WithAllThreeOptionsUsed_Unlocks()
    {
        var context = AchievementContexts.New(
            options: AllOptions,
            winner: PlayerId.Human,
            status: GameStatus.Finished,
            history: [ScanTurn(), WeaponTurn()]);

        Assert.True(Rule.IsUnlocked(context));
    }

    [Fact]
    public void RadarEnabledButNeverUsed_DoesNotUnlock()
    {
        var context = AchievementContexts.New(
            options: AllOptions,
            winner: PlayerId.Human,
            status: GameStatus.Finished,
            history: [PlainShotTurn(), WeaponTurn()]);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void WeaponsEnabledButNeverUsed_DoesNotUnlock()
    {
        var context = AchievementContexts.New(
            options: AllOptions,
            winner: PlayerId.Human,
            status: GameStatus.Finished,
            history: [ScanTurn(), PlainShotTurn()]);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void OneOptionDisabled_DoesNotUnlock()
    {
        var options = AllOptions with { Radar = false };
        var context = AchievementContexts.New(
            options: options,
            winner: PlayerId.Human,
            status: GameStatus.Finished,
            history: [ScanTurn(), WeaponTurn()]);

        Assert.False(Rule.IsUnlocked(context));
    }

    [Fact]
    public void AllOptionsUsed_WithoutVictory_DoesNotUnlock()
    {
        var context = AchievementContexts.New(
            options: AllOptions,
            winner: null,
            status: GameStatus.InProgress,
            history: [ScanTurn(), WeaponTurn()]);

        Assert.False(Rule.IsUnlocked(context));
    }
}
