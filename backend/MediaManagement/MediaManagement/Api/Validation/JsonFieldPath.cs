using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MediaManagement.Api.Validation;

public static partial class JsonFieldPath
{
    public static string? Convert(string? path, Type modelType, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "$")
        {
            return null;
        }

        if (path.StartsWith('$'))
        {
            path = path[1..].TrimStart('.');
        }

        List<string> segments = [];
        Type current = modelType;
        foreach (string segment in path.Split('.'))
        {
            int bracket = segment.IndexOf('[');
            string name = bracket < 0 ? segment : segment[..bracket];
            string indexes = bracket < 0 ? "" : segment[bracket..];
            current = Nullable.GetUnderlyingType(current) ?? current;
            PropertyInfo? property = current
                .GetProperties()
                .FirstOrDefault(p => p.Name == name || JsonName(p, options) == name);
            segments.Add($"{(property is null ? name : JsonName(property, options))}{indexes}");
            if (name.Length > 0)
            {
                current = property?.PropertyType ?? typeof(object);
            }

            foreach (Match _ in IndexPattern().Matches(indexes))
            {
                current = current.IsArray
                    ? current.GetElementType()!
                    : current
                        .GetInterfaces()
                        .Append(current)
                        .FirstOrDefault(t =>
                            t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                        )
                        ?.GetGenericArguments()[0]
                        ?? typeof(object);
            }
        }
        return string.Join('.', segments);
    }

    private static string JsonName(PropertyInfo property, JsonSerializerOptions options) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? options.PropertyNamingPolicy?.ConvertName(property.Name)
        ?? property.Name;

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex IndexPattern();
}
