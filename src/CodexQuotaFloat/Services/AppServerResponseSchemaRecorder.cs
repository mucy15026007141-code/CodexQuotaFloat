using System.Text.Json;
using System.IO;

namespace CodexQuotaFloat.Services;

/// <summary>Persists only response field names, JSON types, and placeholder values; never raw account data.</summary>
public static class AppServerResponseSchemaRecorder
{
    private static readonly object Gate = new();
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "token", "accessToken", "refreshToken", "cookie", "email", "accountId", "id", "session", "authorization", "authentication"
    };

    public static void Record(string directory, string responseName, JsonElement response)
    {
        try
        {
            var path = Path.Combine(directory, "app-server-response-schema.json");
            lock (Gate)
            {
                var document = File.Exists(path)
                    ? JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(path)) ?? new()
                    : new Dictionary<string, object?>();
                document[responseName] = Describe(response);
                File.WriteAllText(path, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { /* Schema recording must not affect quota refresh. */ }
    }

    private static object? Describe(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .Where(property => !SensitiveNames.Contains(property.Name))
            .ToDictionary(property => property.Name, property => Describe(property.Value)),
        JsonValueKind.Array => new Dictionary<string, object?> { ["type"] = "array", ["example"] = element.GetArrayLength() == 0 ? "[]" : new[] { Describe(element[0]) } },
        JsonValueKind.String => new Dictionary<string, string> { ["type"] = "string", ["example"] = "<string>" },
        JsonValueKind.Number => new Dictionary<string, string> { ["type"] = "number", ["example"] = "<number>" },
        JsonValueKind.True or JsonValueKind.False => new Dictionary<string, string> { ["type"] = "boolean", ["example"] = "<boolean>" },
        JsonValueKind.Null => new Dictionary<string, string> { ["type"] = "null", ["example"] = "null" },
        _ => new Dictionary<string, string> { ["type"] = element.ValueKind.ToString().ToLowerInvariant(), ["example"] = "<unknown>" }
    };
}
