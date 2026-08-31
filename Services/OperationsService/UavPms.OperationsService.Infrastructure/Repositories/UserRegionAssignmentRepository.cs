using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class UserRegionAssignmentRepository : IUserRegionAssignmentRepository
{
    private readonly ApplicationDbContext _context;
    public UserRegionAssignmentRepository(ApplicationDbContext context) => _context = context;

    public Task<bool> ExistsAsync(Guid userId, Guid regionId, CancellationToken ct) =>
        _context.UserRegionAssignments.AsNoTracking().AnyAsync(x => x.UserId == userId && x.RegionId == regionId, ct);

    public async Task<IReadOnlySet<Guid>> GetAssignedUserIdsAsync(Guid regionId, IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        (await _context.UserRegionAssignments.AsNoTracking().Where(x => x.RegionId == regionId && userIds.Contains(x.UserId)).Select(x => x.UserId).ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlySet<Guid>> GetRegionIdsAsync(Guid userId, CancellationToken ct) =>
        (await _context.UserRegionAssignments.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.RegionId).ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyList<Region>> GetRegionsAsync(Guid userId, CancellationToken ct) =>
        await _context.UserRegionAssignments.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Region).OrderBy(x => x.RegionName).ToListAsync(ct);

    public Task AddAsync(UserRegionAssignment assignment, CancellationToken ct) => _context.UserRegionAssignments.AddAsync(assignment, ct).AsTask();
    public Task<UserRegionAssignment?> GetAsync(Guid userId, Guid regionId, CancellationToken ct) => _context.UserRegionAssignments.FirstOrDefaultAsync(x => x.UserId == userId && x.RegionId == regionId, ct);
    public void Remove(UserRegionAssignment assignment) => _context.UserRegionAssignments.Remove(assignment);
}
