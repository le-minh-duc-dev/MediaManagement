namespace MediaManagement.Models.Results;

public enum ErrorType
{
    Validation,
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    Unexpected,
}
