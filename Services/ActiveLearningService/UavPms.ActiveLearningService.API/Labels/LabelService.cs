using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UavPms.ActiveLearningService.API.Data;

namespace UavPms.ActiveLearningService.API.Labels;

public record LabelBoxDto(string? ClassName, double X, double Y, double Width, double Height, string? Source, double? Confidence);

public record SaveLabelsRequest(bool IsFullyReviewed, int? FrameIndex, List<LabelBoxDto>? Boxes);

public record SubmitTrainingRequest(string? MediaKey, int? FrameIndex);

public record SaveLabelsResult(string LabelStatus, DateTime UpdatedAt);

public record SubmitTrainingResult(bool Accepted, string Status, string Message);

public record LabelSetResult(Guid AnomalyId, Guid MediaId, int? FrameIndex, string LabelStatus, string? TrainingStatus,
    IReadOnlyList<LabelBoxDto> Boxes, DateTime UpdatedAt, DateTime? SubmittedAt);

/// <summary>Expected business failure, mapped to an HTTP status by the controller.</summary>
public class LabelException(int statusCode, string errorCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string ErrorCode { get; } = errorCode;
}

public class LabelService(ActiveLearningDbContext db)
{
    public static readonly string[] Classes =
        ["insulator", "insulator_broken", "insulator_flashover", "bird_nest", "foreign_object", "broken_strand"];

    public const int MaxBoxes = 500;
    private const double Tolerance = 1e-6;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static List<string> Validate(SaveLabelsRequest request)
    {
        var errors = new List<string>();
        var boxes = request.Boxes;
        if (boxes == null)
        {
            errors.Add("boxes is required (use an empty array for an image without objects).");
            return errors;
        }
        if (boxes.Count > MaxBoxes)
            errors.Add($"At most {MaxBoxes} boxes are allowed.");
        if (request.FrameIndex is < 0)
            errors.Add("frameIndex must be >= 0 or null.");

        for (var i = 0; i < boxes.Count; i++)
        {
            var b = boxes[i];
            var at = $"boxes[{i}]";
            if (b.ClassName == null || !Classes.Contains(b.ClassName))
                errors.Add($"{at}.className must be one of: {string.Join(", ", Classes)}.");
            if (!double.IsFinite(b.X) || !double.IsFinite(b.Y) || !double.IsFinite(b.Width) || !double.IsFinite(b.Height) ||
                b.X < 0 || b.Y < 0 || b.Width <= 0 || b.Height <= 0 ||
                b.X + b.Width > 1 + Tolerance || b.Y + b.Height > 1 + Tolerance)
                errors.Add($"{at}: x, y, width, height must be normalized (0..1) with positive size inside the image.");
            if (b.Source is not ("ai" or "manual"))
                errors.Add($"{at}.source must be 'ai' or 'manual'.");
            else if (b.Source == "manual" && b.Confidence != null)
                errors.Add($"{at}.confidence must be null for a manual box.");
            if (b.Confidence is { } c && (!double.IsFinite(c) || c < 0 || c > 100))
                errors.Add($"{at}.confidence must be between 0 and 100.");
        }
        return errors;
    }

    public async Task<LabelSetResult?> GetAsync(Guid anomalyId, Guid userId, bool checkAssignment, CancellationToken ct)
    {
        var anomaly = await LoadAccessibleAnomalyAsync(anomalyId, userId, checkAssignment, ct);
        var set = await FindSetAsync(anomaly.MediaId, anomaly.FrameIndex, ct);
        if (set == null) return null;
        var boxes = JsonSerializer.Deserialize<List<LabelBoxDto>>(set.Boxes, Json) ?? [];
        return new LabelSetResult(set.AnomalyId, set.MediaId, set.FrameIndex, set.LabelStatus, set.TrainingStatus,
            boxes, set.UpdatedAt, set.SubmittedAt);
    }

    public async Task<SaveLabelsResult> SaveAsync(Guid anomalyId, SaveLabelsRequest request, Guid userId, bool checkAssignment, CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            throw new LabelException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", string.Join(" ", errors));

        var anomaly = await LoadAccessibleAnomalyAsync(anomalyId, userId, checkAssignment, ct);
        EnsureSameFrame(anomaly, request.FrameIndex);

        var set = await FindSetAsync(anomaly.MediaId, anomaly.FrameIndex, ct);
        if (set?.TrainingStatus != null)
            throw new LabelException(StatusCodes.Status409Conflict, "LABELS_LOCKED",
                "Labels were already submitted for training and can no longer be changed.");

        var now = DateTime.UtcNow;
        if (set == null)
        {
            set = new ImageLabelSet { MediaId = anomaly.MediaId, FrameIndex = anomaly.FrameIndex, CreatedAt = now };
            db.ImageLabelSets.Add(set);
        }
        set.AnomalyId = anomaly.Id;
        set.AnalystId = userId;
        set.Boxes = JsonSerializer.Serialize(request.Boxes, Json);
        set.LabelStatus = request.IsFullyReviewed ? ImageLabelSet.FullyReviewed : ImageLabelSet.Draft;
        set.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another analyst saved the same image at the same moment (unique index); let the client retry.
            throw new LabelException(StatusCodes.Status409Conflict, "CONCURRENT_UPDATE",
                "The labels of this image were changed at the same time; please reload and try again.");
        }
        return new SaveLabelsResult(set.LabelStatus, set.UpdatedAt);
    }

    public async Task<SubmitTrainingResult> SubmitAsync(Guid anomalyId, SubmitTrainingRequest request, Guid userId, bool checkAssignment, CancellationToken ct)
    {
        var anomaly = await LoadAccessibleAnomalyAsync(anomalyId, userId, checkAssignment, ct);
        EnsureSameFrame(anomaly, request.FrameIndex);

        var set = await FindSetAsync(anomaly.MediaId, anomaly.FrameIndex, ct);
        if (set == null)
            return new SubmitTrainingResult(false, "NotLabelled", "Ảnh chưa được lưu nhãn.");
        if (set.TrainingStatus == ImageLabelSet.Queued)
            return new SubmitTrainingResult(true, "Accepted", "Ảnh đã nằm trong hàng chờ huấn luyện.");
        if (set.LabelStatus != ImageLabelSet.FullyReviewed)
            return new SubmitTrainingResult(false, "NotFullyReviewed",
                "Chỉ ảnh đã được kiểm tra toàn bộ (FullyReviewed) mới được gửi cho AI học.");

        set.TrainingStatus = ImageLabelSet.Queued;
        set.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new SubmitTrainingResult(true, "Accepted", "AI đã tiếp nhận ảnh vào tập dữ liệu huấn luyện.");
    }

    private Task<ImageLabelSet?> FindSetAsync(Guid mediaId, int? frameIndex, CancellationToken ct) =>
        db.ImageLabelSets.FirstOrDefaultAsync(s => s.MediaId == mediaId && s.FrameIndex == frameIndex, ct);

    private static void EnsureSameFrame(AnomalyRef anomaly, int? requestFrame)
    {
        if (requestFrame != null && requestFrame != anomaly.FrameIndex)
            throw new LabelException(StatusCodes.Status400BadRequest, "FRAME_MISMATCH",
                $"frameIndex {requestFrame} does not match the anomaly frame ({anomaly.FrameIndex?.ToString() ?? "still image"}).");
    }

    /// <summary>Same rule as the anomaly list: an analyst only works on missions they are actively assigned to.</summary>
    private async Task<AnomalyRef> LoadAccessibleAnomalyAsync(Guid anomalyId, Guid userId, bool checkAssignment, CancellationToken ct)
    {
        var anomaly = await db.Anomalies.AsNoTracking().FirstOrDefaultAsync(a => a.Id == anomalyId && !a.IsDeleted, ct)
            ?? throw new LabelException(StatusCodes.Status404NotFound, "NOT_FOUND", $"Anomaly with ID {anomalyId} was not found.");
        if (!checkAssignment) return anomaly;

        var media = await db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == anomaly.MediaId && !m.IsDeleted, ct);
        if (media == null) return anomaly;

        var assigned = await db.MissionAssignments.AsNoTracking().AnyAsync(a =>
            a.MissionId == media.MissionId && a.UserId == userId && !a.IsDeleted && a.Status == "Active" &&
            a.AssignmentRole.ToUpper() == "ANALYST", ct);
        if (!assigned)
            throw new LabelException(StatusCodes.Status403Forbidden, "FORBIDDEN", "You are not assigned as analyst to this mission.");
        return anomaly;
    }
}
