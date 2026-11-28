using System;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

/// <summary>Read-only subset of the shared Regions table (owned by OperationsService); used for region scope checks.</summary>
public class Region : BaseEntity
{
    public Guid? ParentId { get; set; }
}

/// <summary>Read-only subset of the shared UserGeographicScopes table (owned by OperationsService).</summary>
public class UserGeographicScope : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid RegionId { get; set; }
}
