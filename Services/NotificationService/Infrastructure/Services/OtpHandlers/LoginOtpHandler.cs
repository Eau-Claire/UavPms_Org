using System.Threading.Tasks;
using System.Security.Claims;
using UavPms.NotificationService.Domain.Enums;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Domain.Interfaces.Repositories;

namespace UavPms.NotificationService.Infrastructure.Services.OtpHandlers;

public class LoginOtpHandler : IOtpPurposeHandler
{
    private readonly IUserRepository _userRepository;

    public LoginOtpHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public OtpPurpose Purpose => OtpPurpose.Login;
    public bool RequiresAuthentication => false;

    public async Task<PreconditionResult> ValidatePreconditionAsync(string? email, ClaimsPrincipal? currentUser)
    {
        if (string.IsNullOrEmpty(email))
        {
            return PreconditionResult.Failure("Email is required.");
        }

        var user = await _userRepository.GetByUsernameWithRolesAsync(email)
                   ?? await _userRepository.GetByEmailWithRolesAsync(email);
        if (user == null || user.Status != "Active")
        {
            return PreconditionResult.Failure("User not found or inactive.");
        }

        return PreconditionResult.Success(user.Email);
    }
}
