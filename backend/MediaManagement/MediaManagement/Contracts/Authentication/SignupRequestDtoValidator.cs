using FluentValidation;
using MediaManagement.Contracts.ErrorCodes;

namespace MediaManagement.Contracts.Authentication;

public class SignupRequestDtoValidator : AbstractValidator<SignupRequestDto>
{
    public SignupRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .EmailAddress()
            .WithErrorCode(ValidationErrorCodes.InvalidFormat);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode(errorCode: ValidationErrorCodes.Required);

        RuleFor(x => x.FirstName).NotEmpty().WithErrorCode(ValidationErrorCodes.Required);

        RuleFor(x => x.LastName).NotEmpty().WithErrorCode(ValidationErrorCodes.Required);

        RuleFor(x => x.Username).NotEmpty().WithErrorCode(ValidationErrorCodes.Required);
    }
}
