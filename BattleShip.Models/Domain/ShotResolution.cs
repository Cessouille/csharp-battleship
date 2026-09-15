namespace BattleShip.Models.Domain;

public enum ShotOutcome
{
    Miss,
    Hit,
    Sunk
}

public sealed record ShotResolution(Coordinate Target, ShotOutcome Outcome, ShipKind? SunkShipKind);
