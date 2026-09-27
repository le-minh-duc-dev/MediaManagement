namespace MediaManagement.Models.Results;

public class Result
{
    public bool IsSuccess => ErrorType is null;
    public bool IsFailure => !IsSuccess;
    public ErrorType? ErrorType { get; }
    public IReadOnlyList<Error> Errors { get; }

    private protected Result(ErrorType? errorType, IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Error[] copy = [.. errors];
        if (errorType is { } category && !Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(errorType));
        }

        if (
            copy.Any(error => error is null)
            || (errorType is null ? copy.Length != 0 : copy.Length == 0)
        )
        {
            throw new ArgumentException(
                "Success must have no errors; failure must have at least one non-null error.",
                nameof(errors)
            );
        }

        ErrorType = errorType;
        Errors = Array.AsReadOnly(copy);
    }

    public static Result Success() => new(null, []);

    public static Result Failure(ErrorType errorType, params Error[] errors) =>
        new(errorType, errors);
}

public sealed class Result<T> : Result
{
    private readonly T? value;

    public T Value =>
        IsSuccess ? value! : throw new InvalidOperationException("A failed result has no value.");

    private Result(T? value, ErrorType? errorType, IEnumerable<Error> errors)
        : base(errorType, errors)
    {
        this.value = value;
    }

    public static Result<T> Success(T value) => new(value, null, []);

    public static new Result<T> Failure(ErrorType errorType, params Error[] errors) =>
        new(default, errorType, errors);
}
