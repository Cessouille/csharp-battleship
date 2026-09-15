using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>
/// Contrôles de forme uniquement. La taille exacte attendue dépend de l'état de la partie (navires encore à flot) :
/// elle est vérifiée par Game.PlayHumanSalvo et refusée en 409, comme une case déjà jouée.
/// </summary>
public sealed class SalvoRequestDtoValidator : AbstractValidator<SalvoRequestDto>
{
    public SalvoRequestDtoValidator()
    {
        RuleFor(r => r.Shots).Cascade(CascadeMode.Stop).NotNull().NotEmpty()
            .Must(shots => shots.Count <= Fleet.Standard.Count)
            .WithMessage($"Une salve compte au plus {Fleet.Standard.Count} tirs.")
            .Must(shots => shots.Select(s => (s.Row, s.Column)).Distinct().Count() == shots.Count)
            .WithMessage("Une salve ne peut pas viser deux fois la même case.");
        RuleForEach(r => r.Shots).SetValidator(new ShotRequestDtoValidator());
    }
}
