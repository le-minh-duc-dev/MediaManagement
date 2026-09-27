using FluentValidation;
using FluentValidation.Results;
using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace MediaManagement.Api.Validation;

public sealed class ValidationActionFilter(IOptions<JsonOptions> jsonOptions) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        List<Error> errors = [];
        foreach (ParameterDescriptor parameter in context.ActionDescriptor.Parameters)
        {
            if (
                parameter.BindingInfo?.BindingSource == BindingSource.Services
                || !context.ActionArguments.TryGetValue(parameter.Name, out object? value)
                || value is null
            )
            {
                continue;
            }

            Type validatorType = typeof(IValidator<>).MakeGenericType(parameter.ParameterType);
            foreach (
                IValidator validator in context
                    .HttpContext.RequestServices.GetServices(validatorType)
                    .Cast<IValidator>()
            )
            {
                ValidationResult result = await validator.ValidateAsync(
                    new ValidationContext<object>(value),
                    context.HttpContext.RequestAborted
                );
                errors.AddRange(
                    result.Errors.Select(failure =>
                        failure.ToError(
                            JsonFieldPath.Convert(
                                failure.PropertyName,
                                parameter.ParameterType,
                                jsonOptions.Value.JsonSerializerOptions
                            )
                        )
                    )
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
}
