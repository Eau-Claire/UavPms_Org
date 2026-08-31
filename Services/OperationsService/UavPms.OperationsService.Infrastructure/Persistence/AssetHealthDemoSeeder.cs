using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Infrastructure.Persistence;

public static class AssetHealthDemoSeeder
{
    private static readonly Guid RegionId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid ManagerId = Guid.Parse("11111111-1111-1111-1111-111111111102");
    private static readonly Guid InspectorId = Guid.Parse("11111111-1111-1111-1111-111111111103");
    private static readonly Guid UavId = Guid.Parse("11111111-1111-1111-1111-111111111104");
    private static readonly Guid SubstationId = Guid.Parse("11111111-1111-1111-1111-111111111105");
    private static readonly Guid LineId = Guid.Parse("11111111-1111-1111-1111-111111111106");
    private static readonly Guid TowerId = Guid.Parse("11111111-1111-1111-1111-111111111107");
    private static readonly Guid MissionId = Guid.Parse("11111111-1111-1111-1111-111111111108");
    private static readonly Guid MediaId = Guid.Parse("11111111-1111-1111-1111-111111111109");

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.AssetComponents.AnyAsync(a => a.ComponentCode == "INS-DEMO-CRIT-01", cancellationToken))
        {
            return;
        }

        await EnsureReferenceDataAsync(context, cancellationToken);

        var components = new[]
        {
            CreateComponent("INS-DEMO-CRIT-01", "Insulator", 32.5, "Critical Risk", -2),
            CreateComponent("CBL-DEMO-HIGH-01", "Cable", 48.0, "High Risk", -6),
            CreateComponent("TWR-DEMO-MED-01", "Tower Structure", 71.0, "Medium Risk", -12),
            CreateComponent("DMP-DEMO-LOW-01", "Vibration Damper", 93.0, "Low Risk", -20)
        };

        await context.AssetComponents.AddRangeAsync(components, cancellationToken);

        var category = await EnsureDefectCategoryAsync(context, cancellationToken);
        var criticalComponent = components[0];

        await context.DetectedAnomalies.AddRangeAsync(new[]
        {
            new DetectedAnomaly
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111201"),
                MediaId = MediaId,
                TowerId = TowerId,
                ComponentId = criticalComponent.Id,
                CategoryId = category.Id,
                BoundingBox = """{"x":120,"y":80,"width":240,"height":180}""",
                ConfidenceScore = 0.91,
                ValidationStatus = "Confirmed",
                AiSource = "DemoSeed",
                AnalystNotes = "Seeded critical risk defect for local dashboard testing."
            }
        }, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static AssetComponent CreateComponent(
        string code,
        string type,
        double healthScore,
        string riskLevel,
        int inspectedDaysAgo)
        => new()
        {
            Id = Guid.NewGuid(),
            TowerId = TowerId,
            ComponentCode = code,
            ComponentType = type,
            Status = "Operational",
            CurrentHealthScore = healthScore,
            RiskLevel = riskLevel,
            LastInspectedAt = DateTime.UtcNow.AddDays(inspectedDaysAgo)
        };

    private static async Task EnsureReferenceDataAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.Regions.AnyAsync(r => r.Id == RegionId, cancellationToken))
        {
            await context.Regions.AddAsync(new Region { Id = RegionId, RegionName = "Demo North Region" }, cancellationToken);
        }

        await EnsureUserAsync(context, ManagerId, "Demo Manager", "demo.manager@uavpms.local", cancellationToken);
        await EnsureUserAsync(context, InspectorId, "Demo Inspector", "demo.inspector@uavpms.local", cancellationToken);

        if (!await context.Uavs.AnyAsync(u => u.Id == UavId, cancellationToken))
        {
            await context.Uavs.AddAsync(new Uav
            {
                Id = UavId,
                UavCode = "UAV-DEMO-01",
                Model = "Demo Quad",
                Status = DroneStatus.Idle,
                BatteryLevel = 100
            }, cancellationToken);
        }

        if (!await context.Substations.AnyAsync(s => s.Id == SubstationId, cancellationToken))
        {
            await context.Substations.AddAsync(new Substation
            {
                Id = SubstationId,
                RegionAssetId = RegionId,
                SubstationName = "Demo Substation",
                VoltageLevel = "500kV"
            }, cancellationToken);
        }

        if (!await context.TransmissionLines.AnyAsync(l => l.Id == LineId, cancellationToken))
        {
            await context.TransmissionLines.AddAsync(new TransmissionLine
            {
                Id = LineId,
                SubstationAssetId = SubstationId,
                LineName = "Demo Transmission Line",
                IsCriticalEdge = true
            }, cancellationToken);
        }

        if (!await context.Towers.AnyAsync(t => t.Id == TowerId, cancellationToken))
        {
            await context.Towers.AddAsync(new Tower
            {
                Id = TowerId,
                LineAssetId = LineId,
                TowerCode = "TOW-DEMO-01",
                CurrentHealthScore = 61.1,
                RiskLevel = "Medium Risk",
                LastInspectedAt = DateTime.UtcNow.AddDays(-2)
            }, cancellationToken);
        }

        if (!await context.Missions.AnyAsync(m => m.Id == MissionId, cancellationToken))
        {
            await context.Missions.AddAsync(new Mission
            {
                Id = MissionId,
                MissionCode = "MIS-DEMO-ASSET-HEALTH",
                Title = "Demo Asset Health Mission",
                ManagerId = ManagerId,
                InspectorId = InspectorId,
                RegionId = RegionId,
                UavId = UavId,
                Status = MissionStatus.Completed,
                Description = "Seeded mission for frontend asset health dashboard testing.",
                ScheduledStartAt = DateTime.UtcNow.AddDays(-3),
                StartedAt = DateTime.UtcNow.AddDays(-3),
                EndedAt = DateTime.UtcNow.AddDays(-2)
            }, cancellationToken);
        }

        if (!await context.InspectionMedia.AnyAsync(m => m.Id == MediaId, cancellationToken))
        {
            await context.InspectionMedia.AddAsync(new InspectionMedia
            {
                Id = MediaId,
                MissionId = MissionId,
                TowerId = TowerId,
                MediaType = "Image",
                FileUrl = "https://demo.local/uav/asset-health/insulator-critical.jpg",
                AiSource = "DemoSeed",
                ValidationStatus = "Confirmed",
                CapturedAt = DateTime.UtcNow.AddDays(-2)
            }, cancellationToken);
        }
    }

    private static async Task EnsureUserAsync(
        ApplicationDbContext context,
        Guid id,
        string fullName,
        string email,
        CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.Id == id, cancellationToken))
        {
            return;
        }

        await context.Users.AddAsync(new User
        {
            Id = id,
            FullName = fullName,
            Email = email,
            Phone = "0000000000",
            Status = "Active",
            IsEmailVerified = true
        }, cancellationToken);
    }

    private static async Task<DefectCategory> EnsureDefectCategoryAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var category = await context.DefectCategories.FirstOrDefaultAsync(
            c => c.CategoryCode == "DEMO_INSULATOR_DAMAGE",
            cancellationToken);
        if (category != null)
        {
            return category;
        }

        category = new DefectCategory
        {
            CategoryCode = "DEMO_INSULATOR_DAMAGE",
            CategoryName = "Insulator Damage",
            SeverityWeight = 0.95,
            IsEmergencyClass = false,
            Description = "Seeded demo defect category for asset health dashboard testing."
        };

        await context.DefectCategories.AddAsync(category, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return category;
    }
}
