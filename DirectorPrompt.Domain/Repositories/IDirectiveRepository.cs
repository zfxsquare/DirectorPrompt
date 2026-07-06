using DirectorPrompt.Domain.Models;

namespace DirectorPrompt.Domain.Repositories;

public interface IDirectiveRepository
{
    Task<IReadOnlyList<ActiveDirective>> GetActiveAsync(long sessionID, CancellationToken cancellationToken = default);

    Task<ActiveDirective> AddAsync(ActiveDirective directive, CancellationToken cancellationToken = default);

    Task RemoveAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveDirective>> DecrementTTLAsync(long sessionID, CancellationToken cancellationToken = default);
}
