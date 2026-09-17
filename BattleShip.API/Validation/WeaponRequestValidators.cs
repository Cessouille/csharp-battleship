using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class TorpedoRequestDtoValidator : AbstractValidator<TorpedoRequestDto>
{
    public TorpedoRequestDtoValidator()
    {
        RuleFor(r => r.From).MustBeEnumName<TorpedoRequestDto, Edge>("From");
        RuleFor(r => r.Lane).InclusiveBetween(0, BoardGrid.Size - 1);
    }
}

public sealed class AirStrikeRequestDtoValidator : AbstractValidator<AirStrikeRequestDto>
{
    public AirStrikeRequestDtoValidator()
    {
        RuleFor(r => r.Orientation).MustBeEnumName<AirStrikeRequestDto, Orientation>("Orientation");

        // Les enums sont vérifiés d'abord : on ne calcule l'emprise de la frappe que sur une orientation connue.
        RuleFor(r => r)
            .Must(r => WeaponRules.Cells(new WeaponAction.AirStrike(new Coordinate(r.Row, r.Column), Enum.Parse<Orientation>(r.Orientation))) is not null)
            .When(r => Enum.GetNames<Orientation>().Contains(r.Orientation))
            .WithName("AirStrike")
            .WithMessage($"Les {WeaponRules.AirStrikeLength} cases de la frappe doivent tenir dans la grille.");
    }
}
