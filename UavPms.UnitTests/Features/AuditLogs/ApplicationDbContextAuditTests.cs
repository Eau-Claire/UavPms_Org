using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Services;
using UavPms.Infrastructure.Persistence;
using Xunit;

namespace UavPms.UnitTests.Features.AuditLogs;

public class ApplicationDbContextAuditTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ICurrentUserServices> _currentUserServicesMock;
    private readonly Guid _testUserId = Guid.NewGuid();

    public ApplicationDbContextAuditTests()
    {
        _currentUserServicesMock = new Mock<ICurrentUserServices>();
        _currentUserServicesMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserServicesMock.Setup(x => x.UserId).Returns(_testUserId);
        _currentUserServicesMock.Setup(x => x.IpAddress).Returns("127.0.0.1");
        _currentUserServicesMock.Setup(x => x.UserAgent).Returns("Mozilla/5.0");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, _currentUserServicesMock.Object);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldCreateAddedAuditLog_WhenBaseEntityIsAdded()
    {
        var testUser = new User
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "supersecretpassword",
            FullName = "Test User",
            Status = "Active"
        };

        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();

        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var log = auditLogs.First();
        log.TableName.Should().Be("Users");
        log.RecordId.Should().Be(testUser.Id);
        log.ActionType.Should().Be("Added");
        log.UserId.Should().Be(_testUserId);
        log.IpAddress.Should().Be("127.0.0.1");
        log.UserAgent.Should().Be("Mozilla/5.0");
        
        log.NewValues.Should().Contain("\"Username\":\"testuser\"");
        log.NewValues.Should().Contain("\"PasswordHash\":\"[MASKED]\"");
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldCreateModifiedAuditLog_WhenBaseEntityIsUpdated()
    {
        var testUser = new User
        {
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "supersecretpassword",
            FullName = "Test User",
            Status = "Active"
        };
        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        await _context.SaveChangesAsync();

        testUser.FullName = "Updated Name";
        testUser.PasswordHash = "newpassword";
        await _context.SaveChangesAsync();

        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var log = auditLogs.First();
        log.TableName.Should().Be("Users");
        log.ActionType.Should().Be("Modified");
        
        log.OldValues.Should().Contain("\"FullName\":\"Test User\"");
        log.OldValues.Should().Contain("\"PasswordHash\":\"[MASKED]\"");
        
        log.NewValues.Should().Contain("\"FullName\":\"Updated Name\"");
        log.NewValues.Should().Contain("\"PasswordHash\":\"[MASKED]\"");
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldSerializeGeometryToWkt_WhenGeometryPropertyExists()
    {
        var region = new Region
        {
            RegionName = "Hanoi Substation Region",
            Geom = new Point(105.85, 21.02) { SRID = 4326 }
        };

        _context.Regions.Add(region);
        await _context.SaveChangesAsync();

        var log = await _context.AuditLogs.FirstOrDefaultAsync(l => l.TableName == "Regions");
        log.Should().NotBeNull();
        log!.ActionType.Should().Be("Added");
        log.NewValues.Should().Contain("POINT (105.85 21.02)");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
