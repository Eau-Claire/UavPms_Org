using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UavPms.Core.Entities;

namespace UavPms.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Username).IsUnique();
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

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TokenHash)
            .IsRequired();

        builder.HasOne(e => e.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Geom).HasColumnType("geometry");
        builder.HasIndex(e => e.Geom).HasMethod("gist");
    }
}

public class SubstationConfiguration : IEntityTypeConfiguration<Substation>
{
    public void Configure(EntityTypeBuilder<Substation> builder)
    {
        builder.ToTable("Substations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Geom).HasColumnType("geometry");
        builder.HasIndex(e => e.Geom).HasMethod("gist");

        builder.HasOne(e => e.Region)
            .WithMany(r => r.Substations)
            .HasForeignKey(e => e.RegionAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TransmissionLineConfiguration : IEntityTypeConfiguration<TransmissionLine>
{
    public void Configure(EntityTypeBuilder<TransmissionLine> builder)
    {
        builder.ToTable("TransmissionLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Geom).HasColumnType("geometry");
        builder.HasIndex(e => e.Geom).HasMethod("gist");
        builder.HasIndex(e => e.LineName).IsUnique();

        builder.HasOne(e => e.Substation)
            .WithMany(s => s.TransmissionLines)
            .HasForeignKey(e => e.SubstationAssetId)
            .OnDelete(DeleteBehavior.Restrict);
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
        builder.HasIndex(e => e.TowerCode).IsUnique();

        builder.HasOne(e => e.TransmissionLine)
            .WithMany(l => l.Towers)
            .HasForeignKey(e => e.LineAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.AssetCode).IsUnique();

        builder.HasOne(e => e.Tower)
            .WithMany(t => t.Assets)
            .HasForeignKey(e => e.TowerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AssetHealthHistoryConfiguration : IEntityTypeConfiguration<AssetHealthHistory>
{
    public void Configure(EntityTypeBuilder<AssetHealthHistory> builder)
    {
        builder.ToTable("AssetHealthHistories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CalculationLog).HasColumnType("jsonb");

        builder.HasOne(e => e.Asset)
            .WithMany(a => a.HealthHistories)
            .HasForeignKey(e => e.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UavConfiguration : IEntityTypeConfiguration<Uav>
{
    public void Configure(EntityTypeBuilder<Uav> builder)
    {
        builder.ToTable("UAVs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CurrentLocation).HasColumnType("geometry");
        builder.HasIndex(e => e.CurrentLocation).HasMethod("gist");
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
        
        builder.Property(e => e.Title).HasMaxLength(256).IsRequired();
        builder.Property(e => e.RouteData).HasColumnType("text").IsRequired();
        builder.Property(e => e.DroneCode).HasMaxLength(100).IsRequired();

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Inspector)
            .WithMany()
            .HasForeignKey(e => e.InspectorId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasOne(e => e.AssignedToUser)
            .WithMany()
            .HasForeignKey(e => e.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Uav)
            .WithMany(u => u.Missions)
            .HasForeignKey(e => e.UavId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MissionTargetLineConfiguration : IEntityTypeConfiguration<MissionTargetLine>
{
    public void Configure(EntityTypeBuilder<MissionTargetLine> builder)
    {
        builder.ToTable("MissionTargetLines");
        builder.HasKey(e => new { e.MissionId, e.LineAssetId });

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.MissionTargetLines)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.TransmissionLine)
            .WithMany(l => l.MissionTargetLines)
            .HasForeignKey(e => e.LineAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MissionFlightLogConfiguration : IEntityTypeConfiguration<MissionFlightLog>
{
    public void Configure(EntityTypeBuilder<MissionFlightLog> builder)
    {
        builder.ToTable("MissionFlightLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.GpsTrack).HasColumnType("jsonb");

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.MissionFlightLogs)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InspectionMediaConfiguration : IEntityTypeConfiguration<InspectionMedia>
{
    public void Configure(EntityTypeBuilder<InspectionMedia> builder)
    {
        builder.ToTable("InspectionMedia");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.InspectionMedias)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Asset)
            .WithMany(a => a.InspectionMedias)
            .HasForeignKey(e => e.AssetId)
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

public class EmergencyAlertConfiguration : IEntityTypeConfiguration<EmergencyAlert>
{
    public void Configure(EntityTypeBuilder<EmergencyAlert> builder)
    {
        builder.ToTable("EmergencyAlerts");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Anomaly)
            .WithMany(a => a.EmergencyAlerts)
            .HasForeignKey(e => e.AnomalyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Asset)
            .WithMany(a => a.EmergencyAlerts)
            .HasForeignKey(e => e.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.EmergencyAlerts)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AlertEscalationConfiguration : IEntityTypeConfiguration<AlertEscalation>
{
    public void Configure(EntityTypeBuilder<AlertEscalation> builder)
    {
        builder.ToTable("AlertEscalations");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Alert)
            .WithMany(a => a.AlertEscalations)
            .HasForeignKey(e => e.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.EscalatedByUser)
            .WithMany()
            .HasForeignKey(e => e.EscalatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.EscalatedToUser)
            .WithMany()
            .HasForeignKey(e => e.EscalatedTo)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class IncidentReportConfiguration : IEntityTypeConfiguration<IncidentReport>
{
    public void Configure(EntityTypeBuilder<IncidentReport> builder)
    {
        builder.ToTable("IncidentReports");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.IncidentReports)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Reporter)
            .WithMany()
            .HasForeignKey(e => e.ReportedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Asset)
            .WithMany(a => a.IncidentReports)
            .HasForeignKey(e => e.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceTicketConfiguration : IEntityTypeConfiguration<MaintenanceTicket>
{
    public void Configure(EntityTypeBuilder<MaintenanceTicket> builder)
    {
        builder.ToTable("MaintenanceTickets");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TicketCode).IsUnique();

        builder.HasOne(e => e.Anomaly)
            .WithMany(a => a.MaintenanceTickets)
            .HasForeignKey(e => e.AnomalyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Asset)
            .WithMany(a => a.MaintenanceTickets)
            .HasForeignKey(e => e.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Technician)
            .WithMany()
            .HasForeignKey(e => e.TechnicianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceProofConfiguration : IEntityTypeConfiguration<MaintenanceProof>
{
    public void Configure(EntityTypeBuilder<MaintenanceProof> builder)
    {
        builder.ToTable("MaintenanceProofs");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Ticket)
            .WithMany(t => t.MaintenanceProofs)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Uploader)
            .WithMany()
            .HasForeignKey(e => e.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaterialLogConfiguration : IEntityTypeConfiguration<MaterialLog>
{
    public void Configure(EntityTypeBuilder<MaterialLog> builder)
    {
        builder.ToTable("MaterialLogs");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Ticket)
            .WithMany(t => t.MaterialLogs)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Logger)
            .WithMany()
            .HasForeignKey(e => e.LoggedBy)
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

public class TrustedDeviceConfiguration : IEntityTypeConfiguration<TrustedDevice>
{
    public void Configure(EntityTypeBuilder<TrustedDevice> builder)
    {
        builder.ToTable("TrustedDevices");
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


