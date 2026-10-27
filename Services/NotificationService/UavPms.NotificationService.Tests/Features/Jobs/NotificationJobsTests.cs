using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.NotificationService.API.Jobs;
using UavPms.NotificationService.Application.Features.Notifications.Commands.CreateNotification;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Domain.Interfaces.Repositories;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Domain.Models.Monitor;
using UavPms.NotificationService.Infrastructure.Persistence;
using Xunit;

namespace UavPms.NotificationService.Tests.Features.Jobs;

public class NotificationJobsTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    public NotificationJobsTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
    }

    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, null);
    }

    #region Function A & Edge Cases: PushNotificationsJob

    [Fact]
    public async Task NOTIF_JOB_001_PushNotificationsJob_ValidEmail_ShouldSendEmailAndMarkPushed()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "pilot@example.com", FullName = "Nguyễn Văn Bay" };
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Title = "Thông báo kiểm tra",
            Body = "Nhiệm vụ kiểm tra hoàn tất.",
            PushedAt = null
        };

        var notifRepoMock = new Mock<INotificationRepository>();
        var emailServiceMock = new Mock<IEmailService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<PushNotificationsJob>>();

        notifRepoMock.Setup(r => r.GetUnpushedNotificationsWithActiveUserAsync())
            .ReturnsAsync(new List<Notification> { notification });

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationRepository))).Returns(notifRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailService))).Returns(emailServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(uowMock.Object);

        var job = new PushNotificationsJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        emailServiceMock.Verify(e => e.SendEmailAsync(user.Email, notification.Title, notification.Body), Times.Once);
        notification.PushedAt.Should().NotBeNull();
        notification.PushedAt.Should().NotBeNull();
        notification.PushedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_002_PushNotificationsJob_MissingUserEmail_ShouldLogWarningAndMarkPushedToAvoidInfiniteRetry()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "", FullName = "No Email User" };
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Title = "Cảnh báo khẩn cấp",
            Body = "Nội dung cảnh báo.",
            PushedAt = null
        };

        var notifRepoMock = new Mock<INotificationRepository>();
        var emailServiceMock = new Mock<IEmailService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<PushNotificationsJob>>();

        notifRepoMock.Setup(r => r.GetUnpushedNotificationsWithActiveUserAsync())
            .ReturnsAsync(new List<Notification> { notification });

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationRepository))).Returns(notifRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailService))).Returns(emailServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(uowMock.Object);

        var job = new PushNotificationsJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        emailServiceMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        notification.PushedAt.Should().NotBeNull();
        notification.PushedAt.Should().NotBeNull();
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_003_PushNotificationsJob_NoUnpushed_ShouldExitEarly()
    {
        // Arrange
        var notifRepoMock = new Mock<INotificationRepository>();
        var emailServiceMock = new Mock<IEmailService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<PushNotificationsJob>>();

        notifRepoMock.Setup(r => r.GetUnpushedNotificationsWithActiveUserAsync())
            .ReturnsAsync(new List<Notification>());

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationRepository))).Returns(notifRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailService))).Returns(emailServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(uowMock.Object);

        var job = new PushNotificationsJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        emailServiceMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NOTIF_JOB_004_PushNotificationsJob_OneFails_ShouldContinueRemainingNotifications()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Email = "user1@example.com" };
        var user2 = new User { Id = Guid.NewGuid(), Email = "user2@example.com" };
        var user3 = new User { Id = Guid.NewGuid(), Email = "user3@example.com" };

        var notif1 = new Notification { Id = Guid.NewGuid(), UserId = user1.Id, User = user1, Title = "T1", Body = "B1", PushedAt = null };
        var notif2 = new Notification { Id = Guid.NewGuid(), UserId = user2.Id, User = user2, Title = "T2", Body = "B2", PushedAt = null };
        var notif3 = new Notification { Id = Guid.NewGuid(), UserId = user3.Id, User = user3, Title = "T3", Body = "B3", PushedAt = null };

        var notifRepoMock = new Mock<INotificationRepository>();
        var emailServiceMock = new Mock<IEmailService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<PushNotificationsJob>>();

        notifRepoMock.Setup(r => r.GetUnpushedNotificationsWithActiveUserAsync())
            .ReturnsAsync(new List<Notification> { notif1, notif2, notif3 });

        emailServiceMock.Setup(e => e.SendEmailAsync(user1.Email, notif1.Title, notif1.Body)).Returns(Task.CompletedTask);
        emailServiceMock.Setup(e => e.SendEmailAsync(user2.Email, notif2.Title, notif2.Body)).ThrowsAsync(new Exception("SendGrid Failure"));
        emailServiceMock.Setup(e => e.SendEmailAsync(user3.Email, notif3.Title, notif3.Body)).Returns(Task.CompletedTask);

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationRepository))).Returns(notifRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailService))).Returns(emailServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(uowMock.Object);

        var job = new PushNotificationsJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        notif1.PushedAt.Should().NotBeNull();
        notif2.PushedAt.Should().BeNull(); // Left false for retry
        notif3.PushedAt.Should().NotBeNull();
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_005_PushNotificationsJob_SoftDeletedUser_UserLoadedAsNull_ShouldMarkPushedWithoutSendingEmail()
    {
        // Arrange
        // In EF Core, when User is soft-deleted, Include(x => x.User) returns null for notification.User
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            User = null, // Deleted user filtered out by query filter
            Title = "Thông báo người dùng đã xóa",
            Body = "Nội dung",
            PushedAt = null
        };

        var notifRepoMock = new Mock<INotificationRepository>();
        var emailServiceMock = new Mock<IEmailService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<PushNotificationsJob>>();

        notifRepoMock.Setup(r => r.GetUnpushedNotificationsWithActiveUserAsync())
            .ReturnsAsync(new List<Notification> { notification });

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationRepository))).Returns(notifRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailService))).Returns(emailServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(uowMock.Object);

        var job = new PushNotificationsJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        emailServiceMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        notification.PushedAt.Should().NotBeNull(); // Marked pushed so it does not retry perpetually
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_018_PushNotificationsJob_LargeBatch_ShouldProcessAllItems()
    {
        // Arrange
        var batch = new List<Notification>();
        for (int i = 0; i < 50; i++)
        {
            var user = new User { Id = Guid.NewGuid(), Email = $"user{i}@test.com" };
            batch.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                Title = $"Title {i}",
                Body = $"Body {i}",
                PushedAt = null
            });
        }

        var notifRepoMock = new Mock<INotificationRepository>();
        var emailServiceMock = new Mock<IEmailService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<PushNotificationsJob>>();

        notifRepoMock.Setup(r => r.GetUnpushedNotificationsWithActiveUserAsync()).ReturnsAsync(batch);

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationRepository))).Returns(notifRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailService))).Returns(emailServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(uowMock.Object);

        var job = new PushNotificationsJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        emailServiceMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(50));
        batch.All(n => n.PushedAt != null).Should().BeTrue();
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_019_PushNotificationsJob_SendGridRateLimit429_ShouldCatchAndLeaveUnpushedForNextMinutelyRun()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.com" };
        var notif = new Notification { Id = Guid.NewGuid(), UserId = user.Id, User = user, Title = "T", Body = "B", PushedAt = null };

        var notifRepoMock = new Mock<INotificationRepository>();
        var emailServiceMock = new Mock<IEmailService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<PushNotificationsJob>>();

        notifRepoMock.Setup(r => r.GetUnpushedNotificationsWithActiveUserAsync()).ReturnsAsync(new List<Notification> { notif });
        emailServiceMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SendGrid returned status code TooManyRequests for sending email to user@test.com"));

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationRepository))).Returns(notifRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailService))).Returns(emailServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(uowMock.Object);

        var job = new PushNotificationsJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        Func<Task> act = async () => await job.Execute();

        // Assert: Job does not crash application/Hangfire
        await act.Should().NotThrowAsync();
        notif.PushedAt.Should().BeNull(); // Left false for recurring retry
    }

    [Fact]
    public async Task NOTIF_JOB_020_PushNotificationsJob_VietnameseUtf8_ShouldPreserveEncoding()
    {
        // Arrange
        var title = "⚠️ Cảnh báo khẩn cấp: Kiểm tra đường dây 500kV";
        var body = "Phát hiện khuyết tật cách điện tại cột điện #105 cung đoạn Thạnh Mỹ - Pleiku.";
        var user = new User { Id = Guid.NewGuid(), Email = "inspector@test.com" };
        var notif = new Notification { Id = Guid.NewGuid(), UserId = user.Id, User = user, Title = title, Body = body, PushedAt = null };

        var notifRepoMock = new Mock<INotificationRepository>();
        var emailServiceMock = new Mock<IEmailService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<PushNotificationsJob>>();

        notifRepoMock.Setup(r => r.GetUnpushedNotificationsWithActiveUserAsync()).ReturnsAsync(new List<Notification> { notif });

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(INotificationRepository))).Returns(notifRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IEmailService))).Returns(emailServiceMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUnitOfWork))).Returns(uowMock.Object);

        var job = new PushNotificationsJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        emailServiceMock.Verify(e => e.SendEmailAsync(user.Email, title, body), Times.Once);
    }

    #endregion

    #region Function B & Edge Cases: DailySummaryJob

    [Fact]
    public async Task NOTIF_JOB_006_DailySummaryJob_ShouldCreateSummaryForAdminsAndManagers()
    {
        // Arrange
        var admin = new User { Id = Guid.NewGuid(), FullName = "Admin User" };
        var manager = new User { Id = Guid.NewGuid(), FullName = "Manager User" };

        var monitorRepoMock = new Mock<IMonitorRepository>();
        var userRepoMock = new Mock<IUserRepository>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<DailySummaryJob>>();

        monitorRepoMock.Setup(m => m.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitorSummaryModel { TotalMissions = 12, TotalInspections = 45, TotalDefects = 8, CriticalDefects = 2 });
        monitorRepoMock.Setup(m => m.GetRecentDefectsAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<RecentDefectModel>(), 8));
        monitorRepoMock.Setup(m => m.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActiveAlertModel> { new ActiveAlertModel(), new ActiveAlertModel(), new ActiveAlertModel() });

        userRepoMock.Setup(u => u.GetUsersByRoleAsync("SystemAdmin")).ReturnsAsync(new List<User> { admin });
        userRepoMock.Setup(u => u.GetUsersByRoleAsync("Manager")).ReturnsAsync(new List<User> { manager });

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitorRepository))).Returns(monitorRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository))).Returns(userRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        var job = new DailySummaryJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        mediatorMock.Verify(m => m.Send(It.Is<CreateNotificationCommand>(cmd =>
            cmd.UserId == admin.Id &&
            cmd.Type == "DailySummary" &&
            cmd.ReferenceType == "System" &&
            cmd.Title == "📊 Báo cáo tổng hợp hàng ngày" &&
            cmd.Body.Contains("Tổng nhiệm vụ: 12") &&
            cmd.Body.Contains("Tổng kiểm tra: 45") &&
            cmd.Body.Contains("Cảnh báo đang xử lý: 3") &&
            cmd.Body.Contains("Khuyết tật phát hiện: 8 (nghiêm trọng: 2)")
        ), It.IsAny<CancellationToken>()), Times.Once);

        mediatorMock.Verify(m => m.Send(It.Is<CreateNotificationCommand>(cmd => cmd.UserId == manager.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_007_DailySummaryJob_DualRoleUser_ShouldDeduplicateAndSendOnce()
    {
        // Arrange
        var dualUser = new User { Id = Guid.NewGuid(), FullName = "Admin and Manager User" };

        var monitorRepoMock = new Mock<IMonitorRepository>();
        var userRepoMock = new Mock<IUserRepository>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<DailySummaryJob>>();

        monitorRepoMock.Setup(m => m.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitorSummaryModel());
        monitorRepoMock.Setup(m => m.GetRecentDefectsAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<RecentDefectModel>(), 0));
        monitorRepoMock.Setup(m => m.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActiveAlertModel>());

        userRepoMock.Setup(u => u.GetUsersByRoleAsync("SystemAdmin")).ReturnsAsync(new List<User> { dualUser });
        userRepoMock.Setup(u => u.GetUsersByRoleAsync("Manager")).ReturnsAsync(new List<User> { dualUser });

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitorRepository))).Returns(monitorRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository))).Returns(userRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        var job = new DailySummaryJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert: DistinctBy ensured dualUser receives exactly ONE notification
        mediatorMock.Verify(m => m.Send(It.Is<CreateNotificationCommand>(cmd => cmd.UserId == dualUser.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_008_DailySummaryJob_ShouldExcludeOtherRoles()
    {
        // Arrange
        var admin = new User { Id = Guid.NewGuid() };
        var monitorRepoMock = new Mock<IMonitorRepository>();
        var userRepoMock = new Mock<IUserRepository>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<DailySummaryJob>>();

        monitorRepoMock.Setup(m => m.GetSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new MonitorSummaryModel());
        monitorRepoMock.Setup(m => m.GetRecentDefectsAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync((new List<RecentDefectModel>(), 0));
        monitorRepoMock.Setup(m => m.GetActiveAlertsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ActiveAlertModel>());

        userRepoMock.Setup(u => u.GetUsersByRoleAsync("SystemAdmin")).ReturnsAsync(new List<User> { admin });
        userRepoMock.Setup(u => u.GetUsersByRoleAsync("Manager")).ReturnsAsync(new List<User>());

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitorRepository))).Returns(monitorRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository))).Returns(userRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        var job = new DailySummaryJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert: Never queried Inspector, Analyst, Technician
        userRepoMock.Verify(u => u.GetUsersByRoleAsync("Inspector"), Times.Never);
        userRepoMock.Verify(u => u.GetUsersByRoleAsync("Analyst"), Times.Never);
        userRepoMock.Verify(u => u.GetUsersByRoleAsync("Technician"), Times.Never);
    }

    [Fact]
    public async Task NOTIF_JOB_009_DailySummaryJob_WhenMonitorRepositoryThrows_ShouldCatchAndNotCrash()
    {
        // Arrange
        var monitorRepoMock = new Mock<IMonitorRepository>();
        var userRepoMock = new Mock<IUserRepository>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<DailySummaryJob>>();

        monitorRepoMock.Setup(m => m.GetSummaryAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Database down"));

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitorRepository))).Returns(monitorRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository))).Returns(userRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        var job = new DailySummaryJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        Func<Task> act = async () => await job.Execute();

        // Assert
        await act.Should().NotThrowAsync();
        mediatorMock.Verify(m => m.Send(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NOTIF_JOB_010_DailySummaryJob_VietnamTimezoneUtcPlus7_ShouldFormatDateCorrectly()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid() };
        var monitorRepoMock = new Mock<IMonitorRepository>();
        var userRepoMock = new Mock<IUserRepository>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<DailySummaryJob>>();

        monitorRepoMock.Setup(m => m.GetSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new MonitorSummaryModel());
        monitorRepoMock.Setup(m => m.GetRecentDefectsAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync((new List<RecentDefectModel>(), 0));
        monitorRepoMock.Setup(m => m.GetActiveAlertsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ActiveAlertModel>());

        userRepoMock.Setup(u => u.GetUsersByRoleAsync("SystemAdmin")).ReturnsAsync(new List<User> { user });
        userRepoMock.Setup(u => u.GetUsersByRoleAsync("Manager")).ReturnsAsync(new List<User>());

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitorRepository))).Returns(monitorRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository))).Returns(userRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        var job = new DailySummaryJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        var expectedDateStr = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy");
        mediatorMock.Verify(m => m.Send(It.Is<CreateNotificationCommand>(cmd =>
            cmd.Body.StartsWith($"Tổng kết hệ thống UAV-PMS ngày {expectedDateStr}:")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_021_DailySummaryJob_ZeroStats_ShouldFormatCleanlyWithoutException()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid() };
        var monitorRepoMock = new Mock<IMonitorRepository>();
        var userRepoMock = new Mock<IUserRepository>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<DailySummaryJob>>();

        monitorRepoMock.Setup(m => m.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MonitorSummaryModel { TotalMissions = 0, TotalInspections = 0, TotalDefects = 0, CriticalDefects = 0 });
        monitorRepoMock.Setup(m => m.GetRecentDefectsAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<RecentDefectModel>(), 0));
        monitorRepoMock.Setup(m => m.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActiveAlertModel>());

        userRepoMock.Setup(u => u.GetUsersByRoleAsync("SystemAdmin")).ReturnsAsync(new List<User> { user });
        userRepoMock.Setup(u => u.GetUsersByRoleAsync("Manager")).ReturnsAsync(new List<User>());

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitorRepository))).Returns(monitorRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository))).Returns(userRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        var job = new DailySummaryJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        mediatorMock.Verify(m => m.Send(It.Is<CreateNotificationCommand>(cmd =>
            cmd.Body.Contains("• Tổng nhiệm vụ: 0") &&
            cmd.Body.Contains("• Tổng kiểm tra: 0") &&
            cmd.Body.Contains("• Cảnh báo đang xử lý: 0") &&
            cmd.Body.Contains("• Khuyết tật phát hiện: 0 (nghiêm trọng: 0)")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NOTIF_JOB_022_DailySummaryJob_NoAdminsOrManagers_ShouldCompleteWithZeroNotified()
    {
        // Arrange
        var monitorRepoMock = new Mock<IMonitorRepository>();
        var userRepoMock = new Mock<IUserRepository>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<DailySummaryJob>>();

        monitorRepoMock.Setup(m => m.GetSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new MonitorSummaryModel());
        monitorRepoMock.Setup(m => m.GetRecentDefectsAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync((new List<RecentDefectModel>(), 0));
        monitorRepoMock.Setup(m => m.GetActiveAlertsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ActiveAlertModel>());

        userRepoMock.Setup(u => u.GetUsersByRoleAsync("SystemAdmin")).ReturnsAsync(new List<User>());
        userRepoMock.Setup(u => u.GetUsersByRoleAsync("Manager")).ReturnsAsync(new List<User>());

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitorRepository))).Returns(monitorRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository))).Returns(userRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        var job = new DailySummaryJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        Func<Task> act = async () => await job.Execute();

        // Assert
        await act.Should().NotThrowAsync();
        mediatorMock.Verify(m => m.Send(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NOTIF_JOB_023_DailySummaryJob_ShouldSetReferenceTypeSystemAndTypeDailySummary()
    {
        // Arrange
        var admin = new User { Id = Guid.NewGuid() };
        var monitorRepoMock = new Mock<IMonitorRepository>();
        var userRepoMock = new Mock<IUserRepository>();
        var mediatorMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<DailySummaryJob>>();

        monitorRepoMock.Setup(m => m.GetSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new MonitorSummaryModel());
        monitorRepoMock.Setup(m => m.GetRecentDefectsAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync((new List<RecentDefectModel>(), 0));
        monitorRepoMock.Setup(m => m.GetActiveAlertsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<ActiveAlertModel>());

        userRepoMock.Setup(u => u.GetUsersByRoleAsync("SystemAdmin")).ReturnsAsync(new List<User> { admin });
        userRepoMock.Setup(u => u.GetUsersByRoleAsync("Manager")).ReturnsAsync(new List<User>());

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IMonitorRepository))).Returns(monitorRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(IUserRepository))).Returns(userRepoMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ISender))).Returns(mediatorMock.Object);

        var job = new DailySummaryJob(loggerMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        mediatorMock.Verify(m => m.Send(It.Is<CreateNotificationCommand>(cmd =>
            cmd.Type == "DailySummary" &&
            cmd.ReferenceType == "System" &&
            cmd.ReferenceId == null
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Function C & Edge Cases: CleanupJob

    [Fact]
    public async Task NOTIF_JOB_011_025_026_CleanupJob_FilePurge_OlderThan30DaysDeleted_NewerKept()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "uavpms_test_cleanup_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var oldImage = Path.Combine(tempDir, "old_alert.jpg");
            var newImage = Path.Combine(tempDir, "new_alert.jpg");
            var oldLog = Path.Combine(tempDir, "old_temp.log"); // NOTIF_JOB_026: non-image file
            var boundaryOld = Path.Combine(tempDir, "boundary_old.dat"); // 30.1 days
            var boundaryNew = Path.Combine(tempDir, "boundary_new.dat"); // 29.9 days

            File.WriteAllText(oldImage, "old image data");
            File.WriteAllText(newImage, "new image data");
            File.WriteAllText(oldLog, "old log data");
            File.WriteAllText(boundaryOld, "30.1 days old data");
            File.WriteAllText(boundaryNew, "29.9 days old data");

            // Set file creation times
            File.SetCreationTimeUtc(oldImage, DateTime.UtcNow.AddDays(-35));
            File.SetCreationTimeUtc(newImage, DateTime.UtcNow.AddDays(-5));
            File.SetCreationTimeUtc(oldLog, DateTime.UtcNow.AddDays(-31));
            File.SetCreationTimeUtc(boundaryOld, DateTime.UtcNow.AddDays(-30.1));
            File.SetCreationTimeUtc(boundaryNew, DateTime.UtcNow.AddDays(-29.8));

            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["FileStorage:AlertImagesPath"]).Returns(tempDir);

            var dbContext = CreateInMemoryDbContext();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(dbContext);

            var loggerMock = new Mock<ILogger<CleanupJob>>();
            var job = new CleanupJob(loggerMock.Object, configMock.Object, _scopeFactoryMock.Object);

            // Act
            await job.Execute();

            // Assert
            File.Exists(oldImage).Should().BeFalse("files > 30 days must be deleted");
            File.Exists(oldLog).Should().BeFalse("all file extensions > 30 days must be deleted (NOTIF_JOB_026)");
            File.Exists(boundaryOld).Should().BeFalse("30.1 days old file must be deleted (NOTIF_JOB_025)");

            File.Exists(newImage).Should().BeTrue("files < 30 days must be preserved");
            File.Exists(boundaryNew).Should().BeTrue("29.9 days old file must be preserved (NOTIF_JOB_025)");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task NOTIF_JOB_012_013_017_CleanupJob_DatabaseTokenCleanup_ExpiredAndRevokedPurged()
    {
        // Arrange
        var dbContext = CreateInMemoryDbContext();
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.com" };
        dbContext.Users.Add(user);

        // 1. Expired token (ExpiresAt in the past) -> Should be deleted
        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "expired_token_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        // 2. Revoked token > 30 days ago (RevokedAt = 35 days ago, ExpiresAt in future) -> Should be deleted
        var oldRevokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "old_revoked_token_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(10),
            RevokedAt = DateTime.UtcNow.AddDays(-35),
            CreatedAt = DateTime.UtcNow.AddDays(-40)
        };

        // 3. Valid active token (ExpiresAt in future, not revoked) -> Should be kept
        var validToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "valid_token_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        // 4. Recently revoked token (< 30 days ago, ExpiresAt in future) -> Should be kept
        var recentlyRevokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "recent_revoked_token_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            RevokedAt = DateTime.UtcNow.AddDays(-5),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        dbContext.RefreshTokens.AddRange(expiredToken, oldRevokedToken, validToken, recentlyRevokedToken);
        await dbContext.SaveChangesAsync();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["FileStorage:AlertImagesPath"]).Returns(string.Empty);

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(dbContext);

        var loggerMock = new Mock<ILogger<CleanupJob>>();
        var job = new CleanupJob(loggerMock.Object, configMock.Object, _scopeFactoryMock.Object);

        // Act
        await job.Execute();

        // Assert
        var remainingTokens = await dbContext.RefreshTokens.ToListAsync();
        remainingTokens.Should().HaveCount(2);
        remainingTokens.Select(t => t.TokenHash).Should().Contain(new[] { "valid_token_hash", "recent_revoked_token_hash" });
        remainingTokens.Select(t => t.TokenHash).Should().NotContain(new[] { "expired_token_hash", "old_revoked_token_hash" });
    }

    [Fact]
    public async Task NOTIF_JOB_014_CleanupJob_NonExistentDirectory_ShouldSkipFilePurgeAndProceedToDbCleanup()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non_existent_dir_" + Guid.NewGuid());
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["FileStorage:AlertImagesPath"]).Returns(nonExistentPath);

        var dbContext = CreateInMemoryDbContext();
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(dbContext);

        var loggerMock = new Mock<ILogger<CleanupJob>>();
        var job = new CleanupJob(loggerMock.Object, configMock.Object, _scopeFactoryMock.Object);

        // Act
        Func<Task> act = async () => await job.Execute();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NOTIF_JOB_015_CleanupJob_DatabaseError_ShouldCatchAndNotCrash()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["FileStorage:AlertImagesPath"]).Returns(string.Empty);

        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Throws(new Exception("Database connection failure"));

        var loggerMock = new Mock<ILogger<CleanupJob>>();
        var job = new CleanupJob(loggerMock.Object, configMock.Object, _scopeFactoryMock.Object);

        // Act
        Func<Task> act = async () => await job.Execute();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NOTIF_JOB_024_CleanupJob_LockedFile_ShouldCatchIOExceptionAndProceedGracefully()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "uavpms_locked_test_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        try
        {
            var lockedFilePath = Path.Combine(tempDir, "locked_old_file.jpg");
            File.WriteAllText(lockedFilePath, "locked file content");
            File.SetCreationTimeUtc(lockedFilePath, DateTime.UtcNow.AddDays(-40));

            // Lock file exclusively so File.Delete() will throw IOException
            using var fileStream = new FileStream(lockedFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["FileStorage:AlertImagesPath"]).Returns(tempDir);

            var dbContext = CreateInMemoryDbContext();
            _serviceProviderMock.Setup(sp => sp.GetService(typeof(ApplicationDbContext))).Returns(dbContext);

            var loggerMock = new Mock<ILogger<CleanupJob>>();
            var job = new CleanupJob(loggerMock.Object, configMock.Object, _scopeFactoryMock.Object);

            // Act
            Func<Task> act = async () => await job.Execute();

            // Assert: Job does not crash application even when file is locked
            await act.Should().NotThrowAsync();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
            }
        }
    }

    #endregion
}
