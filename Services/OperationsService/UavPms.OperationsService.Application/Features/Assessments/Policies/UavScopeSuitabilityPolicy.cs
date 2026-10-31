using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.Policies;

public sealed record UavScopeSuitabilityResult(
    UavScopeSuitability Suitability,
    int EstimatedFlightsNeeded,
    double CorridorKm,
    double MissionMinutes,
    double EffectiveFlightMinutes,
    double MaxCoverageDistanceKm,
    string Reason);

/// <summary>
/// BR-21: compares the flight distance and time needed for the target towers with the UAV's endurance and
/// operational range. Suitable = one flight, SuitableWithSplit = 2–4 flights, otherwise Unsuitable.
/// Each flight must fit both the usable endurance and the operational range (per-flight corridor ≤ range).
/// Only the scope is judged here: availability, bookings and technical health are separate criteria (BR-20).
/// </summary>
public static class UavScopeSuitabilityPolicy
{
    public const double AverageSpeedKmh = 36.0;          // 10 m/s
    public const double ReserveBatteryPercent = 20.0;
    public const double InspectionMinutesPerTower = 3.0;
    public const int MaxSplitFlights = 4;
    /// <summary>Used only for a hop whose tower has no coordinates (that tower already fails the site check).</summary>
    public const double FallbackTowerSpacingKm = 0.35;

    private const double EarthRadiusKm = 6371.0088;

    /// <summary>Length of the inspection corridor: the sum of the great-circle distances between consecutive towers,
    /// in the given order (assessment sequence).</summary>
    public static double EstimateCorridorKm(IReadOnlyList<Geometry?> orderedTowerGeometries)
    {
        if (orderedTowerGeometries.Count < 2)
            return 0;

        double total = 0;
        for (var i = 1; i < orderedTowerGeometries.Count; i++)
        {
            var a = orderedTowerGeometries[i - 1];
            var b = orderedTowerGeometries[i];
            if (a == null || b == null || a.IsEmpty || b.IsEmpty)
            {
                total += FallbackTowerSpacingKm;
                continue;
            }

            var pa = a.Centroid.Coordinate;
            var pb = b.Centroid.Coordinate;
            total += HaversineKm(pa.Y, pa.X, pb.Y, pb.X);
        }

        return Math.Round(total, 3);
    }

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double deg) => deg * Math.PI / 180.0;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static UavScopeSuitabilityResult Evaluate(double corridorKm, int towerCount, double enduranceMinutes, double operationalRangeKm)
    {
        corridorKm = Math.Max(0, corridorKm);
        towerCount = Math.Max(0, towerCount);
        var missionMinutes = Math.Round(corridorKm / AverageSpeedKmh * 60.0 + towerCount * InspectionMinutesPerTower, 1);

        if (enduranceMinutes <= 0 || operationalRangeKm <= 0)
        {
            return new UavScopeSuitabilityResult(
                UavScopeSuitability.Unsuitable,
                0,
                corridorKm,
                missionMinutes,
                0,
                0,
                "UAV chưa khai báo thời gian bay hoặc tầm hoạt động, không thể ước tính khả năng bao phủ phạm vi.");
        }

        var effectiveFlightMinutes = Math.Round(enduranceMinutes * (1.0 - ReserveBatteryPercent / 100.0), 1);
        var enduranceDistanceKm = effectiveFlightMinutes / 60.0 * AverageSpeedKmh;
        var maxCoverageKm = Math.Round(Math.Min(enduranceDistanceKm, operationalRangeKm), 2);

        var flightsByTime = (int)Math.Ceiling(missionMinutes / effectiveFlightMinutes);
        var flightsByRange = (int)Math.Ceiling(corridorKm / operationalRangeKm);
        var flightsNeeded = Math.Max(1, Math.Max(flightsByTime, flightsByRange));

        if (flightsNeeded == 1)
        {
            return new UavScopeSuitabilityResult(
                UavScopeSuitability.Suitable, 1, corridorKm, missionMinutes, effectiveFlightMinutes, maxCoverageKm,
                $"Một chuyến bay đủ cho toàn bộ phạm vi ({corridorKm:0.##} km, ~{missionMinutes:0.#} phút; tầm bay {operationalRangeKm:0.##} km, {effectiveFlightMinutes:0.#} phút hiệu dụng).");
        }

        if (flightsNeeded <= MaxSplitFlights)
        {
            return new UavScopeSuitabilityResult(
                UavScopeSuitability.SuitableWithSplit, flightsNeeded, corridorKm, missionMinutes, effectiveFlightMinutes, maxCoverageKm,
                $"Phạm vi ({corridorKm:0.##} km, ~{missionMinutes:0.#} phút) vượt quá một chuyến bay; đề xuất chia thành {flightsNeeded} chuyến (mỗi chuyến ≤ {operationalRangeKm:0.##} km và ≤ {effectiveFlightMinutes:0.#} phút).");
        }

        return new UavScopeSuitabilityResult(
            UavScopeSuitability.Unsuitable, flightsNeeded, corridorKm, missionMinutes, effectiveFlightMinutes, maxCoverageKm,
            $"Phạm vi ({corridorKm:0.##} km, ~{missionMinutes:0.#} phút) cần {flightsNeeded} chuyến bay, vượt quá giới hạn {MaxSplitFlights} chuyến của UAV (tầm bay {operationalRangeKm:0.##} km, {effectiveFlightMinutes:0.#} phút hiệu dụng).");
    }
}
