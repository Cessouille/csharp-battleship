namespace BattleShip.App.Game;

/// <summary>
/// Traduit le motif de refus renvoyé par le serveur (champ <c>code</c> d'un 409 REST, détail d'une erreur gRPC)
/// en message lisible. Le client ne décide jamais lui-même qu'un coup est invalide : il affiche le verdict du serveur.
/// </summary>
public static class MoveErrorMessages
{
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["OutOfGrid"] = "Coordonnées hors grille.",
        ["AlreadyPlayed"] = "Case déjà jouée.",
        ["GameAlreadyFinished"] = "La partie est terminée.",
        ["RadarDisabled"] = "Le radar n'est pas activé pour cette partie.",
        ["NoScansLeft"] = "Plus aucun scan radar disponible.",
        ["WrongShotMode"] = "Ce type de tir ne correspond pas au mode de la partie.",
        ["WrongSalvoSize"] = "La salve doit compter exactement un tir par navire encore à flot.",
        ["DuplicateTarget"] = "Une même case est visée deux fois.",
        ["WeaponsDisabled"] = "Les armes spéciales ne sont pas activées pour cette partie.",
        ["NoAmmoLeft"] = "Plus de munition pour cette arme."
    };

    public static string For(string? code) =>
        code is not null && Messages.TryGetValue(code, out var message)
            ? $"Coup refusé : {message}"
            : $"Coup refusé par le serveur ({code ?? "motif inconnu"}).";
}
