namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public class SpatialAssetMatch
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? DistanceMeters { get; set; }
}
