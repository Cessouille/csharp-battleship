namespace BattleShip.Tests.TestData;

/// <summary>Horloge pilotable, pour contrôler le temps écoulé sur un défi de timing sans dépendre d'un vrai minuteur.</summary>
public sealed class FakeClock
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset Get() => Now;
}
