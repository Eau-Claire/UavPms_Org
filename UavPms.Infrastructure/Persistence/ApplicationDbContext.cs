using System.Runtime.Serialization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetTopologySuite.Geometries;
using UavPms.Core.Common;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Services;

namespace UavPms.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserServices? _currentUserServices;
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Substation> Substations => Set<Substation>();
    public DbSet<TransmissionLine> TransmissionLines => Set<TransmissionLine>();
    public DbSet<Tower> Towers => Set<Tower>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetHealthHistory> AssetHealthHistories => Set<AssetHealthHistory>();
    public DbSet<Uav> Uavs => Set<Uav>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionTargetLine> MissionTargetLines => Set<MissionTargetLine>();
    public DbSet<MissionFlightLog> MissionFlightLogs => Set<MissionFlightLog>();
    public DbSet<InspectionMedia> InspectionMedia => Set<InspectionMedia>();
    public DbSet<DefectCategory> DefectCategories => Set<DefectCategory>();
    public DbSet<DetectedAnomaly> DetectedAnomalies => Set<DetectedAnomaly>();
    public DbSet<EmergencyAlert> EmergencyAlerts => Set<EmergencyAlert>();
    public DbSet<AlertEscalation> AlertEscalations => Set<AlertEscalation>();
    public DbSet<IncidentReport> IncidentReports => Set<IncidentReport>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<MaintenanceProof> MaintenanceProofs => Set<MaintenanceProof>();
    public DbSet<MaterialLog> MaterialLogs => Set<MaterialLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TrustedDevice> TrustedDevices => Set<TrustedDevice>();

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserServices? currentUserServices) : base(options)
    {
        _currentUserServices = currentUserServices;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Apply Global Soft Delete Query Filter for BaseEntity descendants
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter((dynamic)CreateFilterExpression(entityType.ClrType));
            }
        }
    }

    private static object CreateFilterExpression(Type type)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(type, "e");
        var property = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        var notExpression = System.Linq.Expressions.Expression.Not(property);
        return System.Linq.Expressions.Expression.Lambda(notExpression, parameter);
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        OnBeforeSaveChanges();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        OnBeforeSaveChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
    }

    private void OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;
            // Chỉ lưu log các lớp kế thừa từ BaseEntity (có primary key Guid Id)
            if (entry.Entity is not BaseEntity baseEntity)
                continue;
            var auditEntry = new AuditEntry(entry)
            {
                TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                RecordId = baseEntity.Id,
                UserId = _currentUserServices?.IsAuthenticated == true ? _currentUserServices.UserId : null,
                IpAddress = _currentUserServices?.IpAddress ?? string.Empty,
                UserAgent = _currentUserServices?.UserAgent ?? string.Empty
            };
            auditEntries.Add(auditEntry);
            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                    continue;
                // 1. Bảo mật: MASKED trường PasswordHash
                if (propertyName == "PasswordHash")
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                        auditEntry.NewValues[propertyName] = "[MASKED]";
                    if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
                        auditEntry.OldValues[propertyName] = "[MASKED]";
                    continue;
                }
                // 2. Không gian địa lý: Chuyển dữ liệu NetTopologySuite Geometry sang WKT (Well-Known Text)
                if (property.Metadata.ClrType.Namespace?.StartsWith("NetTopologySuite") == true)
                {
                    if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    {
                        if (property.CurrentValue is Geometry geom)
                            auditEntry.NewValues[propertyName] = geom.AsText();
                    }
                    if (entry.State == EntityState.Deleted || entry.State == EntityState.Modified)
                    {
                        if (property.OriginalValue is Geometry geomOld)
                            auditEntry.OldValues[propertyName] = geomOld.AsText();
                    }
                    continue;
                }
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.ActionType = "Added";
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        auditEntry.ActionType = "Deleted";
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;
                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            var originalVal = property.OriginalValue;
                            var currentVal = property.CurrentValue;
                            if (!Equals(originalVal, currentVal))
                            {
                                auditEntry.ActionType = "Modified";
                                auditEntry.OldValues[propertyName] = originalVal;
                                auditEntry.NewValues[propertyName] = currentVal;
                            }
                        }
                        break;
                }
            }
        }
        // Chèn các bản ghi log vào db trước khi lưu
        foreach (var auditEntry in auditEntries)
        {
            // Tránh sinh log rác nếu Modified nhưng không có thuộc tính nào thay đổi giá trị
            if (auditEntry.ActionType == "Modified" && auditEntry.OldValues.Count == 0 && auditEntry.NewValues.Count == 0)
                continue;
            if (string.IsNullOrEmpty(auditEntry.ActionType))
                continue;
            AuditLogs.Add(auditEntry.ToAuditLog());
        }
    } 
    
}

public class AuditEntry 
{
    public AuditEntry(EntityEntry entry)
    {
        Entry = entry;
    }

    public EntityEntry Entry { get; }
    public Guid? UserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public Guid RecordId { get; set;}
    public string ActionType { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();
    
    public AuditLog ToAuditLog()
    {
        return new AuditLog
        {
            UserId = UserId,
            TableName = TableName,
            RecordId = RecordId,
            ActionType = ActionType,
            OldValues = OldValues.Count == 0 ? "{}" : JsonSerializer.Serialize(OldValues),
            NewValues = NewValues.Count == 0 ? "{}" : JsonSerializer.Serialize(NewValues),
            IpAddress = IpAddress,
            UserAgent = UserAgent,
            CreatedAt = DateTime.UtcNow
        };
    }
}