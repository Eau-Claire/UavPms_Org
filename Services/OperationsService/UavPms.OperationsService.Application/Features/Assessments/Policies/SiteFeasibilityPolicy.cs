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
    public static Geometry ParseBoundary(string wkt)
    {
        try
        {
            var reader = new WKTReader();
            var g = reader.Read(wkt);
            if (!g.IsValid || g.IsEmpty || g is not (Polygon or MultiPolygon))
                throw new Exception("Geometry must be a non-empty, valid Polygon or MultiPolygon.");
            g.SRID = 4326;
            return g;
        }
        catch (Exception ex)
        {
            throw new BusinessRuleException("INVALID_GEOMETRY", ex.Message);
        }
    }

    public static SiteFeasibilityResult Evaluate(
        Region region,
        IReadOnlyList<Asset> assets,
        Geometry? proposedBoundary,
        IReadOnlyCollection<Guid> requestedAssetIds)
    {
        var findings = new Dictionary<string, object>();
        var missingAssetIds = requestedAssetIds.Except(assets.Select(a => a.Id)).ToList();

        if (missingAssetIds.Count > 0)
        {
            findings["missingAssets"] = missingAssetIds;
            findings["error"] = "One or more requested assets do not exist or are outside region scope.";
            return new SiteFeasibilityResult(
                ReadinessCheckStatus.Failed,
                JsonSerializer.Serialize(findings),
                false,
                "ASSETS_NOT_FOUND_OR_OUTSIDE_SCOPE"
            );
        }

        var inactiveAssets = assets.Where(a => a.Status != "Active" && a.Status != "Operational").Select(a => a.Id).ToList();
        if (inactiveAssets.Count > 0)
        {
            findings["inactiveAssets"] = inactiveAssets;
            findings["error"] = "One or more assets are inactive or under maintenance.";
            return new SiteFeasibilityResult(
                ReadinessCheckStatus.Failed,
                JsonSerializer.Serialize(findings),
                false,
                "ASSETS_INACTIVE"
            );
        }

        var assetsWithoutLocation = assets.Where(a => a.Location == null).Select(a => a.Id).ToList();
        if (proposedBoundary != null)
        {
            if (assetsWithoutLocation.Count > 0)
            {
                findings["assetsWithoutLocation"] = assetsWithoutLocation;
                findings["error"] = "Assets missing geographic location cannot be verified against boundary.";
                return new SiteFeasibilityResult(
                    ReadinessCheckStatus.Failed,
                    JsonSerializer.Serialize(findings),
                    false,
                    "ASSET_MISSING_GEOLOCATION"
                );
            }

            var outsideBoundary = assets.Where(a => a.Location != null && !proposedBoundary.Covers(a.Location)).Select(a => a.Id).ToList();
            if (outsideBoundary.Count > 0)
            {
                findings["assetsOutsideBoundary"] = outsideBoundary;
                findings["error"] = "One or more assets fall outside the proposed mission boundary.";
                return new SiteFeasibilityResult(
                    ReadinessCheckStatus.Failed,
                    JsonSerializer.Serialize(findings),
                    false,
                    "ASSET_OUTSIDE_BOUNDARY"
                );
            }
        }

        findings["totalAssets"] = assets.Count;
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
