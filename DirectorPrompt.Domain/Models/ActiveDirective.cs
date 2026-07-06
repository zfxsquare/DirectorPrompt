using DirectorPrompt.Domain.Enums;

namespace DirectorPrompt.Domain.Models;

public record ActiveDirective
{
    public long ID { get; init; }

    public long ProjectID { get; init; }

    public long SessionID { get; init; }

    public DirectiveType Type { get; init; }

    public string Content { get; init; } = string.Empty;

    /// <summary>
    ///     剩余生效轮数, null 表示永久
    /// </summary>
    public int? TTL { get; init; }

    public DateTime CreatedAt { get; init; }
}
