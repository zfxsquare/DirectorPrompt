namespace DirectorPrompt.Domain.Models;

public record ProjectImportResult
{
    public long ProjectID { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public int KnowledgeEntryCount { get; init; }

    public int StateAttributeCount { get; init; }

    public List<string> Warnings { get; init; } = [];
}
