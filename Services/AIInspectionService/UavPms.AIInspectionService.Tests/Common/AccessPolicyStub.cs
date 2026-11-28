using Moq;
using UavPms.AIInspectionService.Application.Common.Security;
using UavPms.AIInspectionService.Domain.Common;
using UavPms.AIInspectionService.Domain.Entities;

namespace UavPms.AIInspectionService.Tests.Common;

/// <summary>
/// Policy cho phép mọi thao tác, dùng ở các test không kiểm tra phân quyền (luồng duyệt, gán cột, upload...).
/// Phân quyền được kiểm tra riêng trong MissionAccessPolicyTests.
/// </summary>
internal static class AccessPolicyStub
{
    public static IMissionAccessPolicy AllowAll()
    {
        var policy = new Mock<IMissionAccessPolicy>();
        policy.Setup(p => p.GetVisibilityAsync()).ReturnsAsync(MissionVisibility.Everything);
        policy.Setup(p => p.CanViewAsync(It.IsAny<Mission?>())).ReturnsAsync(true);
        policy.Setup(p => p.CanReviewAsync(It.IsAny<Mission?>())).ReturnsAsync(true);
        policy.Setup(p => p.CanUploadAsync(It.IsAny<Mission?>())).ReturnsAsync(true);
        return policy.Object;
    }
}
