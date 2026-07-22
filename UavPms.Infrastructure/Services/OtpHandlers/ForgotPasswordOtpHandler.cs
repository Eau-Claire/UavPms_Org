using System.Threading.Tasks;
using System.Security.Claims;
using UavPms.Core.Enums;
using UavPms.Core.Interfaces.Services;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Infrastructure.Services.OtpHandlers;

public class ForgotPasswordOtpHandler : IOtpPurposeHandler
{
    private readonly IUserRepository _userRepository;

    public ForgotPasswordOtpHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public OtpPurpose Purpose => OtpPurpose.ForgotPassword;
    public bool RequiresAuthentication => false;

    public async Task<PreconditionResult> ValidatePreconditionAsync(string? email, ClaimsPrincipal? currentUser)
    {
        if (string.IsNullOrEmpty(email))
        {
            return PreconditionResult.Failure("Email is required.");
        }

        var user = await _userRepository.GetByEmailWithRolesAsync(email)
                   ?? await _userRepository.GetByUsernameWithRolesAsync(email);
        if (user == null || user.Status != "Active")
        {
            // Silent success to prevent user enumeration
            return PreconditionResult.SilentSuccess();
        }

        return PreconditionResult.Success(user.Email);
    }
}
