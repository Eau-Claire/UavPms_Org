using System.Threading.Tasks;
using System.Security.Claims;
using UavPms.AIInspectionService.Domain.Enums;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;

namespace UavPms.AIInspectionService.Infrastructure.Services.OtpHandlers;

public class EmailVerificationOtpHandler : IOtpPurposeHandler
{
    private readonly IUserRepository _userRepository;

    public EmailVerificationOtpHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public OtpPurpose Purpose => OtpPurpose.EmailVerification;
    public bool RequiresAuthentication => false;

    public async Task<PreconditionResult> ValidatePreconditionAsync(string? email, ClaimsPrincipal? currentUser)
    {
        if (string.IsNullOrEmpty(email))
        {
            return PreconditionResult.Failure("Email is required.");
        }

        var user = await _userRepository.GetByEmailWithRolesAsync(email);
        if (user == null)
        {
            return PreconditionResult.Failure("Email is not registered.");
        }

        if (user.IsEmailVerified)
        {
            return PreconditionResult.Failure("Email is already verified.");
        }

        return PreconditionResult.Success();
    }
}
