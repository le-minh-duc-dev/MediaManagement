using System.Text.Json;
using MediaManagement.Api.Validation;
using MediaManagement.Contracts;
using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace MediaManagement.Contracts.Validation;

public static class InvalidRequestResponse
{
    public static IActionResult Create(ActionContext context)
    {
        JsonSerializerOptions options = context
            .HttpContext.RequestServices.GetRequiredService<IOptions<JsonOptions>>()
            .Value.JsonSerializerOptions;
        List<Error> errors = [];
        foreach (
            KeyValuePair<string, ModelStateEntry?> entry in context.ModelState.Where(entry =>
                entry.Value?.Errors.Count > 0
            )
        )
        {
            string path = entry.Key;
            ParameterDescriptor? parameter = context.ActionDescriptor.Parameters.FirstOrDefault(p =>
                path == p.Name || path.StartsWith($"{p.Name}.", StringComparison.Ordinal)
            );
            if (parameter is not null)
            {
                if (path != parameter.Name)
                {
                    path = path[(parameter.Name.Length + 1)..];
                }
                else if (parameter.BindingInfo?.BindingSource == BindingSource.Body)
                {
                    path = "";
                }
            }
            parameter ??= context.ActionDescriptor.Parameters.FirstOrDefault(p =>
                p.BindingInfo?.BindingSource == BindingSource.Body
            );
            string? field = JsonFieldPath.Convert(
                path,
                parameter?.ParameterType ?? typeof(object),
                options
            );
            errors.Add(new Error("request.invalid", field));
        }
        if (errors.Count == 0)
        {
            errors.Add(new Error("request.invalid"));
        }

        return ApiProblems.ToActionResult(context.HttpContext, ErrorType.BadRequest, errors);
    }
}
