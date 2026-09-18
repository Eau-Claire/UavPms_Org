using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.Commands.CreateReport;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class CreateReportCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReportRepository> _reportRepositoryMock;
    private readonly Mock<ITransmissionLineRepository> _transmissionLineRepositoryMock;
    private readonly Mock<ISubstationRepository> _substationRepositoryMock;
    private readonly Mock<IMissionRepository> _missionRepositoryMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;
    private readonly CreateReportCommandValidator _validator;

    public CreateReportCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _reportRepositoryMock = new Mock<IReportRepository>();
        _transmissionLineRepositoryMock = new Mock<ITransmissionLineRepository>();
        _substationRepositoryMock = new Mock<ISubstationRepository>();
        _missionRepositoryMock = new Mock<IMissionRepository>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
        _validator = new CreateReportCommandValidator();
    }

    [Fact]
    public async Task CreateReport_ShouldCreateDraftReport_WhenValidDataProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserServicesMock.Setup(c => c.UserId).Returns(userId);
        _reportRepositoryMock.Setup(r => r.GetNextReportCodeAsync(It.IsAny<int>())).ReturnsAsync("BC-2026-0001");

        var handler = new CreateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new CreateReportCommand(
            "Báo cáo kiểm tra định kỳ tuyến 500kV",
            "periodic",
            null,
            null,
            null,
            "Mô tả chi tiết",
            null,
            null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Code.Should().Be("BC-2026-0001");
        result.Title.Should().Be("Báo cáo kiểm tra định kỳ tuyến 500kV");
        result.Type.Should().Be("periodic");
        result.Status.Should().Be("draft");

        _reportRepositoryMock.Verify(r => r.AddAsync(It.Is<Report>(x => x.Code == "BC-2026-0001" && x.Status == ReportStatus.Draft)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateReport_ShouldThrowNotFoundException_WhenTransmissionLineDoesNotExist()
    {
        // Arrange
        var lineId = Guid.NewGuid();
        _transmissionLineRepositoryMock.Setup(l => l.GetByIdAsync(lineId, true)).ReturnsAsync((TransmissionLine?)null);

        var handler = new CreateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new CreateReportCommand("Báo cáo", "defect", lineId, null, null, null, null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateReport_ShouldThrowNotFoundException_WhenSubstationDoesNotExist()
    {
        // Arrange
        var substationId = Guid.NewGuid();
        _substationRepositoryMock.Setup(s => s.GetByIdAsync(substationId, true)).ReturnsAsync((Substation?)null);

        var handler = new CreateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new CreateReportCommand("Báo cáo", "defect", null, substationId, null, null, null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateReport_ShouldThrowBusinessRuleException_WhenTypeIsInvalid()
    {
        // Arrange
        var handler = new CreateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new CreateReportCommand("Báo cáo", "invalid_type", null, null, null, null, null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*không hợp lệ*");
    }

    [Fact]
    public async Task CreateReport_ShouldLinkValidMissions_AndFilterOutNonExistentMissions()
    {
        // Arrange
        var validMissionId = Guid.NewGuid();
        var nonExistentMissionId = Guid.NewGuid();
        var validMission = new Mission { Id = validMissionId, MissionCode = "MS-001", Title = "Mission 1", IsDeleted = false };

        _missionRepositoryMock.Setup(m => m.GetByIdAsync(validMissionId, true)).ReturnsAsync(validMission);
        _missionRepositoryMock.Setup(m => m.GetByIdAsync(nonExistentMissionId, true)).ReturnsAsync((Mission?)null);
        _reportRepositoryMock.Setup(r => r.GetNextReportCodeAsync(It.IsAny<int>())).ReturnsAsync("BC-2026-0005");

        Report? capturedReport = null;
        _reportRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Report>()))
            .Callback<Report>(r => capturedReport = r)
            .ReturnsAsync((Report r) => r);

        var handler = new CreateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new CreateReportCommand(
            "Báo cáo liên kết đợt bay",
            "defect",
            null,
            null,
            new List<Guid> { validMissionId, nonExistentMissionId, validMissionId }, // Có duplicate
            null,
            null,
            null
        );

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        capturedReport.Should().NotBeNull();
        capturedReport!.ReportMissions.Should().HaveCount(1);
        capturedReport.ReportMissions.First().MissionId.Should().Be(validMissionId);
    }

    [Fact]
    public async Task CreateReport_ShouldCalculateDefectCountAutomatically_WhenMissionsProvided()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission { Id = missionId, MissionCode = "MS-001", Title = "Mission 1", IsDeleted = false };
        _missionRepositoryMock.Setup(m => m.GetByIdAsync(missionId, true)).ReturnsAsync(mission);
        _reportRepositoryMock.Setup(r => r.GetNextReportCodeAsync(It.IsAny<int>())).ReturnsAsync("BC-2026-0010");
        _reportRepositoryMock.Setup(r => r.CountAnomaliesByMissionIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(7);

        Report? capturedReport = null;
        _reportRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Report>()))
            .Callback<Report>(r => capturedReport = r)
            .ReturnsAsync((Report r) => r);

        var handler = new CreateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new CreateReportCommand("Báo cáo khuyết tật", "defect", null, null, new List<Guid> { missionId }, null, null, null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        capturedReport.Should().NotBeNull();
        capturedReport!.DefectCount.Should().Be(7);
        _reportRepositoryMock.Verify(r => r.CountAnomaliesByMissionIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(missionId))), Times.Once);
    }

    [Theory]
    [InlineData("", "defect", false)]
    [InlineData("   ", "defect", false)]
    [InlineData("Valid Title", "invalid", false)]
    [InlineData("Valid Title", "defect", true)]
    [InlineData("Valid Title", "periodic", true)]
    [InlineData("Valid Title", "thermal", true)]
    [InlineData("Valid Title", "corridor", true)]
    public void Validator_ShouldValidateInputAppropriately(string title, string type, bool expectedValid)
    {
        // Arrange
        var command = new CreateReportCommand(title, type, null, null, null, null, null, null);

        // Act
        var validationResult = _validator.Validate(command);

        // Assert
        validationResult.IsValid.Should().Be(expectedValid);
    }
}
