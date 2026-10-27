using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.Policies;

public record SiteFeasibilityResult(
    ReadinessCheckStatus Status,
    string FindingsJson,
    bool IsValid,
    string? ErrorMessage
);

public static class SiteFeasibilityPolicy
{
    public const int MaxWktLength = 65536; // 64 KB
    public const int MaxVertices = 1000;

    public static Geometry ParseBoundary(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
            throw new BusinessRuleException("INVALID_GEOMETRY", "Boundary WKT cannot be empty.");

        if (wkt.Length > MaxWktLength)
            throw new BusinessRuleException("INVALID_GEOMETRY", $"Boundary WKT length exceeds maximum limit of {MaxWktLength} characters.");

        try
        {
            var reader = new WKTReader();
            var g = reader.Read(wkt);
            if (!g.IsValid || g.IsEmpty || g is not (Polygon or MultiPolygon))
                throw new Exception("Geometry must be a non-empty, valid Polygon or MultiPolygon.");

            if (g.NumPoints > MaxVertices)
                throw new Exception($"Boundary exceeds maximum allowed vertices limit ({MaxVertices}). Actual: {g.NumPoints}.");

            g.SRID = 4326;
            return g;
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessRuleException("INVALID_GEOMETRY", ex.Message);
        }
    }

    public static SiteFeasibilityResult Evaluate(
        Region region,
        IReadOnlyList<Tower> towers,
        Geometry? proposedBoundary,
        IReadOnlyCollection<Guid> requestedTowerIds)
    {
        var findings = new Dictionary<string, object>();
        var missingTowerIds = requestedTowerIds.Except(towers.Select(a => a.Id)).ToList();

        if (missingTowerIds.Count > 0)
        {
            findings["missingTowers"] = missingTowerIds;
            findings["error"] = "One or more requested towers do not exist or are outside region scope.";
            return new SiteFeasibilityResult(
                ReadinessCheckStatus.Failed,
                JsonSerializer.Serialize(findings),
                false,
                "TOWERS_NOT_FOUND_OR_OUTSIDE_SCOPE"
            );
        }

        var towersWithoutLocation = towers.Where(a => a.Geom == null).Select(a => a.Id).ToList();
        if (proposedBoundary != null)
        {
            if (towersWithoutLocation.Count > 0)
            {
                findings["towersWithoutLocation"] = towersWithoutLocation;
                findings["error"] = "Towers missing geographic location cannot be verified against boundary.";
                return new SiteFeasibilityResult(
                    ReadinessCheckStatus.Failed,
                    JsonSerializer.Serialize(findings),
                    false,
                    "TOWER_MISSING_GEOLOCATION"
                );
            }

            var outsideBoundary = towers.Where(a => a.Geom != null && !proposedBoundary.Covers(a.Geom)).Select(a => a.Id).ToList();
            if (outsideBoundary.Count > 0)
            {
                findings["towersOutsideBoundary"] = outsideBoundary;
                findings["error"] = "One or more towers fall outside the proposed mission boundary.";
                return new SiteFeasibilityResult(
                    ReadinessCheckStatus.Failed,
                    JsonSerializer.Serialize(findings),
                    false,
                    "TOWER_OUTSIDE_BOUNDARY"
                );
            }
        }

        findings["totalTowers"] = towers.Count;
        findings["regionId"] = region.Id;
        findings["hasProposedBoundary"] = proposedBoundary != null;
        findings["boundaryCheck"] = proposedBoundary != null ? "Passed" : "NotProvided";
        findings["siteFeasibility"] = "Passed";

        return new SiteFeasibilityResult(
            ReadinessCheckStatus.Passed,
            JsonSerializer.Serialize(findings),
            true,
            null
        );
    }
}
