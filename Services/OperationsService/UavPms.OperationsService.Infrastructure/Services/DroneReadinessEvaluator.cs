using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Assessments.Policies;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Loads current data and applies <see cref="DroneEligibilityPolicy"/> (BR-20 – BR-22). Shared by the assessment
/// evaluation (UC-MF01-04) and GET /drones/eligible so both give the same answer.
/// </summary>
public static class DroneReadinessEvaluator
{
    /// <summary>Corridor length and tower count for the towers of a scope, ordered by sequence.</summary>
    public static async Task<(double CorridorKm, int TowerCount)> EstimateScopeAsync(
        ApplicationDbContext db,
        IEnumerable<PreMissionAssessmentTower> assessmentTowers,
        CancellationToken ct)
    {
        var ordered = assessmentTowers.OrderBy(t => t.Sequence).Select(t => t.TowerId).ToList();
        if (ordered.Count == 0)
            return (0, 0);

        var geometries = await db.Towers
            .Where(t => ordered.Contains(t.Id))
            .Select(t => new { t.Id, t.Geom })
            .ToListAsync(ct);
        var byId = geometries.ToDictionary(x => x.Id, x => x.Geom);

        var orderedGeometries = ordered
            .Select(id => byId.TryGetValue(id, out var g) ? g : null)
            .ToList<Geometry?>();

        return (UavScopeSuitabilityPolicy.EstimateCorridorKm(orderedGeometries), ordered.Count);
    }

    public static async Task<IReadOnlyList<(Uav Drone, DroneEligibilityResult Result)>> EvaluateAsync(
        ApplicationDbContext db,
        DateTime plannedStart,
        DateTime plannedEnd,
        double corridorKm,
        int towerCount,
        CancellationToken ct,
        IReadOnlyCollection<Guid>? onlyDroneIds = null)
    {
        var dronesQuery = db.Uavs.Where(x => !x.IsDeleted);
        if (onlyDroneIds != null)
            dronesQuery = dronesQuery.Where(x => onlyDroneIds.Contains(x.Id));
        var drones = await dronesQuery.OrderBy(x => x.UavCode).ToListAsync(ct);
        var droneIds = drones.Select(d => d.Id).ToList();

        var inspections = await db.DroneTechnicalInspections
            .Where(t => droneIds.Contains(t.DroneId) &&
                        !t.IsDeleted &&
                        t.CompletedAt != null &&
                        (t.Status == DroneTechnicalInspectionStatus.Passed || t.Status == DroneTechnicalInspectionStatus.Failed))
            .ToListAsync(ct);

        var bookings = await db.ResourceBookings
            .Where(b => b.DroneId.HasValue &&
                        droneIds.Contains(b.DroneId.Value) &&
                        b.Status == ResourceBookingStatus.Active &&
                        b.StartAt < plannedEnd &&
                        b.EndAt > plannedStart)
            .ToListAsync(ct);

        var inspectionsByDrone = inspections.ToLookup(t => t.DroneId);
        var bookingsByDrone = bookings.ToLookup(b => b.DroneId!.Value);

        return drones
            .Select(d => (d, DroneEligibilityPolicy.Evaluate(
                d,
                inspectionsByDrone[d.Id],
                bookingsByDrone[d.Id],
                plannedStart,
                plannedEnd,
                corridorKm,
                towerCount)))
            .ToList();
    }
}
