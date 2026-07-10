using DirectorPrompt.Domain.Models;

namespace DirectorPrompt.Domain.Repositories;

public interface IRoundChangeRepository
{
    Task RecordCreateAsync(long roundID, string tableName, long recordID, string? oldDataJSON = null, CancellationToken cancellationToken = default);

    Task RecordUpdateAsync(long roundID, string tableName, long recordID, string oldDataJSON, CancellationToken cancellationToken = default);

    Task RecordDeleteAsync(long roundID, string tableName, long recordID, string oldDataJSON, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoundChange>> GetByRoundAsync(long sessionID, long roundID, CancellationToken cancellationToken = default);

    Task RollbackRoundAsync(long sessionID, long roundID, CancellationToken cancellationToken = default);

    Task RemoveByRoundAsync(long sessionID, long roundID, CancellationToken cancellationToken = default);
}
