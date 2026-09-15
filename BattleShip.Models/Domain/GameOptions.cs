namespace BattleShip.Models.Domain;

public enum ShotMode
{
    Classic,
    Salvo
}

/// <summary>Règles optionnelles choisies à la création de la partie et figées ensuite (voir docs/adr/0009-options-de-partie.md).</summary>
public sealed record GameOptions
{
    public static GameOptions Classic { get; } = new();

    public bool Radar { get; init; }

    public ShotMode ShotMode { get; init; } = ShotMode.Classic;

    public bool SpecialWeapons { get; init; }
}
