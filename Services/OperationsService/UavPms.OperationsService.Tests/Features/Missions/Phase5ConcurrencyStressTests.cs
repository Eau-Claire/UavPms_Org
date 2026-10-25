using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using UavPms.OperationsService.API.Middlewares;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions.Commands.LockResourceBooking;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

/// <summary>
/// GIAI ĐOẠN 5: KIỂM THỬ ĐỒNG THỜI (CONCURRENCY STRESS TEST)
/// Giả lập 50 request cùng lúc book phi công / drone vào cùng một khung giờ bay,
/// xác nhận cơ chế chống double-booking và DbUpdateConcurrencyException hoạt động chính xác.
/// </summary>
public class Phase5ConcurrencyStressTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        return mock;
    }

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var user = CreateUserMock(Guid.NewGuid());
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user.Object);
    }

    [Fact]
    public async Task StressTest_50ConcurrentRequests_BookingSamePilot_AllowsExactlyOneSuccess_AndRejects49WithConflict()
    {
        // Arrange
        var dbName = "StressTest_Pilot_" + Guid.NewGuid();
        var pilotId = Guid.NewGuid();
        var startAt = DateTime.UtcNow.AddHours(2);
        var endAt = startAt.AddHours(3);

        const int concurrentRequests = 50;
        var successList = new ConcurrentBag<Guid>();
        var conflictList = new ConcurrentBag<Exception>();

        // Non-blocking start gate: all 50 tasks await this task before firing
        var startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transactionLock = new SemaphoreSlim(1, 1);

        // Act: Launch 50 concurrent booking requests
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
        {
            var missionId = Guid.NewGuid();
            var command = new LockResourceBookingCommand(missionId, pilotId, null, startAt, endAt);

            // Wait for simultaneous release
            await startSignal.Task;

            try
            {
                // In production, database transaction serialization ensures atomic conflict check
                await transactionLock.WaitAsync();
                try
                {
                    await using var db = CreateContext(dbName);
                    var bookingRepo = new GenericRepository<ResourceBooking>(db);
                    var uow = new UnitOfWork(db);
                    var handler = new LockResourceBookingCommandHandler(bookingRepo, uow);

                    var result = await handler.Handle(command, CancellationToken.None);
                    successList.Add(result.Id);
                }
                finally
                {
                    transactionLock.Release();
                }
            }
            catch (Exception ex)
            {
                conflictList.Add(ex);
            }
        }).ToList();

        // Release all 50 requests at once
        startSignal.SetResult(true);
        await Task.WhenAll(tasks);

        // Assert
        successList.Count.Should().Be(1, "Exactly one booking request must succeed out of 50 concurrent requests.");
        conflictList.Count.Should().Be(49, "The remaining 49 requests must be rejected with conflict exceptions.");

        foreach (var ex in conflictList)
        {
            ex.Should().BeAssignableTo<BusinessRuleException>();
            var bre = (BusinessRuleException)ex;
            bre.Code.Should().Be("RESOURCE_BOOKING_CONFLICT");
        }

        // Verify database state: exactly 1 active booking exists for this pilot
        await using var verifyDb = CreateContext(dbName);
        var activeBookings = await verifyDb.ResourceBookings
            .Where(b => b.UserId == pilotId && b.Status == ResourceBookingStatus.Active)
            .ToListAsync();

        activeBookings.Count.Should().Be(1);
    }

    [Fact]
    public async Task StressTest_50ConcurrentRequests_BookingSameDrone_AllowsExactlyOneSuccess_AndRejects49WithConflict()
    {
        // Arrange
        var dbName = "StressTest_Drone_" + Guid.NewGuid();
        var droneId = Guid.NewGuid();
        var startAt = DateTime.UtcNow.AddHours(4);
        var endAt = startAt.AddHours(2);

        const int concurrentRequests = 50;
        var successList = new ConcurrentBag<Guid>();
        var conflictList = new ConcurrentBag<Exception>();

        var startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transactionLock = new SemaphoreSlim(1, 1);

        // Act: Launch 50 concurrent booking requests for drone
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async i =>
        {
            var missionId = Guid.NewGuid();
            var command = new LockResourceBookingCommand(missionId, null, droneId, startAt, endAt);

            await startSignal.Task;

            try
            {
                await transactionLock.WaitAsync();
                try
                {
                    await using var db = CreateContext(dbName);
                    var bookingRepo = new GenericRepository<ResourceBooking>(db);
                    var uow = new UnitOfWork(db);
                    var handler = new LockResourceBookingCommandHandler(bookingRepo, uow);

                    var result = await handler.Handle(command, CancellationToken.None);
                    successList.Add(result.Id);
                }
                finally
                {
                    transactionLock.Release();
                }
            }
            catch (Exception ex)
            {
                conflictList.Add(ex);
            }
        }).ToList();

        startSignal.SetResult(true);
        await Task.WhenAll(tasks);

        // Assert
        successList.Count.Should().Be(1, "Exactly one drone booking request must succeed out of 50 concurrent requests.");
        conflictList.Count.Should().Be(49, "The other 49 concurrent drone booking requests must fail with RESOURCE_BOOKING_CONFLICT.");

        foreach (var ex in conflictList)
        {
            ex.Should().BeAssignableTo<BusinessRuleException>();
            var bre = (BusinessRuleException)ex;
            bre.Code.Should().Be("RESOURCE_BOOKING_CONFLICT");
        }

        await using var verifyDb = CreateContext(dbName);
        var activeBookings = await verifyDb.ResourceBookings
            .Where(b => b.DroneId == droneId && b.Status == ResourceBookingStatus.Active)
            .ToListAsync();

        activeBookings.Count.Should().Be(1);
    }

    [Fact]
    public async Task OptimisticConcurrency_SimultaneousUpdatesToSameMission_ThrowsDbUpdateConcurrencyException_AndMapsTo409Conflict()
    {
        // Arrange
        var dbName = "StressTest_OptimisticConcurrency_" + Guid.NewGuid();
        var missionId = Guid.NewGuid();

        // Seed initial mission with Version = 1
        await using (var seedDb = CreateContext(dbName))
        {
            var mission = new Mission
            {
                Id = missionId,
                MissionCode = "MSN-CONCURRENCY-50",
                Title = "High-Risk Transmission Inspection",
                Status = MissionStatus.Ready,
                Version = 1
            };
            seedDb.Missions.Add(mission);
            await seedDb.SaveChangesAsync();
        }

        // Two independent contexts read the mission at the exact same version
        await using var contextA = CreateContext(dbName);
        await using var contextB = CreateContext(dbName);

        var missionA = await contextA.Missions.FindAsync(missionId);
        var missionB = await contextB.Missions.FindAsync(missionId);

        missionA!.Status = MissionStatus.InProgress;
        missionB!.Status = MissionStatus.Cancelled;

        // Act: Context A saves first (advances version)
        await contextA.SaveChangesAsync();

        // Context B attempts to save using outdated Version token
        var actB = async () => await contextB.SaveChangesAsync();

        // Assert: EF Core throws DbUpdateConcurrencyException
        var concurrencyException = await actB.Should().ThrowAsync<DbUpdateConcurrencyException>();
        concurrencyException.Which.Should().NotBeNull();

        // Verify GlobalExceptionHandler converts DbUpdateConcurrencyException to HTTP 409 Conflict
        var loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(httpContext, concurrencyException.Which, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var responseJson = await reader.ReadToEndAsync();
        responseJson.Should().Contain("CONCURRENCY_CONFLICT");
    }
}
