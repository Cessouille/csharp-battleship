using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class CreateGameRequestDtoValidator : AbstractValidator<CreateGameRequestDto>
{
    public CreateGameRequestDtoValidator() =>
        // Comparaison aux noms déclarés, pas Enum.TryParse : ce dernier accepterait aussi "7" ou "classic".
        RuleFor(r => r.ShotMode).Must(mode => Enum.GetNames<ShotMode>().Contains(mode))
            .WithMessage($"ShotMode doit valoir l'une des valeurs : {string.Join(", ", Enum.GetNames<ShotMode>())}.");
}
