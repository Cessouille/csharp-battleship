namespace BattleShip.Models.Domain;

public enum ShotMode
{
    Classic,
    Salvo
}

/// <summary>Voir docs/adr/0015-ia-difficulte-reglable.md. Hard = comportement historique (grille de probabilité, ADR 0008), valeur par défaut pour ne rien casser.</summary>
public enum AiDifficulty
{
    Easy,
    Medium,
    Hard
}

/// <summary>Règles optionnelles choisies à la création de la partie et figées ensuite (voir docs/adr/0009-options-de-partie.md).</summary>
public sealed record GameOptions
{
    public static GameOptions Classic { get; } = new();

    public bool Radar { get; init; }

    public ShotMode ShotMode { get; init; } = ShotMode.Classic;

    public bool SpecialWeapons { get; init; }

    public AiDifficulty Difficulty { get; init; } = AiDifficulty.Hard;

    /// <summary>Voir docs/adr/0019-mini-jeu-de-precision.md. Active le mini-jeu de timing pour l'attaque et la défense ensemble.</summary>
    public bool PrecisionMinigame { get; init; }
}
