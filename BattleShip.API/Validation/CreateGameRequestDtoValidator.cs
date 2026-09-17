using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class CreateGameRequestDtoValidator : AbstractValidator<CreateGameRequestDto>
{
    public CreateGameRequestDtoValidator()
    {
        RuleFor(r => r.ShotMode).MustBeEnumName<CreateGameRequestDto, ShotMode>("ShotMode");
        RuleFor(r => r.Difficulty).MustBeEnumName<CreateGameRequestDto, AiDifficulty>("Difficulty");

        // PlayerId n'a pas besoin d'exister déjà (TICKET-14, profil dérivé) : seule la forme GUID est vérifiée ici.
        When(r => r.PlayerId is not null, () =>
        {
            RuleFor(r => r.PlayerId).Must(id => Guid.TryParse(id, out _))
                .WithMessage("PlayerId doit être un GUID valide.");
        });

        // La forme seulement : composition exacte de la flotte et chevauchement restent du ressort du domaine
        // (Game.TryCreateManual), même répartition FluentValidation/domaine que pour les tirs.
        When(r => r.Placements is not null, () =>
        {
            RuleFor(r => r.Placements!.Count).Equal(Fleet.Standard.Count)
                .WithMessage($"Placements doit contenir exactement {Fleet.Standard.Count} navires.");

            RuleForEach(r => r.Placements!).ChildRules(placement =>
            {
                placement.RuleFor(p => p.Kind).MustBeEnumName<ShipPlacementDto, ShipKind>("Kind");
                placement.RuleFor(p => p.Orientation).MustBeEnumName<ShipPlacementDto, Orientation>("Orientation");
                placement.RuleFor(p => p.Row).InclusiveBetween(0, BoardGrid.Size - 1);
                placement.RuleFor(p => p.Column).InclusiveBetween(0, BoardGrid.Size - 1);
            });
        });
    }
}
