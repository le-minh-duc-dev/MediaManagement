using FluentValidation;
using FluentValidation.Results;
using MediaManagement.Contracts;
using MediaManagement.Contracts.ErrorCodes;
using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace MediaManagement.ActionFilters;

public sealed class ValidationActionFilter(IOptions<JsonOptions> jsonOptions) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        List<Error> errors = [];
        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (
                parameter.BindingInfo?.BindingSource == BindingSource.Services
                || !context.ActionArguments.TryGetValue(parameter.Name, out var value)
                || value is null
            )
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(parameter.ParameterType);
            foreach (
                var validator in context
                    .HttpContext.RequestServices.GetServices(validatorType)
                    .Cast<IValidator>()
            )
            {
                var result = await validator.ValidateAsync(
                    new ValidationContext<object>(value),
                    context.HttpContext.RequestAborted
                );

                errors.AddRange(
                    result.Errors.Select(failure => new Error(
                        LocalizableCode(failure.ErrorCode),
                        JsonFieldPath.Normalize(
                            failure.PropertyName,
                            parameter.ParameterType,
                            jsonOptions.Value.JsonSerializerOptions
                        ),
                        Parameters(failure)
                    ))
                );
            }
        }

        if (errors.Count > 0)
        {
            context.Result = ApiProblems.ToActionResult(
                context.HttpContext,
                ErrorType.Validation,
                errors
            );
            return;
        }
        await next();
    }

    private static string LocalizableCode(string code) =>
        !string.IsNullOrWhiteSpace(code)
        && code.Any(c => c is '.' or ':')
        && code.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or ':' or '_')
            ? code
            : ValidationErrorCodes.InvalidValue;

    private static ErrorParameters Parameters(ValidationFailure failure)
    {
        if (failure.CustomState is ErrorParameters explicitParameters)
        {
            return explicitParameters;
        }

        Dictionary<string, object?> parameters = [];
        // Rule limits are safe localization arguments; attempted values and arbitrary state are not.
        foreach (var (placeholder, name) in new[]
            { ("MaxLength", "maxLength"), ("MinLength", "minLength") })
        {
            if (failure.FormattedMessagePlaceholderValues?.TryGetValue(placeholder, out var limit) == true
                && limit is int)
            {
                parameters[name] = limit;
            }
        }
        return new ErrorParameters(parameters);
    }
}
