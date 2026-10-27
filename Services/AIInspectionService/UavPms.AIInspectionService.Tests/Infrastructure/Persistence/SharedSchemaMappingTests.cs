using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Infrastructure.Persistence;

namespace UavPms.AIInspectionService.Tests.Infrastructure.Persistence;

/// <summary>
/// Guards the AIInspectionService mapping against the shared ERD v2.2 schema (built with the Npgsql provider,
/// no database connection is opened).
/// </summary>
public sealed class SharedSchemaMappingTests
{
    private static ApplicationDbContext CreateNpgsqlContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=schema_check", o => o.UseNetTopologySuite())
            .Options;
        return new ApplicationDbContext(options, null);
    }

    [Fact]
    public void Model_ShouldMapErdV22Tables()
    {
        using var context = CreateNpgsqlContext();
        var tables = context.Model.GetEntityTypes().Select(e => e.GetTableName()).ToList();

        tables.Should().Contain(new[] { "AIAnalysisRuns", "UAVs", "MissionAssignments", "DetectedAnomalies", "InspectionMedia" });
        tables.Should().NotContain(new[] { "AIAnalysisRequests", "AssetComponents", "Uavs" });
    }

    [Fact]
    public void CreateScript_ShouldUseErdV22Columns()
    {
        using var context = CreateNpgsqlContext();
        var script = context.Database.GenerateCreateScript();

        script.Should().Contain("\"ExecutionLocation\" character varying(10) NOT NULL");
        script.Should().Contain("\"Status\" character varying(20) NOT NULL");
        script.Should().Contain("\"AnalysisType\" character varying(40) NOT NULL");
        script.Should().Contain("\"Location\" geometry(Point,4326)");
        script.Should().Contain("\"FrameTimeSeconds\" double precision");
        script.Should().Contain("\"FlightStatus\" character varying(20)");
        script.Should().Contain("\"Checksum\" character varying(64)");
        script.Should().Contain("WHERE \"IsDeleted\" = false AND \"Status\" IN ('Pending','Processing')");
        script.Should().Contain("WHERE \"AiDetectionId\" IS NOT NULL AND \"IsDeleted\" = false");

        foreach (var removed in new[] { "\"AssetId\"", "\"AiSource\"", "\"Gps\"", "\"IsRead\"", "\"IsPushed\"",
                     "\"InspectorId\"", "\"UploadedBy\" uuid NOT NULL", "\"VideoFps\"", "\"Timestamp\"", "\"RegionName\"" })
        {
            script.Should().NotContain(removed);
        }
    }

    [Fact]
    public void DetectedAnomaly_ShouldDeriveAiSource()
    {
        new DetectedAnomaly().GetAiSource().Should().Be("Manual");
        new DetectedAnomaly
        {
            AnalysisRunId = Guid.NewGuid(),
            AnalysisRun = new AIAnalysisRun { ExecutionLocation = AIAnalysisRun.Edge, ModelName = "VisionEdge" }
        }.GetAiSource().Should().Be("VisionEdge");
    }
}
