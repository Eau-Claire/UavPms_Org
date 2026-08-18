using Microsoft.EntityFrameworkCore;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Infrastructure.Persistence;

namespace UavPms.AIInspectionService.Infrastructure.Repositories;

internal static class RepositoryMetrics
{
    internal static readonly Histogram QueryDuration = Metrics.CreateHistogram(
        "repository_query_duration_seconds",
        "Duration of repository database queries.",
        new HistogramConfiguration
        {
            LabelNames = ["entity", "operation", "tracking"]
        });
}

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;

    public GenericRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<T?> GetByIdAsync(Guid id, bool track = true)
    {
        return GetByIdAsync(id, track, CancellationToken.None);
    }

    public async Task<T?> GetByIdAsync(
        Guid id,
        bool track,
        CancellationToken cancellationToken)
    {
        using var timer = RepositoryMetrics.QueryDuration
            .WithLabels(typeof(T).Name, "get_by_id", track ? "true" : "false")
            .NewTimer();

        if (track)
        {
            return await _context.Set<T>().FindAsync([id], cancellationToken);
        }
        
        return await _context.Set<T>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken);
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
        if (entity is UavPms.AIInspectionService.Domain.Common.BaseEntity baseEntity)
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
