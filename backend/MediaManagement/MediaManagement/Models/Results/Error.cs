namespace MediaManagement.Models.Results;

public sealed record Error
{
    public string Code { get; }
    public string? Field { get; }
    public ErrorParameters Parameters { get; }

    public Error(string code, string? field = null, ErrorParameters? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Field = string.IsNullOrWhiteSpace(field) ? null : field;
        Parameters = parameters ?? ErrorParameters.Empty;
    }
}
