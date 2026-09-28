namespace MediaManagement.Models.Results;

public class Result
{
    public bool IsSuccess => ErrorType is null;
    public bool IsFailure => !IsSuccess;
    public ErrorType? ErrorType { get; }
    public IReadOnlyCollection<Error> Errors { get; }

    private protected Result(ErrorType? errorType, IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Error[] copy = [.. errors];
        if (errorType is { } category && !Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(errorType));
        }

        var hasNullError = copy.Any(static error => error is null);
        var invalidSuccess = errorType is null && copy.Length > 0;
        var invalidFailure = errorType is not null && copy.Length == 0;

        if (hasNullError || invalidSuccess || invalidFailure)
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
        if (IsSuccess && value is null)
        {
            throw new ArgumentNullException(
                nameof(value),
                "A successful result must have a non-null value."
            );
        }
        this.value = value;
    }

    public static Result<T> Success(T value)
    {
        return new(value, null, []);
    }

    public static new Result<T> Failure(ErrorType errorType, params Error[] errors) =>
        new(default, errorType, errors);
}
