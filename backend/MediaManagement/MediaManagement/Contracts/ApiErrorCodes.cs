namespace MediaManagement.Contracts;

public static class ApiErrorCodes
{
    public const string Validation = "ApiErrorCodes:Validation";
    public const string BadRequest = "ApiErrorCodes:BadRequest";
    public const string Unauthorized = "ApiErrorCodes:Unauthorized";
    public const string Forbidden = "ApiErrorCodes:Forbidden";
    public const string NotFound = "ApiErrorCodes:NotFound";
    public const string Conflict = "ApiErrorCodes:Conflict";
    public const string Unexpected = "ApiErrorCodes:Unexpected";
    public const string MethodNotAllowed = "ApiErrorCodes:MethodNotAllowed";
    public const string UnsupportedMediaType = "ApiErrorCodes:UnsupportedMediaType";
    public const string RateLimitExceeded = "ApiErrorCodes:RateLimitExceeded";
    public const string RequestFailed = "ApiErrorCodes:RequestFailed";
}
