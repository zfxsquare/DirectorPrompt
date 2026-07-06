using System.Collections.Frozen;

namespace DirectorPrompt.Infrastructure.Localization;

public interface ILocalizationParser
{
    FrozenDictionary<string, string> Parse(Stream stream);
}
