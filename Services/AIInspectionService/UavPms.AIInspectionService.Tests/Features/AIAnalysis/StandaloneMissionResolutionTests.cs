using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.AIInspectionService.Application.Common.Exceptions;
using UavPms.AIInspectionService.Application.Features.AIAnalysis.Commands.UploadStandaloneAnalysis;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.AIInspectionService.Infrastructure.Repositories;
using UavPms.AIInspectionService.Tests.Common;
using UavPms.Shared.Contracts.Events;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.AIAnalysis;

/// <summary>
/// Upload không truyền missionId (standalone) chỉ gom vào nhiệm vụ STD- của chính người gọi, không bao giờ gắn vào
/// nhiệm vụ kiểm tra thật hay nhiệm vụ của người khác (trước đây lấy nhiệm vụ mới nhất của cả hệ thống).
/// </summary>
public class StandaloneMissionResolutionTests
{
    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Mock.Of<ICurrentUserServices>());

    private static Task<Guid> UploadWithoutMissionAsync(ApplicationDbContext context, Guid userId)
    {
        var currentUser = new Mock<ICurrentUserServices>();
        currentUser.Setup(u => u.UserId).Returns(userId);
        var fileStorage = new Mock<IFileStorageService>();
        fileStorage.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync((Stream _, string fileName) => $"uav-media/{fileName}");
        var handler = new UploadStandaloneAnalysisCommandHandler(
            new GenericRepository<Mission>(context),
            new GenericRepository<Tower>(context),
            new GenericRepository<InspectionMedia>(context),
            new GenericRepository<AIAnalysisRun>(context),
            fileStorage.Object,
            Mock.Of<IEventPublisher>(),
            currentUser.Object,
            AccessPolicyStub.AllowAll(),
            new UnitOfWork(context),
            Mock.Of<ILogger<UploadStandaloneAnalysisCommandHandler>>(),
            new GenericRepository<OutboxMessage>(context));
        return UploadAsync(handler);
    }

    private static async Task<Guid> UploadAsync(UploadStandaloneAnalysisCommandHandler handler)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("image"));
        var result = await handler.Handle(new UploadStandaloneAnalysisCommand
        {
            Files = new[] { new UploadedFileInput { Stream = stream, FileName = $"{Guid.NewGuid():N}.jpg", ContentType = "image/jpeg", Length = stream.Length } }
        }, CancellationToken.None);
        return result.MissionId;
    }

    [Fact]
    public async Task Standalone_NeverUsesRealOrOtherUsersMissions_AndReusesOwnStandaloneMission()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var ownRealMission = new Mission { Id = Guid.NewGuid(), MissionCode = "MS-OWN", ManagerId = userId, CreatedAt = DateTime.UtcNow };
        var otherUsersStandalone = new Mission { Id = Guid.NewGuid(), MissionCode = "STD-OTHER", ManagerId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        context.Missions.AddRange(ownRealMission, otherUsersStandalone);
        await context.SaveChangesAsync();

        var first = await UploadWithoutMissionAsync(context, userId);
        var second = await UploadWithoutMissionAsync(context, userId);

        first.Should().NotBe(ownRealMission.Id).And.NotBe(otherUsersStandalone.Id);
        second.Should().Be(first);
        var standalone = await context.Missions.SingleAsync(m => m.Id == first);
        standalone.MissionCode.Should().StartWith(Mission.StandaloneCodePrefix);
        standalone.ManagerId.Should().Be(userId);
        (await context.InspectionMedia.CountAsync(m => m.MissionId == ownRealMission.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Standalone_WithoutAuthenticatedUser_RequiresMission()
    {
        await using var context = CreateContext();
        context.Missions.Add(new Mission { Id = Guid.NewGuid(), MissionCode = "MS-ANY", ManagerId = Guid.NewGuid() });
        await context.SaveChangesAsync();

        var act = () => UploadWithoutMissionAsync(context, Guid.Empty);

        await act.Should().ThrowAsync<BusinessRuleException>();
        (await context.InspectionMedia.AnyAsync()).Should().BeFalse();
    }
}
