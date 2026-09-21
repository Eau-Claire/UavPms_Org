using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UavPms.OperationsService.Domain.Entities;
using UavPms.Shared.Contracts.Events;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Email).IsUnique();
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

public class UserGeographicScopeConfiguration : IEntityTypeConfiguration<UserGeographicScope>
{
    public void Configure(EntityTypeBuilder<UserGeographicScope> builder)
    {
        builder.ToTable("UserGeographicScopes", t => t.HasCheckConstraint("CK_UserGeographicScopes_HasScope", "(\"RegionId\" IS NOT NULL OR \"SubstationId\" IS NOT NULL OR \"TransmissionLineId\" IS NOT NULL OR \"ManagementUnitId\" IS NOT NULL)"));
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.UserId, e.RegionId });
        builder.HasIndex(e => new { e.UserId, e.SubstationId });
        builder.HasIndex(e => new { e.UserId, e.TransmissionLineId });
        builder.HasIndex(e => new { e.UserId, e.ManagementUnitId });
        builder.HasOne(e => e.User).WithMany(u => u.GeographicScopes).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
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
    }
}

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("Regions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Geom).HasColumnType("geometry(Geometry,4326)");
        builder.HasIndex(e => e.Geom).HasMethod("gist");
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(e => e.Geom).HasColumnType("geometry(Geometry,4326)");
        builder.HasIndex(e => e.Geom).HasMethod("gist");
        builder.HasIndex(e => e.LineName).IsUnique();
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasIndex(e => e.ManagementUnitId);
        builder.HasOne(e => e.ManagementUnit).WithMany(e => e.PowerLines).HasForeignKey(e => e.ManagementUnitId).OnDelete(DeleteBehavior.Restrict);

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
        builder.ToTable("AssetComponents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.AssetType).HasColumnName("ComponentType");
        builder.Property(e => e.AssetCode).HasColumnName("ComponentCode");
        builder.HasIndex(e => e.AssetCode).IsUnique();
        builder.Property(e => e.Location).HasColumnType("geometry(Point,4326)");
        builder.HasIndex(e => e.Location).HasMethod("gist");
        builder.HasIndex(e => e.PowerLineId);
        builder.HasIndex(e => e.ManagementUnitId);

        builder.HasOne(e => e.Tower)
            .WithMany(t => t.Assets)
            .HasForeignKey(e => e.TowerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PowerLine).WithMany(e => e.Assets).HasForeignKey(e => e.PowerLineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ManagementUnit).WithMany(e => e.Assets).HasForeignKey(e => e.ManagementUnitId).OnDelete(DeleteBehavior.Restrict);
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

        builder.Property(e => e.Status).HasConversion<string>();
        builder.Property(e => e.OperationalStatus).HasConversion<string>();
        builder.Property(e => e.TechnicalHealth).HasConversion<string>();
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
        builder.Property(e => e.Status).HasConversion(
            status => status.ToString(),
            value => ParseMissionStatus(value));
        builder.Property(e => e.MissionType).HasConversion<string>();
        builder.Property(e => e.Boundary).HasColumnType("geometry(Geometry,4326)");
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.RegionId);
        builder.HasIndex(e => e.ScheduleId);
        builder.HasIndex(e => e.PreMissionAssessmentId).IsUnique().HasFilter("\"PreMissionAssessmentId\" IS NOT NULL AND NOT \"IsDeleted\"");
        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL AND NOT \"IsDeleted\"");
        builder.HasOne(e => e.PreMissionAssessment).WithMany().HasForeignKey(e => e.PreMissionAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(e => e.RouteData);
        builder.Ignore(e => e.AssignedToUserId);
        builder.Ignore(e => e.DroneCode);
        builder.Ignore(e => e.AssignedToUser);

        builder.Property(e => e.IsOverdueNotified).HasDefaultValue(false);
        builder.Property(e => e.ManagerInstructions).HasColumnType("text");
        builder.Property(e => e.ConfirmationDeadline);

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Inspector)
            .WithMany()
            .HasForeignKey(e => e.InspectorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Uav)
            .WithMany(u => u.Missions)
            .HasForeignKey(e => e.UavId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Region).WithMany().HasForeignKey(e => e.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Schedule).WithMany().HasForeignKey(e => e.ScheduleId).OnDelete(DeleteBehavior.Restrict);
    }

    private static MissionStatus ParseMissionStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MissionStatus.Draft;
        }

        var normalized = value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty);
        if (normalized.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return MissionStatus.Draft;
        if (normalized.Equals("Executing", StringComparison.OrdinalIgnoreCase)) return MissionStatus.InProgress;
        if (normalized.Equals("PENDINGCONFIRMATION", StringComparison.OrdinalIgnoreCase)) return MissionStatus.PendingAcceptance;
        if (normalized.Equals("CONFIRMED", StringComparison.OrdinalIgnoreCase)) return MissionStatus.Assigned;
        if (normalized.Equals("SUSPENDED", StringComparison.OrdinalIgnoreCase)) return MissionStatus.Suspended;
        if (normalized.Equals("POSTPONED", StringComparison.OrdinalIgnoreCase)) return MissionStatus.Postponed;

        return Enum.TryParse<MissionStatus>(normalized, true, out var status)
            ? status
            : MissionStatus.Draft;
    }
}

public class MissionCommunicationLogConfiguration : IEntityTypeConfiguration<MissionCommunicationLog>
{
    public void Configure(EntityTypeBuilder<MissionCommunicationLog> builder)
    {
        builder.ToTable("MissionCommunicationLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SenderName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.SenderRole).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Content).HasColumnType("text").IsRequired();
        builder.HasIndex(e => e.MissionId);
        builder.HasIndex(e => e.CreatedAt);

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.CommunicationLogs)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Sender)
            .WithMany()
            .HasForeignKey(e => e.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PreMissionAssessmentConfiguration : IEntityTypeConfiguration<PreMissionAssessment>
{
    public void Configure(EntityTypeBuilder<PreMissionAssessment> builder)
    {
        builder.ToTable("PreMissionAssessments", t => t.HasCheckConstraint("CK_PreMissionAssessments_PlannedWindow", "\"PlannedEnd\" > \"PlannedStart\""));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.SiteFeasibilityStatus).HasConversion<string>();
        builder.Property(x => x.OverallTechnicalHealth).HasConversion<string>();
        builder.Property(x => x.Findings).HasColumnType("jsonb");
        builder.Property(x => x.ProposedBoundary).HasColumnType("geometry(Geometry,4326)");
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.ManagerId, x.Status });
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL AND NOT \"IsDeleted\"");
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PreMissionAssessmentAssetConfiguration : IEntityTypeConfiguration<PreMissionAssessmentAsset>
{
    public void Configure(EntityTypeBuilder<PreMissionAssessmentAsset> builder)
    {
        builder.ToTable("PreMissionAssessmentAssets"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AssessmentId, x.AssetId }).IsUnique();
        builder.HasOne(x => x.Assessment).WithMany(x => x.Assets).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PreMissionAssessmentPersonnelConfiguration : IEntityTypeConfiguration<PreMissionAssessmentPersonnel>
{
    public void Configure(EntityTypeBuilder<PreMissionAssessmentPersonnel> builder)
    {
        builder.ToTable("PreMissionAssessmentPersonnel"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AssessmentId, x.UserId }).IsUnique();
        builder.Property(x => x.Findings).HasColumnType("jsonb");
        builder.Property(x => x.EligibilityStatus).HasConversion<string>();
        builder.Property(x => x.AvailabilityStatus).HasConversion<string>();
        builder.HasOne(x => x.Assessment).WithMany(x => x.PersonnelCandidates).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PreMissionAssessmentDroneConfiguration : IEntityTypeConfiguration<PreMissionAssessmentDrone>
{
    public void Configure(EntityTypeBuilder<PreMissionAssessmentDrone> builder)
    {
        builder.ToTable("PreMissionAssessmentDrones"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AssessmentId, x.DroneId }).IsUnique();
        builder.Property(x => x.TechnicalHealth).HasConversion<string>();
        builder.Property(x => x.OperationalAvailabilityStatus).HasConversion<string>();
        builder.Property(x => x.TechnicalEligibilityStatus).HasConversion<string>();
        builder.HasOne(x => x.Assessment).WithMany(x => x.DroneCandidates).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Drone).WithMany().HasForeignKey(x => x.DroneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TechnicalInspection).WithMany().HasForeignKey(x => x.TechnicalInspectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DroneTechnicalInspectionConfiguration : IEntityTypeConfiguration<DroneTechnicalInspection>
{
    public void Configure(EntityTypeBuilder<DroneTechnicalInspection> builder)
    {
        builder.ToTable("DroneTechnicalInspections"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>(); builder.Property(x => x.Health).HasConversion<string>();
        builder.Property(x => x.RawSnapshot).HasColumnType("jsonb");
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.DroneId, x.CompletedAt });
        builder.HasOne(x => x.Drone).WithMany(x => x.TechnicalInspections).HasForeignKey(x => x.DroneId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DroneTechnicalMetricConfiguration : IEntityTypeConfiguration<DroneTechnicalMetric>
{
    public void Configure(EntityTypeBuilder<DroneTechnicalMetric> builder)
    {
        builder.ToTable("DroneTechnicalMetrics"); builder.HasKey(x => x.Id);
        builder.Property(x => x.MetricCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Subsystem).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Metadata).HasColumnType("jsonb");
        builder.HasIndex(x => new { x.InspectionId, x.Subsystem, x.MetricCode }).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_DroneTechnicalMetrics_SingleValue", "(CASE WHEN \"NumericValue\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueText\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"BoolValue\" IS NOT NULL THEN 1 ELSE 0 END) <= 1"));
        builder.HasOne(x => x.Inspection).WithMany(x => x.Metrics).HasForeignKey(x => x.InspectionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class InspectionScheduleConfiguration : IEntityTypeConfiguration<InspectionSchedule>
{
    public void Configure(EntityTypeBuilder<InspectionSchedule> builder)
    {
        builder.ToTable("InspectionSchedules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.RecurrenceRule).HasMaxLength(512).IsRequired();
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MissionAssignmentConfiguration : IEntityTypeConfiguration<MissionAssignment>
{
    public void Configure(EntityTypeBuilder<MissionAssignment> builder)
    {
        builder.ToTable("MissionAssignments"); builder.HasKey(x => x.Id);
        builder.Property(x => x.AssignmentRole).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.ResponseStatus).HasConversion<string>();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.ToTable(t => t.HasCheckConstraint("CK_MissionAssignments_PostponeReason", "\"ResponseStatus\" <> 'Postponed' OR (\"ResponseReason\" IS NOT NULL AND length(trim(\"ResponseReason\")) > 0)"));
        builder.HasIndex(x => new { x.MissionId, x.UserId }).IsUnique().HasFilter("\"Status\" = 'Active' AND NOT \"IsDeleted\"");
        builder.HasOne(x => x.Mission).WithMany(x => x.Assignments).HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ResourceBookingConfiguration : IEntityTypeConfiguration<ResourceBooking>
{
    public void Configure(EntityTypeBuilder<ResourceBooking> builder)
    {
        builder.ToTable("ResourceBookings", t =>
        {
            t.HasCheckConstraint("CK_ResourceBookings_OneResource", "(\"UserId\" IS NOT NULL) <> (\"DroneId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_ResourceBookings_Time", "\"EndAt\" > \"StartAt\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>();
        builder.HasIndex(x => new { x.UserId, x.StartAt, x.EndAt });
        builder.HasIndex(x => new { x.DroneId, x.StartAt, x.EndAt });
        builder.HasOne(x => x.Mission).WithMany(x => x.ResourceBookings).HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Drone).WithMany().HasForeignKey(x => x.DroneId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MissionCheckInConfiguration : IEntityTypeConfiguration<MissionCheckIn>
{
    public void Configure(EntityTypeBuilder<MissionCheckIn> builder)
    {
        builder.ToTable("MissionCheckIns"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>();
        builder.HasIndex(x => new { x.MissionId, x.UserId }).IsUnique().HasFilter("NOT \"IsDeleted\"");
        builder.HasOne(x => x.Mission).WithMany(x => x.CheckIns).HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DroneHandoverConfiguration : IEntityTypeConfiguration<DroneHandover>
{
    public void Configure(EntityTypeBuilder<DroneHandover> builder)
    {
        builder.ToTable("DroneHandovers"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Condition).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.HasIndex(x => new { x.MissionId, x.DroneId }).IsUnique().HasFilter("\"ReturnedAt\" IS NULL AND NOT \"IsDeleted\"");
        builder.HasOne(x => x.Mission).WithMany(x => x.DroneHandovers).HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Drone).WithMany().HasForeignKey(x => x.DroneId).OnDelete(DeleteBehavior.Restrict);
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

public class MissionTargetConfiguration : IEntityTypeConfiguration<MissionTarget>
{
    public void Configure(EntityTypeBuilder<MissionTarget> builder)
    {
        builder.ToTable("MissionTargets");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.MissionId, e.AssetId }).IsUnique();
        builder.Property(e => e.InspectionStatus).HasConversion<string>();

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.MissionTargets)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Asset)
            .WithMany(t => t.MissionTargets)
            .HasForeignKey(e => e.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ManagementUnitConfiguration : IEntityTypeConfiguration<ManagementUnit>
{
    public void Configure(EntityTypeBuilder<ManagementUnit> builder)
    {
        builder.ToTable("ManagementUnits");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LineSegmentConfiguration : IEntityTypeConfiguration<LineSegment>
{
    public void Configure(EntityTypeBuilder<LineSegment> builder)
    {
        builder.ToTable("LineSegments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Geometry).HasColumnType("geometry(Geometry,4326)");
        builder.HasIndex(e => e.Geometry).HasMethod("gist");
        builder.HasIndex(e => e.PowerLineId);
        builder.HasOne(e => e.PowerLine).WithMany(e => e.LineSegments).HasForeignKey(e => e.PowerLineId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.FromAsset).WithMany().HasForeignKey(e => e.FromAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ToAsset).WithMany().HasForeignKey(e => e.ToAssetId).OnDelete(DeleteBehavior.Restrict);
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

        builder.HasOne(e => e.Uploader)
            .WithMany()
            .HasForeignKey(e => e.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Tower)
            .WithMany()
            .HasForeignKey(e => e.TowerId)
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
        builder.Property(e => e.AiDetectionId).HasMaxLength(128);
        builder.Property(e => e.TowerId).HasMaxLength(128);
        builder.HasIndex(e => new { e.MediaId, e.AiDetectionId })
            .IsUnique()
            .HasFilter("\"AiDetectionId\" IS NOT NULL AND \"IsDeleted\" = false");
        builder.Property(e => e.ImageUrl).HasMaxLength(2048);
        builder.Property(e => e.CropUrl).HasMaxLength(2048);
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

        builder.Property(e => e.Status).HasConversion<string>();
        builder.Property(e => e.Priority).HasConversion<string>();
        
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

public class AIAnalysisRequestConfiguration : IEntityTypeConfiguration<AIAnalysisRequest>
{
    public void Configure(EntityTypeBuilder<AIAnalysisRequest> builder)
    {
        builder.ToTable("AIAnalysisRequests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ModelName).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.SourceEventId).IsUnique().HasFilter("\"SourceEventId\" IS NOT NULL");
        builder.HasIndex(e => new { e.MediaId, e.AnalysisType, e.ModelName })
            .IsUnique().HasDatabaseName("IX_AIAnalysisRequests_ActiveLogicalAnalysis")
            .HasFilter("\"MediaId\" IS NOT NULL AND \"IsDeleted\" = false AND \"Status\" IN (0, 1)");

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

        builder.HasOne(e => e.Asset)
            .WithMany()
            .HasForeignKey(e => e.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MessageType).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.MessageType, e.OccurredAt }).HasFilter("\"PublishedAt\" IS NULL AND \"IsDeleted\" = false");
    }
}

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();
        builder.Property(e => e.Title).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.HasOne(e => e.TransmissionLine)
            .WithMany()
            .HasForeignKey(e => e.TransmissionLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Substation)
            .WithMany()
            .HasForeignKey(e => e.SubstationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ApprovedBy)
            .WithMany()
            .HasForeignKey(e => e.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.Type);
        builder.HasIndex(e => e.CreatedAt);
    }
}

public class ReportMissionConfiguration : IEntityTypeConfiguration<ReportMission>
{
    public void Configure(EntityTypeBuilder<ReportMission> builder)
    {
        builder.ToTable("ReportMissions");
        builder.HasKey(e => new { e.ReportId, e.MissionId });

        builder.HasOne(e => e.Report)
            .WithMany(r => r.ReportMissions)
            .HasForeignKey(e => e.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Mission)
            .WithMany()
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

