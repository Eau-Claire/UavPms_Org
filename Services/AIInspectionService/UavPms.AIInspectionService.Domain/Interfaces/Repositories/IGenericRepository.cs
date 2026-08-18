using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Threading;

namespace UavPms.AIInspectionService.Domain.Interfaces.Repositories;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, bool track = true);
    Task<T?> GetByIdAsync(Guid id, bool track, CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> GetAllAsync(bool track = false);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, bool track = false);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
