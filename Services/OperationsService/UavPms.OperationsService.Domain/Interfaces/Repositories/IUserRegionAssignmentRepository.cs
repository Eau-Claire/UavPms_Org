using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IUserRegionAssignmentRepository
{
    Task<bool> ExistsAsync(Guid userId, Guid regionId, CancellationToken cancellationToken);
    Task<IReadOnlySet<Guid>> GetAssignedUserIdsAsync(Guid regionId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    Task<IReadOnlySet<Guid>> GetRegionIdsAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Region>> GetRegionsAsync(Guid userId, CancellationToken cancellationToken);
    Task AddAsync(UserRegionAssignment assignment, CancellationToken cancellationToken);
    Task<UserRegionAssignment?> GetAsync(Guid userId, Guid regionId, CancellationToken cancellationToken);
    void Remove(UserRegionAssignment assignment);
}
