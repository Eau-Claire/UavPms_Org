using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Inspections.Queries.GetByMission;
using UavPms.OperationsService.Application.Features.Inspections.Queries.GetReportById;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class InspectionAuthorizationTests
{
    private readonly Mock<IInspectionMediaRepository> _mediaRepoMock;
    private readonly Mock<IMissionRepository> _missionRepoMock;
    private readonly Mock<ICurrentUserServices> _currentUserMock;
    private readonly Guid _userId = Guid.NewGuid();

    public InspectionAuthorizationTests()
    {
        _mediaRepoMock = new Mock<IInspectionMediaRepository>();
        _missionRepoMock = new Mock<IMissionRepository>();
        _currentUserMock = new Mock<ICurrentUserServices>();

        _currentUserMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(c => c.UserId).Returns(_userId);
        _currentUserMock.Setup(c => c.Roles).Returns(new[] { UserRoles.Inspector });
    }

    [Fact]
    public async Task GetInspectionsByMission_ShouldThrowForbiddenException_WhenUserCannotAccessMission()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        _missionRepoMock.Setup(m => m.UserCanAccessAsync(missionId, _userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new GetInspectionsByMissionQueryHandler(
            _mediaRepoMock.Object,
            _missionRepoMock.Object,
            _currentUserMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetInspectionsByMissionQuery(missionId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("MISSION_ACCESS_DENIED");
    }

    [Fact]
    public async Task GetInspectionsByMission_ShouldReturnMedia_WhenUserCanAccessMission()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        _missionRepoMock.Setup(m => m.UserCanAccessAsync(missionId, _userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mediaRepoMock.Setup(m => m.GetByMissionIdWithDetailsAsync(missionId))
            .ReturnsAsync(new List<InspectionMedia>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    MissionId = missionId,
                    AssetId = Guid.NewGuid(),
                    MediaType = "Image",
                    FileUrl = "/images/sample.jpg",
                    ValidationStatus = "Pending"
                }
            });

        var handler = new GetInspectionsByMissionQueryHandler(
            _mediaRepoMock.Object,
            _missionRepoMock.Object,
            _currentUserMock.Object);

        // Act
        var result = await handler.Handle(new GetInspectionsByMissionQuery(missionId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].FileUrl.Should().Be("/images/sample.jpg");
    }

    [Fact]
    public async Task GetInspectionReportById_ShouldThrowForbiddenException_WhenUserCannotAccessMission()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        _mediaRepoMock.Setup(m => m.ExistsAsync(mediaId)).ReturnsAsync(true);
        _mediaRepoMock.Setup(m => m.GetByIdWithDetailsAsync(mediaId))
            .ReturnsAsync(new InspectionMedia
            {
                Id = mediaId,
                MissionId = missionId,
                AssetId = Guid.NewGuid(),
                MediaType = "Image",
                FileUrl = "/images/secure.jpg"
            });

        _missionRepoMock.Setup(m => m.UserCanAccessAsync(missionId, _userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new GetInspectionReportByIdQueryHandler(
            _mediaRepoMock.Object,
            _missionRepoMock.Object,
            _currentUserMock.Object);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetInspectionReportByIdQuery(mediaId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("MISSION_ACCESS_DENIED");
    }

    [Fact]
    public async Task GetInspectionReportById_ShouldReturnData_WhenUserCanAccessMission()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        _mediaRepoMock.Setup(m => m.ExistsAsync(mediaId)).ReturnsAsync(true);
        _mediaRepoMock.Setup(m => m.GetByIdWithDetailsAsync(mediaId))
            .ReturnsAsync(new InspectionMedia
            {
                Id = mediaId,
                MissionId = missionId,
                AssetId = Guid.NewGuid(),
                MediaType = "Image",
                FileUrl = "/images/allowed.jpg"
            });

        _missionRepoMock.Setup(m => m.UserCanAccessAsync(missionId, _userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new GetInspectionReportByIdQueryHandler(
            _mediaRepoMock.Object,
            _missionRepoMock.Object,
            _currentUserMock.Object);

        // Act
        var result = await handler.Handle(new GetInspectionReportByIdQuery(mediaId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(mediaId);
        result.FileUrl.Should().Be("/images/allowed.jpg");
    }
}
