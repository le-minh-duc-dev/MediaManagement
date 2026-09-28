using FluentValidation;
using FluentValidation.Results;
using MediaManagement.Models.Results;

namespace MediaManagement.Contracts.Validation;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, TProperty> WithParameters<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule,
        ErrorParameters parameters
    ) => rule.WithState(_ => parameters);

    public static Error ToError(this ValidationFailure failure, string? field) =>
        new(
            // Namespaced codes are the application's public contract. Built-in FV codes are not.
            !string.IsNullOrWhiteSpace(failure.ErrorCode) && failure.ErrorCode.Contains('.')
                ? failure.ErrorCode
                : "validation.invalid",
            field,
            failure.CustomState as ErrorParameters
        );
}
