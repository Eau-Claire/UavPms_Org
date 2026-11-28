using System;
using System.Collections.Generic;

namespace UavPms.AIInspectionService.Domain.Common;

/// <summary>
/// Nhiệm vụ (và defect của nhiệm vụ) mà người gọi được xem, dùng để lọc ngay trong SQL:
/// tất cả (SystemAdmin), hoặc nhiệm vụ mình phụ trách, được giao, hay thuộc các vùng <see cref="RegionIds"/>.
/// </summary>
public sealed record MissionVisibility(bool All, Guid UserId, IReadOnlyCollection<Guid> RegionIds)
{
    public static MissionVisibility Everything { get; } = new(true, Guid.Empty, Array.Empty<Guid>());
}
