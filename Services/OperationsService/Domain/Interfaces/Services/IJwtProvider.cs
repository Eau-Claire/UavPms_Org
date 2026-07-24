using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Services;

public interface IJwtProvider
{
    string GenerateAccessToken(User user, IList<string> roles);
    string GenerateRefreshToken();
    string GenerateStepUpToken(User user, string purpose);
}