namespace MediaManagement.Contracts.ErrorCodes;

public class AuthenticationErrorCodes
{
    public const string InvalidCredentials = "AuthenticationErrorCodes:InvalidCredentials";
    public const string UserNotfound = "AuthenticationErrorCodes:UserNotfound";
    public const string AccountLocked = "AuthenticationErrorCodes:AccountLocked";
    public const string AccountDisabled = "AuthenticationErrorCodes:AccountDisabled";
    public const string AccountExpired = "AuthenticationErrorCodes:AccountExpired";
    public const string PasswordExpired = "AuthenticationErrorCodes:PasswordExpired";
    public const string PasswordTooShort = "AuthenticationErrorCodes:PasswordTooShort";
    public const string PasswordTooLong = "AuthenticationErrorCodes:PasswordTooLong";
    public const string PasswordInvalid = "AuthenticationErrorCodes:PasswordInvalid";
}
