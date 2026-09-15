using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class ShotRequestDtoValidator : AbstractValidator<ShotRequestDto>
{
    public ShotRequestDtoValidator()
    {
        RuleFor(r => r.Row).InclusiveBetween(0, BoardGrid.Size - 1);
        RuleFor(r => r.Column).InclusiveBetween(0, BoardGrid.Size - 1);
    }
}
