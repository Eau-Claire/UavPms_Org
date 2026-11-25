using Microsoft.EntityFrameworkCore;

namespace UavPms.ActiveLearningService.API.Data;

/// <summary>
/// Whole-image label set saved by an analyst for active learning. One row per image, or per video frame.
/// Owned by this service (its own migrations); existing tables are only read.
/// </summary>
public class ImageLabelSet
{
    public const string Draft = "Draft";
    public const string FullyReviewed = "FullyReviewed";
    public const string Queued = "Queued";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MediaId { get; set; }
    /// <summary>NULL for a still image.</summary>
    public int? FrameIndex { get; set; }
    /// <summary>Anomaly from which the label editor was opened.</summary>
    public Guid AnomalyId { get; set; }
    /// <summary>JSON array of boxes (normalized top-left x, y, width, height).</summary>
    public string Boxes { get; set; } = "[]";
    public string LabelStatus { get; set; } = Draft;
    /// <summary>NULL = not submitted; Queued = waiting for the next training batch.</summary>
    public string? TrainingStatus { get; set; }
    public Guid AnalystId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
}

/// <summary>Read-only projections of existing shared tables (excluded from this service's migrations).</summary>
public class AnomalyRef
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public int? FrameIndex { get; set; }
    public bool IsDeleted { get; set; }
}

public class MediaRef
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public bool IsDeleted { get; set; }
}

public class MissionAssignmentRef
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public string AssignmentRole { get; set; } = string.Empty;
    public string? Status { get; set; }
    public bool IsDeleted { get; set; }
}

public class ActiveLearningDbContext(DbContextOptions<ActiveLearningDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_ActiveLearning";

    public DbSet<ImageLabelSet> ImageLabelSets => Set<ImageLabelSet>();
    public DbSet<AnomalyRef> Anomalies => Set<AnomalyRef>();
    public DbSet<MediaRef> Media => Set<MediaRef>();
    public DbSet<MissionAssignmentRef> MissionAssignments => Set<MissionAssignmentRef>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ImageLabelSet>(e =>
        {
            e.ToTable("ImageLabelSets", t =>
            {
                t.HasCheckConstraint("CK_ImageLabelSets_LabelStatus", "\"LabelStatus\" IN ('Draft', 'FullyReviewed')");
                t.HasCheckConstraint("CK_ImageLabelSets_TrainingStatus", "\"TrainingStatus\" IS NULL OR \"TrainingStatus\" IN ('Queued')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Boxes).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.LabelStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.TrainingStatus).HasMaxLength(20);
            // One label set per still image and one per video frame (NULLs are distinct in a plain unique index).
            e.HasIndex(x => x.MediaId).IsUnique().HasFilter("\"FrameIndex\" IS NULL")
                .HasDatabaseName("UX_ImageLabelSets_Media_Image");
            e.HasIndex(x => new { x.MediaId, x.FrameIndex }).IsUnique().HasFilter("\"FrameIndex\" IS NOT NULL")
                .HasDatabaseName("UX_ImageLabelSets_Media_Frame");
            e.HasIndex(x => x.TrainingStatus);
        });

        b.Entity<AnomalyRef>(e =>
        {
            e.ToTable("DetectedAnomalies", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
        });
        b.Entity<MediaRef>(e =>
        {
            e.ToTable("InspectionMedia", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
        });
        b.Entity<MissionAssignmentRef>(e =>
        {
            e.ToTable("MissionAssignments", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
        });
    }
}
