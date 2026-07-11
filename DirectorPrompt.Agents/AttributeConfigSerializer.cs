using System.Text.Json;
using System.Text.Json.Serialization;

namespace DirectorPrompt.Agents;

public static class AttributeConfigSerializer
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T? Deserialize<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
