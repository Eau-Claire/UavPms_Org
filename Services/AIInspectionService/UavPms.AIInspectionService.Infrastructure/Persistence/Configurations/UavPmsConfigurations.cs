using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Phone).IsUnique();
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
        builder.Property(e => e.Geom).HasColumnType("geometry");
        builder.HasIndex(e => e.Geom).HasMethod("gist");
    }
}

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("AssetComponents");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Tower)
            .WithMany(t => t.Assets)
            .HasForeignKey(e => e.TowerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UavConfiguration : IEntityTypeConfiguration<Uav>
{
    public void Configure(EntityTypeBuilder<Uav> builder)
    {
        builder.ToTable("Uavs");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.UavCode).IsUnique();
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

        builder.HasOne(e => e.Inspector)
            .WithMany()
            .HasForeignKey(e => e.InspectorId)
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

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.InspectionMedias)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Asset)
            .WithMany(a => a.InspectionMedias)
            .HasForeignKey(e => e.AssetId)
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
        builder.Property(e => e.Gps).HasColumnType("jsonb");

        builder.HasOne(e => e.Media)
            .WithMany(m => m.DetectedAnomalies)
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Asset)
            .WithMany(a => a.DetectedAnomalies)
            .HasForeignKey(e => e.AssetId)
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

public class AIAnalysisRequestConfiguration : IEntityTypeConfiguration<AIAnalysisRequest>
{
    public void Configure(EntityTypeBuilder<AIAnalysisRequest> builder)
    {
        builder.ToTable("AIAnalysisRequests");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Uploader)
            .WithMany()
            .HasForeignKey(e => e.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Media)
            .WithMany()
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Mission)
            .WithMany()
            .HasForeignKey(e => e.MissionId)
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
