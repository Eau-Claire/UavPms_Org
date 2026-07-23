using UavPms.NotificationService.Domain.Entities;

namespace UavPms.NotificationService.Domain.Interfaces.Services;

public interface IJwtProvider
{
    string GenerateAccessToken(User user, IList<string> roles);
    string GenerateRefreshToken();
    string GenerateStepUpToken(User user, string purpose);
}