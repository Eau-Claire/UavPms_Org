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
        // Phone is optional (stored as ''), so only non-empty phones must be unique.
        builder.HasIndex(e => e.Phone).IsUnique().HasFilter("\"Phone\" <> ''");
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
        builder.ToTable("UserGeographicScopes");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.UserId, e.RegionId }).IsUnique().HasFilter("NOT \"IsDeleted\"");
        builder.HasIndex(e => e.RegionId);
        builder.HasOne(e => e.User).WithMany(u => u.GeographicScopes).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Region).WithMany().HasForeignKey(e => e.RegionId).OnDelete(DeleteBehavior.Cascade);
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
        builder.Property(e => e.Name).HasMaxLength(150).IsRequired();
        builder.Property(e => e.Geom).HasColumnType("geometry(Geometry,4326)");
        builder.HasIndex(e => e.Geom).HasMethod("gist");
        builder.HasIndex(e => e.Code).IsUnique();
        builder.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
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

        builder.Property(e => e.PoleType).HasMaxLength(100);
        builder.Property(e => e.Address).HasMaxLength(500);

        builder.HasIndex(e => e.RegionId);

        builder.HasOne(e => e.Region)
            .WithMany(r => r.Towers)
            .HasForeignKey(e => e.RegionId)
            .OnDelete(DeleteBehavior.Restrict);
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

        builder.Property(e => e.FlightStatus).HasConversion<string>();
        builder.Property(e => e.OperationalStatus).HasConversion<string>();
        builder.Property(e => e.TechnicalHealth).HasConversion<string>();
        builder.Property(e => e.Name).HasMaxLength(256);
        builder.Property(e => e.Model).HasMaxLength(256);
        builder.Property(e => e.Category).HasMaxLength(100);
        builder.Property(e => e.SerialNumber).HasMaxLength(256);
        builder.Property(e => e.CameraPayloadInfo).HasMaxLength(512);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasOne(e => e.AssignedRegion).WithMany().HasForeignKey(e => e.AssignedRegionId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.LastTechnicalInspection).WithMany().HasForeignKey(e => e.LastTechnicalInspectionId).OnDelete(DeleteBehavior.SetNull);
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
        builder.HasOne(e => e.PreMissionAssessment).WithOne(p => p.Mission).HasForeignKey<Mission>(e => e.PreMissionAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(e => e.RouteData);
        builder.Ignore(e => e.AssignedToUserId);
        builder.Ignore(e => e.DroneCode);
        builder.Ignore(e => e.AssignedToUser);

        builder.Property(e => e.Priority).HasDefaultValue(MissionPriority.Normal);
        builder.Property(e => e.Objective).HasConversion<string>().HasDefaultValue(InspectionObjective.PeriodicInspection);
        builder.Property(e => e.IsImmediate).HasDefaultValue(false);
        builder.HasIndex(e => new { e.Priority, e.PlannedStart, e.CreatedAt });

        builder.Property(e => e.ManagerInstructions).HasColumnType("text");
        builder.Property(e => e.ConfirmationDeadline);

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Uav)
            .WithMany(u => u.Missions)
            .HasForeignKey(e => e.UavId)
            .IsRequired(false)
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
        builder.ToTable("PreMissionAssessments", t =>
        {
            t.HasCheckConstraint("CK_PreMissionAssessments_PlannedWindow", "\"PlannedEnd\" > \"PlannedStart\"");
            t.HasCheckConstraint("CK_PreMissionAssessments_Status", "\"Status\" IN ('DRAFT', 'EVALUATING', 'READY', 'NOT_READY', 'EXPIRED', 'COMPLETED', 'CANCELLED')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status)
            .HasConversion(
                v => FormatAssessmentStatus(v),
                v => ParseAssessmentStatus(v));
        builder.Property(x => x.SiteFeasibilityStatus).HasConversion<string>();
        builder.Property(x => x.OverallTechnicalHealth).HasConversion<string>();
        builder.Property(x => x.Findings).HasColumnType("jsonb");
        builder.Property(x => x.ProposedBoundary).HasColumnType("geometry(Geometry,4326)");
        builder.HasIndex(x => x.ProposedBoundary).HasMethod("gist");
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.ManagerId, x.Status });
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL AND NOT \"IsDeleted\"");
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
    }

    private static string FormatAssessmentStatus(PreMissionAssessmentStatus status) => status switch
    {
        PreMissionAssessmentStatus.NotReady => "NOT_READY",
        PreMissionAssessmentStatus.Completed => "COMPLETED",
        PreMissionAssessmentStatus.Draft => "DRAFT",
        PreMissionAssessmentStatus.Evaluating => "EVALUATING",
        PreMissionAssessmentStatus.Ready => "READY",
        PreMissionAssessmentStatus.Expired => "EXPIRED",
        PreMissionAssessmentStatus.Cancelled => "CANCELLED",
        _ => status.ToString().ToUpperInvariant()
    };

    private static PreMissionAssessmentStatus ParseAssessmentStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return PreMissionAssessmentStatus.Draft;
        var normalized = value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty);
        if (normalized.Equals("CONSUMED", StringComparison.OrdinalIgnoreCase)) return PreMissionAssessmentStatus.Completed;
        if (normalized.Equals("INCOMPLETE", StringComparison.OrdinalIgnoreCase)) return PreMissionAssessmentStatus.NotReady;
        if (normalized.Equals("NOTREADY", StringComparison.OrdinalIgnoreCase)) return PreMissionAssessmentStatus.NotReady;
        return Enum.TryParse<PreMissionAssessmentStatus>(normalized, true, out var status)
            ? status
            : PreMissionAssessmentStatus.Draft;
    }
}

public class PreMissionAssessmentTowerConfiguration : IEntityTypeConfiguration<PreMissionAssessmentTower>
{
    public void Configure(EntityTypeBuilder<PreMissionAssessmentTower> builder)
    {
        builder.ToTable("PreMissionAssessmentTowers"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.AssessmentId, x.TowerId }).IsUnique();
        builder.HasOne(x => x.Assessment).WithMany(x => x.Towers).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Tower).WithMany().HasForeignKey(x => x.TowerId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(x => x.Suitability).HasConversion<string>();
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
        builder.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
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
        builder.ToTable("InspectionSchedules", t =>
            t.HasCheckConstraint("CK_InspectionSchedules_RecurrenceRule", "btrim(\"RecurrenceRule\") <> ''"));
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
        builder.HasOne(x => x.AssignedBy).WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(x => x.CheckInLocation).HasColumnType("geometry");
        builder.HasIndex(x => x.CheckInLocation).HasMethod("gist");
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
        builder.Property(x => x.Condition).HasMaxLength(500);
        builder.Property(x => x.InitialChecklist).HasColumnType("jsonb");
        builder.Property(x => x.ReturnChecklist).HasColumnType("jsonb");
        builder.Property(x => x.Status).HasConversion<string>();
        builder.HasIndex(x => new { x.MissionId, x.DroneId }).IsUnique().HasFilter("\"ReturnedAt\" IS NULL AND NOT \"IsDeleted\"");
        builder.HasOne(x => x.Mission).WithMany(x => x.DroneHandovers).HasForeignKey(x => x.MissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Drone).WithMany().HasForeignKey(x => x.DroneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.HandedOverByUser).WithMany().HasForeignKey(x => x.HandedOverBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReceivedByUser).WithMany().HasForeignKey(x => x.ReceivedBy).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MissionTargetConfiguration : IEntityTypeConfiguration<MissionTarget>
{
    public void Configure(EntityTypeBuilder<MissionTarget> builder)
    {
        builder.ToTable("MissionTargets");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.MissionId, e.TowerId }).IsUnique();
        builder.Property(e => e.InspectionStatus).HasConversion<string>();

        builder.HasOne(e => e.Mission)
            .WithMany(m => m.MissionTargets)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Tower)
            .WithMany(t => t.MissionTargets)
            .HasForeignKey(e => e.TowerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MissionPriorityDefectConfiguration : IEntityTypeConfiguration<MissionPriorityDefect>
{
    public void Configure(EntityTypeBuilder<MissionPriorityDefect> builder)
    {
        builder.ToTable("MissionPriorityDefects");
        builder.HasKey(e => new { e.MissionId, e.CategoryId });
        builder.HasOne(e => e.Mission).WithMany(m => m.PriorityDefects).HasForeignKey(e => e.MissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Cascade);
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
        builder.Property(e => e.Checksum).HasMaxLength(64);
        builder.HasIndex(e => e.Checksum).IsUnique().HasFilter("\"Checksum\" IS NOT NULL AND NOT \"IsDeleted\"");
        builder.HasIndex(e => e.TowerId);
        builder.HasOne(e => e.Mission)
            .WithMany(m => m.InspectionMedias)
            .HasForeignKey(e => e.MissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Uploader)
            .WithMany()
            .HasForeignKey(e => e.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Tower)
            .WithMany(t => t.InspectionMedia)
            .HasForeignKey(e => e.TowerId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.ToTable("DetectedAnomalies", t => t.HasCheckConstraint(
            "CK_DetectedAnomalies_ConfirmedRequiresTower",
            "\"ValidationStatus\" <> 'Confirmed' OR \"TowerId\" IS NOT NULL"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BoundingBox).HasColumnType("jsonb");
        builder.Property(e => e.Location).HasColumnType("geometry(Point,4326)");
        builder.Property(e => e.AnalystFeedback).HasColumnType("jsonb");
        builder.Property(e => e.AiDetectionId).HasMaxLength(128);
        builder.Property(e => e.ValidationStatus).HasMaxLength(20);
        builder.HasIndex(e => new { e.AnalysisRunId, e.AiDetectionId })
            .IsUnique()
            .HasFilter("\"AiDetectionId\" IS NOT NULL AND \"IsDeleted\" = false");
        builder.HasIndex(e => e.TowerId);
        builder.HasIndex(e => e.SupersededById);
        builder.Property(e => e.ImageUrl).HasMaxLength(2048);
        builder.Property(e => e.CropUrl).HasMaxLength(2048);
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
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Tower)
            .WithMany(t => t.DetectedAnomalies)
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

public class EmergencyAlertConfiguration : IEntityTypeConfiguration<EmergencyAlert>
{
    public void Configure(EntityTypeBuilder<EmergencyAlert> builder)
    {
        builder.ToTable("EmergencyAlerts");
        builder.HasKey(e => e.Id);
        // Plain FK index kept for "all alerts of an anomaly"; the partial unique index below only covers Active rows.
        builder.HasIndex(e => e.AnomalyId, "IX_EmergencyAlerts_AnomalyId");
        // At most one Active alert per anomaly (ERD v2.2); escalated/resolved history is unrestricted.
        builder.HasIndex(e => e.AnomalyId, "UX_EmergencyAlerts_ActivePerAnomaly")
            .IsUnique()
            .HasFilter("\"Status\" = 'Active' AND NOT \"IsDeleted\" AND \"AnomalyId\" IS NOT NULL");

        builder.HasOne(e => e.Anomaly)
            .WithMany(a => a.EmergencyAlerts)
            .HasForeignKey(e => e.AnomalyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ReviewedBy)
            .WithMany()
            .HasForeignKey(e => e.ReviewedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class AlertEscalationConfiguration : IEntityTypeConfiguration<AlertEscalation>
{
    public void Configure(EntityTypeBuilder<AlertEscalation> builder)
    {
        builder.ToTable("AlertEscalations", t =>
            t.HasCheckConstraint("CK_AlertEscalations_DifferentUsers", "\"EscalatedBy\" <> \"EscalatedTo\""));
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

        builder.HasOne(e => e.Tower)
            .WithMany(t => t.IncidentReports)
            .HasForeignKey(e => e.TowerId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
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

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Technician)
            .WithMany()
            .HasForeignKey(e => e.TechnicianId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MaintenanceProofConfiguration : IEntityTypeConfiguration<MaintenanceProof>
{
    public void Configure(EntityTypeBuilder<MaintenanceProof> builder)
    {
        builder.ToTable("MaintenanceProofs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProofType).HasMaxLength(30).HasDefaultValue("After");

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

public class AIAnalysisRunConfiguration : IEntityTypeConfiguration<AIAnalysisRun>
{
    public void Configure(EntityTypeBuilder<AIAnalysisRun> builder)
    {
        builder.ToTable("AIAnalysisRuns", t => t.HasCheckConstraint(
            "CK_AIAnalysisRuns_ExecutionLocation", "\"ExecutionLocation\" IN ('Edge', 'Cloud')"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ExecutionLocation).HasMaxLength(10).IsRequired();
        builder.Property(e => e.AnalysisType).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ModelName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ModelVersion).HasMaxLength(50);
        builder.HasIndex(e => e.SourceEventId).IsUnique().HasFilter("\"SourceEventId\" IS NOT NULL");
        builder.HasIndex(e => new { e.MediaId, e.AnalysisType, e.ModelName })
            .IsUnique().HasDatabaseName("IX_AIAnalysisRuns_ActiveLogicalAnalysis")
            .HasFilter("\"IsDeleted\" = false AND \"Status\" IN ('Pending', 'Processing')");

        builder.HasOne(e => e.Media)
            .WithMany(m => m.AnalysisRuns)
            .HasForeignKey(e => e.MediaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Uav)
            .WithMany()
            .HasForeignKey(e => e.UavId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Requester)
            .WithMany()
            .HasForeignKey(e => e.RequestedBy)
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
        builder.HasIndex(e => new { e.MessageType, e.OccurredAt }).HasFilter("\"PublishedAt\" IS NULL");
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
        builder.Property(e => e.Snapshot).HasColumnType("jsonb");

        builder.HasOne(e => e.Region)
            .WithMany()
            .HasForeignKey(e => e.RegionId)
            .OnDelete(DeleteBehavior.SetNull);

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

public class DroneFlightControllerConfiguration : IEntityTypeConfiguration<DroneFlightController>
{
    public void Configure(EntityTypeBuilder<DroneFlightController> builder)
    {
        builder.ToTable("DroneFlightControllers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FlightControllerUid).IsRequired().HasMaxLength(128);
        builder.Property(x => x.BoardIdentifier).HasMaxLength(100);
        builder.Property(x => x.FirmwareName).HasMaxLength(100);
        builder.Property(x => x.FirmwareVersion).HasMaxLength(100);

        builder.HasIndex(x => x.FlightControllerUid).IsUnique()
            .HasFilter("\"UnpairedAt\" IS NULL AND NOT \"IsDeleted\"");

        builder.HasIndex(x => x.DroneId).IsUnique()
            .HasFilter("\"UnpairedAt\" IS NULL AND NOT \"IsDeleted\"");

        builder.HasOne(x => x.Drone)
            .WithMany(x => x.FlightControllers)
            .HasForeignKey(x => x.DroneId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


