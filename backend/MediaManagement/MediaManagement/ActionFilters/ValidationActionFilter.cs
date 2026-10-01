using FluentValidation;
using MediaManagement.Contracts;
using MediaManagement.Models.Results;
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
                        failure.ErrorMessage,
                        failure.PropertyName
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
}
