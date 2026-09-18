using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.Commands.DeleteReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.UpdateReport;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class UpdateAndDeleteReportCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReportRepository> _reportRepositoryMock;
    private readonly Mock<ITransmissionLineRepository> _transmissionLineRepositoryMock;
    private readonly Mock<ISubstationRepository> _substationRepositoryMock;
    private readonly Mock<IMissionRepository> _missionRepositoryMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;

    public UpdateAndDeleteReportCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _reportRepositoryMock = new Mock<IReportRepository>();
        _transmissionLineRepositoryMock = new Mock<ITransmissionLineRepository>();
        _substationRepositoryMock = new Mock<ISubstationRepository>();
        _missionRepositoryMock = new Mock<IMissionRepository>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
    }

    #region Update Tests

    [Fact]
    public async Task UpdateReport_ShouldUpdateDetails_WhenReportIsDraftAndUserIsCreator()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Title = "Old Title",
            Description = "Old Desc",
            Status = ReportStatus.Draft,
            CreatedBy = creatorId
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(creatorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new UpdateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new UpdateReportCommand(reportId, "New Title", "New Desc", null, null, null, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be("New Title");
        report.Title.Should().Be("New Title");
        report.Description.Should().Be("New Desc");
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateReport_ShouldThrowForbiddenException_WhenUserIsNotCreatorAndNotAdmin()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Title = "Old Title",
            Status = ReportStatus.Draft,
            CreatedBy = creatorId
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(otherUserId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new UpdateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new UpdateReportCommand(reportId, "New Title", null, null, null, null, null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*chỉ có thể chỉnh sửa báo cáo do chính mình tạo ra*");
    }

    [Fact]
    public async Task UpdateReport_ShouldSucceed_WhenUserIsManagerInGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Title = "Old Title",
            Status = ReportStatus.Draft,
            CreatedBy = creatorId
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new UpdateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new UpdateReportCommand(reportId, "New Title By Manager", null, null, null, null, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Title.Should().Be("New Title By Manager");
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateReport_ShouldThrowForbiddenException_WhenManagerIsOutOfGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report
        {
            Id = reportId,
            Title = "Old Title",
            Status = ReportStatus.Draft,
            CreatedBy = creatorId
        };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(false);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new UpdateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new UpdateReportCommand(reportId, "New Title By Manager", null, null, null, null, null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ngoài khu vực quản lý*");
    }

    [Fact]
    public async Task UpdateReport_ShouldRecalculateDefectCount_WhenMissionsUpdated()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var mission = new Mission { Id = missionId, MissionCode = "MS-002", Title = "Mission 2", IsDeleted = false };
        var report = new Report { Id = reportId, Title = "Title", Status = ReportStatus.Draft, DefectCount = 2, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _missionRepositoryMock.Setup(m => m.GetByIdAsync(missionId, true)).ReturnsAsync(mission);
        _reportRepositoryMock.Setup(r => r.CountAnomaliesByMissionIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(9);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(creatorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new UpdateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new UpdateReportCommand(reportId, "New Title", null, null, null, new List<Guid> { missionId }, null, null);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        report.DefectCount.Should().Be(9);
        _reportRepositoryMock.Verify(r => r.CountAnomaliesByMissionIdsAsync(It.Is<IEnumerable<Guid>>(ids => ids.Contains(missionId))), Times.Once);
    }

    [Theory]
    [InlineData(ReportStatus.Pending)]
    [InlineData(ReportStatus.Approved)]
    public async Task UpdateReport_ShouldThrowInvalidOperationException_WhenStatusIsNotDraft(ReportStatus invalidStatus)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Title", Status = invalidStatus, CreatedBy = userId };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(userId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.SystemAdmin });

        var handler = new UpdateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new UpdateReportCommand(reportId, "New Title", null, null, null, null, null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Bản nháp (Draft)*");
    }

    [Fact]
    public async Task UpdateReport_ShouldThrowNotFoundException_WhenReportDoesNotExist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync((Report?)null);

        var handler = new UpdateReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _transmissionLineRepositoryMock.Object,
            _substationRepositoryMock.Object,
            _missionRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        var command = new UpdateReportCommand(reportId, "New Title", null, null, null, null, null, null);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteReport_ShouldSoftDelete_WhenReportIsDraftAndUserIsCreator()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Title", Status = ReportStatus.Draft, IsDeleted = false, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, true)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(creatorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new DeleteReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        var result = await handler.Handle(new DeleteReportCommand(reportId), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        report.IsDeleted.Should().BeTrue();
        report.DeletedAt.Should().NotBeNull();
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteReport_ShouldThrowForbiddenException_WhenUserIsNotCreatorAndNotAdmin()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Title", Status = ReportStatus.Draft, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, true)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(otherUserId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new DeleteReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new DeleteReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*chỉ có thể xóa báo cáo do chính mình tạo ra*");
    }

    [Fact]
    public async Task DeleteReport_ShouldSucceed_WhenUserIsManagerInGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Title", Status = ReportStatus.Draft, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, true)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new DeleteReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        var result = await handler.Handle(new DeleteReportCommand(reportId), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        report.IsDeleted.Should().BeTrue();
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteReport_ShouldThrowForbiddenException_WhenManagerIsOutOfGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Title", Status = ReportStatus.Draft, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, true)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(false);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new DeleteReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new DeleteReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ngoài khu vực quản lý*");
    }

    [Theory]
    [InlineData(ReportStatus.Pending)]
    [InlineData(ReportStatus.Approved)]
    public async Task DeleteReport_ShouldThrowInvalidOperationException_WhenStatusIsNotDraft(ReportStatus invalidStatus)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Title", Status = invalidStatus, CreatedBy = userId };
        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, true)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(userId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.SystemAdmin });

        var handler = new DeleteReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new DeleteReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Bản nháp (Draft)*");
    }

    [Fact]
    public async Task DeleteReport_ShouldThrowNotFoundException_WhenReportDoesNotExist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        _reportRepositoryMock.Setup(r => r.GetByIdAsync(reportId, true)).ReturnsAsync((Report?)null);

        var handler = new DeleteReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new DeleteReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion
}
