using MediaManagement.Contracts.Authentication;
using MediaManagement.Models.Results;

namespace MediaManagement.Interfaces.Services;

public interface IIdentityService
{
    Task<Result> LoginAsync(LoginRequestDto loginRequestDto, CancellationToken cancellationToken);
    Task<Result> SignupAsync(
        SignupRequestDto signupRequestDto,
        CancellationToken cancellationToken
    );
}
