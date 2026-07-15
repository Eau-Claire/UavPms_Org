using System.Threading;
using System.Threading.Tasks;

namespace UavPms.Core.Interfaces.Repositories;

public interface IUnitOfWork
{
    // Lưu tất cả các thay đổi của các Repositories xuống DB trong cùng 1 Transaction
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}