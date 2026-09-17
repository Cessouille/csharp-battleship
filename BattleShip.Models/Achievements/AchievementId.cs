namespace BattleShip.Models.Achievements;

/// <summary>
/// Catalogue des 11 succès (docs/ticket.md TICKET-13/14, docs/adr/0017-systeme-de-succes.md). Les noms sont
/// exposés tels quels au client (voir <see cref="AchievementContext"/>) : les figer ici, jamais les renommer
/// une fois livrés — un catalogue App plus ancien doit pouvoir ignorer un identifiant inconnu, pas planter
/// sur un identifiant renommé.
/// </summary>
public enum AchievementId
{
    /// <summary>💖 Cœur de tirs — dessiner un cœur 5×5 avec ses tirs.</summary>
    CoeurDeTirs,

    /// <summary>🛡️ Sans une égratignure — gagner sans perdre de navire.</summary>
    SansUneEgratignure,

    /// <summary>👑 Reine du difficile — gagner 3 parties en Difficile (inter-parties, TICKET-14).</summary>
    ReineDuDifficile,

    /// <summary>💫 Série étincelante — toucher 5 fois d'affilée.</summary>
    SerieEtincelante,

    /// <summary>💎 Quatre coins — gagner en ayant tiré dans chaque coin.</summary>
    QuatreCoins,

    /// <summary>📏 Rangée parfaite — flotte placée sur deux lignes au plus, évalué dès la création.</summary>
    RangeeParfaite,

    /// <summary>💘 Coup de foudre — toucher dès le premier tir de la partie.</summary>
    CoupDeFoudre,

    /// <summary>🎀 Nœud papillon — dessiner un nœud papillon 5×5 avec ses tirs.</summary>
    NoeudPapillon,

    /// <summary>👛 Panoplie complète — gagner avec Radar + Salvo + armes spéciales, tous réellement utilisés.</summary>
    PanoplieComplete,

    /// <summary>✨ Baguette magique — couler un navire avec une arme spéciale.</summary>
    BaguetteMagique,

    /// <summary>🦋 Glow up — gagner avec un seul navire encore à flot.</summary>
    GlowUp
}
