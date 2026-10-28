using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Infrastructure.Persistence.Configurations;

// NOTE: AIInspectionService maps a subset of the shared database (ERD v2.2).
// OperationsService owns the migrations; this model must never map a column that does not exist.

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Phone).IsUnique().HasFilter("\"Phone\" <> ''");
        builder.HasIndex(e => e.Email).IsUnique();
        builder.Property(e => e.Status).HasConversion<string>();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.HasIndex(e => e.RoleName).IsUnique();
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.HasKey(e => new { e.UserId, e.RoleId });

        builder.HasOne(e => e.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(e => e.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TowerConfiguration : IEntityTypeConfiguration<Tower>
{
    public void Configure(EntityTypeBuilder<Tower> builder)
    {
        builder.ToTable("Towers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TowerCode).HasMaxLength(50);
        builder.Property(e => e.PoleType).HasMaxLength(100);
        builder.Property(e => e.Address).HasMaxLength(500);
        builder.Property(e => e.Geom).HasColumnType("geometry");
        builder.HasIndex(e => e.Geom).HasMethod("gist");
    }
}

public class UavConfiguration : IEntityTypeConfiguration<Uav>
{
    public void Configure(EntityTypeBuilder<Uav> builder)
    {
        builder.ToTable("UAVs");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.UavCode).IsUnique();
        builder.Property(e => e.FlightStatus).HasColumnName("FlightStatus").HasMaxLength(20);
    }
}

public class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("Missions");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.MissionCode).IsUnique();

        builder.HasOne(e => e.Uav)
            .WithMany(u => u.Missions)
            .HasForeignKey(e => e.UavId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MissionAssignmentConfiguration : IEntityTypeConfiguration<MissionAssignment>
{
    public void Configure(EntityTypeBuilder<MissionAssignment> builder)
    {
        builder.ToTable("MissionAssignments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.AssignmentRole).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20);

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.Assignments)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InspectionMediaConfiguration : IEntityTypeConfiguration<InspectionMedia>
{
    public void Configure(EntityTypeBuilder<InspectionMedia> builder)
    {
        builder.ToTable("InspectionMedia");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CaptureLocation).HasColumnType("geometry");
        builder.HasIndex(e => e.CaptureLocation).HasMethod("gist");
        builder.Property(e => e.MediaType).HasMaxLength(10);
        builder.Property(e => e.FileUrl).HasMaxLength(2048);
        builder.Property(e => e.Checksum).HasMaxLength(64);
        builder.HasIndex(e => e.Checksum).IsUnique().HasFilter("\"Checksum\" IS NOT NULL");

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.InspectionMedias)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Tower)
            .WithMany()
            .HasForeignKey(e => e.TowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Uploader)
            .WithMany()
            .HasForeignKey(e => e.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DefectCategoryConfiguration : IEntityTypeConfiguration<DefectCategory>
{
    public void Configure(EntityTypeBuilder<DefectCategory> builder)
    {
        builder.ToTable("DefectCategories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.HasIndex(e => e.CategoryCode).IsUnique();
    }
}

public class DetectedAnomalyConfiguration : IEntityTypeConfiguration<DetectedAnomaly>
{
    public void Configure(EntityTypeBuilder<DetectedAnomaly> builder)
    {
        builder.ToTable("DetectedAnomalies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BoundingBox).HasColumnType("jsonb");
        builder.Property(e => e.AiDetectionId).HasMaxLength(128);
        builder.Property(e => e.ImageUrl).HasMaxLength(2048);
        builder.Property(e => e.CropUrl).HasMaxLength(2048);
        builder.Property(e => e.Location).HasColumnType("geometry(Point,4326)");
        builder.HasIndex(e => e.Location).HasMethod("gist");
        builder.HasIndex(e => new { e.AnalysisRunId, e.AiDetectionId })
            .IsUnique()
            .HasFilter("\"AiDetectionId\" IS NOT NULL AND \"IsDeleted\" = false");

        builder.HasOne(e => e.Media)
            .WithMany(m => m.DetectedAnomalies)
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.AnalysisRun)
            .WithMany(r => r.DetectedAnomalies)
            .HasForeignKey(e => e.AnalysisRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.SupersededBy)
            .WithMany()
            .HasForeignKey(e => e.SupersededById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Tower)
            .WithMany()
            .HasForeignKey(e => e.TowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Category)
            .WithMany(c => c.DetectedAnomalies)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Analyst)
            .WithMany()
            .HasForeignKey(e => e.AnalystId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OldValues).HasColumnType("jsonb");
        builder.Property(e => e.NewValues).HasColumnType("jsonb");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AIAnalysisRunConfiguration : IEntityTypeConfiguration<AIAnalysisRun>
{
    /// <summary>Filtered unique index preventing two active (Pending/Processing) equivalent runs.</summary>
    public const string ActiveLogicalAnalysisIndexName = "IX_AIAnalysisRuns_ActiveLogicalAnalysis";

    public void Configure(EntityTypeBuilder<AIAnalysisRun> builder)
    {
        builder.ToTable("AIAnalysisRuns");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ExecutionLocation).HasMaxLength(10).IsRequired();
        builder.Property(e => e.AnalysisType).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.ModelName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ModelVersion).HasMaxLength(50);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => e.SourceEventId).IsUnique().HasFilter("\"SourceEventId\" IS NOT NULL");
        builder.HasIndex(e => new { e.MediaId, e.AnalysisType, e.ModelName })
            .IsUnique()
            .HasDatabaseName(ActiveLogicalAnalysisIndexName)
            .HasFilter("\"IsDeleted\" = false AND \"Status\" IN ('Pending','Processing')");

        builder.HasOne(e => e.Media)
            .WithMany(m => m.AnalysisRuns)
            .HasForeignKey(e => e.MediaId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Uav)
            .WithMany()
            .HasForeignKey(e => e.UavId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Requester)
            .WithMany()
            .HasForeignKey(e => e.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("SystemSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Category).HasMaxLength(50);
        builder.Property(e => e.DataType).HasMaxLength(20);
        builder.HasIndex(e => e.Key).IsUnique();
        builder.HasIndex(e => e.Category);
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<UavPms.Shared.Contracts.Events.OutboxMessage>
{
    public void Configure(EntityTypeBuilder<UavPms.Shared.Contracts.Events.OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MessageType).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.MessageType, e.OccurredAt }).HasFilter("\"PublishedAt\" IS NULL");
    }
}
