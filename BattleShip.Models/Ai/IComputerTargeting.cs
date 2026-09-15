using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;

namespace BattleShip.Models.Ai;

/// <summary>
/// Stratégie de tir de l'ordinateur. Elle ne reçoit que la vue adverse produite par <see cref="BoardViewMapper"/>
/// (touches, ratés, navires coulés) : aucune implémentation ne peut lire les positions des navires non découverts.
/// </summary>
public interface IComputerTargeting
{
    /// <summary>
    /// Renvoie <paramref name="count"/> cibles distinctes choisies sur la même vue : en Salvo, l'ordinateur ne
    /// profite pas du résultat de ses premiers tirs pour placer les suivants, comme le joueur qui envoie sa salve
    /// d'un bloc (voir docs/adr/0011-mode-salvo.md).
    /// </summary>
    IReadOnlyList<Coordinate> PickTargets(OpponentBoardDto view, int count, Random rng);

    /// <summary>
    /// Arme à utiliser à la place des tirs de ce tour, ou <c>null</c> pour tirer normalement. N'est appelée que si
    /// les armes spéciales sont activées ; par défaut, une stratégie n'utilise jamais d'arme.
    /// </summary>
    WeaponAction? PickWeapon(OpponentBoardDto view, bool torpedoAvailable, bool airStrikeAvailable, Random rng) => null;
}
