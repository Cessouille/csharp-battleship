namespace BattleShip.Tests.TestData;

/// <summary>Jeu de coordonnées hors grille 10x10, partagé par les tests qui vérifient ce rejet à des couches différentes (Board, Game, endpoint HTTP).</summary>
public static class OutOfGridCoordinates
{
    public static TheoryData<int, int> Values => new()
    {
        { -1, 0 },
        { 0, -1 },
        { 10, 0 },
        { 0, 10 }
    };
}
