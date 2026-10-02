using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediaManagement.Contracts;

internal static class JsonFieldPath
{
    public static string? Normalize(string path, Type? type, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "$")
            return null;

        var segments = path.TrimStart('$', '.').Split('.');
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            var bracket = segment.IndexOf('[');
            var name = bracket < 0 ? segment : segment[..bracket];
            var suffix = bracket < 0 ? "" : segment[bracket..];
            var property = type?.GetProperties().FirstOrDefault(p =>
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                || p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == name);
            var jsonName = property?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? options.PropertyNamingPolicy?.ConvertName(property?.Name ?? name)
                ?? property?.Name ?? name;
            segments[index] = jsonName + suffix;
            type = property?.PropertyType;
            if (bracket >= 0 && type is not null)
                type = type.IsArray ? type.GetElementType() : type.GetInterfaces().Append(type)
                    .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    ?.GetGenericArguments()[0];
        }
        return string.Join('.', segments);
    }
}
