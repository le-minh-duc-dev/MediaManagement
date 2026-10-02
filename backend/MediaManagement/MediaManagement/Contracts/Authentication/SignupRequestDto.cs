namespace MediaManagement.Contracts.Authentication;

public record SignupRequestDto(
    string? Username,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    // Id of MediaAsset to be used as the user's avatar.
    Guid? AvatarAssetId
);
