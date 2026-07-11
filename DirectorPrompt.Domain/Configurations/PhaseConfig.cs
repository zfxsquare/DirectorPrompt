using DirectorPrompt.Domain.Models;

namespace DirectorPrompt.Domain.Configurations;

public sealed record PhaseConfig
{
    public List<Phase> Phases { get; init; } = [];
}
