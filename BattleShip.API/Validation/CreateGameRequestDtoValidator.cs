using BattleShip.Models.Contracts;
using BattleShip.Models.Domain;
using FluentValidation;

namespace BattleShip.API.Validation;

public sealed class CreateGameRequestDtoValidator : AbstractValidator<CreateGameRequestDto>
{
    public CreateGameRequestDtoValidator()
    {
        // Comparaison aux noms déclarés, pas Enum.TryParse : ce dernier accepterait aussi "7" ou "classic".
        RuleFor(r => r.ShotMode).Must(mode => Enum.GetNames<ShotMode>().Contains(mode))
            .WithMessage($"ShotMode doit valoir l'une des valeurs : {string.Join(", ", Enum.GetNames<ShotMode>())}.");

        // La forme seulement : composition exacte de la flotte et chevauchement restent du ressort du domaine
        // (Game.TryCreateManual), même répartition FluentValidation/domaine que pour les tirs.
        When(r => r.Placements is not null, () =>
        {
            RuleFor(r => r.Placements!.Count).Equal(Fleet.Standard.Count)
                .WithMessage($"Placements doit contenir exactement {Fleet.Standard.Count} navires.");

            RuleForEach(r => r.Placements!).ChildRules(placement =>
            {
                placement.RuleFor(p => p.Kind).Must(k => Enum.GetNames<ShipKind>().Contains(k))
                    .WithMessage($"Kind doit valoir l'une des valeurs : {string.Join(", ", Enum.GetNames<ShipKind>())}.");
                placement.RuleFor(p => p.Orientation).Must(o => Enum.GetNames<Orientation>().Contains(o))
                    .WithMessage($"Orientation doit valoir l'une des valeurs : {string.Join(", ", Enum.GetNames<Orientation>())}.");
                placement.RuleFor(p => p.Row).InclusiveBetween(0, BoardGrid.Size - 1);
                placement.RuleFor(p => p.Column).InclusiveBetween(0, BoardGrid.Size - 1);
            });
        });
    }
}
