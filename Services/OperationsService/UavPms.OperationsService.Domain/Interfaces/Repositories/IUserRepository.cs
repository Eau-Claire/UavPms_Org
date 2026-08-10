using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailWithRolesAsync(string email);
    Task<List<User>> GetUsersByRoleAsync(string roleName);
    Task<User?> GetByIdWithRolesAsync(Guid id);
    Task<(List<User> Items, int TotalItems)> GetUsersPagedAsync(int page, int pageSize, string? search);
}