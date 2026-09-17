using BattleShip.Models.Achievements;
using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using BattleShip.Tests.TestData;

namespace BattleShip.Tests.Contracts;

/// <summary>
/// Test de non-fuite comportemental (TICKET-12, docs/adr/0017-systeme-de-succes.md), complémentaire au test
/// structurel par réflexion (Engine/Achievements/AchievementEvaluationTests.AchievementContextTests) : deux
/// parties dont la projection visible par le joueur est rigoureusement identique, mais dont un navire adverse
/// caché occupe une position différente et jamais visée, doivent débloquer exactement les mêmes succès. Une
/// règle qui lirait ComputerBoard.Ships (au lieu du contexte anti-fuite) ferait diverger ce test.
/// </summary>
public class AchievementLeakTests
{
    /// <summary>
    /// Navire commun aux deux parties (5,3)-(5,6) + un navire d'une case dont seule la position diffère, en
    /// (0,0) ou (9,9) — jamais visé par la séquence d'actions ci-dessous (fenêtre du cœur en (2,2)-(6,6)).
    /// </summary>
    private static Game BuildGame(Coordinate hiddenShipCell)
    {
        var human = new Board();
        human.TryPlaceShip(ShipKind.Torpilleur, new Coordinate(0, 0), Orientation.Horizontal, 1);

        var computer = new Board();
        computer.TryPlaceShip(ShipKind.Croiseur, new Coordinate(5, 3), Orientation.Horizontal, 4);
        computer.TryPlaceShip(ShipKind.Torpilleur, hiddenShipCell, Orientation.Horizontal, 1);

        return new Game(Guid.NewGuid(), human, computer, GameOptions.Classic, new BottomUpTargeting(), new Random(0));
    }

    private static void FireTheHeartPattern(Game game)
    {
        foreach (var cell in ShotPatterns.Heart)
            game.PlayHumanShot(new Coordinate(2 + cell.Row, 2 + cell.Column));
    }

    [Fact]
    public void TwoGamesWithIdenticalVisibleProjection_UnlockTheSameAchievements()
    {
        var gameWithHiddenShipAtTopLeft = BuildGame(new Coordinate(0, 0));
        var gameWithHiddenShipAtBottomRight = BuildGame(new Coordinate(9, 9));

        FireTheHeartPattern(gameWithHiddenShipAtTopLeft);
        FireTheHeartPattern(gameWithHiddenShipAtBottomRight);

        var stateA = gameWithHiddenShipAtTopLeft.ToGameStateDto();
        var stateB = gameWithHiddenShipAtBottomRight.ToGameStateDto();

        // Précondition : ce que le joueur voit est rigoureusement identique (réutilise le chokepoint anti-fuite
        // existant, BoardViewMapper, plutôt que de le réécrire). Assert.Equivalent compare la structure, pas
        // l'identité des listes imbriquées (deux instances distinctes au contenu identique). GameId neutralisé :
        // deux Game distincts ont toujours des identifiants différents, sans rapport avec une fuite éventuelle.
        Assert.Equivalent(
            stateA with { GameId = Guid.Empty, Achievements = [] },
            stateB with { GameId = Guid.Empty, Achievements = [] },
            strict: true);

        // Sans ce garde-fou, le test passerait trivialement en comparant deux ensembles de succès vides.
        Assert.NotEmpty(gameWithHiddenShipAtTopLeft.Achievements);

        Assert.Equal(gameWithHiddenShipAtTopLeft.Achievements, gameWithHiddenShipAtBottomRight.Achievements);
    }
}
