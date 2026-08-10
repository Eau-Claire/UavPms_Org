using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;

public class OtpVerificationStrategyResolver
{
    private readonly List<IOtpVerificationStrategy> _strategies;

    public OtpVerificationStrategyResolver(List<IOtpVerificationStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IOtpVerificationStrategy Resolve(OtpPurpose purpose)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(purpose));
        if (strategy == null)
        {
            throw new BusinessRuleException($"No OTP strategy registerd for purpose: {purpose}");
        }
        
        return strategy;
    }
}