using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByUsernameWithRolesAsync(string username);
    Task<User?> GetByEmailWithRolesAsync(string email);
    Task<List<User>> GetUsersByRoleAsync(string roleName);
    Task<User?> GetByIdWithRolesAsync(Guid id);
    Task<(List<User> Items, int TotalItems)> GetUsersPagedAsync(int page, int pageSize, string? search);
}