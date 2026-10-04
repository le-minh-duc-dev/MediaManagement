using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MediaManagement.ActionFilters;

public sealed class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var errors = new List<ValidationError>();

        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (parameter.BindingInfo?.BindingSource == BindingSource.Services)
            {
                continue;
            }

            if (
                !context.ActionArguments.TryGetValue(parameter.Name, out var argument)
                || argument is null
            )
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(parameter.ParameterType);

            var validators = context
                .HttpContext.RequestServices.GetServices(validatorType)
                .Cast<IValidator>();

            foreach (var validator in validators)
            {
                var validationContext = new ValidationContext<object>(argument);

                var validationResult = await validator.ValidateAsync(
                    validationContext,
                    context.HttpContext.RequestAborted
                );

                errors.AddRange(validationResult.Errors.Select(MapValidationError));
            }
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(
                new ValidationProblemResponse
                {
                    Status = StatusCodes.Status400BadRequest,
                    TraceId = context.HttpContext.TraceIdentifier,
                    Errors = errors,
                }
            );

            return;
        }

        await next();
    }

    private static ValidationError MapValidationError(ValidationFailure failure)
    {
        return new ValidationError
        {
            Field = failure.PropertyName,
            Code = failure.ErrorCode,
            Message = failure.ErrorMessage,
        };
    }
}

public sealed class ValidationProblemResponse
{
    public int Status { get; init; }

    public string? TraceId { get; init; }

    public IReadOnlyCollection<ValidationError> Errors { get; init; } = [];
}

public sealed class ValidationError
{
    public required string Field { get; init; }

    public required string Code { get; init; }

    public required string Message { get; init; }
}
