namespace BattleShip.Models.Domain;

public enum ShipKind
{
    PorteAvions,
    Croiseur,
    ContreTorpilleur,
    SousMarin,
    Torpilleur
}

public static class Fleet
{
    public static IReadOnlyList<(ShipKind Kind, int Size)> Standard { get; } =
    [
        (ShipKind.PorteAvions, 5),
        (ShipKind.Croiseur, 4),
        (ShipKind.ContreTorpilleur, 3),
        (ShipKind.SousMarin, 3),
        (ShipKind.Torpilleur, 2)
    ];
}
