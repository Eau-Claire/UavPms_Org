using UavPms.IdentityService.Application.Features.Auth.DTOs;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;

namespace UavPms.IdentityService.Application.Features.Auth.Commands.VerifyOtp.Strategies;

public interface IOtpVerificationStrategy
{
    /// <summary>
    /// Kiểm tra Strategy này có xử lý đuơc OtpPurpose này hay không
    /// </summary>
    bool CanHandle(OtpPurpose purpose);
    
    /// <summary>
    /// Thực thi nghiệp vụ sau khi mã OTP được xác thực thành công
    /// </summary>
    Task<OtpVerifyResultDto> VerifyAsync(
        User user,
        VerifyOtpCommand request, 
        CancellationToken cancellationToken);
}