using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Reports.Commands.ApproveReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.RejectReport;
using UavPms.OperationsService.Application.Features.Reports.Commands.SubmitReport;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Reports;

public class ReportWorkflowTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IReportRepository> _reportRepositoryMock;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;

    public ReportWorkflowTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _reportRepositoryMock = new Mock<IReportRepository>();
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
    }

    #region Submit Tests

    [Fact]
    public async Task SubmitReport_ShouldChangeStatusToPending_WhenReportIsDraftAndUserIsCreator()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Draft, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(creatorId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new SubmitReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        var result = await handler.Handle(new SubmitReportCommand(reportId), CancellationToken.None);

        // Assert
        result.Status.Should().Be("pending");
        report.Status.Should().Be(ReportStatus.Pending);
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitReport_ShouldThrowForbiddenException_WhenUserIsNotCreatorAndNotAdmin()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Draft, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(otherUserId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new SubmitReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new SubmitReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*không có quyền gửi duyệt*");
    }

    [Fact]
    public async Task SubmitReport_ShouldSucceed_WhenUserIsManagerInGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Draft, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new SubmitReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        var result = await handler.Handle(new SubmitReportCommand(reportId), CancellationToken.None);

        // Assert
        result.Status.Should().Be("pending");
        report.Status.Should().Be(ReportStatus.Pending);
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitReport_ShouldThrowForbiddenException_WhenManagerIsOutOfGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Draft, CreatedBy = creatorId };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(false);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new SubmitReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new SubmitReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ngoài khu vực quản lý*");
    }

    [Theory]
    [InlineData(ReportStatus.Pending)]
    [InlineData(ReportStatus.Approved)]
    public async Task SubmitReport_ShouldThrowInvalidOperationException_WhenStatusIsNotDraft(ReportStatus invalidStatus)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = invalidStatus, CreatedBy = userId };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(userId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.SystemAdmin });

        var handler = new SubmitReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new SubmitReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Bản nháp (Draft)*");
    }

    [Fact]
    public async Task SubmitReport_ShouldThrowNotFoundException_WhenReportDoesNotExist()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync((Report?)null);

        var handler = new SubmitReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new SubmitReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Approve Tests

    [Fact]
    public async Task ApproveReport_ShouldChangeStatusToApproved_AndRecordApprover_WhenReportIsPendingAndManagerInScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Pending };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(approverId, report)).ReturnsAsync(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(approverId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new ApproveReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        var result = await handler.Handle(new ApproveReportCommand(reportId), CancellationToken.None);

        // Assert
        result.Status.Should().Be("approved");
        report.Status.Should().Be(ReportStatus.Approved);
        report.ApprovedById.Should().Be(approverId);
        report.ApprovedAt.Should().NotBeNull();
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveReport_ShouldThrowForbiddenException_WhenUserIsNotManagerOrAdmin()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Pending };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Inspector });

        var handler = new ApproveReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new ApproveReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Chỉ cấp quản lý*");
    }

    [Fact]
    public async Task ApproveReport_ShouldThrowForbiddenException_WhenManagerIsOutOfGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Pending };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(false);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new ApproveReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new ApproveReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ngoài khu vực quản lý*");
    }

    [Theory]
    [InlineData(ReportStatus.Draft)]
    [InlineData(ReportStatus.Approved)]
    public async Task ApproveReport_ShouldThrowInvalidOperationException_WhenStatusIsNotPending(ReportStatus invalidStatus)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = invalidStatus };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.SystemAdmin });

        var handler = new ApproveReportCommandHandler(
            _unitOfWorkMock.Object,
            _reportRepositoryMock.Object,
            _currentUserServicesMock.Object
        );

        // Act
        Func<Task> act = async () => await handler.Handle(new ApproveReportCommand(reportId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Chờ duyệt (Pending)*");
    }

    #endregion

    #region Reject Tests

    [Fact]
    public async Task RejectReport_ShouldRevertStatusToDraft_AndRecordReason_WhenReportIsPendingAndManagerInScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Pending };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(true);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new RejectReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        var result = await handler.Handle(new RejectReportCommand(reportId, "Cần bổ sung ảnh hiện trường cột 20-25"), CancellationToken.None);

        // Assert
        result.Status.Should().Be("draft");
        report.Status.Should().Be(ReportStatus.Draft);
        report.RejectionReason.Should().Be("Cần bổ sung ảnh hiện trường cột 20-25");
        _reportRepositoryMock.Verify(r => r.UpdateAsync(report), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectReport_ShouldThrowForbiddenException_WhenManagerIsOutOfGeographicScope()
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = ReportStatus.Pending };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _reportRepositoryMock.Setup(r => r.CanUserManageReportAsync(managerId, report)).ReturnsAsync(false);
        _currentUserServicesMock.Setup(c => c.UserId).Returns(managerId);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.Manager });

        var handler = new RejectReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new RejectReportCommand(reportId, "Lý do hợp lệ"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ngoài khu vực quản lý*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectReport_ShouldThrowBusinessRuleException_WhenReasonIsEmptyOrWhitespace(string invalidReason)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var handler = new RejectReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new RejectReportCommand(reportId, invalidReason), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*không được để trống*");
    }

    [Theory]
    [InlineData(ReportStatus.Draft)]
    [InlineData(ReportStatus.Approved)]
    public async Task RejectReport_ShouldThrowInvalidOperationException_WhenStatusIsNotPending(ReportStatus invalidStatus)
    {
        // Arrange
        var reportId = Guid.NewGuid();
        var report = new Report { Id = reportId, Title = "Report 1", Status = invalidStatus };
        _reportRepositoryMock.Setup(r => r.GetReportByIdWithDetailsAsync(reportId)).ReturnsAsync(report);
        _currentUserServicesMock.Setup(c => c.Roles).Returns(new List<string> { UserRoles.SystemAdmin });

        var handler = new RejectReportCommandHandler(_unitOfWorkMock.Object, _reportRepositoryMock.Object, _currentUserServicesMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new RejectReportCommand(reportId, "Lý do hợp lệ"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Chờ duyệt (Pending)*");
    }

    #endregion
}
