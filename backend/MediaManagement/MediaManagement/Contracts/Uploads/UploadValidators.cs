using FluentValidation;
using MediaManagement.Entities;
using MediaManagement.Models;
using Microsoft.Extensions.Options;

namespace MediaManagement.Contracts.Uploads;

public sealed class UploadItemValidator : AbstractValidator<UploadItemRequest>
{
    public UploadItemValidator(IOptions<UploadOptions> options)
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .MaximumLength(255)
            .WithErrorCode(ValidationErrorCodes.InvalidMaximumLength)
            .Must(x => x is null || !x.Any(char.IsControl))
            .WithErrorCode(ValidationErrorCodes.InvalidFormat);
        RuleFor(x => x.SizeBytes)
            .InclusiveBetween(1, options.Value.MaxFileSizeBytes)
            .WithErrorCode(ValidationErrorCodes.InvalidValue);
        RuleFor(x => x.ContentType)
            .Must(x => options.Value.AllowedContentTypes.Contains(x, StringComparer.Ordinal))
            .WithErrorCode(ValidationErrorCodes.InvalidValue);
    }
}

public sealed class CreateUploadSessionValidator : AbstractValidator<CreateUploadSessionRequest>
{
    public CreateUploadSessionValidator(IOptions<UploadOptions> options)
    {
        RuleFor(x => x.Items)
            .NotNull()
            .WithErrorCode(ValidationErrorCodes.Required)
            .Must(x => x is null || x.Count >= 1 && x.Count <= options.Value.MaxItems)
            .WithErrorCode(ValidationErrorCodes.InvalidRangeLength);
        RuleForEach(x => x.Items)
            .NotNull()
            .WithErrorCode(ValidationErrorCodes.Required)
            .SetValidator(new UploadItemValidator(options));
    }
}

public sealed class CompleteUploadSessionValidator : AbstractValidator<CompleteUploadSessionRequest>
{
    public CompleteUploadSessionValidator(IOptions<UploadOptions> options)
    {
        RuleFor(x => x.UploadedMediaAssetIds)
            .NotNull()
            .WithErrorCode(ValidationErrorCodes.Required)
            .Must(x => x is null || x.Count <= options.Value.MaxItems)
            .WithErrorCode(ValidationErrorCodes.InvalidMaximumLength)
            .Must(x => x is null || x.Distinct().Count() == x.Count)
            .WithErrorCode(BusinessErrorCodes.UploadSession.AssetIdsDuplicate);
        RuleForEach(x => x.UploadedMediaAssetIds)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.InvalidMinimumLength);
    }
}
