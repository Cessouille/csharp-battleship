namespace BattleShip.App.Game;

/// <summary>Portée d'un succès : sur une seule partie (lu dans GameStateDto.Achievements) ou cumulé entre parties (PlayerProfileDto.Achievements).</summary>
public enum AchievementScope
{
    Partie,
    InterParties
}

/// <summary>
/// Présentation d'un succès — libellé, emoji, description, image (voir wwwroot/img/succes/CREDITS.md) : le
/// serveur n'expose que des identifiants (TICKET-12, docs/adr/0017-systeme-de-succes.md), toute la mise en
/// forme vit ici, côté App.
/// </summary>
public sealed record AchievementInfo(string Id, string Emoji, string Name, string Description, string ImagePath, AchievementScope Scope);

public static class AchievementCatalog
{
    public static IReadOnlyList<AchievementInfo> All { get; } =
    [
        new("CoeurDeTirs", "💖", "Cœur de tirs",
            "Dessiner un cœur avec vos tirs, dans une fenêtre 5×5 quelconque de la grille adverse.",
            "img/succes/coeur-de-tirs.svg", AchievementScope.Partie),
        new("SansUneEgratignure", "🛡️", "Sans une égratignure",
            "Gagner une partie sans qu'aucun de vos navires ne soit coulé.",
            "img/succes/sans-une-egratignure.svg", AchievementScope.Partie),
        new("ReineDuDifficile", "👑", "Reine du difficile",
            "Gagner 3 parties en difficulté Difficile.",
            "img/succes/reine-du-difficile.svg", AchievementScope.InterParties),
        new("SerieEtincelante", "💫", "Série étincelante",
            "Toucher 5 fois d'affilée.",
            "img/succes/serie-etincelante.svg", AchievementScope.Partie),
        new("QuatreCoins", "💎", "Quatre coins",
            "Gagner une partie en ayant tiré dans les quatre coins de la grille adverse.",
            "img/succes/quatre-coins.svg", AchievementScope.Partie),
        new("RangeeParfaite", "📏", "Rangée parfaite",
            "Placer votre flotte sur deux lignes au plus (placement manuel).",
            "img/succes/rangee-parfaite.svg", AchievementScope.Partie),
        new("CoupDeFoudre", "💘", "Coup de foudre",
            "Toucher dès le premier tir de la partie.",
            "img/succes/coup-de-foudre.svg", AchievementScope.Partie),
        new("NoeudPapillon", "🎀", "Nœud papillon",
            "Dessiner un nœud papillon avec vos tirs, dans une fenêtre 5×5 quelconque de la grille adverse.",
            "img/succes/noeud-papillon.svg", AchievementScope.Partie),
        new("PanoplieComplete", "👛", "Panoplie complète",
            "Gagner une partie avec Radar, Salvo et armes spéciales activés, en ayant utilisé le radar et une arme.",
            "img/succes/panoplie-complete.svg", AchievementScope.Partie),
        new("BaguetteMagique", "✨", "Baguette magique",
            "Couler un navire adverse avec une arme spéciale.",
            "img/succes/baguette-magique.svg", AchievementScope.Partie),
        new("GlowUp", "🦋", "Glow up",
            "Gagner une partie avec un seul de vos navires encore à flot.",
            "img/succes/glow-up.svg", AchievementScope.Partie)
    ];

    /// <summary>Un identifiant inconnu (App plus ancienne que l'API) est ignoré, jamais une exception.</summary>
    public static AchievementInfo? Find(string id) => All.FirstOrDefault(a => a.Id == id);
}
