using System.Reflection;
using BattleShip.Models.Achievements;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Engine.Achievements;

/// <summary>
/// TICKET-12 (docs/ticket.md, docs/adr/0017-systeme-de-succes.md) : socle d'évaluation, indépendant de tout
/// succès particulier. Un navire d'une case pour le joueur et l'ordinateur, disposés pour ne partager aucune
/// ligne (donc jamais RangeeParfaite ici, sauf le test dédié).
/// </summary>
public class AchievementEvaluationTests
{
    private static Game CreateGame(GameOptions? options = null) =>
        new(
            Guid.NewGuid(),
            BuildBoard(new Coordinate(0, 0)),
            BuildBoard(new Coordinate(9, 9)),
            options,
            new BottomUpTargeting(),
            new Random(0));

    private static Board BuildBoard(Coordinate origin)
    {
        var board = new Board();
        board.TryPlaceShip(ShipKind.Torpilleur, origin, Orientation.Horizontal, 1);
        return board;
    }

    [Fact]
    public void NewGame_UnlocksOnlyPlacementAchievements()
    {
        // Flotte sur 1 seule ligne : débloque RangeeParfaite dès la création. Aucun autre succès ne doit
        // apparaître sur une partie qui vient de naître (historique vide) : une règle en .All(...)/.Any(...)
        // qui renvoie true sur une séquence vide serait un bug (vérité vacue).
        var game = CreateGame();

        Assert.Equal([AchievementId.RangeeParfaite], game.Achievements);
    }

    [Fact]
    public void RejectedMove_OutOfGrid_DoesNotChangeAchievements()
    {
        var game = CreateGame();
        var before = game.Achievements.ToList();

        game.PlayHumanShot(new Coordinate(-1, 0)); // hors grille : refusé

        Assert.Equal(before, game.Achievements);
    }

    [Fact]
    public void RejectedMove_AlreadyPlayed_DoesNotChangeAchievements()
    {
        var game = CreateGame();
        game.PlayHumanShot(new Coordinate(0, 1));
        var afterFirstShot = game.Achievements.ToList();

        game.PlayHumanShot(new Coordinate(0, 1)); // déjà jouée : refusé

        Assert.Equal(afterFirstShot, game.Achievements);
    }

    [Fact]
    public void RejectedMove_GameAlreadyFinished_DoesNotChangeAchievements()
    {
        var game = CreateGame();
        game.PlayHumanShot(new Coordinate(0, 0)); // coule le seul navire adverse : Winner = Human
        var afterVictory = game.Achievements.ToList();

        game.PlayHumanShot(new Coordinate(0, 1)); // partie finie : refusé

        Assert.Equal(afterVictory, game.Achievements);
    }

    [Fact]
    public void UnlockedAchievement_SurvivesFurtherTurns_WithoutDuplicate()
    {
        var game = CreateGame();

        game.PlayHumanShot(new Coordinate(0, 1));
        game.PlayHumanShot(new Coordinate(0, 2));

        Assert.Single(game.Achievements, id => id == AchievementId.RangeeParfaite);
    }

    /// <summary>
    /// Le catalogue se remplit ticket par ticket (voir docs/ticket.md) : à ce stade seule RangeeParfaite est
    /// implémentée. La couverture complète de l'enum (hors ReineDuDifficile, inter-parties) est vérifiée une
    /// fois toutes les règles intra-partie livrées, par
    /// AchievementRulesCoverageTests.EveryIntraGameAchievementId_HasExactlyOneRule.
    /// </summary>
    [Fact]
    public void AchievementRules_HaveNoDuplicateIds()
    {
        var ruleIds = AchievementRules.All.Select(r => r.Id).ToList();

        Assert.Equal(ruleIds.Distinct().Count(), ruleIds.Count);
    }

    [Fact]
    public void Achievements_FollowAchievementRulesOrder()
    {
        var game = CreateGame();
        var expectedOrder = AchievementRules.All.Select(r => r.Id).ToList();

        Assert.All(
            Enumerable.Range(1, game.Achievements.Count - 1),
            i => Assert.True(
                expectedOrder.IndexOf(game.Achievements[i - 1]) < expectedOrder.IndexOf(game.Achievements[i])));
    }
}

/// <summary>
/// Test de non-fuite structurel : AchievementContext ne doit exposer, directement ou via une collection,
/// que des informations déjà visibles par le joueur — jamais Board/Ship/Game (qui donneraient accès à la
/// flotte adverse cachée). Échoue à la compilation logique du test dès qu'un type interdit apparaît.
/// </summary>
public class AchievementContextTests
{
    private static readonly Type[] Allowed =
    [
        typeof(GameOptions), typeof(PlayerId), typeof(GameStatus), typeof(TurnResult),
        typeof(Coordinate), typeof(OwnShipView), typeof(ShipKind), typeof(ShotOutcome),
        typeof(ScanResult), typeof(WeaponKind), typeof(ShotResolution)
    ];

    private static readonly Type[] Forbidden = [typeof(Board), typeof(Ship), typeof(Game)];

    [Fact]
    public void Context_ExposesNoOpponentFleet()
    {
        foreach (var property in typeof(AchievementContext).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var elementType = UnwrapElementType(property.PropertyType);
            Assert.DoesNotContain(elementType, Forbidden);
            Assert.Contains(elementType, Allowed);
        }
    }

    private static Type UnwrapElementType(Type type)
    {
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            if (args.Length == 1)
                return Nullable.GetUnderlyingType(type) ?? args[0];
        }

        return type;
    }
}
