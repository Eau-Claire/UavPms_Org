using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Infrastructure.Persistence;

namespace UavPms.NotificationService.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<T?> GetByIdAsync(Guid id, bool track = true)
    {
        if (track)
        {
            return await _context.Set<T>().FindAsync(id);
        }
        
        return await _context.Set<T>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(bool track = false)
    {
        return track 
            ? await _context.Set<T>().ToListAsync()
            : await _context.Set<T>().AsNoTracking().ToListAsync();
    }

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, bool track = false)
    {
        return track
            ? await _context.Set<T>().Where(predicate).ToListAsync()
            : await _context.Set<T>().AsNoTracking().Where(predicate).ToListAsync();
    }

    public async Task<T> AddAsync(T entity)
    {
        await _context.Set<T>().AddAsync(entity);
        return entity;
    }

    public Task UpdateAsync(T entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity)
    {
        if (entity is UavPms.NotificationService.Domain.Common.BaseEntity baseEntity)
        {
            baseEntity.IsDeleted = true;
            baseEntity.DeletedAt = DateTime.UtcNow;
            _context.Entry(entity).State = EntityState.Modified;
        }
        else
        {
            _context.Set<T>().Remove(entity);
        }
        return Task.CompletedTask;
    }
}
