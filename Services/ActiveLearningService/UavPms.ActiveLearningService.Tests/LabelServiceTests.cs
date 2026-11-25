using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UavPms.ActiveLearningService.API.Data;
using UavPms.ActiveLearningService.API.Labels;

namespace UavPms.ActiveLearningService.Tests;

public class LabelServiceTests
{
    private readonly Guid _analyst = Guid.NewGuid();
    private readonly Guid _mission = Guid.NewGuid();
    private readonly Guid _media = Guid.NewGuid();
    private readonly Guid _videoMedia = Guid.NewGuid();
    private readonly Guid _imageAnomaly = Guid.NewGuid();
    private readonly Guid _secondImageAnomaly = Guid.NewGuid();
    private readonly Guid _frameAnomaly = Guid.NewGuid();
    private readonly ActiveLearningDbContext _db;
    private readonly LabelService _service;

    public LabelServiceTests()
    {
        _db = new ActiveLearningDbContext(new DbContextOptionsBuilder<ActiveLearningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Media.AddRange(new MediaRef { Id = _media, MissionId = _mission }, new MediaRef { Id = _videoMedia, MissionId = _mission });
        _db.Anomalies.AddRange(
            new AnomalyRef { Id = _imageAnomaly, MediaId = _media },
            new AnomalyRef { Id = _secondImageAnomaly, MediaId = _media },
            new AnomalyRef { Id = _frameAnomaly, MediaId = _videoMedia, FrameIndex = 12 });
        _db.MissionAssignments.Add(new MissionAssignmentRef
            { Id = Guid.NewGuid(), MissionId = _mission, UserId = _analyst, AssignmentRole = "ANALYST", Status = "Active" });
        _db.SaveChanges();
        _service = new LabelService(_db);
    }

    private static LabelBoxDto Box(string cls = "insulator_broken", string source = "ai", double? conf = 88) =>
        new(cls, 0.15, 0.2, 0.25, 0.3, source, conf);

    private static SaveLabelsRequest Request(bool full, params LabelBoxDto[] boxes) => new(full, null, boxes.ToList());

    private async Task<LabelException> Fails(Func<Task> act)
    {
        var ex = await Assert.ThrowsAsync<LabelException>(act);
        return ex;
    }

    [Fact]
    public void Validate_accepts_the_fe_example_and_an_empty_image()
    {
        LabelService.Validate(Request(true, Box(), new LabelBoxDto("insulator", 0.52, 0.18, 0.14, 0.22, "manual", null)))
            .Should().BeEmpty();
        LabelService.Validate(Request(true)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("cat", 0.1, 0.1, 0.2, 0.2, "ai", 50.0)]          // unknown class
    [InlineData("insulator", 0.9, 0.1, 0.2, 0.2, "ai", 50.0)]    // x + width > 1
    [InlineData("insulator", 0.1, 0.1, 0.0, 0.2, "ai", 50.0)]    // zero width
    [InlineData("insulator", -0.1, 0.1, 0.2, 0.2, "ai", 50.0)]   // negative x
    [InlineData("insulator", 0.1, 0.1, 0.2, 0.2, "model", 50.0)] // bad source
    [InlineData("insulator", 0.1, 0.1, 0.2, 0.2, "manual", 50.0)] // manual box with confidence
    [InlineData("insulator", 0.1, 0.1, 0.2, 0.2, "ai", 101.0)]   // confidence out of range
    public void Validate_rejects_invalid_boxes(string cls, double x, double y, double w, double h, string source, double conf)
    {
        LabelService.Validate(Request(true, new LabelBoxDto(cls, x, y, w, h, source, conf))).Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_requires_boxes_array_and_non_negative_frame()
    {
        LabelService.Validate(new SaveLabelsRequest(true, null, null)).Should().NotBeEmpty();
        LabelService.Validate(new SaveLabelsRequest(true, -1, [])).Should().NotBeEmpty();
    }

    [Fact]
    public async Task Save_creates_draft_then_updates_the_same_image_row()
    {
        var draft = await _service.SaveAsync(_imageAnomaly, Request(false, Box()), _analyst, default);
        draft.LabelStatus.Should().Be("Draft");

        // A second anomaly on the same image edits the same label set (labels are per image, not per anomaly).
        var full = await _service.SaveAsync(_secondImageAnomaly, Request(true), _analyst, default);
        full.LabelStatus.Should().Be("FullyReviewed");

        var rows = await _db.ImageLabelSets.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Boxes.Should().Be("[]");
        rows[0].AnomalyId.Should().Be(_secondImageAnomaly);
    }

    [Fact]
    public async Task Get_returns_saved_boxes_or_null()
    {
        (await _service.GetAsync(_imageAnomaly, _analyst, true, default)).Should().BeNull();
        await _service.SaveAsync(_imageAnomaly, Request(true, Box(), Box("insulator", "manual", null)), _analyst, default);

        var got = await _service.GetAsync(_imageAnomaly, _analyst, true, default);
        got!.LabelStatus.Should().Be("FullyReviewed");
        got.TrainingStatus.Should().BeNull();
        got.Boxes.Should().HaveCount(2);
        got.Boxes[1].Should().Be(Box("insulator", "manual", null));
    }

    [Fact]
    public async Task Video_frame_uses_the_anomaly_frame_and_rejects_a_mismatch()
    {
        await _service.SaveAsync(_frameAnomaly, new SaveLabelsRequest(true, 12, [Box()]), _analyst, default);
        (await _db.ImageLabelSets.SingleAsync()).FrameIndex.Should().Be(12);

        var ex = await Fails(() => _service.SaveAsync(_frameAnomaly, new SaveLabelsRequest(true, 3, [Box()]), _analyst, default));
        ex.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ex.ErrorCode.Should().Be("FRAME_MISMATCH");
    }

    [Fact]
    public async Task Save_rejects_invalid_payload_unknown_anomaly_and_unassigned_analyst()
    {
        (await Fails(() => _service.SaveAsync(_imageAnomaly, Request(true, Box("cat")), _analyst, default)))
            .StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await Fails(() => _service.SaveAsync(Guid.NewGuid(), Request(true), _analyst, default)))
            .StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await Fails(() => _service.SaveAsync(_imageAnomaly, Request(true), Guid.NewGuid(), default)))
            .StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _db.ImageLabelSets.Should().BeEmpty();
    }

    [Fact]
    public async Task Inactive_or_non_analyst_assignment_is_forbidden()
    {
        var inspector = Guid.NewGuid();
        var removed = Guid.NewGuid();
        _db.MissionAssignments.AddRange(
            new MissionAssignmentRef { Id = Guid.NewGuid(), MissionId = _mission, UserId = inspector, AssignmentRole = "INSPECTOR", Status = "Active" },
            new MissionAssignmentRef { Id = Guid.NewGuid(), MissionId = _mission, UserId = removed, AssignmentRole = "ANALYST", Status = "Removed" });
        await _db.SaveChangesAsync();

        (await Fails(() => _service.SaveAsync(_imageAnomaly, Request(true), inspector, default))).ErrorCode.Should().Be("FORBIDDEN");
        (await Fails(() => _service.SaveAsync(_imageAnomaly, Request(true), removed, default))).ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Submit_requires_saved_fully_reviewed_labels()
    {
        var none = await _service.SubmitAsync(_imageAnomaly, new SubmitTrainingRequest("anomaly:x", null), _analyst, default);
        none.Should().Be(new SubmitTrainingResult(false, "NotLabelled", none.Message));

        await _service.SaveAsync(_imageAnomaly, Request(false, Box()), _analyst, default);
        var draft = await _service.SubmitAsync(_imageAnomaly, new SubmitTrainingRequest(null, null), _analyst, default);
        draft.Accepted.Should().BeFalse();
        draft.Status.Should().Be("NotFullyReviewed");
        (await _db.ImageLabelSets.SingleAsync()).TrainingStatus.Should().BeNull();
    }

    [Fact]
    public async Task Submit_queues_once_and_then_locks_the_labels()
    {
        await _service.SaveAsync(_imageAnomaly, Request(true, Box()), _analyst, default);

        var first = await _service.SubmitAsync(_imageAnomaly, new SubmitTrainingRequest($"media:{_media}", null), _analyst, default);
        first.Should().Be(new SubmitTrainingResult(true, "Accepted", "AI đã tiếp nhận ảnh vào tập dữ liệu huấn luyện."));
        var set = await _db.ImageLabelSets.SingleAsync();
        set.TrainingStatus.Should().Be("Queued");
        set.SubmittedAt.Should().NotBeNull();

        var again = await _service.SubmitAsync(_imageAnomaly, new SubmitTrainingRequest(null, null), _analyst, default);
        again.Accepted.Should().BeTrue();
        again.Status.Should().Be("Accepted");

        var locked = await Fails(() => _service.SaveAsync(_imageAnomaly, Request(true), _analyst, default));
        locked.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        locked.ErrorCode.Should().Be("LABELS_LOCKED");
    }

    [Fact]
    public async Task Admin_or_manager_can_read_without_assignment()
    {
        await _service.SaveAsync(_imageAnomaly, Request(true, Box()), _analyst, default);
        (await _service.GetAsync(_imageAnomaly, Guid.NewGuid(), checkAssignment: false, default)).Should().NotBeNull();
    }
}
