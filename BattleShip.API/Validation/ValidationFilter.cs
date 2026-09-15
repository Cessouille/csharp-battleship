using FluentValidation;

namespace BattleShip.API.Validation;

/// <summary>Filtre d'endpoint générique : valide le premier argument de type <typeparamref name="T"/> avant d'exécuter le endpoint.</summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return await next(context);

        var result = await validator.ValidateAsync(argument);
        return result.IsValid
            ? await next(context)
            : Results.ValidationProblem(result.ToDictionary());
    }
}
