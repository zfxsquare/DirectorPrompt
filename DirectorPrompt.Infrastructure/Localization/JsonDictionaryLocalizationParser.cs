using System.Collections.Frozen;
using System.Text.Json;

namespace DirectorPrompt.Infrastructure.Localization;

public sealed class JSONDictionaryLocalizationParser : ILocalizationParser
{
    private static readonly FrozenDictionary<string, string> EmptyResource =
        new Dictionary<string, string>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JSONOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FrozenDictionary<string, string> Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream, JSONOptions) ?? [];

        return dict.Count == 0 ?
                   EmptyResource :
                   dict.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
