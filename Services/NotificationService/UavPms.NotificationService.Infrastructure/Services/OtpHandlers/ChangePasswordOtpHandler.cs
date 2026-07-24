using System;
using System.Threading.Tasks;
using System.Security.Claims;
using UavPms.NotificationService.Domain.Enums;
using UavPms.NotificationService.Domain.Interfaces.Services;
using UavPms.NotificationService.Domain.Interfaces.Repositories;

namespace UavPms.NotificationService.Infrastructure.Services.OtpHandlers;

public class ChangePasswordOtpHandler : IOtpPurposeHandler
{
    private readonly IUserRepository _userRepository;

    public ChangePasswordOtpHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public OtpPurpose Purpose => OtpPurpose.ChangePassword;
    public bool RequiresAuthentication => true;

    public async Task<PreconditionResult> ValidatePreconditionAsync(string? email, ClaimsPrincipal? currentUser)
    {
        if (currentUser == null)
        {
            return PreconditionResult.Failure("Authentication is required.");
        }

        var userIdString = currentUser.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return PreconditionResult.Failure("Invalid user token claims.");
        }

        var user = await _userRepository.GetByIdAsync(userId, track: false);
        if (user == null || user.Status != "Active")
        {
            return PreconditionResult.Failure("User not found or inactive.");
        }

        return PreconditionResult.Success();
    }
}
