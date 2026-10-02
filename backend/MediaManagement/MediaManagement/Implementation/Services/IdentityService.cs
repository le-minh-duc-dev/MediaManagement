using MediaManagement.Contracts.Authentication;
using MediaManagement.Entities;
using MediaManagement.Interfaces.Repositories;
using MediaManagement.Interfaces.Services;
using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Identity;

namespace MediaManagement.Implementation.Services;

public sealed class IdentityService(
    IUserProfileRepository userProfileRepository,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ICurrentUser CurrentUser
) : ServiceBase(CurrentUser), IIdentityService
{
    public async Task<Result> LoginAsync(
        LoginRequestDto loginRequestDto,
        CancellationToken cancellationToken
    )
    {
        var user = await userManager.FindByEmailAsync(loginRequestDto.Email);

        if (user is null)
        {
            return Result.Failure(ErrorType.Unauthorized);
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            loginRequestDto.Password,
            isPersistent: false,
            lockoutOnFailure: true
        );

        return !result.Succeeded ? Result.Failure(ErrorType.Unauthorized) : Result.Success();
    }

    public async Task<Result> SignupAsync(
        SignupRequestDto signupRequestDto,
        CancellationToken cancellationToken
    )
    {
        var user = await userManager.FindByEmailAsync(signupRequestDto.Email);

        return user is { }
            ? Result.Failure(ErrorType.Conflict)
            : await userProfileRepository.ExecuteInTransactionAsync(
                async cancellationToken =>
                {
                    var newUser = new IdentityUser
                    {
                        Id = CreateNewGuid().ToString(),
                        UserName =
                            signupRequestDto.Username
                            ?? GenerateUsernameFromEmail(signupRequestDto.Email),
                        Email = signupRequestDto.Email,
                    };

                    var identityResult = await userManager.CreateAsync(
                        newUser,
                        signupRequestDto.Password
                    );

                    if (!identityResult.Succeeded)
                    {
                        return Result.Failure(ErrorType.Unexpected);
                    }

                    var newUserProfile = new UserProfile
                    {
                        Id = CreateNewGuid(),
                        Avatar =
                            signupRequestDto.AvatarAssetId.HasValue
                            && signupRequestDto.AvatarAssetId.Value != Guid.Empty
                                ? Image.CreateDefaultImageWithMediaAssetId(
                                    CreateNewGuid(),
                                    CreateNewGuid(),
                                    signupRequestDto.AvatarAssetId.Value
                                )
                                : null,
                        UserId = newUser.Id,
                    };

                    userProfileRepository.Add(newUserProfile);

                    return Result.Success();
                },
                cancellationToken
            );
    }

    private static string GenerateUsernameFromEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email, nameof(email));
        var atIndex = email.IndexOf('@');
        return atIndex > 0
            ? email[..atIndex]
            : throw new ArgumentException("Invalid email address");
    }
}
