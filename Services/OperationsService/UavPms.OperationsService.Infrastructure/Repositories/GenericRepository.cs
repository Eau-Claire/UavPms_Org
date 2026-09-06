using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Infrastructure.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // All generic reads, including tracked direct-ID reads, execute the geographic predicate.
    protected IQueryable<T> ReadQuery
    {
        get
        {
            var access = new GeographicAccessFilter(_context, _context.CurrentUser);
            object query = typeof(T) == typeof(Asset) ? access.ApplyToAssets(_context.Assets)
                : typeof(T) == typeof(Tower) ? access.ApplyToTowers(_context.Towers)
                : typeof(T) == typeof(TransmissionLine) ? access.ApplyToLines(_context.TransmissionLines)
                : typeof(T) == typeof(Substation) ? access.ApplyToSubstations(_context.Substations)
                : typeof(T) == typeof(ManagementUnit) ? access.ApplyToManagementUnits(_context.ManagementUnits)
                : typeof(T) == typeof(Region) ? access.ApplyToRegions(_context.Regions)
                : _context.Set<T>();
            return (IQueryable<T>)query;
        }
    }

    public async Task<T?> GetByIdAsync(Guid id, bool track = true) =>
        await (track ? ReadQuery : ReadQuery.AsNoTracking()).FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);

    public async Task<IReadOnlyList<T>> GetAllAsync(bool track = false) =>
        await (track ? ReadQuery : ReadQuery.AsNoTracking()).ToListAsync();

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, bool track = false) =>
        await (track ? ReadQuery : ReadQuery.AsNoTracking()).Where(predicate).ToListAsync();

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
        if (entity is UavPms.OperationsService.Domain.Common.BaseEntity baseEntity)
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
