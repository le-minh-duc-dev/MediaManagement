using FluentValidation;
using MediaManagement.Contracts.ErrorCodes;

namespace MediaManagement.Contracts.Authentication;

public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithErrorCode(ValidationErrorCodes.Required)
            .EmailAddress()
            .WithErrorCode(ValidationErrorCodes.InvalidFormat);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithErrorCode(errorCode: ValidationErrorCodes.Required);
    }
}
