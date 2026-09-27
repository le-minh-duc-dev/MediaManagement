using System.Collections.ObjectModel;

namespace MediaManagement.Models.Results;

/// <summary>Explicit, JSON-safe localization arguments. Never pass attempted input values here.</summary>
public sealed class ErrorParameters : ReadOnlyDictionary<string, object?>
{
    public static new ErrorParameters Empty { get; } = new(new Dictionary<string, object?>());

    public ErrorParameters(IDictionary<string, object?> parameters)
        : base(Copy(parameters)) { }

    private static Dictionary<string, object?> Copy(IDictionary<string, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Dictionary<string, object?> copy = [];
        foreach ((string? key, object? value) in parameters)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            bool supported =
                value
                    is null
                        or string
                        or bool
                        or byte
                        or sbyte
                        or short
                        or ushort
                        or int
                        or uint
                        or long
                        or ulong
                        or decimal
                || value is double d && double.IsFinite(d)
                || value is float f && float.IsFinite(f);
            if (!supported)
            {
                throw new ArgumentException(
                    $"Parameter '{key}' must be a finite JSON scalar.",
                    nameof(parameters)
                );
            }
            copy.Add(key, value);
        }
        return copy;
    }
}
