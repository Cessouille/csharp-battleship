using FluentValidation;

namespace BattleShip.API.Validation;

public static class EnumValidationExtensions
{
    /// <summary>
    /// Comparaison aux noms déclarés de <typeparamref name="TEnum"/>, pas <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> :
    /// ce dernier accepterait aussi une valeur numérique ("7") ou insensible à la casse ("classic").
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeEnumName<T, TEnum>(this IRuleBuilder<T, string> rule, string fieldName)
        where TEnum : struct, Enum =>
        rule.Must(value => Enum.GetNames<TEnum>().Contains(value))
            .WithMessage($"{fieldName} doit valoir l'une des valeurs : {string.Join(", ", Enum.GetNames<TEnum>())}.");
}
